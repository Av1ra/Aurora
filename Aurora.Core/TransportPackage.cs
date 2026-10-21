using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Aurora.Core;

public static class TransportPackage
{
 public static string[] References(string script)=>VehicleScript.Tokens(script).Where(t=>t.Index==0&&t.Key is "FAMILY" or "TRAINSET" or "TRAINSET_REVERSE"||t.Key=="HORSE"&&t.Index==3&&t.Value!="single").Select(t=>t.Value).Distinct().ToArray();
 public static string Write(string rootExport,string? rootNativeId,string itemId,string rootName,IReadOnlyDictionary<string,string> nativeObjects,string destination,bool includeRelated=true)
 {
  if(!ulong.TryParse(itemId,out ulong id)||id==0||!Regex.IsMatch(rootName,@"^[A-Za-z0-9_-]{1,63}$"))throw new InvalidDataException("Invalid Workshop item ID or object name");
  string final=Path.Combine(destination,"Aurora-Transport-"+Guid.NewGuid().ToString("N")),stage=final+".building";Directory.CreateDirectory(stage);var aliases=new Dictionary<string,string>(StringComparer.Ordinal);var sources=new Dictionary<string,string>{{rootName,Path.GetFullPath(rootExport)}};if(rootNativeId!=null)aliases[rootNativeId]=rootName;var external=new HashSet<string>();var queue=new Queue<string>();queue.Enqueue(rootName);
  try{
   while(queue.TryDequeue(out string? name)){
    string folder=sources[name],scriptFile=Path.Combine(folder,"script.ini");if(!File.Exists(scriptFile)||!File.Exists(Path.Combine(folder,"main.nmf")))throw new InvalidDataException("Vehicle requires main.nmf and script.ini: "+name);string script=File.ReadAllText(scriptFile);
    foreach(string reference in References(script)){if(aliases.ContainsKey(reference))continue;if(includeRelated&&nativeObjects.TryGetValue(reference,out string? source)){if(sources.Count>=128)throw new InvalidDataException("Transport package exceeds 128 objects");string alias="object_"+sources.Count;while(sources.ContainsKey(alias))alias+="_";aliases[reference]=alias;sources[alias]=Path.GetDirectoryName(source)!;queue.Enqueue(alias);}else external.Add(reference);}
   }
   foreach(var entry in sources){string source=entry.Value,target=Path.Combine(stage,entry.Key);Directory.CreateDirectory(target);string script=File.ReadAllText(Path.Combine(source,"script.ini"));string? media=VehicleAssets.Media(source);
    string Copy(string path){byte[] bytes=File.ReadAllBytes(path);string name="asset-"+Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()+Path.GetExtension(path).ToLowerInvariant();string file=Path.Combine(target,name);if(!File.Exists(file))File.WriteAllBytes(file,bytes);return name;}
    // Preserve every exported root asset, including PBR maps and generated previews.
    if(entry.Key==rootName){foreach(string file in Directory.GetFiles(source)){string name=Path.GetFileName(file);if(name.Equals("EXPORT-FAILED.txt",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Incomplete root export");File.Copy(file,Path.Combine(target,name));}}
    else{
     File.Copy(Path.Combine(source,"main.nmf"),Path.Combine(target,"main.nmf"));
     foreach(string material in Directory.GetFiles(source,"material*.mtl").Where(p=>Regex.IsMatch(Path.GetFileName(p),@"^material(?:_\d+)?\.mtl$",RegexOptions.IgnoreCase))){string text=VehicleAssets.MapMaterial(File.ReadAllText(material),r=>Copy(MaterialFiles.Resolve(material,r)));File.WriteAllText(Path.Combine(target,Path.GetFileName(material)),text,new UTF8Encoding(false));if(MaterialFiles.Companion(material) is string pbr){string rules=MaterialFiles.MapPbr(File.ReadAllText(pbr),r=>Copy(MaterialFiles.Resolve(pbr,r)));File.WriteAllText(Path.Combine(target,MaterialFiles.PairedName(material)),rules,new UTF8Encoding(false));}}
     if(!File.Exists(Path.Combine(target,"material.mtl")))throw new InvalidDataException("Vehicle material.mtl missing: "+entry.Key);
     foreach(string optional in new[]{"bbox.bin","preview.dds","preview_side.dds"})if(File.Exists(Path.Combine(source,optional)))File.Copy(Path.Combine(source,optional),Path.Combine(target,optional));
    }
    script=VehicleSoundFiles.Export(script,source,target);script=VehicleAssets.Export(script,source,target,media);script=VehicleScript.RewriteTokens(script,t=>Regex.IsMatch(t.Key,@"^LOD\d*$")&&t.Index==0?Copy(VehicleAssets.Resolve(t.Value,source,media)):null);
    script=VehicleScript.RewriteTokens(script,t=>(t.Index==0&&t.Key is "FAMILY" or "TRAINSET" or "TRAINSET_REVERSE"||t.Key=="HORSE"&&t.Index==3)&&aliases.TryGetValue(t.Value,out string? alias)?itemId+"/"+alias:null);File.WriteAllText(Path.Combine(target,"script.ini"),script,new UTF8Encoding(false));
   }
   File.WriteAllText(Path.Combine(stage,"workshopscript.ini"),string.Join(Environment.NewLine,sources.Keys.Select(n=>"$OBJECT_VEHICLE "+n))+Environment.NewLine,new UTF8Encoding(false));
   File.WriteAllText(Path.Combine(stage,"aurora-transport.json"),JsonSerializer.Serialize(new{Schema=1,ItemId=itemId,Objects=sources.Keys,ExternalObjects=external.Order().ToArray()},new JsonSerializerOptions{WriteIndented=true}));Directory.Move(stage,final);return final;
  }catch{File.WriteAllText(Path.Combine(stage,"EXPORT-FAILED.txt"),"Incomplete transport package. Do not install.");throw;}
 }
}
