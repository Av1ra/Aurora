using Aurora.Core;
using Aurora.Editor;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
static void Check(bool ok,string text){if(!ok)throw new Exception("FAIL "+text);Console.WriteLine("PASS "+text);}
string source=args[0],mask=args[1];var root=Path.Combine(Path.GetTempPath(),"Aurora-export-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
var lods=Directory.GetFiles(source,"*.nmf").Where(x=>Path.GetFileName(x)=="main.nmf"||Path.GetFileName(x).StartsWith("lod")).ToDictionary(x=>x,x=>new NativeModel(File.ReadAllBytes(x)));
var hashes=Directory.GetFiles(source).ToDictionary(x=>x,x=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x))));
string mtl=Path.Combine(source,"material.mtl"),name=lods.First().Value.Materials[0];
var settings=new Dictionary<string,MaterialSettings>{{name,new(Metallic:.7f,Roughness:.25f,NormalY:-1)}};
foreach(var mode in new[]{"pbr","objects","mask"})
{
 var destination=Path.Combine(root,mode);Directory.CreateDirectory(destination);
 var glass=lods.ToDictionary(x=>x.Key,x=>mode=="objects"?new HashSet<string>{x.Value.Nodes[0].Name}:new HashSet<string>());
 await ModelExport.Export(lods,glass,settings,mtl,mode=="mask"?mask:null,mode=="mask"?name:null,destination,p=>Task.FromResult(DdsImage.Decode(File.ReadAllBytes(p))));
 string result=Directory.GetDirectories(destination).Single();
 foreach(var pair in lods){var updated=new NativeModel(File.ReadAllBytes(Path.Combine(result,Path.GetFileName(pair.Key))));Check(updated.Nodes.Count==pair.Value.Nodes.Count,mode+" all LOD nodes retained");}
 var pbr=File.ReadAllText(Path.Combine(result,"material.pbr"));Check(pbr.Contains("metallic_factor = 0.7"),mode+" configured PBR retained");
 if(mode=="objects")Check(pbr.Contains("aurora:glass_default"),"object glass uses shared 1-pixel asset");
 if(mode=="mask")Check(pbr.Contains("glass_mask = glass.dds")&&File.Exists(Path.Combine(result,"glass-split-report.json")),"UV alpha glass split produced report");
 var material=File.ReadAllText(Path.Combine(result,"material.mtl"));Check(!Regex.IsMatch(material,@"(?m)^\s*\$TEXTURE\s"),"portable MTL uses TEXTURE_MTL");
 foreach(Match match in Regex.Matches(material,@"(?m)^\s*\$TEXTURE_MTL\s+\d+\s+([^\r\n]+)"))Check(File.Exists(Path.Combine(result,match.Groups[1].Value.Trim())),"portable MTL texture exists");
 Console.WriteLine("EXPORT_FIXTURE "+result);
}
foreach(var pair in hashes)Check(pair.Value==Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pair.Key))),"source unchanged "+Path.GetFileName(pair.Key));
Console.WriteLine("AURORA_EXPORT_TESTS_PASS");
