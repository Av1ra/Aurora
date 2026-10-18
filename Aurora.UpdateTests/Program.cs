using Aurora.Launcher;
using Aurora.Core;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO.Compression;

if (args.Length > 1)
{
    string keyText=File.ReadAllText(args[0]);
    string output=args[1];
    var archives=Directory.GetFiles(output,"*.zip");
    if(archives.Length!=1 || Path.GetFileName(archives[0])!="Aurora-0.2-win-x64.zip")
        throw new Exception("Expected exactly one Launcher + Editor archive");
    var metadata=JsonSerializer.Deserialize<LauncherRelease>(File.ReadAllText(Path.Combine(output,"aurora-update.json")))!;
    if(metadata.Asset!=Path.GetFileName(archives[0]) || metadata.Version!="0.2")
        throw new Exception("Unexpected release asset");
    using(var key=ECDsa.Create())
    {
        key.ImportFromPem(keyText);
        var signed=Encoding.UTF8.GetBytes(metadata.Version+"\n"+metadata.Asset+"\n"+metadata.Sha256);
        if(!key.VerifyData(signed,Convert.FromBase64String(metadata.Signature),HashAlgorithmName.SHA256))
            throw new Exception("Invalid update signature");
    }
    using(var input=File.OpenRead(archives[0]))
        if(!Convert.ToHexString(SHA256.HashData(input)).Equals(metadata.Sha256,StringComparison.OrdinalIgnoreCase))
            throw new Exception("Package SHA-256 mismatch");
    var unpack=Path.Combine(Path.GetTempPath(),"Aurora-single-package-"+Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(unpack);
    using(var zip=ZipFile.OpenRead(archives[0]))
    foreach(var entry in zip.Entries)
    {
        if(entry.FullName.EndsWith('/'))continue;
        var target=ModCatalog.SafePath(unpack,entry.FullName);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        entry.ExtractToFile(target);
    }
    if(!File.Exists(Path.Combine(unpack,"Aurora Launcher.exe"))||
       !File.Exists(Path.Combine(unpack,"Aurora Editor.exe"))||
       !File.Exists(Path.Combine(unpack,"app","Aurora.Launcher.exe"))||
       !File.Exists(Path.Combine(unpack,"app","Aurora.Bootstrap.exe"))||
       !File.Exists(Path.Combine(unpack,"app","Aurora.Installer.exe")))
        throw new Exception("Missing Launcher / Editor application entry points");
    var catalog=new ModCatalog(keyText);
    var editor=catalog.Verify(Path.Combine(unpack,"app","Editor"));
    if(editor.Id!="aurora.editor" ||
       !File.Exists(Path.Combine(unpack,"app","Editor","Aurora.Editor.Module.dll")) ||
       !File.Exists(Path.Combine(unpack,"app","Editor","Assets","viewport.html")))
        throw new Exception("Bundled editor incomplete");
    foreach(var folder in Directory.GetDirectories(Path.Combine(unpack,"app","Modules")))
        _=catalog.Verify(folder);
    if(Directory.EnumerateFiles(unpack,"*.pk8",SearchOption.AllDirectories).Any() ||
       Directory.GetFiles(unpack,"*.zip",SearchOption.AllDirectories).Any())
        throw new Exception("Private signing key or duplicate archives shipped");
    if(File.Exists(Path.Combine(unpack,"Aurora.exe")) || File.Exists(Path.Combine(unpack,"Aurora.Editor.exe")))
        throw new Exception("Legacy launcher entry points duplicated");
    Console.WriteLine("PASS signed Aurora 0.2: exactly one Launcher + Editor archive");
    Console.WriteLine("PASS both application entry points, shared runtime and signed embedded Editor");
    Console.WriteLine("PASS optional module signatures and no duplicate ZIP/private keys");
    Console.WriteLine("AURORA_RELEASE_PACKAGES_PASS");
    return;
}
var updater=new UpdateService();
var release=await updater.Check();
if(release==null)throw new Exception("Run online smoke with -p:Version=0.0.0");
var folder=await updater.DownloadVerified(File.ReadAllText(args[0]));
if(folder==null ||
   !File.Exists(Path.Combine(folder,"Aurora Launcher.exe")) ||
   !File.Exists(Path.Combine(folder,"Aurora Editor.exe")) ||
   !File.Exists(Path.Combine(folder,"app","Editor","acode.lnchr")))
    throw new Exception("Incomplete Launcher + Editor update");
Console.WriteLine("PASS one signed bundle downloads both Aurora Launcher and Aurora Editor");
