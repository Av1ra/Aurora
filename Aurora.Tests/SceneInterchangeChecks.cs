using Aurora.Core;
using System.Numerics;
static class SceneInterchangeChecks
{
 public static async Task Run(Action<bool,string> check,Action<Action,string> reject)
 {
  string folder=Path.Combine(Path.GetTempPath(),"Aurora-scene-тест-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  try{
   var frame=Matrix4x4.CreateScale(2,1,3)*Matrix4x4.CreateRotationY(.6f)*Matrix4x4.CreateTranslation(4,2,-3);
   float[] local=[0,0,0,1,0,0,0,1,0,3,0,0,4,0,0,3,1,0];var positions=Enumerable.Range(0,6).SelectMany(i=>{var p=Vector3.Transform(new(local[i*3],local[i*3+1],local[i*3+2]),frame);return new[]{p.X,p.Y,p.Z};}).ToArray();
   var normals=Enumerable.Repeat(new[]{0f,0f,1f},6).SelectMany(x=>x).ToArray();
   var mesh=new SceneObject("корпус",1,SceneInterchange.Values(frame),positions,normals,new float[12],[0,1,2,3,4,5],[new(0,3,0),new(3,3,1)]);
   var dummy=new SceneObject("vehicle_dummy",-1,SceneInterchange.Values(Matrix4x4.CreateTranslation(1,0,0)),[],[],[],[],[]);
   var scene=new InterchangeScene("{\"test\":true}",[new("краска"),new("rubber")],[mesh,dummy]);var model=SceneInterchange.Model(scene);
   bool Near(IEnumerable<float> a,IEnumerable<float> b)=>a.Zip(b).All(p=>Math.Abs(p.First-p.Second)<.0001f);
   check(Near(model.Nodes[0].Positions,positions)&&Near(SceneInterchange.Values(RigidScene.Frame(model,model.Nodes[0])),mesh.Frame),"scene native world geometry and nonuniform rotated pivot");
   var split=ModelEditing.Split(model,mesh.Name);check(split.Nodes.Count==2&&split.Nodes.All(n=>Near(SceneInterchange.Values(RigidScene.Frame(split,n)),mesh.Frame)),"disconnect split preserves every piece pivot");
   string binary=Path.Combine(folder,"scene.asb");SceneInterchange.WriteBinary(binary,scene);var binaryScene=SceneInterchange.ReadBinary(binary);check(binaryScene.Nodes[0].Parent==1&&binaryScene.Metadata==scene.Metadata,"scene binary hierarchy and metadata");
   string obj=Path.Combine(folder,"scene.obj");SceneInterchange.ExportObj(obj,scene);var objScene=SceneInterchange.ImportObj(obj);check(objScene.Nodes.Length==2&&objScene.Nodes[0].Name==mesh.Name&&Near(objScene.Nodes[0].Frame,mesh.Frame)&&Near(objScene.Nodes[0].Positions,positions),"OBJ companion restores named pivots, dummy and world geometry");
   reject(()=>SceneInterchange.Validate(scene with{Nodes=[mesh with{Parent=1},dummy with{Parent=0}]}),"cyclic scene hierarchy");
   string? bridge=Environment.GetEnvironmentVariable("AURORA_SCENE_BRIDGE");if(bridge!=null){string fbx=Path.Combine(folder,"модель.fbx");await SceneInterchange.ExportFbx(bridge,fbx,scene);var back=await SceneInterchange.ImportFbx(bridge,fbx);var body=back.Nodes.Single(n=>n.Name==mesh.Name);check(back.Nodes.Any(n=>n.Name==dummy.Name)&&back.Nodes[body.Parent].Name==dummy.Name&&back.Metadata==scene.Metadata,"FBX binary roundtrip names, dummy, parent and custom metadata");check(Near(body.Frame,mesh.Frame)&&Near(body.Positions,positions)&&Near(body.Normals,normals)&&body.Groups.Length==2&&back.Materials.Length==2,"FBX units, transforms, normals and material groups");}
  }finally{Directory.Delete(folder,true);}
 }
}
