using System.Text;
using System.Text.RegularExpressions;
namespace Aurora.Core;

public static class NativeMaterialText
{
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
