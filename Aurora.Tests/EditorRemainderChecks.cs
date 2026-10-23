using Aurora.Editor;
using Aurora.Core;
using System.Numerics;
using System.Text.Json;
static class EditorRemainderChecks
{
 public static void Run(Action<bool,string> check,Action<Action,string> reject)
 {
  var track=new AnimationTrack("wheel",[2,1,3],[new(0,AnimationPose.Identity),new(100,new([1,2,3],[80,150,270],[2,.5f,1.5f]))],AxisRotation:[12,30,40]);
  var rebound=AnimationEditing.RebindPivot(track,new(-3,4,2),100,true);
  float[] Values(Matrix4x4 m)=>[m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
  check(Enumerable.Range(0,101).All(f=>Values(track.Matrix(f)).Zip(Values(rebound.Matrix(f))).All(p=>MathF.Abs(p.First-p.Second)<.0001f)),"rebind preserves every integer-frame world matrix with oriented axis and nonuniform scale");
  check(rebound.PivotSource=="Object"&&rebound.Keys.Length==101&&track.Keys.Length==2,"rebind is immutable and records explicit object-axis binding");
  var extended=AnimationEditing.RebindPivot(track,new(1,2,3),10000,true);extended.Validate(10000);check(extended.Keys.Length==10001,"full supported timeline length can be rebound");
  var moved=AnimationEditing.RebindPivot(track,new(1,2,3),100,false);check(ReferenceEquals(moved.Keys,track.Keys),"explicit axis relocation can retain editable sparse keys");
  reject(()=>AnimationEditing.RebindPivot(track,new(float.NaN,0,0),100,true),"nonfinite pivot rebind");
  string script="$TYPE VEHICLETYPE_ROAD\n$NAME 123\n$END\n";string literal=VehicleName.Write(script,false,"Bus (special)");check(!VehicleScript.Fields(literal).Any(f=>f.Key=="NAME")&&VehicleScript.Fields(literal).Any(f=>f.Key=="NAME_STR"&&f.Value.Contains("Bus (special)")),"literal naming retains parentheses and removes localization directive");
  string localized=VehicleName.Write(literal,true,"456");check(VehicleScript.Fields(localized).Any(f=>f.Key=="NAME"&&f.Value=="456")&&!VehicleScript.Fields(localized).Any(f=>f.Key=="NAME_STR"),"localization ID and literal modes roundtrip without competing directives");
  reject(()=>VehicleName.Write(script,true,"abc"),"invalid localization ID");reject(()=>VehicleName.Write(script,false,"line\nbreak"),"control characters in literal vehicle name");
  var model=ObjImporter.Parse("o wheel\nv 2 0 4\nv 4 0 4\nv 3 2 4\nf 1 2 3\n").Model;var objectId=Guid.NewGuid();var project=new EditorProject(1,[new("main.nmf","model.nmf",[],new(){{"wheel",objectId}},[objectId])],[new("paint","material.mtl",[])],0,0,null,null,script,[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1],Animations:[new("work",30,100,[track with{ObjectId=objectId}])]);var entries=new Dictionary<string,byte[]>{{"document.json",JsonSerializer.SerializeToUtf8Bytes(project)},{"model.nmf",model.Data},{"material.mtl",System.Text.Encoding.UTF8.GetBytes("$SUBMATERIAL paint\n$END\n")}};
  string folder=Path.Combine(Path.GetTempPath(),"Aurora-identity-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);try{var loaded=ProjectData.Load(entries,name=>Path.Combine(folder,name));check(loaded.Document.Models[0].ObjectIds!["wheel"]==objectId&&loaded.Document.Models[0].ExplicitPivots!.Single()==objectId&&loaded.Document.Animations![0].Tracks[0].ObjectId==objectId,"project restore preserves stable IDs, explicit zero-origin metadata and track references");var invalid=project with{Models=[project.Models[0] with{ExplicitPivots=[Guid.NewGuid()]}]};entries["document.json"]=JsonSerializer.SerializeToUtf8Bytes(invalid);reject(()=>ProjectData.Load(entries,name=>Path.Combine(folder,name)),"unknown explicit pivot identity");}finally{Directory.Delete(folder,true);}
  var identified=track with{ObjectId=Guid.NewGuid()};var roundtrip=JsonSerializer.Deserialize<AnimationTrack>(JsonSerializer.Serialize(identified))!;check(roundtrip.ObjectId==identified.ObjectId&&roundtrip.PivotSource==identified.PivotSource,"stable object ID and axis source survive project JSON");
  var split=AnimationEditing.SplitMesh([new("test",30,100,[identified])],"wheel",["left","right"]);check(split[0].Tracks.All(t=>t.ObjectId==null),"split pieces do not inherit one duplicate object identity");
 }
}
