using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Aurora.Core;
namespace Aurora.Launcher;
record LauncherRelease(string Version,string Asset,string Sha256,string Signature);
sealed class UpdateService
{
 const string Repo="Av1ra/Aurora";
 readonly HttpClient http=new();
 LauncherRelease? latest;
 public UpdateService(){http.DefaultRequestHeaders.UserAgent.ParseAdd("Aurora/0.1.0");http.Timeout=TimeSpan.FromMinutes(5);}
 public async Task<LauncherRelease?> Check()
 {
  using var json=JsonDocument.Parse(await http.GetStringAsync("https://api.github.com/repos/"+Repo+"/releases/latest"));
  var assets=json.RootElement.GetProperty("assets").EnumerateArray().ToArray();
  string url=assets.Single(x=>x.GetProperty("name").GetString()=="aurora-update.json").GetProperty("browser_download_url").GetString()!;
  latest=JsonSerializer.Deserialize<LauncherRelease>(await http.GetStringAsync(url))??throw new InvalidDataException("Missing release metadata");
  if(!Version.TryParse(latest.Version,out var version)||version<=Version.Parse(ModCatalog.LauncherVersion))return null;
  if(!System.Text.RegularExpressions.Regex.IsMatch(latest.Asset,@"^Aurora-[0-9.]+-win-x64\.zip$"))throw new InvalidDataException("Invalid update asset");
  return latest;
 }
 public async Task<string?> DownloadVerified(string key)
 {
  if(await Check()==null)return null;
  return await Download(latest!,key);
 }
 public async Task<string> DownloadEditor(string key)
 {
  using var json=JsonDocument.Parse(await http.GetStringAsync("https://api.github.com/repos/"+Repo+"/releases/latest"));
  string url=json.RootElement.GetProperty("assets").EnumerateArray().Single(x=>x.GetProperty("name").GetString()=="aurora-editor.json").GetProperty("browser_download_url").GetString()!;
  var release=JsonSerializer.Deserialize<LauncherRelease>(await http.GetStringAsync(url))??throw new InvalidDataException("Нет описания редактора.");
  if(!System.Text.RegularExpressions.Regex.IsMatch(release.Asset,@"^Aurora-Editor-[0-9.]+-win-x64\.zip$"))throw new InvalidDataException("Invalid editor asset");
  return await Download(release,key);
 }
 async Task<string> Download(LauncherRelease release,string key)
 {
  using var signature=ECDsa.Create();signature.ImportFromPem(key);
  var signed=System.Text.Encoding.UTF8.GetBytes(release.Version+"\n"+release.Asset+"\n"+release.Sha256);
  if(!signature.VerifyData(signed,Convert.FromBase64String(release.Signature),HashAlgorithmName.SHA256))throw new InvalidDataException("Invalid update signature");
  var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aurora","updates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  var archive=Path.Combine(root,"update.zip");
  await using(var input=await http.GetStreamAsync("https://github.com/"+Repo+"/releases/download/v"+release.Version+"/"+release.Asset))
  await using(var output=File.Create(archive))await input.CopyToAsync(output);
  using(var input=File.OpenRead(archive))if(!Convert.ToHexString(SHA256.HashData(input)).Equals(release.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Update SHA-256 mismatch");
  var unpack=Path.Combine(root,"package");Directory.CreateDirectory(unpack);
  using var zip=ZipFile.OpenRead(archive);
  foreach(var entry in zip.Entries){if(entry.FullName.EndsWith('/'))continue;var target=ModCatalog.SafePath(unpack,entry.FullName);if(entry.ExternalAttributes>>16 is var attr && (attr&0xf000)==0xa000)throw new InvalidDataException("Linked ZIP entry");Directory.CreateDirectory(Path.GetDirectoryName(target)!);entry.ExtractToFile(target,false);}
  bool editor=release.Asset.StartsWith("Aurora-Editor-");
  if(!File.Exists(Path.Combine(unpack,editor?"Editor/acode.lnchr":"Aurora.Launcher.exe")))throw new InvalidDataException("Incomplete update");return unpack;
 }
 public async Task InstallEditor(string key)
 {
  string package=await DownloadEditor(key),source=Path.Combine(package,"Editor");
  var manifest=new ModCatalog(key).Verify(source);if(manifest.Id!="aurora.editor")throw new InvalidDataException("Неверный редактор.");
  string root=AppContext.BaseDirectory,staging=Path.Combine(root,".editor-new-"+Guid.NewGuid().ToString("N")),target=Path.Combine(root,"Editor"),backup=Path.Combine(root,".editor-backup-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(staging);
  foreach(string file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories)){string relative=Path.GetRelativePath(source,file).Replace('\\','/');string destination=ModCatalog.SafePath(staging,relative);Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(file,destination);}
  _=new ModCatalog(key).Verify(staging);
  bool moved=false;try{if(Directory.Exists(target)){Directory.Move(target,backup);moved=true;}Directory.Move(staging,target);}catch{if(moved&&!Directory.Exists(target))Directory.Move(backup,target);throw;}
 }
 public void StartReplacement(string folder)
 {
  // Separate process waits for the current launcher; no shell, no downloaded scripts.
  var info=new ProcessStartInfo(Path.Combine(folder,"Aurora.Installer.exe")){UseShellExecute=false};
  info.ArgumentList.Add("--replace");info.ArgumentList.Add(AppContext.BaseDirectory);info.ArgumentList.Add(Environment.ProcessId.ToString());Process.Start(info);
 }
}
