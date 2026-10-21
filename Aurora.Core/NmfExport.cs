namespace Aurora.Core;

public readonly record struct ModelStatistics(int Objects,long Triangles,long Vertices)
{
 public static ModelStatistics Of(NativeModel model)=>new(model.Nodes.Count,model.Nodes.Sum(n=>(long)n.IndexCount/3),model.Nodes.Sum(n=>(long)n.VertexCount));
}

public static class NmfExport
{
 public static string Write(IReadOnlyDictionary<string,NativeModel> models,IReadOnlyDictionary<string,HashSet<string>> glass,string destination)
 {
  if(models.Count==0)throw new InvalidDataException("No model loaded");
  var names=ModelFileNames.Allocate(models.Keys);
  if(names.Values.Any(n=>string.IsNullOrWhiteSpace(n)||!n.EndsWith(".nmf",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Invalid NMF name");
  bool withGlass=models.Keys.Any(k=>glass.TryGetValue(k,out var nodes)&&nodes.Count>0);
  if(withGlass&&models.Keys.Any(k=>!glass.TryGetValue(k,out var nodes)||nodes.Count==0))throw new InvalidDataException("Отметьте стеклянные объекты во всех LOD.");
  var payload=models.Select(pair=>(Name:names[pair.Key],Data:withGlass?pair.Value.AssignGlass(glass[pair.Key]):pair.Value.Data)).ToArray();
  string target=Path.Combine(Path.GetFullPath(destination),"Aurora-NMF-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]);
  string staging=target+".building";Directory.CreateDirectory(staging);
  try{foreach(var file in payload)File.WriteAllBytes(Path.Combine(staging,file.Name),file.Data);Directory.Move(staging,target);return target;}
  catch{try{foreach(var file in payload)File.Delete(Path.Combine(staging,file.Name));Directory.Delete(staging);}catch{}throw;}
 }
}
