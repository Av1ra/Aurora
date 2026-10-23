using Aurora.Core;
using System.Numerics;
static class MeshElementChecks
{
 public static void Run(Action<bool,string> check,Action<Action,string> reject)
 {
  var frame=Matrix4x4.CreateRotationY(.5f)*Matrix4x4.CreateTranslation(4,2,3);float[] p=[-3,0,0,-2,0,0,-3,1,0,2,0,0,3,0,0,2,1,0];var positions=Enumerable.Range(0,6).SelectMany(i=>{var v=Vector3.Transform(new(p[i*3],p[i*3+1],p[i*3+2]),frame);return new[]{v.X,v.Y,v.Z};}).ToArray();var mesh=new RigidMesh("tireShape",positions,Enumerable.Repeat(new[]{0f,0f,1f},6).SelectMany(x=>x).ToArray(),new float[12],[0,1,2,3,4,5],[new(0,6,0)],frame);var model=RigidScene.Build(["rubber"],[mesh]);var elements=MeshElements.Read(model.Nodes[0]);check(elements.Length==2&&elements.All(e=>e.Triangles.Length==1),"connected elements are independent of their common material group");
  var detached=MeshElements.Detach(model,0,new HashSet<int>{1});check(detached.Name=="mesh"&&detached.Model.Nodes.Count==2&&detached.Model.Nodes[0].IndexCount==3&&detached.Model.Nodes[1].IndexCount==3,"detach selected element only and assign mesh name");var node=detached.Model.Nodes[1];var expected=(new Vector3(positions[9],positions[10],positions[11])+new Vector3(positions[12],positions[13],positions[14])+new Vector3(positions[15],positions[16],positions[17]));check(Vector3.Distance(RigidScene.Frame(detached.Model,node).Translation,new((node.Positions.Where((_,i)=>i%3==0).Min()+node.Positions.Where((_,i)=>i%3==0).Max())/2,(node.Positions.Where((_,i)=>i%3==1).Min()+node.Positions.Where((_,i)=>i%3==1).Max())/2,(node.Positions.Where((_,i)=>i%3==2).Min()+node.Positions.Where((_,i)=>i%3==2).Max())/2))<.0001f,"detached part pivot is centred on that part with original oriented axes");
  var second=MeshElements.Detach(detached.Model,0,new HashSet<int>{0});check(second.Name=="mesh_1"&&second.RemovedOriginal&&second.Model.Nodes.Count==2,"later unnamed parts use mesh_1 and do not duplicate a fully detached source");
  reject(()=>MeshElements.Detach(model,0,new HashSet<int>()),"empty element detach");
  check(Vector3.Distance(ObjectPivots.Local(model,model.Nodes[0]),frame.Translation)<.0001f,"local object pivot retains an explicit source frame");
  var rebased=ObjectPivots.SetFrames(detached.Model,new Dictionary<int,Matrix4x4>{{1,Matrix4x4.CreateRotationZ(.3f)*Matrix4x4.CreateTranslation(8,9,10)}});
  check(RigidScene.Frame(rebased,rebased.Nodes[0])==RigidScene.Frame(detached.Model,detached.Model.Nodes[0])&&Vector3.Distance(RigidScene.Frame(rebased,rebased.Nodes[1]).Translation,new(8,9,10))<.0001f,"each object pivot edits independently without changing the other object's frame");
  check(rebased.Nodes.SelectMany(n=>n.Positions).Zip(detached.Model.Nodes.SelectMany(n=>n.Positions)).All(v=>MathF.Abs(v.First-v.Second)<.00001f),"pivot-only editing preserves visible geometry through native NMF serialization");
  check(ReferenceEquals(ObjectPivots.InitialiseSharedOrigins(rebased),rebased),"existing individual DCC pivots are retained");
  var raw=ObjImporter.Parse("o front\nv 2 0 4\nv 4 0 4\nv 3 2 4\nf 1 2 3\no rear\nv -4 0 -4\nv -2 0 -4\nv -3 2 -4\nf 4 5 6\n").Model;var individual=ObjectPivots.InitialiseSharedOrigins(raw);check(Vector3.Distance(ObjectPivots.Local(raw,raw.Nodes[0]),new(3,1,4))<.0001f,"legacy identity-frame objects use their own centre for tools and new animation axes");
  check(Vector3.Distance(RigidScene.Frame(individual,individual.Nodes[0]).Translation,new(3,1,4))<.0001f&&Vector3.Distance(RigidScene.Frame(individual,individual.Nodes[1]).Translation,new(-3,1,-4))<.0001f,"legacy shared identity origins become independent object centres");
  check(WheelDiagnostics.Read(ModelEditing.Rename(raw,"front","tireShape")).All(w=>!w.Steering),"generic tireShape is not forced to steer without native steering names");
  var adjacent=ObjImporter.Parse("o surface\nv 0 0 0\nv 1 0 0\nv 0 1 0\nv 1 1 0\nusemtl first\nf 1 2 3\nusemtl second\nf 2 4 3\n").Model;check(MeshElements.Read(adjacent.Nodes[0]).Length==1,"shared geometric edge joins UV/material seams into one connected element");
 }
}
