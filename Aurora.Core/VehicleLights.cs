using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
namespace Aurora.Core;

public sealed record VehicleLight(bool Rear,Vector3 Position);
public static class VehicleLights
{
 static readonly Regex lights=new(@"(?m)^[\t ]*\$LIGHT_(FORWARD|BACKWARD)[\t ]+([^\r\n]+)",RegexOptions.CultureInvariant);
 static float Coordinate(string text){if(float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out float value)&&float.IsFinite(value))return value;var prefix=Regex.Match(text,@"^[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?");if(!prefix.Success||!float.TryParse(prefix.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||!float.IsFinite(value))throw new InvalidDataException("Некорректные координаты огня.");return value;}
 static Vector3 Position(string text){var parts=text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Take(3).ToArray();if(parts.Length!=3)throw new InvalidDataException("Некорректные координаты огня.");var p=parts.Select(Coordinate).ToArray();return new(p[0],p[1],p[2]);}
 public static string[] Warnings(string script)=>VehicleScript.Tokens(script).Where(t=>t.Key is "LIGHT_FORWARD" or "LIGHT_BACKWARD"&&t.Index<3).Where(t=>!float.TryParse(t.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out _)).Select(t=>t.Key+" "+t.Value).ToArray();
 static string Coordinates(Vector3 p){if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z))throw new InvalidDataException("Некорректные координаты огня.");return string.Join(" ",new[]{p.X,p.Y,p.Z}.Select(x=>x.ToString("G9",CultureInfo.InvariantCulture)));}
 public static List<VehicleLight> Read(string text){var tokens=VehicleScript.Tokens(text);return VehicleScript.Fields(text).Where(f=>f.Key is "LIGHT_FORWARD" or "LIGHT_BACKWARD").Select(f=>new VehicleLight(f.Key=="LIGHT_BACKWARD",Position(string.Join(" ",tokens.Where(t=>t.Key==f.Key&&t.Occurrence==f.Occurrence).Take(3).Select(t=>t.Value))))).ToList();}
 public static string Write(string script,IEnumerable<VehicleLight> values)
 {
  if(!VehicleScript.Fields(script).Any(f=>f.Key=="TYPE"&&f.Value.StartsWith("VEHICLETYPE_",StringComparison.Ordinal)))throw new InvalidDataException("Выберите script.ini транспортного средства.");
  var requested=values.ToArray();if(Read(script).SequenceEqual(requested))return script;string clean=script;
  foreach(string key in new[]{"LIGHT_FORWARD","LIGHT_BACKWARD"}){var target=requested.Where(l=>l.Rear==(key=="LIGHT_BACKWARD")).ToArray();var previous=Read(script).Where(l=>l.Rear==(key=="LIGHT_BACKWARD")).ToArray();var existing=VehicleScript.Fields(clean).Where(f=>f.Key==key).ToArray();for(int i=existing.Length-1;i>=target.Length;i--)clean=VehicleScript.RemoveEntry(clean,key,i);for(int i=0;i<target.Length;i++)if(i>=previous.Length||target[i]!=previous[i])clean=VehicleScript.SetArguments(clean,key,i,Coordinates(target[i].Position).Split(' '));}return clean;
 }
 public static string Transform(string script,Matrix4x4 transform)
 {
  if(transform==Matrix4x4.Identity)return script;
  var points=new Dictionary<string,int>(StringComparer.Ordinal){["LIGHT_FORWARD"]=0,["LIGHT_BACKWARD"]=0,["LIGHT_BLINKER"]=0,["DRIVER"]=0,["PARTICLE_MOVEMENT"]=1,["PARTICLE_ENGINE"]=1,["WATER_POINT"]=0,["SIREN_POINT"]=0,["TRACKTOR_TOOL_POINT"]=0,["MOVEMENT_WHEEL_FRONT"]=0,["MOVEMENT_WHEEL_BACK"]=0,["PARTICLE_JETPOINT"]=0,["HORSE"]=0,["DUMPER_DECK_PIVOT"]=1,["MIXER_DRUM"]=1};
  var known=new HashSet<string>(StringComparer.Ordinal){"TYPE","SOUND_PARAMS","SOUND_PARAMS_WORKSHOP","NAME","NAME_STR","DESCRIPTION","COST_RUB","COST_USD","COST_WORK","RESOURCE_CAPACITY","RESOURCE_TRANSPORT_TYPE","RESOURCE_ALLOW_ONLY","RESOURCE_VISUALIZATION","CARGOVEHICLE_VISUALIZATION","AVAILABLE","COUNTRY","COUNTRY_STR","LIFESPAN","PURCHASE_EXCLUDE","FAMILY","MOVEMENT_SPEED","MOVEMENT_POWER_KW","MOVEMENT_EMPTY_WEIGHT","MOVEMENT_CONSPUMPTION","MOVEMENT_HILL_ACCELERATION","MOVEMENT_HILL_SPEED","TAKEOFF_DISTANCE","CABIN_LIGHT","CABIN_HEAVY","TRACTION_ELECTRIC","SINGLE_HORSE_POWER","HORSE_MATERIAL"};
  foreach(var field in VehicleScript.Fields(script)){
   string key=field.Key;if(!known.Contains(key)&&!points.ContainsKey(key)&&!key.StartsWith("SKILL_",StringComparison.Ordinal)&&!key.StartsWith("TRAINGROUP_",StringComparison.Ordinal)&&!Regex.IsMatch(key,@"^(LOD\d*|LODDISTANCE\d*)$"))throw new InvalidDataException("Pivot: неподдерживаемая секция script.ini: "+key);
  }
  string updated=script;var tokens=VehicleScript.Tokens(script);foreach(var field in VehicleScript.Fields(script).Where(f=>points.ContainsKey(f.Key))){
   var args=tokens.Where(t=>t.Key==field.Key&&t.Occurrence==field.Occurrence).Select(t=>t.Value).ToArray();int start=points[field.Key];if(args.Length<start+3)throw new InvalidDataException("Invalid pivot point: "+field.Key);
   foreach(int at in field.Key=="MIXER_DRUM"?new[]{start,start+3}:new[]{start}){if(args.Length<at+3)throw new InvalidDataException("Invalid mixer axis");var position=Position(string.Join(" ",args.Skip(at).Take(3)));if(position.Y<=-999)continue;Coordinates(Vector3.Transform(position,transform)).Split(' ').CopyTo(args,at);}
   updated=VehicleScript.SetArguments(updated,field.Key,field.Occurrence,args);
  }
  foreach(var field in VehicleScript.Fields(script).Where(f=>f.Key=="RESOURCE_VISUALIZATION"))updated=VehicleScript.SetCargo(updated,VehicleScript.TransformCargo(VehicleScript.Cargo(script,field.Occurrence)!,transform),field.Occurrence);
  foreach(var field in VehicleScript.Fields(script).Where(f=>f.Key=="CARGOVEHICLE_VISUALIZATION")){var box=VehicleCargoBox.Read(script,field.Key,field.Occurrence)??throw new InvalidDataException("Missing cargo box");var corners=new[]{box.Min,new Vector3(box.Min.X,box.Min.Y,box.Max.Z),new Vector3(box.Min.X,box.Max.Y,box.Min.Z),new Vector3(box.Min.X,box.Max.Y,box.Max.Z),new Vector3(box.Max.X,box.Min.Y,box.Min.Z),new Vector3(box.Max.X,box.Min.Y,box.Max.Z),new Vector3(box.Max.X,box.Max.Y,box.Min.Z),box.Max}.Select(v=>Vector3.Transform(v,transform)).ToArray();if(MathF.Abs(transform.M12)+MathF.Abs(transform.M13)+MathF.Abs(transform.M21)+MathF.Abs(transform.M23)+MathF.Abs(transform.M31)+MathF.Abs(transform.M32)>1e-5f)throw new InvalidDataException("Pivot: vehicle cargo box cannot represent a rotated platform");updated=VehicleCargoBox.Write(updated,field.Key,field.Occurrence,new(corners.Aggregate(Vector3.Min),corners.Aggregate(Vector3.Max)));}
  return updated;
 }
}
