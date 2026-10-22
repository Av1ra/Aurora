using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Aurora.Core;
namespace Aurora.Launcher;
record LauncherRelease(string Version,string Asset,string Sha256,string Signature,string? BuildId=null);
sealed class UpdateService
{
 const string Repo="Av1ra/Aurora";
 readonly HttpClient http=new();
 LauncherRelease? latest;
 public UpdateService(){http.DefaultRequestHeaders.UserAgent.ParseAdd("Aurora/0.2");http.Timeout=TimeSpan.FromMinutes(5);}
 async Task<JsonDocument?> PublishedRelease()
 {
  using var response=await http.GetAsync("https://api.github.com/repos/"+Repo+"/releases/latest");
  if(response.StatusCode!=System.Net.HttpStatusCode.NotFound){response.EnsureSuccessStatusCode();return JsonDocument.Parse(await response.Content.ReadAsStringAsync());}
  using var list=JsonDocument.Parse(await http.GetStringAsync("https://api.github.com/repos/"+Repo+"/releases"));
  foreach(var item in list.RootElement.EnumerateArray())if(!item.GetProperty("draft").GetBoolean())return JsonDocument.Parse(item.GetRawText());
  return null;
 }
 // Product version remains 0.2; signed release metadata identifies each build.
 internal static string? InstalledBuildId(){
  string path=Path.Combine(AppContext.BaseDirectory,"build-info.json");
  if(!File.Exists(path))return null;
  try{using var json=JsonDocument.Parse(File.ReadAllText(path));return json.RootElement.GetProperty("Commit").GetString();}
  catch(Exception ex) when(ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException){return null;}
 }
 internal static byte[] ReleaseSignaturePayload(LauncherRelease release)=>
  System.Text.Encoding.UTF8.GetBytes(string.IsNullOrEmpty(release.BuildId)
    ? release.Version+"\n"+release.Asset+"\n"+release.Sha256
    : "Aurora-release-v2\n"+release.Version+"\n"+release.Asset+"\n"+release.Sha256+"\n"+release.BuildId);
 public async Task<LauncherRelease?> Check()
 {
  using var json=await PublishedRelease();if(json==null){latest=null;return null;}
  var assets=json.RootElement.GetProperty("assets").EnumerateArray().ToArray();
  string url=assets.Single(x=>x.GetProperty("name").GetString()=="aurora-update.json").GetProperty("browser_download_url").GetString()!;
  latest=JsonSerializer.Deserialize<LauncherRelease>(await http.GetStringAsync(url))??throw new InvalidDataException("Missing release metadata");
  if(!Version.TryParse(latest.Version,out var version)||version<Version.Parse(ModCatalog.LauncherVersion))return null;
  if(!System.Text.RegularExpressions.Regex.IsMatch(latest.Asset,@"^Aurora-[0-9.]+-win-x64\.zip$"))throw new InvalidDataException("Invalid update asset");
  if(latest.BuildId!=null&&!System.Text.RegularExpressions.Regex.IsMatch(latest.BuildId,@"^[a-fA-F0-9]{40}$"))throw new InvalidDataException("Invalid update build ID");
  if(version==Version.Parse(ModCatalog.LauncherVersion)&&
     (string.IsNullOrEmpty(latest.BuildId)||string.Equals(latest.BuildId,InstalledBuildId(),StringComparison.OrdinalIgnoreCase)))return null;
  return latest;
 }
 public async Task<string?> DownloadVerified(string key)
 {
  if(await Check()==null)return null;
  return await Download(latest!,key);
 }
 async Task<string> Download(LauncherRelease release,string key)
 {
  using var signature=ECDsa.Create();signature.ImportFromPem(key);
  var signed=ReleaseSignaturePayload(release);
  if(!signature.VerifyData(signed,Convert.FromBase64String(release.Signature),HashAlgorithmName.SHA256))throw new InvalidDataException("Invalid update signature");
  var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aurora","updates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  var archive=Path.Combine(root,"update.zip");
  using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(5));
  using(var response=await http.GetAsync("https://github.com/"+Repo+"/releases/download/v"+release.Version+"/"+release.Asset,HttpCompletionOption.ResponseHeadersRead,timeout.Token))
  {response.EnsureSuccessStatusCode();await using var input=await response.Content.ReadAsStreamAsync(timeout.Token);await using var output=File.Create(archive);await input.CopyToAsync(output,timeout.Token);}
  using(var input=File.OpenRead(archive))if(!Convert.ToHexString(SHA256.HashData(input)).Equals(release.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Update SHA-256 mismatch");
  var unpack=Path.Combine(root,"package");Directory.CreateDirectory(unpack);
  using var zip=ZipFile.OpenRead(archive);
  foreach(var entry in zip.Entries){if(entry.FullName.EndsWith('/'))continue;var target=ModCatalog.SafePath(unpack,entry.FullName);if(entry.ExternalAttributes>>16 is var attr && (attr&0xf000)==0xa000)throw new InvalidDataException("Linked ZIP entry");Directory.CreateDirectory(Path.GetDirectoryName(target)!);entry.ExtractToFile(target,false);}
  if(!File.Exists(Path.Combine(unpack,"Aurora Launcher.exe")) ||
     !File.Exists(Path.Combine(unpack,"Aurora Editor.exe")) ||
     !File.Exists(Path.Combine(unpack,"app","Editor","acode.lnchr")))
    throw new InvalidDataException("Incomplete Aurora Launcher / Aurora Editor bundle");
  _=new ModCatalog(key).Verify(Path.Combine(unpack,"app","Editor"));
  return unpack;
 }
 public void StartReplacement(string folder)
 {
  // Separate process waits for the current launcher; no shell, no downloaded scripts.
  var info=new ProcessStartInfo(Path.Combine(folder,"app","Aurora.Installer.exe")){UseShellExecute=false};
  info.ArgumentList.Add("--replace");info.ArgumentList.Add(InstallationPaths.Root);info.ArgumentList.Add(Environment.ProcessId.ToString());Process.Start(info);
 }
}
