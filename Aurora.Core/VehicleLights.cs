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
 public static List<VehicleLight> Read(string text)=>lights.Matches(text).Select(m=>new VehicleLight(m.Groups[1].Value=="BACKWARD",Position(m.Groups[2].Value))).ToList();
 public static string Write(string script,IEnumerable<VehicleLight> values)
 {
  if(!Regex.IsMatch(script,@"(?m)^\s*\$TYPE\s+VEHICLETYPE_"))throw new InvalidDataException("Выберите script.ini транспортного средства.");
  string clean=lights.Replace(script,"");string newline=script.Contains("\r\n")?"\r\n":"\n";
  string block=string.Join(newline,values.Select(l=>"$LIGHT_"+(l.Rear?"BACKWARD":"FORWARD")+" "+Coordinates(l.Position)))+newline;
  // Native scripts terminate at 'end'; insert before it, never after it.
  var end=Regex.Match(clean,@"(?im)^[\t ]*end[\t ]*\r?$");
  return end.Success?clean.Insert(end.Index,block):clean+newline+block;
 }
 public static string Transform(string script,Matrix4x4 transform)
 {
  if(transform==Matrix4x4.Identity)return script;
  // Multi-line cargo/attachment geometry needs its own editor. Refuse to
  // silently leave an unrotated resource box or unsupported attachment.
  var known=new HashSet<string>(StringComparer.Ordinal){"TYPE","SOUND_PARAMS","NAME","DESCRIPTION","COST_RUB","COST_WORK","RESOURCE_CAPACITY","RESOURCE_TRANSPORT_TYPE","AVAILABLE","COUNTRY","SKILL_PERSONAL","DRIVER","LIGHT_FORWARD","LIGHT_BACKWARD","LIGHT_BLINKER","PARTICLE_MOVEMENT","PARTICLE_ENGINE","FAMILY"};
  foreach(Match m in Regex.Matches(script,@"(?m)^[\t ]*\$([A-Z_0-9]+)")){
   string key=m.Groups[1].Value;if(!known.Contains(key)&&!key.StartsWith("MOVEMENT_",StringComparison.Ordinal)&&!Regex.IsMatch(key,@"^(LOD\d+|LODDISTANCE\d+)$"))throw new InvalidDataException("Pivot: неподдерживаемая секция script.ini: "+key);
  }
  return Regex.Replace(script,@"(?m)^([\t ]*\$(?:LIGHT_FORWARD|LIGHT_BACKWARD|LIGHT_BLINKER|DRIVER|PARTICLE_MOVEMENT|PARTICLE_ENGINE)[\t ]+)([^\r\n]+)",m=>{
   string suffix=m.Groups[2].Value;string prefix=m.Groups[1].Value;
   if(prefix.Contains("PARTICLE_",StringComparison.Ordinal)){int split=suffix.IndexOfAny([' ','\t']);if(split<0)throw new InvalidDataException("Invalid particle position");prefix+=suffix[..split]+" ";suffix=suffix[(split+1)..];}
   var p=Position(suffix);if(p.Y<=-999)return m.Value; // Native disabled emitter sentinel.
   return prefix+Coordinates(Vector3.Transform(p,transform));
  });
 }
}
