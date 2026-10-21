using System.Text;
using System.Text.RegularExpressions;
namespace Aurora.Core;

public static class NativeMaterialText
{
 public static string[] Names(string text){var names=Regex.Matches(text,@"(?m)^[\t ]*\$SUBMATERIAL[^\S\r\n]+([^\r\n]+)").Select(m=>m.Groups[1].Value.Trim()).ToArray();if(names.Length==0||names.Distinct().Count()!=names.Length)throw new InvalidDataException("Invalid native material names");return names;}
 public static string AlignNames(string text,IReadOnlyList<string> names)
 {
  var headers=Regex.Matches(text,@"(?m)^[\t ]*\$SUBMATERIAL[^\S\r\n]+([^\r\n]+)");var original=headers.Select(h=>h.Groups[1].Value.Trim()).ToArray();if(original.Distinct().Count()!=original.Length)throw new InvalidDataException("Duplicate native material name");if(names.All(original.Contains))return text;if(original.Length!=names.Count||names.Distinct().Count()!=names.Count)throw new InvalidDataException("Material slot count differs from model");for(int i=0;i<original.Length;i++)if(names.Contains(original[i])&&names[i]!=original[i])throw new InvalidDataException("Ambiguous native material slot correspondence");for(int i=headers.Count-1;i>=0;i--){var group=headers[i].Groups[1];text=text[..group.Index]+names[i]+text[(group.Index+group.Length)..];}return text;
 }
 static (int Start,int End,int Header) Block(string text,string material){var headers=Regex.Matches(text,@"(?m)^[\t ]*\$SUBMATERIAL[^\S\r\n]+([^\r\n]+)");var matches=headers.Cast<Match>().Where(m=>m.Groups[1].Value.Trim()==material).ToArray();if(matches.Length!=1)throw new InvalidDataException("Material not found uniquely: "+material);var h=matches[0];int index=Array.IndexOf(headers.Cast<Match>().ToArray(),h);return(h.Index,index+1<headers.Count?headers[index+1].Index:text.Length,h.Length);}
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
  var headers=Regex.Matches(text,@"(?m)^\s*\$SUBMATERIAL[^\S\r\n]+([^\r\n]+)");var matches=headers.Cast<Match>().Where(m=>m.Groups[1].Value.Trim()==material).ToArray();if(matches.Length!=1)throw new InvalidDataException("Material not found uniquely: "+material);
  var header=matches[0];int index=Array.IndexOf(headers.Cast<Match>().ToArray(),header);int end=index+1<headers.Count?headers[index+1].Index:text.Length;
  string block=text[header.Index..end];string pattern=@"(?m)^[^\S\r\n]*\$TEXTURE(?:_MTL)?[^\S\r\n]+"+slot+@"[^\S\r\n]+[^\r\n]+";string line="$TEXTURE_MTL "+slot+" "+path;
  if(Regex.Matches(block,pattern).Count>1)throw new InvalidDataException("Duplicate native texture slot");
  block=Regex.IsMatch(block,pattern)?Regex.Replace(block,pattern,_=>line):block.Insert(header.Length,Environment.NewLine+line);
  return text[..header.Index]+block+text[end..];
 }
}
