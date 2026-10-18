using Aurora.Core;
using System.Security.Cryptography;
using System.Text.Json;
static void Check(bool value,string label){if(!value)throw new Exception("FAIL "+label);Console.WriteLine("PASS "+label);}
static void Reject(Action action,string label){try{action();}catch{Console.WriteLine("PASS rejected "+label);return;}throw new Exception("FAIL accepted "+label);}
var root=Path.Combine(Path.GetTempPath(),"Aurora-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);string pem=key.ExportSubjectPublicKeyInfoPem();
File.WriteAllText(Path.Combine(root,"module.dll"),"fixture-not-executable");
var manifest=new ModManifest(1,"test.graphics","Графика","graphics","1.0.0","0.1.0","pbr-runtime.ini",[new("module.dll",Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"module.dll")))))],[new("pbr","PBR","features","enabled")]);
void Sign(ModManifest m){byte[] payload=JsonSerializer.SerializeToUtf8Bytes(m);File.WriteAllText(Path.Combine(root,"acode.lnchr"),JsonSerializer.Serialize(new SignedEnvelope(Convert.ToBase64String(payload),Convert.ToBase64String(key.SignData(payload,HashAlgorithmName.SHA256)))));}
Sign(manifest);var catalog=new ModCatalog(pem);Check(catalog.Verify(root).Id=="test.graphics","valid publisher signature and SHA");
File.AppendAllText(Path.Combine(root,"module.dll"),"tamper");Reject(()=>catalog.Verify(root),"tampered DLL");File.WriteAllText(Path.Combine(root,"module.dll"),"fixture-not-executable");
using(var other=ECDsa.Create(ECCurve.NamedCurves.nistP256))Reject(()=>new ModCatalog(other.ExportSubjectPublicKeyInfoPem()).Verify(root),"untrusted publisher");
Sign(manifest with{MinimumLauncher="99.0.0"});Reject(()=>catalog.Verify(root),"future incompatible module");
Sign(manifest with{Config="../pbr.ini"});Reject(()=>catalog.Verify(root),"config traversal");
foreach(var path in new[]{"../x","/x","c:/x","a\\..\\x","a//x","x:stream"})Reject(()=>ModCatalog.SafePath(root,path),"unsafe path "+path);
Check(catalog.Discover([Path.Combine(root,"empty")]).Mods.Count==0,"empty library yields empty menu");
Directory.CreateDirectory(Path.Combine(root,"Payload","mods"));
File.WriteAllText(Path.Combine(root,"Payload","mods","m.dll"),"new-overlay");File.WriteAllText(Path.Combine(root,"Payload","mods","pbr.dll"),"new-graphics");
var installManifest=manifest with{Files=new[]{"m.dll","pbr.dll"}.Select(x=>new ModFile("Payload/mods/"+x,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"Payload","mods",x)))))).ToArray()};Sign(installManifest);
string game=Path.Combine(root,"game");Directory.CreateDirectory(Path.Combine(game,"mods"));File.WriteAllText(Path.Combine(game,"SOVIET64.exe"),"fixture-not-game");File.WriteAllText(Path.Combine(game,"mods","m.dll"),"old-overlay");Directory.CreateDirectory(Path.Combine(game,"mods","pbr.dll"));
Reject(()=>ModuleInstaller.Install(new(root,installManifest),game,pem),"destination collision");Check(File.ReadAllText(Path.Combine(game,"mods","m.dll"))=="old-overlay","partial install rolls back old DLL");
Directory.Delete(Path.Combine(game,"mods","pbr.dll"));File.WriteAllText(Path.Combine(game,"mods","pbr-runtime.ini"),"user-settings-unchanged");
ModuleInstaller.Install(new(root,installManifest),game,pem);Check(File.ReadAllText(Path.Combine(game,"mods","pbr.dll"))=="new-graphics"&&File.ReadAllText(Path.Combine(game,"mods","pbr-runtime.ini"))=="user-settings-unchanged","signed module installs with preserved user INI");
var pixel=new RgbaImage(1,1,[160,190,210,32]);var encoded=DdsImage.Encode(pixel);var decoded=DdsImage.Decode(encoded);Check(encoded.Length==132&&decoded.Pixels.SequenceEqual(pixel.Pixels),"one-pixel DDS lossless RGBA alpha");
Reject(()=>new NativeModel([1,2,3]),"truncated NMF");
if(args.Length>0)
{
 int passed=0,rejected=0;foreach(var path in Directory.EnumerateFiles(args[0],"*.nmf",SearchOption.AllDirectories).Take(180))
 {
  NativeModel model;try{model=new(File.ReadAllBytes(path));}catch(InvalidDataException){rejected++;continue;}
  var names=new HashSet<string>{model.Nodes[0].Name};var glass=new NativeModel(model.AssignGlass(names));
  Check(glass.Nodes.Count==model.Nodes.Count&&glass.Materials.Length==model.Materials.Length+1,"glass preserves model structure "+Path.GetFileName(Path.GetDirectoryName(path))+"/"+Path.GetFileName(path));
  for(int i=0;i<model.Nodes.Count;i++){var a=model.Nodes[i];var b=glass.Nodes[i];Check(a.Positions.SequenceEqual(b.Positions)&&a.Normals.SequenceEqual(b.Normals)&&a.Uv.SequenceEqual(b.Uv)&&a.Indices.SequenceEqual(b.Indices),"geometry/UV/transform unchanged");}
  passed++;
 }
 Console.WriteLine($"NMF_CORPUS accepted={passed} rejected={rejected}");Check(passed>0,"native corpus has supported models");
}
Console.WriteLine("AURORA_TESTS_PASS");

// Schema 2 numeric controls are data owned by independent modules.
var numeric=manifest with{Schema=2,Placement="settings",Options=[new("degrees","FOV","camera","degrees",Kind:"slider",Minimum:20,Maximum:100,DefaultValue:45)]};
Sign(numeric);Check(catalog.Verify(root).Options[0].DefaultValue==45,"signed numeric module metadata");
Sign(numeric with{Stage="alpha"});Check(catalog.Verify(root).Stage=="alpha","signed alpha module stage");
Sign(numeric with{Stage="unknown"});Reject(()=>catalog.Verify(root),"invalid module stage");
Sign(numeric with{Schema=1});Reject(()=>catalog.Verify(root),"numeric control in old schema");
Sign(numeric with{Options=[new("degrees","FOV","camera","degrees",Kind:"slider",Minimum:20,Maximum:100,DefaultValue:150)]});Reject(()=>catalog.Verify(root),"invalid numeric bounds");
// Workshop content wins an equal-version tie against the bundled fallback.
var bundled=Path.Combine(root,"bundled");var workshop=Path.Combine(root,"workshop","12345");
Directory.CreateDirectory(bundled);Directory.CreateDirectory(workshop);
Sign(manifest);
foreach(var destination in new[]{bundled,workshop})foreach(var name in new[]{"acode.lnchr","module.dll"})File.Copy(Path.Combine(root,name),Path.Combine(destination,name));
Check(catalog.Discover([bundled,Path.GetDirectoryName(workshop)!]).Mods.Single().Folder==workshop,"Workshop overrides bundled equal-version module");
File.AppendAllText(Path.Combine(workshop,"module.dll"),"tampered-update");
var fallback=catalog.Discover([bundled,Path.GetDirectoryName(workshop)!]);
Check(fallback.Mods.Single().Folder==bundled&&fallback.Errors.Count==1,"tampered Workshop update rejected with fallback");

// Registered Workshop items use the declared Steam ID, including creator's workshop_wip layout.
var steamGame=Path.Combine(root,"steam-fixture","steamapps","common","SovietRepublic");
var creator=Path.Combine(steamGame,"media_soviet","workshop_wip","3822550289");
Directory.CreateDirectory(Path.Combine(creator,"mods"));
File.WriteAllText(Path.Combine(creator,"manifest.json"),"{}");
File.WriteAllText(Path.Combine(creator,"workshopconfig.ini"),"fixture");
foreach(var name in new[]{"m.dll","pbr.dll","glass_default.dds"})File.WriteAllText(Path.Combine(creator,"mods",name),"fixture");
var foundWorkshop=SteamDiscovery.FindRegisteredWorkshop(steamGame);
Check(foundWorkshop.Count==1&&foundWorkshop[0].Complete&&foundWorkshop[0].Folder==creator,"registered Steam ID locates complete workshop_wip payload");
File.Delete(Path.Combine(creator,"mods","pbr.dll"));
foundWorkshop=SteamDiscovery.FindRegisteredWorkshop(steamGame);
Check(foundWorkshop.Count==1&&!foundWorkshop[0].Complete&&foundWorkshop[0].MissingFiles.SequenceEqual(["mods/pbr.dll"]),"incomplete Workshop source is reported, never treated as complete");
var subscribed=Path.Combine(root,"steam-fixture","steamapps","workshop","content","784150","3822550289");
Directory.CreateDirectory(Path.Combine(subscribed,"mods"));
foreach(var name in new[]{"m.dll","pbr.dll","glass_default.dds"})File.WriteAllText(Path.Combine(subscribed,"mods",name),"fixture");
foundWorkshop=SteamDiscovery.FindRegisteredWorkshop(steamGame);
Check(foundWorkshop.Count==1&&foundWorkshop[0].Complete&&foundWorkshop[0].Folder==subscribed,"complete Steam subscription wins over incomplete workshop_wip");
Directory.CreateDirectory(Path.Combine(root,"steam-fixture","steamapps","workshop","content","784150","9999999999"));
Check(SteamDiscovery.FindRegisteredWorkshop(steamGame).Count==1,"unregistered Steam IDs are ignored");


// Unsigned Workshop release metadata is a trigger only for explicitly registered Steam IDs.
File.WriteAllText(Path.Combine(steamGame,"SOVIET64.exe"),"test-game");
Check(WorkshopUpdates.Scan(steamGame).Offers.Count==0,"no popup while aurora-module.json is missing");
var releaseFile=Path.Combine(subscribed,WorkshopUpdates.MetadataName);
var release=new WorkshopRelease(1,"3822550289","PBR","1.0.0","2026-10-18",["mods/m.dll","mods/pbr.dll","mods/glass_default.dds"]);
File.WriteAllText(releaseFile,JsonSerializer.Serialize(release));
var pending=WorkshopUpdates.Scan(steamGame);
Check(pending.Offers.Count==1 && pending.Offers[0].FirstInstall &&
      pending.Offers[0].Release.ReleaseDate=="2026-10-18","first installation offered with exact declared release date");
Check(WorkshopUpdates.ReadInstalled(steamGame,"3822550289")==null,"first installation does not invent a previous version");
WorkshopUpdates.Install(steamGame,pending.Offers[0]);
Check(WorkshopUpdates.Scan(steamGame).Offers.Count==0,"installed matching Workshop payload produces no repeat popup");
Check(WorkshopUpdates.ReadInstalled(steamGame,"3822550289")?.Version=="1.0.0","installed version recorded separately from release");
File.WriteAllText(Path.Combine(subscribed,"mods","pbr.dll"),"changed-without-version-bump");
pending=WorkshopUpdates.Scan(steamGame);
Check(pending.Offers.Count==1 && !pending.Offers[0].FirstInstall,"changed DLL triggers update even when version unchanged");
WorkshopUpdates.Install(steamGame,pending.Offers[0]);
Check(File.ReadAllText(Path.Combine(steamGame,"mods","pbr.dll"))=="changed-without-version-bump" &&
      WorkshopUpdates.Scan(steamGame).Offers.Count==0,"changed DLL copied and receipt updated");
File.WriteAllText(releaseFile,JsonSerializer.Serialize(release with{Version="1.1.0",ReleaseDate="2026-10-19"}));
pending=WorkshopUpdates.Scan(steamGame);
Check(pending.Offers.Count==1 && pending.Offers[0].Installed?.Version=="1.0.0" &&
      pending.Offers[0].Release.Version=="1.1.0" && pending.Offers[0].Release.ReleaseDate=="2026-10-19",
      "popup compares installed and available versions and release dates");
File.WriteAllText(Path.Combine(subscribed,"mods","m.dll"),"changed-after-scan");
Reject(()=>WorkshopUpdates.Install(steamGame,pending.Offers[0]),"changed files between scan and install");
Check(WorkshopUpdates.ReadInstalled(steamGame,"3822550289")?.Version=="1.0.0","stale update did not overwrite installed state");
pending=WorkshopUpdates.Scan(steamGame);
WorkshopUpdates.Install(steamGame,pending.Offers[0]);
Check(WorkshopUpdates.ReadInstalled(steamGame,"3822550289")?.Version=="1.1.0" &&
      WorkshopUpdates.Scan(steamGame).Offers.Count==0,"new release installed and no longer offered");
File.WriteAllText(releaseFile,JsonSerializer.Serialize(release with{Version="0.9.0"}));
Check(WorkshopUpdates.Scan(steamGame).Offers.Count==0,"older Workshop release does not downgrade installed game");
File.WriteAllText(releaseFile,JsonSerializer.Serialize(release with{SteamId="9999999999",Version="2.0.0"}));
Check(WorkshopUpdates.Scan(steamGame).Offers.Count==0 && WorkshopUpdates.Scan(steamGame).Errors.Count>0,
      "metadata cannot impersonate a different registered ID");
File.Delete(releaseFile);
Check(WorkshopUpdates.Scan(steamGame).Offers.Count==0,"empty registered Workshop folder never triggers a popup");

// No persistent backups. Removed files must disappear only if this launcher owns them.
var otherGameFile=Path.Combine(steamGame,"mods","player-settings.ini");
File.WriteAllText(otherGameFile,"keep-user-settings");
File.WriteAllText(releaseFile,JsonSerializer.Serialize(release with{
    Version="1.2.0",ReleaseDate="2026-10-20",
    Files=["mods/pbr.dll","mods/glass_default.dds"]}));
pending=WorkshopUpdates.Scan(steamGame);
Check(pending.Offers.Count==1 && pending.Offers[0].SourceFiles.Length==2,
      "manifest can retire one previously registered DLL");
WorkshopUpdates.Install(steamGame,pending.Offers[0]);
Check(!File.Exists(Path.Combine(steamGame,"mods","m.dll")),
      "obsolete previously tracked DLL removed from the game");
Check(File.ReadAllText(otherGameFile)=="keep-user-settings",
      "untracked files and user settings remain untouched");
Check(!Directory.Exists(Path.Combine(steamGame,"mods","aurora-backups")) &&
      !Directory.EnumerateDirectories(Path.Combine(steamGame,"mods",".aurora-workshop"),".transaction-*").Any(),
      "no persistent or temporary backup directories remain after successful update");

// A player-customized tracked file must not be deleted simply because metadata
// stops listing it: the receipt's original hash must still match.
File.WriteAllText(releaseFile,JsonSerializer.Serialize(release with{Version="1.3.0",ReleaseDate="2026-10-21"}));
pending=WorkshopUpdates.Scan(steamGame);WorkshopUpdates.Install(steamGame,pending.Offers[0]);
File.WriteAllText(Path.Combine(steamGame,"mods","m.dll"),"user-customized-binary");
File.WriteAllText(releaseFile,JsonSerializer.Serialize(release with{
    Version="1.4.0",ReleaseDate="2026-10-22",
    Files=["mods/pbr.dll","mods/glass_default.dds"]}));
pending=WorkshopUpdates.Scan(steamGame);WorkshopUpdates.Install(steamGame,pending.Offers[0]);
Check(File.ReadAllText(Path.Combine(steamGame,"mods","m.dll"))=="user-customized-binary" &&
      WorkshopUpdates.ReadInstalled(steamGame,"3822550289")!.Files.Length==2,
      "modified files are preserved and removed from the managed inventory");
Check(!Directory.EnumerateDirectories(Path.Combine(steamGame,"mods",".aurora-workshop"),".transaction-*").Any(),
      "temporary rollback staging removed after preserving user edits");

