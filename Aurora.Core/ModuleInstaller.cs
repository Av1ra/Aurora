using System.Security.Cryptography;
using System.Text.Json;
namespace Aurora.Core;
public static class ModuleInstaller
{
 public static void Install(VerifiedMod mod,string game,string key)
 {
  var verified=new ModCatalog(key).Verify(mod.Folder);
  if(verified.Id!=mod.Manifest.Id||verified.Version!=mod.Manifest.Version)throw new InvalidDataException("Module changed during installation");
  if(!File.Exists(Path.Combine(game,"SOVIET64.exe")))throw new InvalidDataException("Game folder missing");
  var receiptRoot=Path.Combine(game,"mods","aurora-backups",DateTime.UtcNow.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(receiptRoot);
  var changes=new List<(string Destination,string? Backup)>();
  try
  {
   foreach(var file in verified.Files.Where(x=>x.Path.StartsWith("Payload/",StringComparison.Ordinal)))
   {
    string relative=file.Path[8..];
    if(!(relative=="pbr.exe"||relative=="mods/pbr.dll"||relative=="mods/m.dll"||relative=="mods/glass_default.dds"||relative.StartsWith("mods/library/")))throw new InvalidDataException("Unsupported module destination: "+relative);
    string source=ModCatalog.SafePath(mod.Folder,file.Path),destination=ModCatalog.SafePath(game,relative);
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    string staged=destination+".aurora-"+Guid.NewGuid().ToString("N");
    try
    {
     File.Copy(source,staged);using(var stream=File.OpenRead(staged))if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(file.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Module changed while copying");
     string? backup=null;if(File.Exists(destination)){backup=Path.Combine(receiptRoot,changes.Count+".bak");File.Copy(destination,backup);}
     changes.Add((destination,backup));File.Move(staged,destination,true);
    }finally{if(File.Exists(staged))File.Delete(staged);}
   }
   File.WriteAllText(Path.Combine(receiptRoot,"receipt.json"),JsonSerializer.Serialize(new{verified.Id,verified.Version,Files=changes.Select(x=>new{x.Destination,x.Backup})}));
  }
  catch{foreach(var change in changes.AsEnumerable().Reverse()){if(change.Backup!=null)File.Copy(change.Backup,change.Destination,true);else if(File.Exists(change.Destination))File.Delete(change.Destination);}throw;}
 }
}
