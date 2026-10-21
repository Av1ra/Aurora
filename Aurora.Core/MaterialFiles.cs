namespace Aurora.Core;
public static class MaterialFiles
{
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
