namespace Aurora.Core;
public static class ModelFileNames
{
 public static Dictionary<string,string> Allocate(IEnumerable<string> paths)
 {
  var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int index=0;
  foreach(string path in paths){string name=index==0?"main.nmf":Path.GetFileName(path);if(index>0&&!used.Add(name)){int suffix=index;do{name="lod"+(suffix++)+".nmf";}while(!used.Add(name));}else if(index==0)used.Add(name);result.Add(path,name);index++;}return result;
 }
 public static string Resolve(IReadOnlyDictionary<string,string> names,string reference,string sourceFolder)
 {
  string normalized=reference.Replace('/',Path.DirectorySeparatorChar);string path=Path.IsPathRooted(normalized)?normalized:Path.GetFullPath(Path.Combine(sourceFolder,normalized));
  var exact=names.FirstOrDefault(p=>p.Key.Equals(path,StringComparison.OrdinalIgnoreCase));if(exact.Key!=null)return exact.Value;
  string? media=VehicleAssets.Media(sourceFolder);if(media!=null&&!Path.IsPathRooted(normalized)){path=Path.GetFullPath(Path.Combine(media,normalized));exact=names.FirstOrDefault(p=>p.Key.Equals(path,StringComparison.OrdinalIgnoreCase));if(exact.Key!=null)return exact.Value;}
  var candidates=names.Where(p=>Path.GetFileName(p.Key).Equals(Path.GetFileName(normalized),StringComparison.OrdinalIgnoreCase)).ToArray();if(candidates.Length==1)return candidates[0].Value;
  throw new InvalidDataException(candidates.Length>1?"Ambiguous LOD model: "+reference:"Добавьте LOD, указанный в script.ini: "+reference);
 }
}
