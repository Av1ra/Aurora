using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
namespace Aurora.Core;
public sealed record VehicleField(string Key,int Occurrence,string Value,string Body);
public sealed record CargoLayout(Vector3 Rotation,Vector3 EmptyPosition,Vector3 EmptyScale,Vector3 FullPosition,Vector3 FullScale);
public static class VehicleScript
{
 static readonly Regex headers=new(@"(?im)^[\t \uFEFF]*\$([A-Z_0-9]+)[\t ]*([^\r\n]*)");
 static Match[] Headers(string script){int end=Regex.Match(script,@"(?im)^[\t ]*end[\t ]*(?://[^\r\n]*)?\r?$") is {Success:true} m?m.Index:script.Length;return headers.Matches(script).Where(m=>m.Index<end).ToArray();}
 static int Comment(string text){bool quote=false;for(int i=0;i<text.Length;i++){if(text[i]=='"')quote=!quote;if(!quote&&(i==0||char.IsWhiteSpace(text[i-1]))&&(text[i]=='#'||i+1<text.Length&&(text.Substring(i,2)=="//"||text.Substring(i,2)=="--")))return i;}return text.Length;}
 static string Data(string text)=>text[..Comment(text)].Trim();
 static bool Scalar(string key)=>key=="TAKEOFF_DISTANCE"||NewFields.Contains(key)||key.StartsWith("MOVEMENT_",StringComparison.Ordinal)||key.StartsWith("SKILL_",StringComparison.Ordinal);
 public static VehicleField[] Fields(string script)
 {
  var matches=Headers(script);var count=new Dictionary<string,int>();var end=Regex.Match(script,@"(?im)^[\t ]*end[\t ]*(?://[^\r\n]*)?\r?$");
  return matches.Select((m,i)=>{string key=m.Groups[1].Value.ToUpperInvariant();int occurrence=count.GetValueOrDefault(key);count[key]=occurrence+1;int next=i+1<matches.Length?matches[i+1].Index:script.Length;if(end.Success&&end.Index>m.Index)next=Math.Min(next,end.Index);string body=script[(m.Index+m.Length)..next],value=Data(m.Groups[2].Value);if(value.Length==0&&Scalar(key))value=body.Split(['\r','\n']).Select(Data).FirstOrDefault(v=>v.Length>0)??"";return new VehicleField(key,occurrence,value,body);}).ToArray();
 }
 public static string Set(string script,string key,int occurrence,string? value)
 {
  if(!Regex.IsMatch(key,@"^[A-Z_0-9]+$")||occurrence<0||value?.IndexOfAny(['\r','\n'])>=0)throw new InvalidDataException("Invalid vehicle setting");
  var matches=Headers(script).Where(m=>m.Groups[1].Value.Equals(key,StringComparison.OrdinalIgnoreCase)).ToArray();
  if(occurrence>=matches.Length){if(value==null)return script;if(occurrence!=matches.Length)throw new InvalidDataException("Invalid setting occurrence");return Insert(script,"$"+key+(value.Length>0?" "+value:"")+"\n");}
  var match=matches[occurrence];var field=Fields(script).Single(f=>f.Key==key&&f.Occurrence==occurrence);if(value==field.Value)return script;
  int start=match.Groups[2].Index,length=match.Groups[2].Length;string line=match.Groups[2].Value;
  if(Data(line).Length==0&&Scalar(key)&&field.Value.Length>0){var bodyLine=Regex.Matches(field.Body,@"[^\r\n]+").First(m=>Data(m.Value).Length>0);start=match.Index+match.Length+bodyLine.Index;line=bodyLine.Value;length=line.Length;}
  if(value==null){string result=script[..match.Index]+script[(match.Index+match.Length)..];if(start>=match.Index+match.Length)result=result.Remove(start-match.Length,length);return result;}
  int comment=Comment(line);string suffix=comment<line.Length?" "+line[comment..]:"";string indent=new(line.TakeWhile(c=>c is ' ' or '\t').ToArray());return script[..start]+indent+(start==match.Groups[1].Index+match.Groups[1].Length&&value.Length>0?" ":"")+value+suffix+script[(start+length)..];
 }
 static string Insert(string script,string text){var end=Regex.Match(script,@"(?im)^[\t ]*end[\t ]*(?://[^\r\n]*)?\r?$");return end.Success?script.Insert(end.Index,text):script.TrimEnd()+"\n"+text+"end\n";}
 public static string SetBody(string script,string key,int occurrence,string body){if(body.Contains('$')||Regex.IsMatch(body,@"(?im)^\s*end\s*$"))throw new InvalidDataException("Invalid setting body");var match=Headers(script).Where(m=>m.Groups[1].Value.Equals(key,StringComparison.OrdinalIgnoreCase)).ElementAt(occurrence);var field=Fields(script).Single(f=>f.Key==key&&f.Occurrence==occurrence);int at=match.Index+match.Length;return script[..at]+"\n"+body.TrimEnd()+"\n"+script[(at+field.Body.Length)..];}
 static Vector3 Vector(string line){var values=line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(p=>float.Parse(p,CultureInfo.InvariantCulture)).ToArray();if(values.Length!=3||values.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Invalid cargo vector");return new(values[0],values[1],values[2]);}
 static string Vector(Vector3 v){if(!float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z))throw new InvalidDataException("Invalid cargo vector");return string.Join(" ",new[]{v.X,v.Y,v.Z}.Select(f=>f.ToString("G9",CultureInfo.InvariantCulture)));}
 public static CargoLayout? Cargo(string script,int occurrence=0)
 {
  var field=Fields(script).FirstOrDefault(f=>f.Key=="RESOURCE_VISUALIZATION"&&f.Occurrence==occurrence);if(field==null)return null;
  // Negative numeric coordinates are valid; only explicit comment markers skip.
  var lines=field.Body.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).Select(Data).Where(l=>l.Length>0).ToArray();
  if(lines.Length!=8||lines[0]!="rotation"||lines[2]!="min"||lines[5]!="max")throw new InvalidDataException("Unsupported cargo visualization layout");return new(Vector(lines[1]),Vector(lines[3]),Vector(lines[4]),Vector(lines[6]),Vector(lines[7]));
 }
 public static string SetCargo(string script,CargoLayout? layout,int occurrence=0)
 {
  var field=Fields(script).FirstOrDefault(f=>f.Key=="RESOURCE_VISUALIZATION"&&f.Occurrence==occurrence);string block=layout==null?"":"$RESOURCE_VISUALIZATION\nrotation\n"+Vector(layout.Rotation)+"\nmin\n"+Vector(layout.EmptyPosition)+"\n"+Vector(layout.EmptyScale)+"\nmax\n"+Vector(layout.FullPosition)+"\n"+Vector(layout.FullScale)+"\n";
  if(layout!=null&&(layout.EmptyScale.X<0||layout.EmptyScale.Y<0||layout.EmptyScale.Z<0||layout.FullScale.X<0||layout.FullScale.Y<0||layout.FullScale.Z<0))throw new InvalidDataException("Cargo scale must be nonnegative");
  if(field==null){if(occurrence!=Fields(script).Count(f=>f.Key=="RESOURCE_VISUALIZATION"))throw new InvalidDataException("Invalid cargo occurrence");return layout==null?script:Insert(script,block);}var match=Headers(script).Where(m=>m.Groups[1].Value.Equals("RESOURCE_VISUALIZATION",StringComparison.OrdinalIgnoreCase)).ElementAt(occurrence);int length=match.Length+field.Body.Length;return script[..match.Index]+block+script[(match.Index+length)..];
 }
 public static CargoLayout TransformCargo(CargoLayout cargo,Matrix4x4 transform)
 {
  var r=cargo.Rotation;var rotation=transform;rotation.Translation=Vector3.Zero;var matrix=Matrix4x4.CreateRotationX(r.X)*Matrix4x4.CreateRotationY(r.Y)*Matrix4x4.CreateRotationZ(r.Z)*rotation;
  float y=MathF.Asin(Math.Clamp(-matrix.M13,-1,1));var angles=MathF.Abs(MathF.Cos(y))>1e-5f?new Vector3(MathF.Atan2(matrix.M23,matrix.M33),y,MathF.Atan2(matrix.M12,matrix.M11)):new Vector3(0,y,MathF.Atan2(-matrix.M21,matrix.M22));
  return cargo with{Rotation=angles,EmptyPosition=Vector3.Transform(cargo.EmptyPosition,transform),FullPosition=Vector3.Transform(cargo.FullPosition,transform)};
 }
 public static string[] NewFields=["TYPE","NAME_STR","DESCRIPTION","COST_RUB","COST_USD","AVAILABLE","COUNTRY","LIFESPAN","FAMILY","MOVEMENT_SPEED","MOVEMENT_POWER_KW","MOVEMENT_EMPTY_WEIGHT","MOVEMENT_CONSPUMPTION","RESOURCE_CAPACITY","RESOURCE_TRANSPORT_TYPE","RESOURCE_ALLOW_ONLY","SKILL_PERSONAL","SKILL_CONSTRUCTION_CRANE","SKILL_FIRETRUCK","SOUND_PARAMS","DRIVER","LOD1","LOD2","LODDISTANCE1","LODDISTANCE2"];
}
