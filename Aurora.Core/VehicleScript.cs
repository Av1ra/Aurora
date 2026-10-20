using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
namespace Aurora.Core;
public sealed record VehicleField(string Key,int Occurrence,string Value,string Body);
public sealed record CargoLayout(Vector3 Rotation,Vector3 EmptyPosition,Vector3 EmptyScale,Vector3 FullPosition,Vector3 FullScale);
public static class VehicleScript
{
 static readonly Regex headers=new(@"(?m)^[\t ]*\$([A-Z_0-9]+)[\t ]*([^\r\n]*)");
 public static VehicleField[] Fields(string script)
 {
  var matches=headers.Matches(script);var count=new Dictionary<string,int>();var end=Regex.Match(script,@"(?im)^[\t ]*end[\t ]*\r?$");
  return matches.Select((m,i)=>{string key=m.Groups[1].Value;int occurrence=count.GetValueOrDefault(key);count[key]=occurrence+1;int next=i+1<matches.Count?matches[i+1].Index:script.Length;if(end.Success&&end.Index>m.Index)next=Math.Min(next,end.Index);return new VehicleField(key,occurrence,m.Groups[2].Value.Trim(),script[(m.Index+m.Length)..next]);}).ToArray();
 }
 public static string Set(string script,string key,int occurrence,string? value)
 {
  if(!Regex.IsMatch(key,@"^[A-Z_0-9]+$")||occurrence<0||value?.IndexOfAny(['\r','\n'])>=0)throw new InvalidDataException("Invalid vehicle setting");
  var matches=headers.Matches(script).Where(m=>m.Groups[1].Value==key).ToArray();
  if(occurrence>=matches.Length){if(value==null)return script;if(occurrence!=matches.Length)throw new InvalidDataException("Invalid setting occurrence");return Insert(script,"$"+key+(value.Length>0?" "+value:"")+"\n");}
  var match=matches[occurrence];return script[..match.Index]+(value==null?"":"$"+key+(value.Length>0?" "+value:""))+script[(match.Index+match.Length)..];
 }
 static string Insert(string script,string text){var end=Regex.Match(script,@"(?im)^[\t ]*end[\t ]*\r?$");return end.Success?script.Insert(end.Index,text):script.TrimEnd()+"\n"+text+"end\n";}
 public static string SetBody(string script,string key,int occurrence,string body){if(body.Contains('$')||Regex.IsMatch(body,@"(?im)^\s*end\s*$"))throw new InvalidDataException("Invalid setting body");var match=headers.Matches(script).Where(m=>m.Groups[1].Value==key).ElementAt(occurrence);var field=Fields(script).Single(f=>f.Key==key&&f.Occurrence==occurrence);int at=match.Index+match.Length;return script[..at]+"\n"+body.TrimEnd()+"\n"+script[(at+field.Body.Length)..];}
 static Vector3 Vector(string line){var values=line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(p=>float.Parse(p,CultureInfo.InvariantCulture)).ToArray();if(values.Length!=3||values.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Invalid cargo vector");return new(values[0],values[1],values[2]);}
 static string Vector(Vector3 v){if(!float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z))throw new InvalidDataException("Invalid cargo vector");return string.Join(" ",new[]{v.X,v.Y,v.Z}.Select(f=>f.ToString("G9",CultureInfo.InvariantCulture)));}
 public static CargoLayout? Cargo(string script,int occurrence=0)
 {
  var field=Fields(script).FirstOrDefault(f=>f.Key=="RESOURCE_VISUALIZATION"&&f.Occurrence==occurrence);if(field==null)return null;
  // Negative numeric coordinates are valid; only explicit comment markers skip.
  var lines=field.Body.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).Select(l=>l.Trim()).Where(l=>l.Length>0&&!l.StartsWith("--",StringComparison.Ordinal)&&!l.StartsWith('#')).ToArray();
  if(lines.Length!=8||lines[0]!="rotation"||lines[2]!="min"||lines[5]!="max")throw new InvalidDataException("Unsupported cargo visualization layout");return new(Vector(lines[1]),Vector(lines[3]),Vector(lines[4]),Vector(lines[6]),Vector(lines[7]));
 }
 public static string SetCargo(string script,CargoLayout? layout,int occurrence=0)
 {
  var field=Fields(script).FirstOrDefault(f=>f.Key=="RESOURCE_VISUALIZATION"&&f.Occurrence==occurrence);string block=layout==null?"":"$RESOURCE_VISUALIZATION\nrotation\n"+Vector(layout.Rotation)+"\nmin\n"+Vector(layout.EmptyPosition)+"\n"+Vector(layout.EmptyScale)+"\nmax\n"+Vector(layout.FullPosition)+"\n"+Vector(layout.FullScale)+"\n";
  if(layout!=null&&(layout.EmptyScale.X<0||layout.EmptyScale.Y<0||layout.EmptyScale.Z<0||layout.FullScale.X<0||layout.FullScale.Y<0||layout.FullScale.Z<0))throw new InvalidDataException("Cargo scale must be nonnegative");
  if(field==null){if(occurrence!=Fields(script).Count(f=>f.Key=="RESOURCE_VISUALIZATION"))throw new InvalidDataException("Invalid cargo occurrence");return layout==null?script:Insert(script,block);}var match=headers.Matches(script).Where(m=>m.Groups[1].Value=="RESOURCE_VISUALIZATION").ElementAt(occurrence);int length=match.Length+field.Body.Length;return script[..match.Index]+block+script[(match.Index+length)..];
 }
 public static CargoLayout TransformCargo(CargoLayout cargo,Matrix4x4 transform)
 {
  var r=cargo.Rotation;var rotation=transform;rotation.Translation=Vector3.Zero;var matrix=Matrix4x4.CreateRotationX(r.X)*Matrix4x4.CreateRotationY(r.Y)*Matrix4x4.CreateRotationZ(r.Z)*rotation;
  float y=MathF.Asin(Math.Clamp(-matrix.M13,-1,1));var angles=MathF.Abs(MathF.Cos(y))>1e-5f?new Vector3(MathF.Atan2(matrix.M23,matrix.M33),y,MathF.Atan2(matrix.M12,matrix.M11)):new Vector3(0,y,MathF.Atan2(-matrix.M21,matrix.M22));
  return cargo with{Rotation=angles,EmptyPosition=Vector3.Transform(cargo.EmptyPosition,transform),FullPosition=Vector3.Transform(cargo.FullPosition,transform)};
 }
 public static string[] NewFields=["TYPE","NAME_STR","DESCRIPTION","COST_RUB","COST_USD","AVAILABLE","COUNTRY","LIFESPAN","FAMILY","MOVEMENT_SPEED","MOVEMENT_POWER_KW","MOVEMENT_EMPTY_WEIGHT","MOVEMENT_CONSPUMPTION","RESOURCE_CAPACITY","RESOURCE_TRANSPORT_TYPE","RESOURCE_ALLOW_ONLY","SKILL_PERSONAL","SKILL_CONSTRUCTION_CRANE","SKILL_FIRETRUCK","SOUND_PARAMS","DRIVER","LOD1","LOD2","LODDISTANCE1","LODDISTANCE2"];
}
