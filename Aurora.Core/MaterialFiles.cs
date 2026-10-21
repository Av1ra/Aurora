namespace Aurora.Core;
public static class MaterialFiles
{
 public static string PairedName(string native){string stem=Path.GetFileNameWithoutExtension(native);var match=System.Text.RegularExpressions.Regex.Match(stem,@"^material(_\d+)?$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);return match.Success?"material_pbr"+match.Groups[1].Value+".mtl":stem+"_pbr.mtl";}
 public static string? Companion(string native){string folder=Path.GetDirectoryName(Path.GetFullPath(native))!;return new[]{Path.Combine(folder,PairedName(native)),Path.ChangeExtension(native,"pbr"),Path.Combine(folder,Path.GetFileNameWithoutExtension(native)+".pbr.mtl"),Path.Combine(folder,"pbr.mtl")}.FirstOrDefault(p=>!Path.GetFullPath(p).Equals(Path.GetFullPath(native),StringComparison.OrdinalIgnoreCase)&&File.Exists(p));}
 public static string MapPbr(string text,Func<string,string> map)=>System.Text.RegularExpressions.Regex.Replace(text,@"(?m)^(\s*\{\s*(?:base_color|normal|orm|metallic|roughness|ao|emissive|glass|glass_mask)\s*=\s*)([^{}\r\n]*?)(\s*\}[^\r\n]*)$",m=>{string value=m.Groups[2].Value.Trim().Trim('"');return value is "none" or "default"||value.StartsWith("aurora:",StringComparison.Ordinal)?m.Value:m.Groups[1].Value+map(value)+m.Groups[3].Value;});
 public static string Resolve(string mtl,string reference)
 {
  reference=reference.Trim().Trim('"').Replace('/',Path.DirectorySeparatorChar);
  if(Path.IsPathRooted(reference)&&File.Exists(reference))return Path.GetFullPath(reference);
  string directory=Path.GetDirectoryName(Path.GetFullPath(mtl))!;
  string local=Path.GetFullPath(Path.Combine(directory,reference));string? media=VehicleAssets.Media(directory);string? native=media==null?null:Path.GetFullPath(Path.Combine(media,reference));
  if(File.Exists(local)){if(native!=null&&File.Exists(native)&&!native.Equals(local,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Ambiguous MTL texture reference: "+reference);return local;}
  if(native!=null&&File.Exists(native))return native;
  throw new FileNotFoundException("MTL texture not found: "+reference);
 }
}
