using Aurora.Launcher;
if(args.Length>1)
{
 var keyText=File.ReadAllText(args[0]);
 foreach(var name in new[]{"aurora-update.json","aurora-editor.json"})
 {
  var releaseData=System.Text.Json.JsonSerializer.Deserialize<LauncherRelease>(File.ReadAllText(Path.Combine(args[1],name)))!;
  using var key=System.Security.Cryptography.ECDsa.Create();key.ImportFromPem(keyText);
  if(!key.VerifyData(System.Text.Encoding.UTF8.GetBytes(releaseData.Version+"\n"+releaseData.Asset+"\n"+releaseData.Sha256),Convert.FromBase64String(releaseData.Signature),System.Security.Cryptography.HashAlgorithmName.SHA256))throw new Exception("Invalid signature");
  var archive=Path.Combine(args[1],releaseData.Asset);
  using(var file=File.OpenRead(archive))if(!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file)).Equals(releaseData.Sha256,StringComparison.OrdinalIgnoreCase))throw new Exception("Hash mismatch");
  var unpack=Path.Combine(Path.GetTempPath(),"Aurora-release-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(unpack);
  using(var zip=System.IO.Compression.ZipFile.OpenRead(archive))foreach(var entry in zip.Entries){if(entry.FullName.EndsWith('/'))continue;var path=Aurora.Core.ModCatalog.SafePath(unpack,entry.FullName);Directory.CreateDirectory(Path.GetDirectoryName(path)!);System.IO.Compression.ZipFileExtensions.ExtractToFile(entry,path);}
  if(name=="aurora-editor.json")_ =new Aurora.Core.ModCatalog(keyText).Verify(Path.Combine(unpack,"Editor"));
  else if(Directory.Exists(Path.Combine(unpack,"Editor"))||!File.Exists(Path.Combine(unpack,"Aurora.Launcher.exe")))throw new Exception("Unexpected main payload");
  Console.WriteLine("PASS release signature, hash and extracted contents: "+releaseData.Asset);
 }
 Console.WriteLine("AURORA_RELEASE_PACKAGES_PASS");return;
}
var updater=new UpdateService();var release=await updater.Check();
if(release==null)throw new Exception("Run this smoke with -p:Version=0.0.0 to simulate an older installed launcher.");
Console.WriteLine("PASS public release discovered: "+release.Version);
var folder=await updater.DownloadVerified(File.ReadAllText(args[0]));
if(folder==null||!File.Exists(Path.Combine(folder,"Aurora.Bootstrap.exe"))||!File.Exists(Path.Combine(folder,"Aurora.Installer.exe"))||Directory.Exists(Path.Combine(folder,"Editor")))throw new Exception("Incomplete/oversized launcher update");
Console.WriteLine("PASS signature, SHA-256, safe extraction and compact launcher payload");
var editor=await updater.DownloadEditor(File.ReadAllText(args[0]));
var manifest=new Aurora.Core.ModCatalog(File.ReadAllText(args[0])).Verify(Path.Combine(editor,"Editor"));
if(manifest.Id!="aurora.editor")throw new Exception("Wrong addon");
Console.WriteLine("PASS separate editor signature and all addon hashes");
Console.WriteLine("No game files or installed app were changed.");
Console.WriteLine("AURORA_ONLINE_UPDATE_PASS");
