using Aurora.Core;
using System.Text.Json;
if(args.Length>1&&args[0]=="--inspect"){
 foreach(string path in args.Skip(1))try{var model=new NativeModel(File.ReadAllBytes(path));Console.WriteLine(JsonSerializer.Serialize(new{File=path,model.Animated,Nodes=model.Nodes.Select(n=>new{n.Name,n.VertexCount,n.IndexCount})}));}catch(Exception ex){Console.WriteLine(JsonSerializer.Serialize(new{File=path,Error=ex.Message}));}return;
}
if(args.Length!=2)throw new ArgumentException("NmfAudit <media_soviet> <report.json>");
var objects=VehicleCatalog.Load(args[0],true).Objects;
var paths=objects.Select(o=>Path.GetDirectoryName(o.Script)!).Distinct(StringComparer.OrdinalIgnoreCase).SelectMany(folder=>Directory.GetFiles(folder,"*.nmf")).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();
int readable=0,animated=0;var failures=new List<object>();
foreach(string path in paths)try{var model=new NativeModel(File.ReadAllBytes(path));readable++;if(model.Animated)animated++;}catch(Exception ex){failures.Add(new{File=Path.GetRelativePath(args[0],path).Replace('\\','/'),Error=ex.Message});}
string output=Path.GetFullPath(args[1]);Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.WriteAllText(output,JsonSerializer.Serialize(new{Files=paths.Length,Readable=readable,Animated=animated,Failures=failures},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(new{Files=paths.Length,Readable=readable,Animated=animated,Failures=failures.Count}));
