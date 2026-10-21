using Aurora.Core;
using System.Security.Cryptography;
using System.Text.Json;
static void Check(bool value,string label){if(!value)throw new Exception("FAIL "+label);Console.WriteLine("PASS "+label);}
static void Reject(Action action,string label){try{action();}catch{Console.WriteLine("PASS rejected "+label);return;}throw new Exception("FAIL accepted "+label);}
ObjImportChecks.Run(Check,Reject);
await ObjExportChecks.Run(Check);
await LodImportChecks.Run(Check,Reject);
await VehicleChecks.Run(Check,Reject);
await EditorRevisionChecks.Run(Check,Reject);
ModelToolsChecks.Run(Check,Reject);
VehicleWorkflowChecks.Run(Check,Reject);
VehicleProfileChecks.Run(Check);
await TransportAuthoringChecks.Run(Check);
TransportCompletenessChecks.Run(Check,Reject,args.FirstOrDefault());
VehicleAssetChecks.Run(Check,Reject,args.FirstOrDefault());
TransportParameterChecks.Run(Check,Reject,args.FirstOrDefault());
MechanismAxisChecks.Run(Check,Reject,args.FirstOrDefault());
await SharedTransportMaterialChecks.Run(Check,Reject,args.FirstOrDefault());
CargoFrameChecks.Run(Check,Reject);
MaterialPropertyChecks.Run(Check,Reject);
MaterialTerminatorChecks.Run(Check,Reject,args.FirstOrDefault());
await PartialMaterialChecks.Run(Check,Reject);
TransportPackageChecks.Run(Check,Reject);
TransportWorkspaceChecks.Run(Check,Reject);
await WorkspaceExportChecks.Run(Check,Reject);
await NativeTransportExportChecks.Run(Check,args.FirstOrDefault());
VehicleCatalogChecks.Run(Check,Reject,args.FirstOrDefault());
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
  var pivotMatrix=ModelOrientation.Transform(model,1,4,true,false);var oriented=ModelOrientation.Apply(model,pivotMatrix);
  Check(oriented.Nodes.Count==model.Nodes.Count&&oriented.Materials.SequenceEqual(model.Materials),"pivot bake retains native parts and materials");
  for(int i=0;i<model.Nodes.Count;i++){
   var before=model.Nodes[i];var after=oriented.Nodes[i];
   Check(before.Indices.SequenceEqual(after.Indices)&&before.Uv.SequenceEqual(after.Uv)&&before.Groups.SequenceEqual(after.Groups),"native pivot retains triangles, UVs and material assignment");
   Check(Enumerable.Range(0,before.VertexCount).All(v=>{var expected=System.Numerics.Vector3.Transform(new(before.Positions[v*3],before.Positions[v*3+1],before.Positions[v*3+2]),pivotMatrix);return System.Numerics.Vector3.Distance(expected,new(after.Positions[v*3],after.Positions[v*3+1],after.Positions[v*3+2]))<1e-5f;}),"native pivot retains all transformed vertices");
  }
  var names=new HashSet<string>{model.Nodes[0].Name};var glass=new NativeModel(model.AssignGlass(names));
  Check(glass.Nodes.Count==model.Nodes.Count&&glass.Materials.Length==model.Materials.Length+1,"glass preserves model structure "+Path.GetFileName(Path.GetDirectoryName(path))+"/"+Path.GetFileName(path));
  for(int i=0;i<model.Nodes.Count;i++){var a=model.Nodes[i];var b=glass.Nodes[i];Check(a.Positions.SequenceEqual(b.Positions)&&a.Normals.SequenceEqual(b.Normals)&&a.Uv.SequenceEqual(b.Uv)&&a.Indices.SequenceEqual(b.Indices),"geometry/UV/transform unchanged");}
  passed++;
 }
 Console.WriteLine($"NMF_CORPUS accepted={passed} rejected={rejected}");Check(passed>0,"native corpus has supported models");
 var exportFiles=Directory.GetFiles(args[0],"*.nmf");
 if(exportFiles.Length>0){
  var exportModels=exportFiles.ToDictionary(p=>p,p=>new NativeModel(File.ReadAllBytes(p)));
  var sourceHashes=exportFiles.ToDictionary(p=>p,p=>SHA256.HashData(File.ReadAllBytes(p)));
  var exportGlass=exportModels.Keys.ToDictionary(p=>p,p=>new HashSet<string>());
  string nmfOutput=NmfExport.Write(exportModels,exportGlass,root);
  var exportNames=ModelFileNames.Allocate(exportFiles);Check(exportFiles.All(p=>File.ReadAllBytes(Path.Combine(nmfOutput,exportNames[p])).SequenceEqual(exportModels[p].Data)),"NMF-only export retains every LOD byte-for-byte under canonical names");
  foreach(var pair in exportModels)exportGlass[pair.Key].Add(pair.Value.Nodes[0].Name);
  string glassOutput=NmfExport.Write(exportModels,exportGlass,root);
  Check(exportFiles.All(p=>new NativeModel(File.ReadAllBytes(Path.Combine(glassOutput,exportNames[p]))).Materials.Contains("Aurora_Glass")),"NMF-only export applies object glass to every LOD");
  Check(exportFiles.All(p=>sourceHashes[p].SequenceEqual(SHA256.HashData(File.ReadAllBytes(p)))),"NMF exports never modify source files");
  if(exportModels.Count>1){exportGlass[exportFiles[0]].Clear();Reject(()=>NmfExport.Write(exportModels,exportGlass,root),"incomplete glass assignments across exported LODs");}
  var duplicate=exportModels.Take(1).Single();var duplicates=new Dictionary<string,NativeModel>{{duplicate.Key,duplicate.Value},{Path.Combine(root,Path.GetFileName(duplicate.Key)),duplicate.Value}};
  string duplicateOutput=NmfExport.Write(duplicates,new Dictionary<string,HashSet<string>>(),root);Check(Directory.GetFiles(duplicateOutput,"*.nmf").Length==2&&File.Exists(Path.Combine(duplicateOutput,"main.nmf")),"same-basename models receive distinct export names");
 }
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
string[] fixtureRoots=[Path.Combine(root,"steam-fixture","steamapps","workshop","content","784150"),Path.Combine(steamGame,"media_soviet","workshop_wip")];
var foundWorkshop=SteamDiscovery.FindRegisteredWorkshop(steamGame,fixtureRoots);
Check(foundWorkshop.Count==1&&foundWorkshop[0].Complete&&foundWorkshop[0].Folder==creator,"registered Steam ID locates complete workshop_wip payload");
File.Delete(Path.Combine(creator,"mods","pbr.dll"));
foundWorkshop=SteamDiscovery.FindRegisteredWorkshop(steamGame,fixtureRoots);
Check(foundWorkshop.Count==1&&!foundWorkshop[0].Complete&&foundWorkshop[0].MissingFiles.SequenceEqual(["mods/pbr.dll"]),"incomplete Workshop source is reported, never treated as complete");
var subscribed=Path.Combine(root,"steam-fixture","steamapps","workshop","content","784150","3822550289");
Directory.CreateDirectory(Path.Combine(subscribed,"mods"));
foreach(var name in new[]{"m.dll","pbr.dll","glass_default.dds"})File.WriteAllText(Path.Combine(subscribed,"mods",name),"fixture");
foundWorkshop=SteamDiscovery.FindRegisteredWorkshop(steamGame,fixtureRoots);
Check(foundWorkshop.Count==1&&foundWorkshop[0].Complete&&foundWorkshop[0].Folder==subscribed,"complete Steam subscription wins over incomplete workshop_wip");
Directory.CreateDirectory(Path.Combine(root,"steam-fixture","steamapps","workshop","content","784150","9999999999"));
Check(SteamDiscovery.FindRegisteredWorkshop(steamGame,fixtureRoots).Count==1,"unregistered Steam IDs are ignored");


// Unsigned Workshop release metadata is a trigger only for explicitly registered Steam IDs.
File.WriteAllText(Path.Combine(steamGame,"SOVIET64.exe"),"test-game");
Check(WorkshopUpdates.Scan(steamGame,fixtureRoots).Offers.Count==0,"no popup while aurora-module.json is missing");
var releaseFile=Path.Combine(subscribed,WorkshopUpdates.MetadataName);
var release=new WorkshopRelease(1,"3822550289","PBR","1.0.0","2026-10-18",["mods/m.dll","mods/pbr.dll","mods/glass_default.dds"]);
File.WriteAllText(releaseFile,JsonSerializer.Serialize(release));
// An incomplete upload should be ignored quietly until Steam finishes copying it.
File.Delete(Path.Combine(subscribed,"mods","pbr.dll"));
var incomplete=WorkshopUpdates.Scan(steamGame,fixtureRoots);
Check(incomplete.Offers.Count==0 && incomplete.Errors.Count==0,
      "incomplete registered Workshop release triggers no prompt");
File.WriteAllText(Path.Combine(subscribed,"mods","pbr.dll"),"fixture");
var pending=WorkshopUpdates.Scan(steamGame,fixtureRoots);
Check(pending.Offers.Count==1 && pending.Offers[0].FirstInstall &&
      pending.Offers[0].Release.ReleaseDate=="2026-10-18","first installation offered with exact declared release date");
Check(WorkshopUpdates.ReadInstalled(steamGame,"3822550289")==null,"first installation does not invent a previous version");
WorkshopUpdates.Install(steamGame,pending.Offers[0]);
Check(WorkshopUpdates.Scan(steamGame,fixtureRoots).Offers.Count==0,"installed matching Workshop payload produces no repeat popup");
Check(WorkshopUpdates.ReadInstalled(steamGame,"3822550289")?.Version=="1.0.0","installed version recorded separately from release");
// A release date change is a legitimate metadata update even without a version bump.
File.WriteAllText(releaseFile,JsonSerializer.Serialize(release with{ReleaseDate="2026-10-19"}));
pending=WorkshopUpdates.Scan(steamGame,fixtureRoots);
Check(pending.Offers.Count==1 && pending.Offers[0].Release.ReleaseDate=="2026-10-19",
      "release date change alone triggers a comparison prompt");
WorkshopUpdates.Install(steamGame,pending.Offers[0]);
Check(WorkshopUpdates.ReadInstalled(steamGame,"3822550289")?.ReleaseDate=="2026-10-19" &&
      WorkshopUpdates.Scan(steamGame,fixtureRoots).Offers.Count==0,"date-only change is recorded and not offered again");
File.WriteAllText(Path.Combine(subscribed,"mods","pbr.dll"),"changed-without-version-bump");
pending=WorkshopUpdates.Scan(steamGame,fixtureRoots);
Check(pending.Offers.Count==1 && !pending.Offers[0].FirstInstall,"changed DLL triggers update even when version unchanged");
WorkshopUpdates.Install(steamGame,pending.Offers[0]);
Check(File.ReadAllText(Path.Combine(steamGame,"mods","pbr.dll"))=="changed-without-version-bump" &&
      WorkshopUpdates.Scan(steamGame,fixtureRoots).Offers.Count==0,"changed DLL copied and receipt updated");
File.WriteAllText(releaseFile,JsonSerializer.Serialize(release with{Version="1.1.0",ReleaseDate="2026-10-19"}));
pending=WorkshopUpdates.Scan(steamGame,fixtureRoots);
Check(pending.Offers.Count==1 && pending.Offers[0].Installed?.Version=="1.0.0" &&
      pending.Offers[0].Release.Version=="1.1.0" && pending.Offers[0].Release.ReleaseDate=="2026-10-19",
      "popup compares installed and available versions and release dates");
File.WriteAllText(Path.Combine(subscribed,"mods","m.dll"),"changed-after-scan");
Reject(()=>WorkshopUpdates.Install(steamGame,pending.Offers[0]),"changed files between scan and install");
Check(WorkshopUpdates.ReadInstalled(steamGame,"3822550289")?.Version=="1.0.0","stale update did not overwrite installed state");
pending=WorkshopUpdates.Scan(steamGame,fixtureRoots);
WorkshopUpdates.Install(steamGame,pending.Offers[0]);
Check(WorkshopUpdates.ReadInstalled(steamGame,"3822550289")?.Version=="1.1.0" &&
      WorkshopUpdates.Scan(steamGame,fixtureRoots).Offers.Count==0,"new release installed and no longer offered");
File.WriteAllText(releaseFile,JsonSerializer.Serialize(release with{Version="0.9.0"}));
Check(WorkshopUpdates.Scan(steamGame,fixtureRoots).Offers.Count==0,"older Workshop release does not downgrade installed game");
File.WriteAllText(releaseFile,JsonSerializer.Serialize(release with{SteamId="9999999999",Version="2.0.0"}));
Check(WorkshopUpdates.Scan(steamGame,fixtureRoots).Offers.Count==0 && WorkshopUpdates.Scan(steamGame,fixtureRoots).Errors.Count>0,
      "metadata cannot impersonate a different registered ID");
File.Delete(releaseFile);
Check(WorkshopUpdates.Scan(steamGame,fixtureRoots).Offers.Count==0,"empty registered Workshop folder never triggers a popup");

// No persistent backups. Removed files must disappear only if this launcher owns them.
var otherGameFile=Path.Combine(steamGame,"mods","player-settings.ini");
File.WriteAllText(otherGameFile,"keep-user-settings");
File.WriteAllText(releaseFile,JsonSerializer.Serialize(release with{
    Version="1.2.0",ReleaseDate="2026-10-20",
    Files=["mods/pbr.dll","mods/glass_default.dds"]}));
pending=WorkshopUpdates.Scan(steamGame,fixtureRoots);
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
pending=WorkshopUpdates.Scan(steamGame,fixtureRoots);WorkshopUpdates.Install(steamGame,pending.Offers[0]);
File.WriteAllText(Path.Combine(steamGame,"mods","m.dll"),"user-customized-binary");
File.WriteAllText(releaseFile,JsonSerializer.Serialize(release with{
    Version="1.4.0",ReleaseDate="2026-10-22",
    Files=["mods/pbr.dll","mods/glass_default.dds"]}));
pending=WorkshopUpdates.Scan(steamGame,fixtureRoots);WorkshopUpdates.Install(steamGame,pending.Offers[0]);
Check(File.ReadAllText(Path.Combine(steamGame,"mods","m.dll"))=="user-customized-binary" &&
      WorkshopUpdates.ReadInstalled(steamGame,"3822550289")!.Files.Length==2,
      "modified files are preserved and removed from the managed inventory");
Check(!Directory.EnumerateDirectories(Path.Combine(steamGame,"mods",".aurora-workshop"),".transaction-*").Any(),
      "temporary rollback staging removed after preserving user edits");


// Dependencies are signed module data. Older manifests with no Requires remain valid.
var dependencyModule=manifest with{Options=[new("master","PBR","features","enabled"),new("glass","Glass","features","glass",Requires:["master"])]};
Sign(dependencyModule);Check(catalog.Verify(root).Options[1].Requires!.Single()=="master","signed control dependencies supported");
Sign(dependencyModule with{Options=[new("glass","Glass","features","glass",Requires:["missing"])]});Reject(()=>catalog.Verify(root),"missing prerequisite control");
Sign(dependencyModule with{Options=[new("master","PBR","features","enabled",Requires:["glass"]),new("glass","Glass","features","glass",Requires:["master"])]});Reject(()=>catalog.Verify(root),"cyclic control dependencies");
Sign(dependencyModule with{Options=[new("master","PBR","features","enabled"),new("master","Glass","features","glass")]});Reject(()=>catalog.Verify(root),"duplicate control identity");
Sign(manifest);Check(catalog.Verify(root).Options[0].Requires==null,"legacy manifest without dependencies still supported");

// Launch synchronization must not undo a confirmed Workshop update or resurrect a retired DLL.
Sign(installManifest);var signedRuntime=new VerifiedMod(root,catalog.Verify(root));
var guardedGame=Path.Combine(root,"guarded-game");Directory.CreateDirectory(Path.Combine(guardedGame,"mods"));
foreach(var file in installManifest.Files)File.Copy(Path.Combine(root,file.Path.Replace('/',Path.DirectorySeparatorChar)),Path.Combine(guardedGame,file.Path[8..].Replace('/',Path.DirectorySeparatorChar)));
var guardedReceipt=new WorkshopInstalled("3822550289","2.0","2026-10-19",installManifest.Files.Select(f=>new WorkshopFileHash(f.Path[8..],f.Sha256)).ToArray());
string guardedReceiptPath=WorkshopUpdates.ReceiptPath(guardedGame,"3822550289");Directory.CreateDirectory(Path.GetDirectoryName(guardedReceiptPath)!);File.WriteAllText(guardedReceiptPath,JsonSerializer.Serialize(guardedReceipt));
ModuleRuntimeCompatibility.EnsureWorkshopCompatible(guardedGame,[signedRuntime]);Check(true,"matching Workshop runtime remains launch-compatible");
string guardedPbr=Path.Combine(guardedGame,"mods","pbr.dll");File.WriteAllText(guardedPbr,"new-workshop-runtime");
var updatedHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(guardedPbr)));
File.WriteAllText(guardedReceiptPath,JsonSerializer.Serialize(guardedReceipt with{Files=guardedReceipt.Files.Select(f=>f.Path=="mods/pbr.dll"?f with{Sha256=updatedHash}:f).ToArray()}));
Reject(()=>ModuleRuntimeCompatibility.EnsureWorkshopCompatible(guardedGame,[signedRuntime]),"old signed fallback cannot replace newer Workshop runtime");
Check(File.ReadAllText(guardedPbr)=="new-workshop-runtime","conflicting Workshop DLL preserved without mutation");
File.WriteAllText(guardedReceiptPath,JsonSerializer.Serialize(guardedReceipt));
Reject(()=>ModuleRuntimeCompatibility.EnsureWorkshopCompatible(guardedGame,[signedRuntime]),"user-modified Workshop runtime not silently overwritten");
File.Copy(Path.Combine(root,"Payload","mods","pbr.dll"),guardedPbr,true);
File.WriteAllText(guardedReceiptPath,JsonSerializer.Serialize(guardedReceipt with{Files=guardedReceipt.Files.Where(f=>f.Path!="mods/m.dll").ToArray()}));File.Delete(Path.Combine(guardedGame,"mods","m.dll"));
Reject(()=>ModuleRuntimeCompatibility.EnsureWorkshopCompatible(guardedGame,[signedRuntime]),"old fallback cannot resurrect retired Workshop DLL");
Check(!File.Exists(Path.Combine(guardedGame,"mods","m.dll")),"retired file remains absent");
File.WriteAllText(guardedReceiptPath,JsonSerializer.Serialize(guardedReceipt));File.Delete(guardedPbr);
ModuleRuntimeCompatibility.EnsureWorkshopCompatible(guardedGame,[signedRuntime]);Check(true,"matching signed package may repair missing exact runtime files");
var compatibilityOffer=new WorkshopUpdateOffer(SteamDiscovery.RegisteredMods.Single(r=>r.SteamId=="3822550289"),root,new WorkshopRelease(1,"3822550289","PBR","2.0","2026-10-19",guardedReceipt.Files.Select(f=>f.Path).ToArray()),null,guardedReceipt.Files);
Check(ModuleRuntimeCompatibility.OfferMatchesSignedRuntime(compatibilityOffer,[signedRuntime]),"matching Workshop offer is installable with its signed runtime");
Check(!ModuleRuntimeCompatibility.OfferMatchesSignedRuntime(compatibilityOffer,[]),"Workshop offer cannot promise a missing signed runtime");
Check(!ModuleRuntimeCompatibility.OfferMatchesSignedRuntime(compatibilityOffer with{SourceFiles=compatibilityOffer.SourceFiles.Select(f=>f.Path=="mods/pbr.dll"?f with{Sha256=updatedHash}:f).ToArray()},[signedRuntime]),"changed Workshop bytes need a matching signed package");
Check(!ModuleRuntimeCompatibility.OfferMatchesSignedRuntime(compatibilityOffer with{SourceFiles=compatibilityOffer.SourceFiles.Where(f=>f.Path!="mods/m.dll").ToArray()},[signedRuntime]),"retired Workshop files cannot be silently restored by an install offer");

// A fixed public 0.2 version must not let an already open UI act on a changed descriptor.
Sign(manifest);var snapshot=new VerifiedMod(root,catalog.Verify(root));
Check(catalog.VerifySnapshot(snapshot).Id==manifest.Id,"unchanged signed module snapshot accepted");
Sign(manifest with{Name="Same-version refreshed module"});
Reject(()=>catalog.VerifySnapshot(snapshot),"same-version signed metadata refresh requires rescan");
var untouchedGame=Path.Combine(root,"snapshot-game");Directory.CreateDirectory(untouchedGame);File.WriteAllText(Path.Combine(untouchedGame,"SOVIET64.exe"),"fixture");
Reject(()=>ModuleInstaller.Install(snapshot,untouchedGame,pem),"stale module install plan rejected before writes");
Check(!Directory.Exists(Path.Combine(untouchedGame,"mods")),"stale install leaves game files untouched");

Check(Aurora.Shared.WindowSizing.Fit(520,260,96,1920,1080)==(520,260),"100-percent window size retains logical dimensions");
Check(Aurora.Shared.WindowSizing.Fit(520,260,144,1920,1080)==(780,390),"150-percent window scales with WinUI controls");
Check(Aurora.Shared.WindowSizing.Fit(1250,850,144,1920,1040)==(1872,992),"large editor fits the monitor work area at high DPI");
Check(Aurora.Shared.WindowSizing.Fit(520,260,0,1920,1080)==(520,260),"unknown DPI falls back to standard scale");
Check(Aurora.Shared.WindowSizing.Fit(520,260,192,20,20)==(1,1),"small work area never produces negative dimensions");

string viewportDocument="<html><body>trusted viewport</body></html>";
string viewportData=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(viewportDocument));
Check(ViewportNavigation.IsAllowed("about:blank",null),"viewport permits initial blank document");
Check(ViewportNavigation.IsAllowed("data:text/html;base64,"+viewportData,viewportDocument),"viewport permits its exact encoded document");
Check(ViewportNavigation.IsAllowed("data:text/html;charset=utf-8;base64,"+viewportData,viewportDocument),"viewport permits explicit UTF-8 document encoding");
Check(!ViewportNavigation.IsAllowed("data:text/html;base64,"+viewportData,null),"viewport rejects data before trusted document is assigned");
Check(!ViewportNavigation.IsAllowed("data:text/html;base64,PHNjcmlwdD4=",viewportDocument),"viewport rejects different encoded HTML");
Check(!ViewportNavigation.IsAllowed("https://example.invalid/",viewportDocument)&&!ViewportNavigation.IsAllowed("file:///C:/model.nmf",viewportDocument),"viewport rejects web and dropped file navigation");
