using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
namespace Aurora.Core;

public sealed record VehicleLight(bool Rear,Vector3 Position);
public static class VehicleLights
{
 static readonly Regex lights=new(@"(?m)^[\t ]*\$LIGHT_(FORWARD|BACKWARD)[\t ]+([^\r\n]+)",RegexOptions.CultureInvariant);
 static Vector3 Position(string text){var parts=text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);if(parts.Length!=3)throw new InvalidDataException("Некорректные координаты огня.");var p=parts.Select(x=>float.Parse(x,CultureInfo.InvariantCulture)).ToArray();if(p.Any(x=>!float.IsFinite(x)))throw new InvalidDataException("Некорректные координаты огня.");return new(p[0],p[1],p[2]);}
 static string Coordinates(Vector3 p){if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z))throw new InvalidDataException("Некорректные координаты огня.");return string.Join(" ",new[]{p.X,p.Y,p.Z}.Select(x=>x.ToString("G9",CultureInfo.InvariantCulture)));}
 public static List<VehicleLight> Read(string text)=>VehicleScript.Fields(text).Where(f=>f.Key is "LIGHT_FORWARD" or "LIGHT_BACKWARD").Select(f=>new VehicleLight(f.Key=="LIGHT_BACKWARD",Position(f.Value))).ToList();
 public static string Write(string script,IEnumerable<VehicleLight> values)
 {
  if(!VehicleScript.Fields(script).Any(f=>f.Key=="TYPE"&&f.Value.StartsWith("VEHICLETYPE_",StringComparison.Ordinal)))throw new InvalidDataException("Выберите script.ini транспортного средства.");
  string clean=script;foreach(var field in VehicleScript.Fields(script).Where(f=>f.Key is "LIGHT_FORWARD" or "LIGHT_BACKWARD").Reverse())clean=VehicleScript.Set(clean,field.Key,field.Occurrence,null);
  int front=0,rear=0;foreach(var light in values)clean=VehicleScript.Set(clean,light.Rear?"LIGHT_BACKWARD":"LIGHT_FORWARD",light.Rear?rear++:front++,Coordinates(light.Position));return clean;
 }
 public static string Transform(string script,Matrix4x4 transform)
 {
  if(transform==Matrix4x4.Identity)return script;
  // Main cargo frames are understood; unknown joint/attachment sections still
  // reject a partial transformation rather than leaving stale coordinates.
  var known=new HashSet<string>(StringComparer.Ordinal){"TYPE","SOUND_PARAMS","NAME","NAME_STR","DESCRIPTION","COST_RUB","COST_USD","COST_WORK","RESOURCE_CAPACITY","RESOURCE_TRANSPORT_TYPE","RESOURCE_ALLOW_ONLY","RESOURCE_VISUALIZATION","AVAILABLE","COUNTRY","LIFESPAN","PURCHASE_EXCLUDE","DRIVER","LIGHT_FORWARD","LIGHT_BACKWARD","LIGHT_BLINKER","PARTICLE_MOVEMENT","PARTICLE_ENGINE","FAMILY"};
  foreach(var field in VehicleScript.Fields(script)){
   string key=field.Key;if(!known.Contains(key)&&!key.StartsWith("MOVEMENT_",StringComparison.Ordinal)&&!key.StartsWith("SKILL_",StringComparison.Ordinal)&&!Regex.IsMatch(key,@"^(LOD\d*|LODDISTANCE\d*)$"))throw new InvalidDataException("Pivot: неподдерживаемая секция script.ini: "+key);
  }
  string updated=script;foreach(var field in VehicleScript.Fields(script).Where(f=>f.Key is "LIGHT_FORWARD" or "LIGHT_BACKWARD" or "LIGHT_BLINKER" or "DRIVER" or "PARTICLE_MOVEMENT" or "PARTICLE_ENGINE")){
   string suffix=field.Value,prefix="";
   if(field.Key.StartsWith("PARTICLE_",StringComparison.Ordinal)){int split=suffix.IndexOfAny([' ','\t']);if(split<0)throw new InvalidDataException("Invalid particle position");prefix=suffix[..split]+" ";suffix=suffix[(split+1)..];}
   var p=Position(suffix);if(p.Y<=-999)continue;
   updated=VehicleScript.Set(updated,field.Key,field.Occurrence,prefix+Coordinates(Vector3.Transform(p,transform)));
  }
  foreach(var field in VehicleScript.Fields(script).Where(f=>f.Key=="RESOURCE_VISUALIZATION"))updated=VehicleScript.SetCargo(updated,VehicleScript.TransformCargo(VehicleScript.Cargo(script,field.Occurrence)!,transform),field.Occurrence);
  return updated;
 }
}
