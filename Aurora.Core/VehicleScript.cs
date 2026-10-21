using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
namespace Aurora.Core;
public sealed record VehicleField(string Key,int Occurrence,string Value,string Body);
public sealed record VehicleToken(string Key,int Occurrence,int Index,string Value,int Start,int Length);
public sealed record CargoLayout(Vector3 Rotation,Vector3 EmptyPosition,Vector3 EmptyScale,Vector3 FullPosition,Vector3 FullScale);
public static class VehicleScript
{
 static readonly Regex headers=new(@"(?im)^[\t \uFEFF]*\$([A-Z_0-9]+)[\t ]*([^\r\n]*)");
 static readonly Regex terminator=new(@"(?im)^[\t \uFEFF]*(?:\$END|end)[\t ]*(?:(?://|#|--)[^\r\n]*)?\r?$");
 public static int ActiveLength(string script)=>terminator.Match(script) is {Success:true} m?m.Index:script.Length;
 static Match[] Headers(string script){int end=terminator.Match(script) is {Success:true} m?m.Index:script.Length;return headers.Matches(script).Where(m=>m.Index<end).ToArray();}
 static int Comment(string text){bool quote=false;for(int i=0;i<text.Length;i++){if(text[i]=='"')quote=!quote;if(!quote&&(i==0||char.IsWhiteSpace(text[i-1]))&&(text[i]=='#'||i+1<text.Length&&(text.Substring(i,2)=="//"||text.Substring(i,2)=="--")))return i;}return text.Length;}
 static string Data(string text)=>text[..Comment(text)].Trim();
 public static VehicleToken[] Tokens(string script)
 {
  var result=new List<VehicleToken>();var matches=Headers(script);var counts=new Dictionary<string,int>();
  for(int i=0;i<matches.Length;i++){
   var header=matches[i];string key=header.Groups[1].Value.ToUpperInvariant();int occurrence=counts.GetValueOrDefault(key);counts[key]=occurrence+1;int end=i+1<matches.Length?matches[i+1].Index:(terminator.Match(script) is {Success:true} stop?stop.Index:script.Length),index=0;
   int start=header.Groups[2].Index;string content=script[start..end];foreach(Match line in Regex.Matches(content,@"[^\r\n]+")){string data=line.Value[..Comment(line.Value)];foreach(Match token in Regex.Matches(data,"\"[^\"]*\"|\\S+"))result.Add(new(key,occurrence,index++,token.Value.Trim('"'),start+line.Index+token.Index,token.Length));}
  }return result.ToArray();
 }
 public static string RewriteTokens(string script,Func<VehicleToken,string?> rewrite)
 {
  foreach(var token in Tokens(script).Reverse()){string? value=rewrite(token);if(value==null||value==token.Value)continue;if(value.IndexOfAny(['\r','\n','"'])>=0)throw new InvalidDataException("Invalid vehicle token");string text=value.Any(char.IsWhiteSpace)?"\""+value+"\"":value;script=script[..token.Start]+text+script[(token.Start+token.Length)..];}return script;
 }
 static bool Scalar(string key)=>key=="TAKEOFF_DISTANCE"||NewFields.Contains(key)||key.StartsWith("MOVEMENT_",StringComparison.Ordinal)||key.StartsWith("SKILL_",StringComparison.Ordinal);
 public static VehicleField[] Fields(string script)
 {
  var matches=Headers(script);var count=new Dictionary<string,int>();var end=terminator.Match(script);
  return matches.Select((m,i)=>{string key=m.Groups[1].Value.ToUpperInvariant();int occurrence=count.GetValueOrDefault(key);count[key]=occurrence+1;int next=i+1<matches.Length?matches[i+1].Index:script.Length;if(end.Success&&end.Index>m.Index)next=Math.Min(next,end.Index);string body=script[(m.Index+m.Length)..next],value=Data(m.Groups[2].Value);if(value.Length==0&&Scalar(key))value=body.Split(['\r','\n']).Select(Data).FirstOrDefault(v=>v.Length>0)??"";return new VehicleField(key,occurrence,value,body);}).ToArray();
 }
 public static string Set(string script,string key,int occurrence,string? value)
 {
  if(!Regex.IsMatch(key,@"^[A-Z_0-9]+$")||key=="END"||occurrence<0||value?.IndexOfAny(['\r','\n'])>=0)throw new InvalidDataException("Invalid vehicle setting");
  var matches=Headers(script).Where(m=>m.Groups[1].Value.Equals(key,StringComparison.OrdinalIgnoreCase)).ToArray();
  if(occurrence>=matches.Length){if(value==null)return script;if(occurrence!=matches.Length)throw new InvalidDataException("Invalid setting occurrence");return Insert(script,"$"+key+(value.Length>0?" "+value:"")+"\n");}
  var match=matches[occurrence];var field=Fields(script).Single(f=>f.Key==key&&f.Occurrence==occurrence);if(value==field.Value)return script;
  int start=match.Groups[2].Index,length=match.Groups[2].Length;string line=match.Groups[2].Value;
  if(Data(line).Length==0&&Scalar(key)&&field.Value.Length>0){var bodyLine=Regex.Matches(field.Body,@"[^\r\n]+").First(m=>Data(m.Value).Length>0);start=match.Index+match.Length+bodyLine.Index;line=bodyLine.Value;length=line.Length;}
  if(value==null){string result=script[..match.Index]+script[(match.Index+match.Length)..];if(start>=match.Index+match.Length)result=result.Remove(start-match.Length,length);return result;}
  int comment=Comment(line);string suffix=comment<line.Length?" "+line[comment..]:"";string indent=new(line.TakeWhile(c=>c is ' ' or '\t').ToArray());return script[..start]+indent+(start==match.Groups[1].Index+match.Groups[1].Length&&value.Length>0?" ":"")+value+suffix+script[(start+length)..];
 }
 static string Insert(string script,string text){string newline=script.Contains("\r\n",StringComparison.Ordinal)?"\r\n":"\n";text=text.Replace("\r\n","\n").Replace("\n",newline);var end=terminator.Match(script);return end.Success?script.Insert(end.Index,text):script.TrimEnd()+newline+text+"end"+newline;}
 public static string SetBody(string script,string key,int occurrence,string body){if(body.Contains('$')||Regex.IsMatch(body,@"(?im)^\s*end\s*$"))throw new InvalidDataException("Invalid setting body");var match=Headers(script).Where(m=>m.Groups[1].Value.Equals(key,StringComparison.OrdinalIgnoreCase)).ElementAt(occurrence);var field=Fields(script).Single(f=>f.Key==key&&f.Occurrence==occurrence);int at=match.Index+match.Length;return script[..at]+"\n"+body.TrimEnd()+"\n"+script[(at+field.Body.Length)..];}
 public static string SetValues(string script,string key,IEnumerable<string> values)
 {
  var entries=values.ToArray();if(entries.Any(v=>string.IsNullOrWhiteSpace(v)||v.IndexOfAny(['\r','\n'])>=0))throw new InvalidDataException("Invalid setting values");
  var previous=Fields(script).Where(f=>f.Key==key).ToArray();
  if(previous.Select(f=>f.Value).SequenceEqual(entries))return script;
  for(int i=previous.Length-1;i>=entries.Length;i--)script=Set(script,key,i,null);
  for(int i=0;i<entries.Length;i++)script=Set(script,key,i,entries[i]);return script;
 }
 public static string SetArguments(string script,string key,int occurrence,IReadOnlyList<string> arguments)
 {
  if(arguments.Any(a=>string.IsNullOrWhiteSpace(a)||a.IndexOfAny(['\r','\n','"'])>=0))throw new InvalidDataException("Invalid vehicle arguments");
  var existing=Tokens(script).Where(t=>t.Key==key&&t.Occurrence==occurrence).ToArray();
  if(Fields(script).Any(f=>f.Key==key&&f.Occurrence==occurrence)){
   if(existing.Length<arguments.Count)throw new InvalidDataException("Unsupported vehicle argument layout: "+key);
   return RewriteTokens(script,t=>t.Key==key&&t.Occurrence==occurrence&&t.Index<arguments.Count?arguments[t.Index]:null);
  }
  return Set(script,key,occurrence,string.Join(" ",arguments.Select(a=>a.Any(char.IsWhiteSpace)?"\""+a+"\"":a)));
 }
 public static string RemoveEntry(string script,string key,int occurrence)
 {
  var matches=Headers(script);int i=Array.FindIndex(matches,m=>m.Groups[1].Value.Equals(key,StringComparison.OrdinalIgnoreCase)&&matches.TakeWhile(p=>p.Index<m.Index).Count(p=>p.Groups[1].Value.Equals(key,StringComparison.OrdinalIgnoreCase))==occurrence);if(i<0)return script;int end=i+1<matches.Length?matches[i+1].Index:(terminator.Match(script) is {Success:true} stop?stop.Index:script.Length);return script[..matches[i].Index]+script[end..];
 }
 public static string RenamePart(string script,string oldName,string newName)
 {
  if(newName.Length is <1 or >63||newName.Any(c=>c<33||c>126))throw new InvalidDataException("Invalid NMF name");
  string[] keys=["DUMPER_DECK_PIVOT","DUMPER_JOINTDECK_PIVOT","MIXER_DRUM","MIXER_JOINTDRUM"];
  foreach(var match in Headers(script).Where(m=>keys.Contains(m.Groups[1].Value.ToUpperInvariant())).Reverse()){
   int at=match.Groups[2].Index;string data=Data(match.Groups[2].Value);
   if(data.Length==0){int end=Headers(script).FirstOrDefault(m=>m.Index>match.Index)?.Index??(terminator.Match(script) is {Success:true} stop?stop.Index:script.Length);string body=script[(match.Index+match.Length)..end];var line=Regex.Matches(body,@"[^\r\n]+").FirstOrDefault(l=>Data(l.Value).Length>0);if(line==null)continue;at=match.Index+match.Length+line.Index;}
   var token=Regex.Match(script[at..],@"^[\t ]*(\S+)");if(token.Success&&token.Groups[1].Value==oldName){int start=at+token.Groups[1].Index;script=script[..start]+newName+script[(start+oldName.Length)..];}
  }return script;
 }
 static Vector3 Vector(string line){var values=line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(p=>float.Parse(p,CultureInfo.InvariantCulture)).ToArray();if(values.Length!=3||values.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Invalid cargo vector");return new(values[0],values[1],values[2]);}
 static string Vector(Vector3 v){if(!float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z))throw new InvalidDataException("Invalid cargo vector");return string.Join(" ",new[]{v.X,v.Y,v.Z}.Select(f=>f.ToString("G9",CultureInfo.InvariantCulture)));}
 public static CargoLayout? Cargo(string script,int occurrence=0,string key="RESOURCE_VISUALIZATION")
 {
  if(key is not ("RESOURCE_VISUALIZATION" or "RESOURCE_VISUALIZATION_JOINT" or "RESOURCE_VISUALIZATION_BONE"))throw new InvalidDataException("Invalid cargo frame");var field=Fields(script).FirstOrDefault(f=>f.Key==key&&f.Occurrence==occurrence);if(field==null)return null;
  var tokens=Tokens(script).Where(t=>t.Key==key&&t.Occurrence==occurrence).Skip(key=="RESOURCE_VISUALIZATION_BONE"?1:0).Select(t=>t.Value).ToArray();
  if(tokens.Length<18||tokens[0]!="rotation"||tokens[4]!="min"||tokens[11]!="max")throw new InvalidDataException("Unsupported cargo visualization layout");Vector3 V(int at)=>Vector(string.Join(" ",tokens.Skip(at).Take(3)));return new(V(1),V(5),V(8),V(12),V(15));
 }
 public static string SetCargo(string script,CargoLayout? layout,int occurrence=0,string key="RESOURCE_VISUALIZATION",string? bone=null)
 {
  if(key is not ("RESOURCE_VISUALIZATION" or "RESOURCE_VISUALIZATION_JOINT" or "RESOURCE_VISUALIZATION_BONE"))throw new InvalidDataException("Invalid cargo frame");var field=Fields(script).FirstOrDefault(f=>f.Key==key&&f.Occurrence==occurrence);bone??=field?.Value;if(key=="RESOURCE_VISUALIZATION_BONE"&&layout!=null&&(string.IsNullOrWhiteSpace(bone)||bone.Any(char.IsWhiteSpace)))throw new InvalidDataException("Choose a cargo bone");string block=layout==null?"":"$"+key+(key=="RESOURCE_VISUALIZATION_BONE"?" "+bone:"")+"\nrotation\n"+Vector(layout.Rotation)+"\nmin\n"+Vector(layout.EmptyPosition)+"\n"+Vector(layout.EmptyScale)+"\nmax\n"+Vector(layout.FullPosition)+"\n"+Vector(layout.FullScale)+"\n";
  if(layout!=null&&(layout.EmptyScale.X<0||layout.EmptyScale.Y<0||layout.EmptyScale.Z<0||layout.FullScale.X<0||layout.FullScale.Y<0||layout.FullScale.Z<0))throw new InvalidDataException("Cargo scale must be nonnegative");
  if(field==null){if(occurrence!=Fields(script).Count(f=>f.Key==key))throw new InvalidDataException("Invalid cargo occurrence");return layout==null?script:Insert(script,block);}if(layout==null)return RemoveEntry(script,key,occurrence);if(Cargo(script,occurrence,key)==layout&&(key!="RESOURCE_VISUALIZATION_BONE"||bone==field.Value))return script;
  var args=new List<string>();if(key=="RESOURCE_VISUALIZATION_BONE")args.Add(bone!);args.Add("rotation");args.AddRange(Vector(layout.Rotation).Split(' '));args.Add("min");args.AddRange(Vector(layout.EmptyPosition).Split(' '));args.AddRange(Vector(layout.EmptyScale).Split(' '));args.Add("max");args.AddRange(Vector(layout.FullPosition).Split(' '));args.AddRange(Vector(layout.FullScale).Split(' '));return SetArguments(script,key,occurrence,args);
 }
 public static CargoLayout TransformCargo(CargoLayout cargo,Matrix4x4 transform)
 {
  var r=cargo.Rotation;var rotation=transform;rotation.Translation=Vector3.Zero;var matrix=Matrix4x4.CreateRotationX(r.X)*Matrix4x4.CreateRotationY(r.Y)*Matrix4x4.CreateRotationZ(r.Z)*rotation;
  float y=MathF.Asin(Math.Clamp(-matrix.M13,-1,1));var angles=MathF.Abs(MathF.Cos(y))>1e-5f?new Vector3(MathF.Atan2(matrix.M23,matrix.M33),y,MathF.Atan2(matrix.M12,matrix.M11)):new Vector3(0,y,MathF.Atan2(-matrix.M21,matrix.M22));
  return cargo with{Rotation=angles,EmptyPosition=Vector3.Transform(cargo.EmptyPosition,transform),FullPosition=Vector3.Transform(cargo.FullPosition,transform)};
 }
 public static string[] NewFields=["TYPE","NAME_STR","DESCRIPTION","COST_RUB","COST_USD","AVAILABLE","COUNTRY","LIFESPAN","FAMILY","MOVEMENT_SPEED","MOVEMENT_POWER_KW","MOVEMENT_EMPTY_WEIGHT","MOVEMENT_CONSPUMPTION","RESOURCE_CAPACITY","RESOURCE_TRANSPORT_TYPE","RESOURCE_ALLOW_ONLY","SKILL_PERSONAL","SKILL_CONSTRUCTION_CRANE","SKILL_FIRETRUCK","SOUND_PARAMS","DRIVER","LOD1","LOD2","LODDISTANCE1","LODDISTANCE2"];
}
