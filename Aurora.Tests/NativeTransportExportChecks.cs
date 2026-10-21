using Aurora.Core;
using Aurora.Editor;
using System.Text.RegularExpressions;
static class NativeTransportExportChecks
{
 public static async Task Run(Action<bool,string> check,string? native)
 {
  if(native==null||VehicleAssets.Media(native) is not string media)return;var catalog=VehicleCatalog.Load(media,true);check(catalog.Objects.All(o=>VehicleCatalog.ObjectIdentity(o.Script)==o.Id),"native object identity remains stable across single-project and workspace serialization");string root=Path.Combine(Path.GetTempPath(),"Aurora-native-exports-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   foreach(string type in new[]{"VEHICLETYPE_ROAD","VEHICLETYPE_RAIL_LOCOMOTIVE","VEHICLETYPE_RAIL_VAGON","VEHICLETYPE_SHIP","VEHICLETYPE_AIRPLANE","VEHICLETYPE_HELICOPTER","VEHICLETYPE_CABIN"}){
    bool tested=false;var failures=new List<string>();foreach(var vehicle in catalog.Objects.Where(o=>o.Type==type)){
     string folder=Path.GetDirectoryName(vehicle.Script)!,primary=Path.Combine(folder,"main.nmf"),material=Path.Combine(folder,"material.mtl");if(!File.Exists(primary)||!File.Exists(material))continue;string script=File.ReadAllText(vehicle.Script);var models=new Dictionary<string,NativeModel>();try{models[primary]=new(File.ReadAllBytes(primary));foreach(var t in VehicleScript.Tokens(script).Where(t=>Regex.IsMatch(t.Key,@"^LOD\d*$")&&t.Index==0)){string path=VehicleAssets.Resolve(t.Value,folder,media);models.TryAdd(path,new(File.ReadAllBytes(path)));}}catch(InvalidDataException){continue;}
     var maps=MaterialDocuments.Native(material,models.Values.First());string output=Path.Combine(root,type);Directory.CreateDirectory(output);try{await ModelExport.Export(models,models.Keys.ToDictionary(p=>p,_=>new HashSet<string>()),maps,material,null,null,output,p=>Task.FromResult(DdsImage.Decode(File.ReadAllBytes(p))),script,useSourceScript:false);string result=Directory.GetDirectories(output).Single();string exported=File.ReadAllText(Path.Combine(result,"script.ini"));foreach(var token in VehicleScript.Tokens(exported).Where(t=>VehicleAssets.IsModelReference(t)||Regex.IsMatch(t.Key,@"^LOD\d*$")&&t.Index==0))if(!File.Exists(Path.Combine(result,token.Value)))throw new Exception("Missing export dependency "+token.Value);string mtl=Path.Combine(result,"material.mtl");VehicleAssets.MapMaterial(File.ReadAllText(mtl),r=>{if(!File.Exists(MaterialFiles.Resolve(mtl,r)))throw new Exception("Missing texture "+r);return r;});check(VehicleCatalog.DescribeClass(exported,true).Type==type&&File.Exists(Path.Combine(result,"main.nmf")),"installed native transport export, local models and textures: "+type+" / "+vehicle.Id);tested=true;break;}catch(Exception ex){failures.Add(vehicle.Id+": "+ex.Message);throw new Exception("Native export failure "+string.Join("; ",failures),ex);}
    }check(tested,"native rigid export fixture exists for "+type);
   }
  }finally{Directory.Delete(root,true);}
 }
}
