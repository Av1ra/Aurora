namespace Aurora.Core;
public static class MaterialFiles
{
 public static string Resolve(string mtl,string reference)
 {
  reference=reference.Trim().Trim('"').Replace('/',Path.DirectorySeparatorChar);
  if(Path.IsPathRooted(reference)&&File.Exists(reference))return Path.GetFullPath(reference);
  string directory=Path.GetDirectoryName(Path.GetFullPath(mtl))!;
  // Native MTLs resolve from media_soviet; Workshop MTLs commonly use local paths.
  for(string? parent=directory;parent!=null;parent=Path.GetDirectoryName(parent))
  {
   string file=Path.GetFullPath(Path.Combine(parent,reference));if(File.Exists(file))return file;
  }
  string local=Path.Combine(directory,Path.GetFileName(reference));if(File.Exists(local))return local;
  throw new FileNotFoundException("MTL texture not found: "+reference);
 }
}
