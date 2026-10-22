using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
namespace Aurora.Core;
public sealed record SceneMaterial(string Name,string Texture="");
public sealed record SceneObject(string Name,int Parent,float[] Frame,float[] Positions,float[] Normals,float[] Uv,int[] Indices,DrawGroup[] Groups)
{
 [System.Text.Json.Serialization.JsonIgnore] public Matrix4x4 Matrix=>SceneInterchange.Matrix(Frame);
 [System.Text.Json.Serialization.JsonIgnore] public bool IsMesh=>Positions.Length>0;
}
public sealed record InterchangeScene(string Metadata,SceneMaterial[] Materials,SceneObject[] Nodes);
public static class SceneInterchange
{
 static readonly JsonSerializerOptions json=new(){WriteIndented=true};
 public static float[] Values(Matrix4x4 m)=>[m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
 public static Matrix4x4 Matrix(float[] a){if(a.Length!=16||a.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Invalid scene matrix");return new(a[0],a[1],a[2],a[3],a[4],a[5],a[6],a[7],a[8],a[9],a[10],a[11],a[12],a[13],a[14],a[15]);}
 public static SceneObject Object(NativeModel model,MeshNode node,int parent=-1)=>new(node.Name,parent,Values(RigidScene.Frame(model,node)),node.Positions,node.Normals,node.Uv,node.Indices.Select(i=>(int)i).ToArray(),node.Groups);
 public static NativeModel Model(InterchangeScene scene)
 {
  Validate(scene);var meshes=scene.Nodes.Where(n=>n.IsMesh).ToArray();if(meshes.Select(n=>n.Name).Distinct().Count()!=meshes.Length)throw new InvalidDataException("FBX contains duplicate mesh names; rename them before importing");
  return RigidScene.Build(scene.Materials.Select(m=>m.Name).ToArray(),meshes.Select(n=>new RigidMesh(n.Name,n.Positions,n.Normals,n.Uv,n.Indices.Select(i=>checked((ushort)i)).ToArray(),n.Groups,n.Matrix)));
 }
 public static void Validate(InterchangeScene scene)
 {
  if(scene.Nodes.Length is <1 or >4096||scene.Materials.Length is <1 or >512)throw new InvalidDataException("Scene limits exceeded");
  for(int i=0;i<scene.Nodes.Length;i++){var n=scene.Nodes[i];if(n.Parent< -1||n.Parent>=scene.Nodes.Length||n.Parent==i||!Matrix4x4.Invert(n.Matrix,out _))throw new InvalidDataException("Invalid scene parent or frame");int parent=n.Parent,depth=0;while(parent>=0){if(parent>=scene.Nodes.Length||parent==i||++depth>128)throw new InvalidDataException("Cyclic or excessive hierarchy");parent=scene.Nodes[parent].Parent;}
   int vertices=n.Positions.Length/3;if(n.Positions.Length%3!=0||n.Normals.Length!=vertices*3||n.Uv.Length!=vertices*2||vertices>65536||n.Indices.Length%3!=0||n.Indices.Length>3000000||n.Indices.Any(v=>v<0||v>=vertices)||n.Positions.Concat(n.Normals).Concat(n.Uv).Any(v=>!float.IsFinite(v))||n.Groups.Any(g=>g.First<0||g.Count<0||g.First+g.Count>n.Indices.Length||g.Material<0||g.Material>=scene.Materials.Length))throw new InvalidDataException("Invalid scene geometry");
  }
 }
 public static void WriteBinary(string path,InterchangeScene scene)
 {
  Validate(scene);using var file=File.Create(path);using var w=new BinaryWriter(file);void String(string s){byte[] bytes=Encoding.UTF8.GetBytes(s);w.Write(bytes.Length);w.Write(bytes);}
  w.Write("AURSCN01"u8);String(scene.Metadata);w.Write(scene.Materials.Length);foreach(var m in scene.Materials){String(m.Name);String(m.Texture);}w.Write(scene.Nodes.Length);
  foreach(var n in scene.Nodes){String(n.Name);w.Write(n.Parent);foreach(float v in n.Frame)w.Write(v);w.Write(n.Positions.Length/3);foreach(var array in new[]{n.Positions,n.Normals,n.Uv})foreach(float v in array)w.Write(v);w.Write(n.Indices.Length);foreach(int i in n.Indices)w.Write(i);w.Write(n.Groups.Length);foreach(var g in n.Groups){w.Write(g.First);w.Write(g.Count);w.Write(g.Material);}}
 }
 public static InterchangeScene ReadBinary(string path)
 {
  if(new FileInfo(path).Length>128L*1024*1024)throw new InvalidDataException("Scene exceeds 128 MiB");using var file=File.OpenRead(path);using var r=new BinaryReader(file);
  int Count(int max){int n=r.ReadInt32();if(n<0||n>max)throw new InvalidDataException("Scene limit exceeded");return n;}
  string String(){int n=Count(16*1024*1024);byte[] bytes=r.ReadBytes(n);if(bytes.Length!=n)throw new EndOfStreamException();return new UTF8Encoding(false,true).GetString(bytes);}
  float[] Floats(int n)=>Enumerable.Range(0,n).Select(_=>r.ReadSingle()).ToArray();
  if(!r.ReadBytes(8).AsSpan().SequenceEqual("AURSCN01"u8))throw new InvalidDataException("Unknown scene schema");string meta=String();int nm=Count(512);var materials=Enumerable.Range(0,nm).Select(_=>new SceneMaterial(String(),String())).ToArray();int nn=Count(4096);var nodes=new SceneObject[nn];
  for(int i=0;i<nn;i++){string name=String();int parent=r.ReadInt32();float[] frame=Floats(16);int nv=Count(65536);var p=Floats(nv*3);var n=Floats(nv*3);var uv=Floats(nv*2);int ni=Count(3000000);int[] indices=Enumerable.Range(0,ni).Select(_=>r.ReadInt32()).ToArray();int ng=Count(512);var groups=Enumerable.Range(0,ng).Select(_=>new DrawGroup(r.ReadInt32(),r.ReadInt32(),r.ReadInt32())).ToArray();nodes[i]=new(name,parent,frame,p,n,uv,indices,groups);}
  if(file.Position!=file.Length)throw new InvalidDataException("Trailing scene data");var scene=new InterchangeScene(meta,materials,nodes);Validate(scene);return scene;
 }
 public static async Task<InterchangeScene> ImportFbx(string bridge,string path)
 {
  string temp=Path.Combine(Path.GetTempPath(),"Aurora-scene-"+Guid.NewGuid().ToString("N")+".asb");try{await Run(bridge,"import",path,temp);return ReadBinary(temp);}finally{if(File.Exists(temp))File.Delete(temp);}
 }
 public static async Task ExportFbx(string bridge,string path,InterchangeScene scene)
 {
  string temp=Path.Combine(Path.GetTempPath(),"Aurora-scene-"+Guid.NewGuid().ToString("N")+".asb");string pending=path+".writing.fbx";
  try{WriteBinary(temp,scene);await Run(bridge,"export",temp,pending);File.Move(pending,path,true);}finally{if(File.Exists(temp))File.Delete(temp);if(File.Exists(pending))File.Delete(pending);}
 }
 static async Task Run(string bridge,string mode,string input,string output)
 {
  var start=new ProcessStartInfo(bridge){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};start.ArgumentList.Add(mode);start.ArgumentList.Add(Path.GetFullPath(input));start.ArgumentList.Add(Path.GetFullPath(output));using var process=Process.Start(start)??throw new IOException("Cannot start FBX converter");var error=process.StandardError.ReadToEndAsync();try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));}catch{if(!process.HasExited)process.Kill(true);throw;}string message=await error;if(process.ExitCode!=0)throw new InvalidDataException("FBX: "+message.Trim());
 }
 public static void ExportObj(string path,InterchangeScene scene)
 {
  Validate(scene);var text=new StringBuilder("# Aurora scene. Pivot/helper data: "+Path.GetFileName(path)+".aur-scene.json\nmtllib "+Path.GetFileNameWithoutExtension(path)+".mtl\n");int offset=1;
  string F(float v)=>v.ToString("R",CultureInfo.InvariantCulture);
  foreach(var n in scene.Nodes.Where(n=>n.IsMesh)){text.AppendLine("o "+n.Name);for(int i=0;i<n.Positions.Length;i+=3)text.AppendLine("v "+F(n.Positions[i])+" "+F(n.Positions[i+1])+" "+F(n.Positions[i+2]));for(int i=0;i<n.Normals.Length;i+=3)text.AppendLine("vn "+F(n.Normals[i])+" "+F(n.Normals[i+1])+" "+F(n.Normals[i+2]));for(int i=0;i<n.Uv.Length;i+=2)text.AppendLine("vt "+F(n.Uv[i])+" "+F(1-n.Uv[i+1]));foreach(var g in n.Groups){text.AppendLine("usemtl "+scene.Materials[g.Material].Name);for(int i=g.First;i<g.First+g.Count;i+=3)text.AppendLine("f "+string.Join(" ",n.Indices.AsSpan(i,3).ToArray().Select(v=>$"{v+offset}/{v+offset}/{v+offset}")));}offset+=n.Positions.Length/3;}
  File.WriteAllText(path,text.ToString(),new UTF8Encoding(false));File.WriteAllText(path+".aur-scene.json",JsonSerializer.Serialize(scene,json),new UTF8Encoding(false));
  File.WriteAllText(Path.ChangeExtension(path,".mtl"),string.Join("\n",scene.Materials.Select(m=>"newmtl "+m.Name+"\nKd 1 1 1\n"+(m.Texture.Length==0?"":"map_Kd "+m.Texture.Replace('\\','/')+"\n"))),new UTF8Encoding(false));
 }
 public static InterchangeScene ImportObj(string path)
 {
  var result=ObjImporter.Read(path);string sidecar=path+".aur-scene.json";if(!File.Exists(sidecar))return new("",result.Model.Materials.Select(m=>new SceneMaterial(m)).ToArray(),result.Model.Nodes.Select(n=>Object(result.Model,n)).ToArray());
  if(new FileInfo(sidecar).Length>128L*1024*1024)throw new InvalidDataException("Scene metadata too large");var saved=JsonSerializer.Deserialize<InterchangeScene>(File.ReadAllText(sidecar))??throw new InvalidDataException("Invalid scene metadata");Validate(saved);
  var meshes=saved.Nodes.Where(n=>n.IsMesh).ToArray();var names=meshes.Select(n=>ObjImporter.Identifier(n.Name)).ToArray();if(meshes.Length!=result.Model.Nodes.Count||names.Distinct().Count()!=names.Length)throw new InvalidDataException("OBJ objects differ from pivot companion; use FBX for structural changes");var nodes=saved.Nodes.Select(n=>{if(!n.IsMesh)return n;var imported=result.Model.Nodes.SingleOrDefault(m=>m.Name==ObjImporter.Identifier(n.Name))??throw new InvalidDataException("OBJ object missing from pivot companion: "+n.Name);return n with{Positions=imported.Positions,Normals=imported.Normals,Uv=imported.Uv,Indices=imported.Indices.Select(v=>(int)v).ToArray(),Groups=imported.Groups};}).ToArray();
  var materials=result.Model.Materials.Select(m=>saved.Materials.FirstOrDefault(s=>ObjImporter.Identifier(s.Name)==m)??new(m)).ToArray();var scene=saved with{Nodes=nodes,Materials=materials};Validate(scene);return scene;
 }
}
