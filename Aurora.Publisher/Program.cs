using Aurora.Core;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
if(args[0]=="dev-key")
{
 if(File.Exists(args[1]))throw new IOException("Development key already exists");
 using var developmentKey=ECDsa.Create(ECCurve.NamedCurves.nistP256);
 File.WriteAllBytes(args[1],developmentKey.ExportPkcs8PrivateKey());
 File.WriteAllText(args[2],developmentKey.ExportSubjectPublicKeyInfoPem());return;
}
if(args[0]=="install-local")
{
 string pem=File.ReadAllText(args[1]);var manifest=new ModCatalog(pem).Verify(args[2]);ModuleInstaller.Install(new(args[2],manifest),args[3],pem);Console.WriteLine("Verified module installed with backup");return;
}
var secret=Environment.GetEnvironmentVariable("AURORA_SIGNING_KEY")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aurora","publisher","publisher.pk8");
Directory.CreateDirectory(Path.GetDirectoryName(secret)!);
if(!File.Exists(secret)&&args[0]!="public")throw new InvalidOperationException("Publisher key missing. Restore the existing private key; do not silently create a replacement.");
using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
if(File.Exists(secret))key.ImportPkcs8PrivateKey(File.ReadAllBytes(secret),out _);else File.WriteAllBytes(secret,key.ExportPkcs8PrivateKey());
if(args[0]=="public")File.WriteAllText(args[1],key.ExportSubjectPublicKeyInfoPem());
else if(args[0]=="pixel")File.WriteAllBytes(args[1],DdsImage.Encode(new(1,1,[160,190,210,32])));
else if(args[0]=="manifest")
{
 var payload=File.ReadAllBytes(args[1]);
 var envelope=new SignedEnvelope(Convert.ToBase64String(payload),Convert.ToBase64String(key.SignData(payload,HashAlgorithmName.SHA256)));
 File.WriteAllText(args[2],JsonSerializer.Serialize(envelope));
}
else if(args[0]=="release")
{
 using var file=File.OpenRead(args[2]);var hash=Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();
 string asset=Path.GetFileName(args[2]),version=args[1];
 using var archive=ZipFile.OpenRead(args[2]);
 var buildEntry=archive.GetEntry("app/build-info.json")??throw new InvalidDataException("Missing build-info.json from release");
 using var buildStream=buildEntry.Open();
 using var buildInfo=JsonDocument.Parse(buildStream);
 string buildId=buildInfo.RootElement.GetProperty("Commit").GetString()??"";
 if(buildId.Length!=40||!buildId.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid release build commit");
 string payload="Aurora-release-v2\n"+version+"\n"+asset+"\n"+hash+"\n"+buildId;
 string signature=Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes(payload),HashAlgorithmName.SHA256));
 File.WriteAllText(args[3],JsonSerializer.Serialize(new{Version=version,Asset=asset,Sha256=hash,Signature=signature,BuildId=buildId},new JsonSerializerOptions{WriteIndented=true}));
}
else throw new ArgumentException("public <pem> | manifest <json> <acode.lnchr> | release <version> <zip> <metadata>");
