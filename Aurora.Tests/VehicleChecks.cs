using Aurora.Core;
using Aurora.Editor;
using System.Numerics;
using System.Text.RegularExpressions;

static class VehicleChecks
{
 public static async Task Run(Action<bool,string> check,Action<Action,string> reject)
 {
  var source=ObjImporter.Parse("o lamp\nv 3 2 -1\nv 5 2 -1\nv 3 4 -1\nusemtl Paint\nf 1 2 3").Model;
  var original=source.Data.ToArray();
  foreach(int front in Enumerable.Range(0,6))foreach(int up in Enumerable.Range(0,6)){
   if(MathF.Abs(Vector3.Dot(ModelOrientation.Axes[front],ModelOrientation.Axes[up]))>.1f)continue;
   var t=ModelOrientation.Transform(source,front,up,false,false);
   check(Vector3.Distance(Vector3.TransformNormal(ModelOrientation.Axes[front],t),Vector3.UnitZ)<1e-5f&&Vector3.Distance(Vector3.TransformNormal(ModelOrientation.Axes[up],t),Vector3.UnitY)<1e-5f&&MathF.Abs(t.GetDeterminant()-1)<1e-5f,"axis correction maps front/up without mirroring "+front+"/"+up);
  }
  reject(()=>ModelOrientation.Transform(source,0,1,false,false),"parallel forward/up axes");
  var transform=ModelOrientation.Transform(source,2,4,true,true);var result=ModelOrientation.Apply(source,transform);var node=result.Nodes[0];
  check(source.Data.SequenceEqual(original)&&node.Uv.SequenceEqual(source.Nodes[0].Uv)&&node.Indices.SequenceEqual(source.Nodes[0].Indices)&&node.Groups.SequenceEqual(source.Nodes[0].Groups),"pivot bake retains source, UVs, winding and materials");
  check(node.Positions.Where((_,i)=>i%3==1).Min()==0&&node.Positions.Where((_,i)=>i%3==2).Min()==-node.Positions.Where((_,i)=>i%3==2).Max(),"pivot centres model and places base on ground");
  check(ModelOrientation.Apply(result,Matrix4x4.Identity).Data.SequenceEqual(result.Data),"baked rigid stream is stable on repeated serialization");
  var raw=source.Data.ToArray();var n=source.Nodes[0];BitConverter.GetBytes(2f).CopyTo(raw,n.Offset+76);BitConverter.GetBytes(3f).CopyTo(raw,n.Offset+128);var placed=new NativeModel(raw);
  check(ModelOrientation.Apply(placed,Matrix4x4.Identity).Nodes[0].Positions.SequenceEqual(placed.Nodes[0].Positions),"pivot bake retains native per-part placement before normalization");
  BitConverter.GetBytes(1f).CopyTo(raw,n.AttributeOffset+n.VertexCount*12);BitConverter.GetBytes(1f).CopyTo(raw,n.AttributeOffset+n.VertexCount*12+8);var scaled=new NativeModel(raw);
  check(MathF.Abs(scaled.Nodes[0].Normals[0]-.5f)<1e-6f&&scaled.Nodes[0].Normals[2]==1,"native normal transform uses inverse transpose for scaled parts");

  string script="$TYPE VEHICLETYPE_ROAD\r\n$MOVEMENT_SPEED 50\r\n$LIGHT_FORWARD 1 2 3\r\n$LIGHT_BLINKER 0 -1000 0\r\n$DRIVER 1 2 3\r\n$PARTICLE_MOVEMENT smoke 1 2 3\r\n$LOD1 old/lod1.nmf\r\nend\r\n";
  var lights=VehicleLights.Read(script);lights.Add(new(true,new(-1,2,-3)));string configured=VehicleLights.Write(script,lights);
  check(VehicleLights.Read(configured).Count==2&&configured.IndexOf("$LIGHT_BACKWARD",StringComparison.Ordinal)<configured.IndexOf("end",StringComparison.Ordinal)&&configured.Contains("$MOVEMENT_SPEED 50")&&configured.Contains("$LIGHT_BLINKER 0 -1000 0"),"native light assignment precedes end and retains physics and blinkers");
  var rotated=VehicleLights.Transform(configured,transform);var expected=Vector3.Transform(new(1,2,3),transform);
  check(Vector3.Distance(VehicleLights.Read(rotated)[0].Position,expected)<1e-5f&&rotated.Contains("$LIGHT_BLINKER 0 -1000 0")&&rotated.Contains("$PARTICLE_MOVEMENT smoke"),"pivot transforms lights/driver/particles and preserves disabled emitters");
  check(VehicleLights.Read(VehicleLights.Write(configured,[])).Count==0,"removing lights does not resurrect original positions");
  reject(()=>VehicleLights.Write("$TYPE BUILDING\nend",lights),"non-vehicle script template");
  reject(()=>VehicleLights.Transform(script+"$RESOURCE_VISUALIZATION\n",transform),"unsupported spatial script sections instead of silently corrupting them");
  reject(()=>VehicleLights.Write(script,[new(false,new(float.NaN,0,0))]),"nonfinite light position");

  string root=Path.Combine(Path.GetTempPath(),"Aurora-vehicle-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   string map=Path.Combine(root,"blank.dds");File.WriteAllBytes(map,DdsImage.Encode(new(1,1,[128,128,128,255])));string mtl=Path.Combine(root,"material.mtl");File.WriteAllText(mtl,NativeMaterialText.Create(source.Materials,map,map,map));
   string scriptFile=Path.Combine(root,"script.ini");File.WriteAllText(scriptFile,script);
   string main=Path.Combine(root,"main.nmf"),lod=Path.Combine(root,"lod1.nmf");var models=new Dictionary<string,NativeModel>{{main,result},{lod,ModelOrientation.Apply(source,transform)}};
   await ModelExport.Export(models,models.Keys.ToDictionary(k=>k,_=>new HashSet<string>()),new(),mtl,null,null,root,p=>Task.FromResult(DdsImage.Decode(File.ReadAllBytes(p))),rotated);
   string folder=Directory.GetDirectories(root,"Aurora-Model-*").Single();string saved=File.ReadAllText(Path.Combine(folder,"script.ini"));
   check(VehicleLights.Read(saved).SequenceEqual(VehicleLights.Read(rotated))&&Regex.IsMatch(saved,@"\$LOD1 lod1\.nmf")&&File.ReadAllText(scriptFile)==script,"full export saves edited game lights, rewrites LOD links and preserves source script");
   check(new NativeModel(File.ReadAllBytes(Path.Combine(folder,"lod1.nmf"))).Nodes[0].Positions.SequenceEqual(result.Nodes[0].Positions)&&File.ReadAllBytes(Path.Combine(folder,"bbox.bin")).Length==24,"same pivot across LODs with rebuilt native hitbox");
   string second=Path.Combine(root,"snapshot-only");Directory.CreateDirectory(second);
   await ModelExport.Export(models,models.Keys.ToDictionary(k=>k,_=>new HashSet<string>()),new(),mtl,null,null,second,p=>Task.FromResult(DdsImage.Decode(File.ReadAllBytes(p))),useSourceScript:false);
   check(!File.Exists(Path.Combine(Directory.GetDirectories(second).Single(),"script.ini")),"editor snapshot does not silently re-read an untransformed script from disk");
  }finally{Directory.Delete(root,true);}
 }
}
