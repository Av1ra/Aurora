using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Aurora.Core;

public sealed record TransportWorkspace(string Active,Dictionary<string,Dictionary<string,byte[]>> Objects)
{
 sealed record Manifest(int Schema,string Active,Dictionary<string,Dictionary<string,string>> Objects);
 public static bool ValidName(string name)=>Regex.IsMatch(name,@"^[A-Za-z0-9_-]{1,63}$");
 public Dictionary<string,byte[]> Pack()
 {
  if(Objects.Count is <1 or >64||!Objects.ContainsKey(Active)||Objects.Keys.Any(n=>!ValidName(n))||Objects.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=Objects.Count)throw new InvalidDataException("Invalid transport workspace");
  var data=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);var objects=new Dictionary<string,Dictionary<string,string>>();
  foreach(var item in Objects){if(item.Value.Count is <1 or >2048||!item.Value.ContainsKey("document.json")||item.Value.Keys.Any(p=>!AuroraProjectArchive.IsPortablePath(p)))throw new InvalidDataException("Invalid vehicle document");var files=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);foreach(var file in item.Value){string id="data/"+Convert.ToHexString(SHA256.HashData(file.Value)).ToLowerInvariant();if(!data.TryAdd(id,file.Value)&&!data[id].SequenceEqual(file.Value))throw new InvalidDataException("Project data collision");if(!files.TryAdd(file.Key,id))throw new InvalidDataException("Duplicate vehicle asset path");}objects[item.Key]=files;}
  data["transport.json"]=JsonSerializer.SerializeToUtf8Bytes(new Manifest(1,Active,objects));if(data.Count>2048||data.Values.Sum(b=>(long)b.Length)>256L*1024*1024)throw new InvalidDataException("Workspace exceeds archive limits");return data;
 }
 public static TransportWorkspace Unpack(IReadOnlyDictionary<string,byte[]> data)
 {
  if(!data.TryGetValue("transport.json",out var json)){if(!data.ContainsKey("document.json"))throw new InvalidDataException("Project document missing");return new("vehicle",new(){{"vehicle",new(data,StringComparer.OrdinalIgnoreCase)}});}
  var manifest=JsonSerializer.Deserialize<Manifest>(json)??throw new InvalidDataException("Invalid workspace manifest");if(manifest.Schema!=1||manifest.Objects==null||manifest.Objects.Count is <1 or >64||!manifest.Objects.ContainsKey(manifest.Active)||manifest.Objects.Keys.Any(n=>!ValidName(n))||manifest.Objects.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=manifest.Objects.Count)throw new InvalidDataException("Invalid workspace state");
  var objects=new Dictionary<string,Dictionary<string,byte[]>>();var verified=new HashSet<string>();foreach(var item in manifest.Objects){if(item.Value==null||item.Value.Count is <1 or >2048||!item.Value.ContainsKey("document.json"))throw new InvalidDataException("Invalid vehicle document");var files=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);foreach(var file in item.Value){if(!AuroraProjectArchive.IsPortablePath(file.Key)||file.Value==null||!Regex.IsMatch(file.Value,@"^data/[0-9a-f]{64}$")||!data.TryGetValue(file.Value,out var bytes)||!files.TryAdd(file.Key,bytes))throw new InvalidDataException("Invalid workspace dependency");if(verified.Add(file.Value)&&file.Value!="data/"+Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant())throw new InvalidDataException("Workspace dependency hash differs");}objects.Add(item.Key,files);}return new(manifest.Active,objects);
 }
}
