using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace Aurora.Core;

public static class VehicleAssets
{
 static readonly HashSet<string> extensions=new(StringComparer.OrdinalIgnoreCase){".nmf",".naf",".mtl"};
 public static bool IsModelReference(VehicleToken token)=>!Regex.IsMatch(token.Key,@"^LOD\d*$")&&extensions.Contains(Path.GetExtension(token.Value));
 public static string? Media(string start){for(string? folder=Path.GetFullPath(start);folder!=null;folder=Path.GetDirectoryName(folder)){if(Path.GetFileName(folder).Equals("media_soviet",StringComparison.OrdinalIgnoreCase))return folder;string media=Path.Combine(folder,"media_soviet");if(Directory.Exists(Path.Combine(media,"vehicles")))return media;}return null;}
 public static string Resolve(string reference,string sourceFolder,string? media=null)
 {
  reference=reference.Replace('/',Path.DirectorySeparatorChar);if(Path.IsPathRooted(reference)){if(File.Exists(reference))return Path.GetFullPath(reference);throw new FileNotFoundException("Vehicle asset not found: "+reference,reference);}
  string local=Path.GetFullPath(Path.Combine(sourceFolder,reference));if(File.Exists(local))return local;
  media??=Media(sourceFolder);if(media!=null){string game=Path.GetFullPath(Path.Combine(media,reference));if(File.Exists(game))return game;}
  throw new FileNotFoundException("Vehicle asset not found: "+reference,reference);
 }
 public static string Map(string script,Func<string,string> map)=>VehicleScript.RewriteTokens(script,t=>IsModelReference(t)?map(t.Value):null);
 public static string MapMaterial(string text,Func<string,string> map)=>MapMaterial(text,(reference,local)=>map(reference));
 public static string MapMaterial(string text,Func<string,bool,string> map){int end=VehicleScript.ActiveLength(text);return Regex.Replace(text[..end],@"(?m)^\s*\$TEXTURE(?<local>_MTL)?\s+(?<slot>\d+)\s+(?<path>[^\r\n]+)",m=>"$TEXTURE_MTL "+m.Groups["slot"].Value+" "+map(m.Groups["path"].Value.Trim().Trim('"'),m.Groups["local"].Success))+text[end..];}

 public static string Pack(string script,string sourceFolder,Dictionary<string,byte[]> entries,string? media=null)
 {
  var packed=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  string Asset(string path,bool material){path=Path.GetFullPath(path);if(packed.TryGetValue(path,out string? existing))return existing;byte[] bytes=File.ReadAllBytes(path);
   byte[]? companion=null;if(material){bytes=Encoding.UTF8.GetBytes(MapMaterial(File.ReadAllText(path),(r,local)=>Path.GetFileName(Asset(MaterialFiles.ResolveNative(path,r,local),false))));if(MaterialFiles.Companion(path) is string pbr)companion=Encoding.UTF8.GetBytes(MaterialFiles.MapPbr(File.ReadAllText(pbr),r=>Path.GetFileName(Asset(MaterialFiles.Resolve(pbr,r),false))));}
   string name="assets/"+Convert.ToHexString(SHA256.HashData(companion==null?bytes:[..bytes,0,..companion])).ToLowerInvariant()+Path.GetExtension(path).ToLowerInvariant();packed[path]=name;void Put(string key,byte[] value){if(!entries.TryAdd(key,value)&&!entries[key].SequenceEqual(value))throw new InvalidDataException("Project material dependency collision");}Put(name,bytes);if(companion!=null)Put("assets/"+MaterialFiles.PairedName(name),companion);return name;
  }
  return Map(script,r=>{string path=Resolve(r,sourceFolder,media);return Asset(path,Path.GetExtension(path).Equals(".mtl",StringComparison.OrdinalIgnoreCase));});
 }
 public static string Export(string script,string sourceFolder,string output,string? media=null)
 {
  var copied=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  string Copy(string reference,string folder){string path=Resolve(reference,folder,media);if(copied.TryGetValue(path,out string? prior))return prior;byte[] bytes=File.ReadAllBytes(path);string name="asset-"+Convert.ToHexString(SHA256.HashData(bytes))[..20].ToLowerInvariant()+Path.GetExtension(path).ToLowerInvariant();copied[path]=name;
   byte[]? companion=null;if(Path.GetExtension(path).Equals(".mtl",StringComparison.OrdinalIgnoreCase)){string text=MapMaterial(File.ReadAllText(path),(r,local)=>Copy(MaterialFiles.ResolveNative(path,r,local),Path.GetDirectoryName(path)!));bytes=Encoding.UTF8.GetBytes(text);if(MaterialFiles.Companion(path) is string pbr)companion=Encoding.UTF8.GetBytes(MaterialFiles.MapPbr(File.ReadAllText(pbr),r=>Copy(MaterialFiles.Resolve(pbr,r),Path.GetDirectoryName(pbr)!)));}
   name="asset-"+Convert.ToHexString(SHA256.HashData(companion==null?bytes:[..bytes,0,..companion]))[..20].ToLowerInvariant()+Path.GetExtension(path).ToLowerInvariant();copied[path]=name;void Put(string file,byte[] value){string target=Path.Combine(output,file);if(File.Exists(target)){if(!File.ReadAllBytes(target).SequenceEqual(value))throw new InvalidDataException("Vehicle dependency collision: "+file);}else File.WriteAllBytes(target,value);}Put(name,bytes);if(companion!=null)Put(MaterialFiles.PairedName(name),companion);return name;
  }
  return Map(script,r=>Copy(r,sourceFolder));
 }
}
