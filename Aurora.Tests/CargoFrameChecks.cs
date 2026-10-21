using Aurora.Core;
using System.Numerics;
static class CargoFrameChecks
{
 public static void Run(Action<bool,string> check,Action<Action,string> reject)
 {
  string script="$TYPE VEHICLETYPE_ROAD\n$END\n";var cargo=new CargoLayout(new(.1f,.2f,.3f),new(-1,-4,2),new(.1f),new(1,-2,3),new(2));
  foreach(string key in new[]{"RESOURCE_VISUALIZATION","RESOURCE_VISUALIZATION_JOINT","RESOURCE_VISUALIZATION_BONE"})script=VehicleScript.SetCargo(script,cargo,0,key,key.EndsWith("BONE")?"container":null);
  foreach(string key in new[]{"RESOURCE_VISUALIZATION","RESOURCE_VISUALIZATION_JOINT","RESOURCE_VISUALIZATION_BONE"})check(VehicleScript.Cargo(script,0,key)==cargo&&VehicleScript.SetCargo(script,cargo,0,key,key.EndsWith("BONE")?"container":null)==script,"independent cargo frame preserves negative coordinates and exact unchanged bytes: "+key);
  var moved=cargo with{FullPosition=new(4,5,6)};string changed=VehicleScript.SetCargo(script,moved,0,"RESOURCE_VISUALIZATION_JOINT");check(VehicleScript.Cargo(changed)==cargo&&VehicleScript.Cargo(changed,0,"RESOURCE_VISUALIZATION_JOINT")==moved&&VehicleScript.Fields(changed).Single(f=>f.Key=="RESOURCE_VISUALIZATION_BONE").Value=="container","joint cargo edits do not move body cargo or replace bone binding");
  var box=new CargoBox(new(-1,-5,4),new(1,-5,-4));string withBox=VehicleCargoBox.Write(script,"CARGOVEHICLE_VISUALIZATION_JOINT",0,box);check(VehicleCargoBox.Read(withBox,"CARGOVEHICLE_VISUALIZATION_JOINT")==box,"vehicle platform preserves reversed Z corners and negative height");var scaled=box.MoveScale(new(2,3,4),new(4,0,8));check(scaled.Centre==new Vector3(2,3,4)&&scaled.Size==new Vector3(4,0,8)&&scaled.Min.Z>scaled.Max.Z,"platform move and scale retain original axis direction and planar height");reject(()=>VehicleCargoBox.Write(script,"UNKNOWN",0,box),"unsupported cargo box key");
  string inline="$RESOURCE_VISUALIZATION rotation 0 0 0 min 0 0 0 1 1 1 max 0 2 0 2 2 2 // keep\n$END\n";var read=VehicleScript.Cargo(inline)!;check(VehicleScript.SetCargo(inline,read with{FullPosition=new(1,2,3)}).Contains("max 1 2 3 2 2 2 // keep"),"cargo edits preserve inline native layout and comments");
 }
}
