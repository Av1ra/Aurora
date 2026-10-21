using Aurora.Core;
using System.Numerics;
using System.Text;

static class ObjImportChecks
{
 public static void Run(Action<bool,string> check,Action<Action,string> reject)
 {
  string sizeFixture=Path.Combine(Path.GetTempPath(),"Aurora-oversized-"+Guid.NewGuid().ToString("N")+".obj");try{using(var stream=File.Create(sizeFixture))stream.SetLength(64L*1024*1024+1);reject(()=>ObjImporter.Read(sizeFixture),"oversized OBJ rejected before parsing or allocating its geometry");}finally{File.Delete(sizeFixture);}
  const string vertices="v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\n";
  var imported=ObjImporter.Parse(vertices+"vt 0 .25\nvt 1 .25\nvt 1 .75\nvt 0 .75\no Body\nusemtl Paint\nf 1/1 2/2 3/3 4/4\no Window\nusemtl Glass\nf -4 -3 -2\n");
  var model=imported.Model;
  check(model.Nodes.Count==2&&model.Materials.SequenceEqual(new[]{"Paint","Glass"})&&model.Nodes.Sum(n=>n.IndexCount/3)==3,"OBJ preserves object/material boundaries and negative indices");
  check(Enumerable.Range(0,model.Nodes[0].VertexCount).All(i=>model.Nodes[0].Uv[i*2+1]==(model.Nodes[0].Positions[i*3+1]==0?.75f:.25f))&&model.Nodes[0].Normals.Where((_,i)=>i%3==2).All(n=>MathF.Abs(n-1)<1e-6f),"OBJ converts UV origin and calculates unit face normals");
  check(model.Nodes.All(n=>model.I(n.Offset+252)==0x108&&model.F(n.Offset+76)==1&&model.F(n.Offset+140)==1),"OBJ emits the native cooker rigid stream and identity pivots");
  const string concave="v 0 0 0\nv 2 0 0\nv 2 2 0\nv 1 1 0\nv 0 2 0\nf 1 2 3 4 5";
  float Area(NativeModel mesh){float area=0;foreach(var node in mesh.Nodes)for(int i=0;i<node.Indices.Length;i+=3){Vector3 P(int at){int v=node.Indices[at]*3;return new(node.Positions[v],node.Positions[v+1],node.Positions[v+2]);}area+=Vector3.Cross(P(i+1)-P(i),P(i+2)-P(i)).Z*.5f;}return area;}
  check(MathF.Abs(Area(ObjImporter.Parse(concave).Model)-3)<1e-6f&&MathF.Abs(Area(ObjImporter.Parse(concave.Replace("f 1 2 3 4 5","f 5 4 3 2 1")).Model)+3)<1e-6f,"concave OBJ triangulation preserves area and winding in both directions");
  const string bent="v 0 0 0\nv 1 0 0\nv 0 1 0\nv 0 0 1\n";
  var smooth=ObjImporter.Parse(bent+"s 1\nf 1 2 3\nf 1 4 2").Model.Nodes[0];
  check(smooth.VertexCount==4&&MathF.Abs(smooth.Normals[1]-.70710677f)<1e-5f&&MathF.Abs(smooth.Normals[2]-.70710677f)<1e-5f,"OBJ smoothing groups average adjacent face normals");
  check(ObjImporter.Parse(bent+"s off\nf 1 2 3\nf 1 4 2").Model.Nodes[0].VertexCount==6,"OBJ flat shading preserves hard edges");
  check(ObjImporter.Parse(vertices+"vn 1 0 0\nf 1//1 2//1 3//1").Model.Nodes[0].Normals[0]==1,"explicit OBJ normals are retained");
  check(ObjImporter.Parse("\ufeff"+vertices+"usemtl Кузов\nf 1 2 3").MaterialNames["Кузов"].All(c=>c<128),"OBJ BOM and Unicode identifiers map to stable native names");
  foreach(var bad in new[]{vertices+"f 0 1 2",vertices+"f 1 2 99",vertices+"f 1 1 2",vertices+"f 1 3 2 4",bent+"f 1 2 3 4",vertices+"vn 0 0 0\nf 1//1 2//1 3//1","v NaN 0 0\nf 1 1 1"})reject(()=>ObjImporter.Parse(bad),"malformed/nonplanar/degenerate OBJ");
  var large=new StringBuilder();for(int i=0;i<22000;i++)large.AppendLine($"v {i} 0 0\nv {i+1} 0 0\nv {i} 1 0");for(int i=0;i<22000;i++)large.AppendLine($"f {i*3+1} {i*3+2} {i*3+3}");
  var split=ObjImporter.Parse(large.ToString()).Model;check(split.Nodes.Count==2&&split.Nodes.All(n=>n.VertexCount<=65536)&&split.Nodes.Sum(n=>n.IndexCount/3)==22000,"large OBJ splits into native 16-bit objects without dropping triangles");
  string material=NativeMaterialText.Create(new[]{"Paint","Glass"},"diffuse.dds","specular.dds","normal.dds");string other=material[material.IndexOf("$SUBMATERIAL Glass",StringComparison.Ordinal)..];
  string edited=NativeMaterialText.SetTexture(material,"Paint",1,"C:/textures/new spec.dds");
  check(edited.Contains("$TEXTURE_MTL 1 C:/textures/new spec.dds")&&edited[edited.IndexOf("$SUBMATERIAL Glass",StringComparison.Ordinal)..]==other,"specular import changes only its native material slot, retaining other materials");
  check(NativeMaterialText.SetTexture("$SUBMATERIAL Paint\n$DIFFUSECOLOR 1 1 1 1\n$END\n","Paint",2,"normal.dds").Contains("$TEXTURE_MTL 2 normal.dds"),"texture import adds a missing native channel");
  reject(()=>NativeMaterialText.SetTexture(material,"Missing",0,"map.dds"),"unknown texture material");
 }
}
