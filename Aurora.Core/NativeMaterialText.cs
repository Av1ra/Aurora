using System.Text;
using System.Text.RegularExpressions;
namespace Aurora.Core;

public static class NativeMaterialText
{
 static Match[] Headers(string text)=>Regex.Matches(text[..VehicleScript.ActiveLength(text)],@"(?m)^[\t ]*\$SUBMATERIAL[^\S\r\n]+([^\r\n]+)").ToArray();
 public static string Normalize(string text)
 {
  var headers=Headers(text);var remove=new List<(int Start,int Length)>();string[] Lines(string block)=>block.Split('\n').Select(l=>l.Trim()).Where(l=>l.Length>0).ToArray();
  bool Contains(string[] full,string[] subset){var counts=full.GroupBy(l=>l).ToDictionary(g=>g.Key,g=>g.Count());foreach(string line in subset){if(!counts.TryGetValue(line,out int count)||count==0)return false;counts[line]=count-1;}return true;}
  foreach(var group in headers.Select((h,i)=>(Header:h,Index:i)).GroupBy(p=>p.Header.Groups[1].Value.Trim()).Where(g=>g.Count()>1)){
   (int Start,int End,string[] Lines)? kept=null;foreach(var p in group){int end=p.Index+1<headers.Length?headers[p.Index+1].Index:VehicleScript.ActiveLength(text);var lines=Lines(text[(p.Header.Index+p.Header.Length)..end]);if(kept==null){kept=(p.Header.Index,end,lines);continue;}var previous=kept.Value;if(Contains(previous.Lines,lines))remove.Add((p.Header.Index,end-p.Header.Index));else if(Contains(lines,previous.Lines)){remove.Add((previous.Start,previous.End-previous.Start));kept=(p.Header.Index,end,lines);}else throw new InvalidDataException("Conflicting duplicate native material: "+group.Key);}
  }
  foreach(var r in remove.OrderByDescending(r=>r.Start))text=text.Remove(r.Start,r.Length);return text;
 }
 public static string[] Names(string text){var names=Headers(Normalize(text)).Select(m=>m.Groups[1].Value.Trim()).ToArray();if(names.Length==0)throw new InvalidDataException("Invalid native material names");return names;}
 public static string Append(string text,string materials){int end=VehicleScript.ActiveLength(text);return text[..end]+"\n"+materials[..VehicleScript.ActiveLength(materials)]+text[end..];}
 public static string AlignNames(string text,IReadOnlyList<string> names)
 {
  text=Normalize(text);var headers=Headers(text);var original=headers.Select(h=>h.Groups[1].Value.Trim()).ToArray();if(names.All(original.Contains))return text;if(original.Length!=names.Count||names.Distinct().Count()!=names.Count)throw new InvalidDataException("Material slot count differs from model");for(int i=0;i<original.Length;i++)if(names.Contains(original[i])&&names[i]!=original[i])throw new InvalidDataException("Ambiguous native material slot correspondence");for(int i=headers.Length-1;i>=0;i--){var group=headers[i].Groups[1];text=text[..group.Index]+names[i]+text[(group.Index+group.Length)..];}return text;
 }
 static (int Start,int End,int Header) Block(string text,string material){var headers=Headers(text);var matches=headers.Where(m=>m.Groups[1].Value.Trim()==material).ToArray();if(matches.Length!=1)throw new InvalidDataException("Material not found uniquely: "+material);var h=matches[0];int index=Array.IndexOf(headers,h);return(h.Index,index+1<headers.Length?headers[index+1].Index:VehicleScript.ActiveLength(text),h.Length);}
 public static float[]? Property(string text,string material,string key)
 {
  var range=Block(text,material);var block=text[range.Start..range.End];var fields=VehicleScript.Fields(block).Where(f=>f.Key==key).ToArray();if(fields.Length==0)return null;int count=key=="SPECULARPOWER"?1:4;float[]? result=null;
  foreach(var field in fields){var tokens=VehicleScript.Tokens(block).Where(t=>t.Key==key&&t.Occurrence==field.Occurrence).Take(count).ToArray();if(tokens.Length!=count)throw new InvalidDataException("Invalid material property: "+key);var values=tokens.Select(t=>float.Parse(t.Value,System.Globalization.CultureInfo.InvariantCulture)).ToArray();if(values.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Invalid material value");if(result!=null&&!result.SequenceEqual(values))throw new InvalidDataException("Conflicting duplicate material property: "+key);result=values;}return result;
 }
 public static string SetProperty(string text,string material,string key,float[] values){if(key is not ("DIFFUSECOLOR" or "AMBIENTCOLOR" or "SPECULARCOLOR" or "SPECULARPOWER")||values.Length!=(key=="SPECULARPOWER"?1:4)||values.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Invalid material property");if(Property(text,material,key)?.SequenceEqual(values)==true)return text;var range=Block(text,material);string block=text[range.Start..range.End];string[] args=values.Select(v=>v.ToString("G9",System.Globalization.CultureInfo.InvariantCulture)).ToArray();if(VehicleScript.Fields(block).Any(f=>f.Key==key)){foreach(var field in VehicleScript.Fields(block).Where(f=>f.Key==key).ToArray())block=VehicleScript.SetArguments(block,key,field.Occurrence,args);}else block=block.Insert(range.Header,Environment.NewLine+"$"+key+" "+string.Join(" ",args));return text[..range.Start]+block+text[range.End..];}
 public static string Create(IEnumerable<string> materials,string diffuse,string specular,string normal)
 {
  var text=new StringBuilder();foreach(string name in materials){if(name.IndexOfAny(['\r','\n','$'])>=0)throw new InvalidDataException("Invalid material name");text.AppendLine("$SUBMATERIAL "+name).AppendLine("$TEXTURE_MTL 0 "+diffuse).AppendLine("$TEXTURE_MTL 1 "+specular).AppendLine("$TEXTURE_MTL 2 "+normal).AppendLine("$DIFFUSECOLOR 1 1 1 1").AppendLine("$SPECULARCOLOR 1 1 1 1").AppendLine("$AMBIENTCOLOR 1 1 1 1").AppendLine("$SPECULARPOWER 16");}return text.AppendLine("$END").ToString();
 }
 public static string SetTexture(string text,string material,int slot,string path)
 {
  if(slot is <0 or >2||path.IndexOfAny(['\r','\n'])>=0)throw new InvalidDataException("Invalid native texture assignment");
  var range=Block(text,material);string block=text[range.Start..range.End];string pattern=@"(?m)^[^\S\r\n]*\$TEXTURE(?:_MTL)?[^\S\r\n]+"+slot+@"[^\S\r\n]+[^\r\n]+";string line="$TEXTURE_MTL "+slot+" "+path;
  if(Regex.Matches(block,pattern).Count>1)throw new InvalidDataException("Duplicate native texture slot");
  block=Regex.IsMatch(block,pattern)?Regex.Replace(block,pattern,_=>line):block.Insert(range.Header,Environment.NewLine+line);
  return text[..range.Start]+block+text[range.End..];
 }
}
