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
  var release=latest!;using var signature=ECDsa.Create();signature.ImportFromPem(key);
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
  if(!File.Exists(Path.Combine(unpack,"Aurora.Launcher.exe")))throw new InvalidDataException("Incomplete update");return unpack;
 }
 public void StartReplacement(string folder)
 {
  // Separate process waits for the current launcher; no shell, no downloaded scripts.
  var info=new ProcessStartInfo(Path.Combine(folder,"Aurora.Installer.exe")){UseShellExecute=false};
  info.ArgumentList.Add("--replace");info.ArgumentList.Add(AppContext.BaseDirectory);info.ArgumentList.Add(Environment.ProcessId.ToString());Process.Start(info);
 }
}
