using Aurora.Core;
using System.Numerics;
static class MechanismAxisChecks
{
 public static void Run(Action<bool,string> check,Action<Action,string> reject,string? native)
 {
  var axis=MechanismAxis.Read("$MIXER_DRUM drum 1 2 3 1 2 5\n$END\n","MIXER_DRUM",0);var rotation=axis.Rotation(90);
  check(Vector3.Distance(Vector3.Transform(axis.Start,rotation),axis.Start)<1e-5f&&Vector3.Distance(Vector3.Transform(axis.End,rotation),axis.End)<1e-5f&&Vector3.Distance(Vector3.Transform(new(2,2,3),rotation),new(1,3,3))<1e-5f,"mixer pose fixes both axis endpoints and rotates around the offset axis");
  reject(()=>MechanismAxis.Read("$MIXER_DRUM drum 0 0 0 0 0 0\n","MIXER_DRUM",0),"coincident mixer axis points rejected");reject(()=>axis.Rotation(float.NaN),"nonfinite diagnostic pose rejected");
  var model=ObjImporter.Parse("o drum\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\no cab\nf 1 3 2\n").Model;var bytes=model.Data.ToArray();var points=model.Nodes[0].Positions.ToArray();var pose=axis.Pose(model,45);check(pose[0].Positions!=model.Nodes[0].Positions&&ReferenceEquals(pose[1],model.Nodes[1])&&model.Data.SequenceEqual(bytes)&&model.Nodes[0].Positions.SequenceEqual(points),"mechanism view leaves other meshes and source NMF geometry unchanged");reject(()=>new MechanismAxis("missing",Vector3.Zero,Vector3.UnitX).Pose(model,20),"missing mechanism mesh cannot pose another object");
  if(native==null||VehicleAssets.Media(Path.GetDirectoryName(native)??native) is not string media)return;int verified=0,missing=0;
  foreach(string file in Directory.EnumerateFiles(Path.Combine(media,"vehicles"),"script.ini",SearchOption.AllDirectories)){
   string script=File.ReadAllText(file);foreach(var field in VehicleScript.Fields(script).Where(f=>MechanismAxis.Supports(f.Key))){string? reference=field.Key.Contains("JOINT",StringComparison.Ordinal)?VehicleScript.Tokens(script).FirstOrDefault(t=>t.Key=="JOINT"&&t.Index==0)?.Value:Path.Combine(Path.GetDirectoryName(file)!,"main.nmf");if(reference==null)continue;NativeModel actual;try{actual=new(File.ReadAllBytes(VehicleAssets.Resolve(reference,Path.GetDirectoryName(file)!,media)));}catch(InvalidDataException){continue;}var mechanism=MechanismAxis.Read(script,field.Key,field.Occurrence);if(actual.Nodes.Count(n=>n.Name==mechanism.Mesh)!=1){missing++;continue;}var posed=mechanism.Pose(actual,30);var original=actual.Nodes.Single(n=>n.Name==mechanism.Mesh);var changed=posed.Single(n=>n.Name==mechanism.Mesh);for(int i=0;i<original.VertexCount;i++){var p=new Vector3(original.Positions[i*3],original.Positions[i*3+1],original.Positions[i*3+2]);var q=new Vector3(changed.Positions[i*3],changed.Positions[i*3+1],changed.Positions[i*3+2]);if(MathF.Abs(Vector3.Distance(p,mechanism.Start)-Vector3.Distance(q,mechanism.Start))>1e-3f)throw new Exception("Mechanism pose changes size: "+file);}verified++;}
  }check(verified>20,"native dumper/mixer and joint-local axes preserve size while posing the configured mesh: "+verified+"; native missing/ambiguous mesh references: "+missing);
 }
}
