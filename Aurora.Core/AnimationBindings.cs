using System.Globalization;
namespace Aurora.Core;
public static class AnimationBindings
{
 public static readonly (string Key,string Label)[] Actions=[("Preview","Только просмотр"),("WORKING_ANIM","Работа механизма"),("LANDING_GEAR_ANIM","Выпуск и уборка шасси"),("MOVEMENT_ANIM","Движение локомотива"),("GARBAGE_LOADING_ANIM","Загрузка мусоровоза"),("GARBAGE_UNLOADING_ANIM","Выгрузка мусоровоза"),("GARBAGE_UNLOADING_ANIM2","Дополнительная разгрузка мусоровоза")];
 public static bool Applies(string action,string type)=>action=="Preview"||action=="WORKING_ANIM"&&type=="VEHICLETYPE_ROAD"||action=="LANDING_GEAR_ANIM"&&type=="VEHICLETYPE_AIRPLANE"||action=="MOVEMENT_ANIM"&&type is "VEHICLETYPE_RAIL_LOCOMOTIVE" or "VEHICLETYPE_RAIL_VAGON"||action.StartsWith("GARBAGE_",StringComparison.Ordinal)&&type=="VEHICLETYPE_ROAD";
 public static bool AppliesToScript(string action,string script)
 {
  var fields=VehicleScript.Fields(script);string type=fields.FirstOrDefault(f=>f.Key=="TYPE")?.Value??"";if(!Applies(action,type))return false;
  if(action.StartsWith("GARBAGE_",StringComparison.Ordinal))return fields.Any(f=>f.Key is "SKILL_WASTE_CONTAINER_BIG" or "SKILL_WASTE_CONTAINER_SMALL"||f.Key=="RESOURCE_TRANSPORT_TYPE"&&f.Value=="RESOURCE_TRANSPORT_WASTE");return true;
 }
 public static bool Attached(string action)=>action is "LANDING_GEAR_ANIM" or "MOVEMENT_ANIM" or "GARBAGE_LOADING_ANIM" or "GARBAGE_UNLOADING_ANIM" or "GARBAGE_UNLOADING_ANIM2";
 public static NativeModel StaticModel(NativeModel model,AnimationClip[] clips){var moving=clips.Where(c=>Attached(c.Action)).SelectMany(c=>c.Tracks).Select(t=>t.Mesh).ToHashSet();return model.Nodes.Any(n=>moving.Contains(n.Name))?ModelEditing.KeepMeshes(model,model.Nodes.Where(n=>!moving.Contains(n.Name)).Select(n=>n.Name).ToHashSet()):model;}
 public static string Write(string script,NativeModel model,AnimationClip[] clips,string output,string? previousScript=null)
 {
  var selected=clips.Where(c=>c.Action!="Preview").ToArray();if(selected.Select(c=>c.Action).Distinct().Count()!=selected.Length)throw new InvalidDataException("Only one clip can bind to each vehicle action");
  foreach(var clip in selected){if(!Actions.Any(a=>a.Key==clip.Action)||!AppliesToScript(clip.Action,script))throw new InvalidDataException("Animation action is incompatible with vehicle type");string stem="animation_"+clip.Action.ToLowerInvariant();var animated=Attached(clip.Action)?ModelEditing.KeepMeshes(model,clip.Tracks.Select(t=>t.Mesh).ToHashSet()):model;File.WriteAllBytes(Path.Combine(output,stem+".nmf"),AnimatedNmf.Write(animated,clip));File.WriteAllBytes(Path.Combine(output,stem+".naf"),AnimatedNmf.Naf(clip));string[] old=VehicleScript.Tokens(previousScript??script).Where(t=>t.Key==clip.Action&&t.Occurrence==0).Select(t=>t.Value).ToArray();string args=stem+".nmf";
   if(clip.Action=="WORKING_ANIM")args+=" "+(old.Length>=3?string.Join(" ",old.Skip(1)):"0 0");if(clip.Action=="MOVEMENT_ANIM")args+=" "+(old.Length>=2?old[1]:500f.ToString(CultureInfo.InvariantCulture));script=VehicleScript.Set(script,clip.Action,0,args);
  }
  var parents=selected.Where(c=>Attached(c.Action)).SelectMany(c=>c.Tracks).Where(t=>t.CargoParent==t.Mesh).Select(t=>t.Mesh).Distinct().ToArray();if(parents.Length>1)throw new InvalidDataException("Cargo must use the same animated parent across game actions");
  if(parents.Length==1){if(selected.Where(c=>Attached(c.Action)).Any(c=>!c.Tracks.Any(t=>t.Mesh==parents[0])))throw new InvalidDataException("Every attached animation needs the cargo parent track");string bone=AnimatedNmf.Bone(parents[0]);var normal=VehicleScript.Fields(script).Where(f=>f.Key=="RESOURCE_VISUALIZATION").ToArray();var layouts=normal.Select(f=>VehicleScript.Cargo(script,f.Occurrence)!).ToArray();foreach(var field in normal.Reverse())script=VehicleScript.RemoveEntry(script,field.Key,field.Occurrence);foreach(var field in VehicleScript.Fields(script).Where(f=>f.Key=="RESOURCE_VISUALIZATION_BONE").ToArray())script=VehicleScript.SetCargo(script,VehicleScript.Cargo(script,field.Occurrence,field.Key),field.Occurrence,field.Key,bone);foreach(var layout in layouts)script=VehicleScript.SetCargo(script,layout,VehicleScript.Fields(script).Count(f=>f.Key=="RESOURCE_VISUALIZATION_BONE"),"RESOURCE_VISUALIZATION_BONE",bone);}
  return script;
 }
}
