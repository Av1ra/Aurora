using System.Numerics;
using System.Text;
namespace Aurora.Core;
public sealed record RigidMesh(string Name,float[] Positions,float[] Normals,float[] Uv,ushort[] Indices,DrawGroup[] Groups,Matrix4x4 Frame);
public static class RigidScene
{
 public static Matrix4x4 Frame(NativeModel model,MeshNode node){var a=Enumerable.Range(0,16).Select(i=>model.F(node.Offset+76+i*4)).ToArray();return new(a[0],a[1],a[2],a[3],a[4],a[5],a[6],a[7],a[8],a[9],a[10],a[11],a[12],a[13],a[14],a[15]);}
 public static RigidMesh Mesh(NativeModel model,MeshNode node)=>new(node.Name,node.Positions,node.Normals,node.Uv,node.Indices,node.Groups,Frame(model,node));
 public static NativeModel Build(string[] materials,IEnumerable<RigidMesh> source)
 {
  if(materials.Length is <1 or >512)throw new InvalidDataException("Invalid material count");var meshes=source.ToArray();if(meshes.Length is <1 or >2048)throw new InvalidDataException("Invalid mesh count");
  using var stream=new MemoryStream();using var w=new BinaryWriter(stream);
  void Name(string name){byte[] bytes=Encoding.UTF8.GetBytes(name);if(bytes.Length is <1 or >63||name.Any(char.IsControl))throw new InvalidDataException("Mesh/material name exceeds native limits");w.Write(bytes);w.Write(new byte[64-bytes.Length]);}
  void Patch(long at,int value){long end=stream.Position;stream.Position=at;w.Write(value);stream.Position=end;}
  void V(Vector3 v){if(!float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z))throw new InvalidDataException("Nonfinite scene coordinate");w.Write(v.X);w.Write(v.Y);w.Write(v.Z);}
  void Matrix(Matrix4x4 m){foreach(float x in new[]{m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44}){if(!float.IsFinite(x))throw new InvalidDataException("Nonfinite scene frame");w.Write(x);}}
  w.Write("fromObj\0"u8);w.Write(materials.Length);w.Write(meshes.Length);w.Write(0);foreach(string name in materials)Name(name);
  foreach(var mesh in meshes){int vertices=mesh.Positions.Length/3;if(vertices is <1 or >65536||mesh.Positions.Length%3!=0||mesh.Normals.Length!=vertices*3||mesh.Uv.Length!=vertices*2||mesh.Indices.Length==0||mesh.Indices.Length%3!=0||mesh.Indices.Any(i=>i>=vertices)||mesh.Uv.Any(v=>!float.IsFinite(v))||mesh.Groups.Length==0||mesh.Groups.Any(g=>g.First<0||g.Count<0||g.First%3!=0||g.Count%3!=0||(long)g.First+g.Count>mesh.Indices.Length||g.Material<0||g.Material>=materials.Length)||!Matrix4x4.Invert(mesh.Frame,out var inverse))throw new InvalidDataException("Invalid rigid mesh");
   var p=Enumerable.Range(0,vertices).Select(i=>Vector3.Transform(new(mesh.Positions[i*3],mesh.Positions[i*3+1],mesh.Positions[i*3+2]),inverse)).ToArray();var normal=Matrix4x4.Transpose(mesh.Frame);
   long start=stream.Position;w.Write(0);w.Write(0);Name(mesh.Name);w.Write((ushort)65535);w.Write((ushort)0);Matrix(mesh.Frame);Matrix(mesh.Frame);
   V(new(p.Min(v=>v.X),p.Min(v=>v.Y),p.Min(v=>v.Z)));V(new(p.Max(v=>v.X),p.Max(v=>v.Y),p.Max(v=>v.Z)));w.Write(1);
   long lod=stream.Position;w.Write(0);w.Write(vertices);w.Write(mesh.Indices.Length);w.Write(mesh.Groups.Length);w.Write(0);w.Write(0x108);w.Write(0);
   foreach(ushort i in mesh.Indices)w.Write(i);foreach(var v in p)V(v);for(int i=0;i<vertices;i++){var n=Vector3.TransformNormal(new(mesh.Normals[i*3],mesh.Normals[i*3+1],mesh.Normals[i*3+2]),normal);V(n.LengthSquared()>1e-16f?Vector3.Normalize(n):Vector3.UnitY);}foreach(float uv in mesh.Uv)w.Write(uv);
   foreach(var group in mesh.Groups){w.Write(group.First);w.Write(group.Count);w.Write((ushort)group.Material);w.Write((ushort)0);}Patch(start+4,checked((int)(stream.Position-start)));Patch(lod,checked((int)(stream.Position-lod)));
  }
  Patch(16,checked((int)stream.Length));return new(stream.ToArray());
 }
 public static RigidMesh Triangles(RigidMesh mesh,IEnumerable<int> triangles,string name)
 {
  var positions=new List<float>();var normals=new List<float>();var uv=new List<float>();var indices=new List<ushort>();var groups=new List<DrawGroup>();var vertices=new Dictionary<int,ushort>();
  foreach(int triangle in triangles){int first=triangle*3;var group=mesh.Groups.FirstOrDefault(g=>first>=g.First&&first<g.First+g.Count)??throw new InvalidDataException("Triangle has no material");if(groups.Count==0||groups[^1].Material!=group.Material)groups.Add(new(indices.Count,0,group.Material));
   for(int j=0;j<3;j++){int old=mesh.Indices[first+j];if(!vertices.TryGetValue(old,out ushort next)){if(vertices.Count>=65536)throw new InvalidDataException("Native vertex limit exceeded");next=(ushort)vertices.Count;vertices[old]=next;positions.AddRange(mesh.Positions.AsSpan(old*3,3).ToArray());normals.AddRange(mesh.Normals.AsSpan(old*3,3).ToArray());uv.AddRange(mesh.Uv.AsSpan(old*2,2).ToArray());}indices.Add(next);}groups[^1]=groups[^1] with{Count=groups[^1].Count+3};
  }
  return new(name,positions.ToArray(),normals.ToArray(),uv.ToArray(),indices.ToArray(),groups.ToArray(),mesh.Frame);
 }
}
