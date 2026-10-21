using Aurora.Core;
using Aurora.Editor;
using System.Text;
using System.Text.Json;
static class TransportAuthoringChecks
{
 public static async Task Run(Action<bool,string> check)
 {
  string root=Path.Combine(Path.GetTempPath(),"Aurora-authoring-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   var model=ObjImporter.Parse("o body\nv -1 0 -2\nv 1 0 -2\nv 0 2 2\nvt 0 0\nvt 1 0\nvt .5 1\nusemtl Paint\nf 1/1 2/2 3/3\n").Model;var objects=new Dictionary<string,Dictionary<string,byte[]>>();var types=new Dictionary<string,string>();
   foreach(string type in VehicleProfiles.All(true).Select(p=>p.Type).Distinct()){
    var profiles=VehicleProfiles.All(true).Where(p=>p.Type==type).ToArray();var profile=profiles.FirstOrDefault(p=>p.Flags.SequenceEqual(type=="VEHICLETYPE_CABIN"?new[]{"CABIN_LIGHT"}:type=="VEHICLETYPE_RAIL_LOCOMOTIVE"?new[]{"TRAINGROUP_LOCOMOTIVE"}:type=="VEHICLETYPE_RAIL_VAGON"?new[]{"TRAINGROUP_VAGON"}:Array.Empty<string>()))??profiles[0];string name="unit_"+objects.Count;types[name]=type;
    string script=VehicleCatalog.ApplyClass("$TYPE VEHICLETYPE_ROAD\n$NAME_STR \"Authored vehicle\"\n$CUSTOM keep\n$END\n",profile);script=VehicleCatalog.ApplyPurchase(script,"RUB",39011,1955,1980,2000);script=VehicleScript.Set(script,"MOVEMENT_EMPTY_WEIGHT",0,"10");if(type!="VEHICLETYPE_CABIN"){script=VehicleScript.Set(script,"MOVEMENT_SPEED",0,"60");script=VehicleScript.Set(script,"MOVEMENT_POWER_KW",0,"100");}script=VehicleScript.Set(script,"LOD1",0,"models/lod.nmf");script=VehicleScript.Set(script,"LODDISTANCE1",0,"150");
    if(type=="VEHICLETYPE_ROAD")script=VehicleScript.SetArguments(script,"JOINT",0,["assets/part.nmf","0.5","0"]);
    if(type=="VEHICLETYPE_CABIN"){script=VehicleScript.SetArguments(script,"CRANE_STATIC",0,["assets/part.nmf"]);script=VehicleScript.SetArguments(script,"CRANE_DYNAMIC",0,["assets/part.nmf","0","0","0"]);}
    if(type=="VEHICLETYPE_HELICOPTER"){script=VehicleScript.SetArguments(script,"PROPELER_HELICOPTER",0,["1","assets/part.nmf","assets/part.nmf","assets/part.nmf","assets/part.mtl"]);script=VehicleScript.SetArguments(script,"PROPELER_HELICOPTER_POINT_DIR",0,["0","2","0","0","0","0"]);}
    if(type=="VEHICLETYPE_AIRPLANE")script=VehicleScript.Set(script,"TAKEOFF_DISTANCE",0,"700");
    if(type=="VEHICLETYPE_SHIP")script=VehicleScript.SetArguments(script,"WAVEPOINTFRONT",0,["position","0","0","2","coordstep","0.001","bumpfactor","1","srcsize","1","dstsize","10","maxtime","10"]);
    var document=new EditorProject(1,[new("main.nmf","models/main.nmf",[]),new("lod.nmf","models/lod.nmf",[])],[new("material","native/material.mtl",new(){{"Paint",new(Base:"assets/paint.dds",Normal:"assets/normal.dds",Roughness:.4f)}})],0,0,null,null,script,[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1],Previews:new(Automatic:false));
    objects[name]=new(){{"document.json",JsonSerializer.SerializeToUtf8Bytes(document)},{"models/main.nmf",model.Data},{"models/lod.nmf",model.Data},{"native/material.mtl",Encoding.UTF8.GetBytes(NativeMaterialText.Create(["Paint"],"../assets/paint.dds","../assets/paint.dds","../assets/normal.dds"))},{"assets/part.nmf",model.Data},{"assets/part.mtl",Encoding.UTF8.GetBytes(NativeMaterialText.Create(["Paint"],"paint.dds","paint.dds","normal.dds"))},{"assets/paint.dds",DdsImage.Encode(new(1,1,[140,180,220,255]))},{"assets/normal.dds",DdsImage.Encode(new(1,1,[128,128,255,255]))}};
   }
   string archive=Path.Combine(root,"all-transport.aur");AuroraProjectArchive.Write(archive,new TransportWorkspace(objects.Keys.First(),objects).Pack());objects.Clear();var restored=TransportWorkspace.Unpack(AuroraProjectArchive.Read(archive));
   foreach(var entry in restored.Objects){var state=ProjectData.Load(entry.Value,p=>Path.Combine(root,"restored",entry.Key,p));string destination=Path.Combine(root,"export",entry.Key);Directory.CreateDirectory(destination);string output=await VariantExport.Write(state.Variants,state.Models,state.Glass,state.Mask,state.Document.MaskMaterial,state.Vehicle,destination,p=>Task.FromResult(DdsImage.Decode(File.ReadAllBytes(p))));string script=File.ReadAllText(Path.Combine(output,"script.ini"));check(VehicleScript.Fields(script).Single(f=>f.Key=="TYPE").Value==types[entry.Key]&&script.Contains("$CUSTOM keep")&&script.Contains("$AVAILABLE 1955 1980")&&File.Exists(Path.Combine(output,"main.nmf"))&&File.Exists(Path.Combine(output,"lod.nmf"))&&File.Exists(Path.Combine(output,"bbox.bin")),"OBJ authoring, one portable project and complete local export without a game donor: "+types[entry.Key]);foreach(var t in VehicleScript.Tokens(script).Where(t=>VehicleAssets.IsModelReference(t)||t.Key=="LOD1"&&t.Index==0))if(Path.IsPathRooted(t.Value)||!File.Exists(Path.Combine(output,t.Value)))throw new Exception("Nonportable authored dependency: "+t.Value);foreach(string material in Directory.EnumerateFiles(output,"*.mtl"))VehicleAssets.MapMaterial(File.ReadAllText(material),r=>{if(!File.Exists(MaterialFiles.Resolve(material,r)))throw new Exception("Authored texture missing: "+r);return r;});}
  }finally{Directory.Delete(root,true);}
 }
}
