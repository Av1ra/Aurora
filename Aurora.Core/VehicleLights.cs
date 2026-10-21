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
