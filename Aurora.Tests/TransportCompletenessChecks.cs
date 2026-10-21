using Aurora.Core;
using System.Numerics;
static class TransportCompletenessChecks
{
 public static void Run(Action<bool,string> check,Action<Action,string> reject,string? native=null)
 {
  foreach(string end in new[]{"end","$END","$end # stop","END // stop"}){
   string script="$TYPE VEHICLETYPE_ROAD\r\n$MOVEMENT_SPEED\r\n60 // retain\r\n$UNKNOWN\r\nopaque body\r\n"+end+"\r\n$MOVEMENT_SPEED 999\r\n";
   check(VehicleScript.Fields(script).Count(f=>f.Key=="MOVEMENT_SPEED")==1&&!VehicleScript.Fields(script).Any(f=>f.Key=="END"),"transport terminator excludes trailing fields: "+end);
   string edited=VehicleScript.Set(script,"FAMILY",0,"base_vehicle");
   check(edited.IndexOf("$FAMILY",StringComparison.Ordinal)<edited.IndexOf(end,StringComparison.Ordinal)&&edited.EndsWith("$MOVEMENT_SPEED 999\r\n",StringComparison.Ordinal)&&edited.Contains("$UNKNOWN\r\nopaque body\r\n"),"insertion preserves unknown body and post-terminator bytes: "+end);
   check(VehicleScript.Set(script,"MOVEMENT_SPEED",0,"70").Contains("70 // retain"),"multiline value preserves its comment: "+end);
   check(VehicleScript.Fields(VehicleLods.Write(script,[new("LOD1","lod.nmf",150)])).Any(f=>f.Key=="LOD1"),"LOD written before native terminator: "+end);
  }
  reject(()=>VehicleScript.Set("$END\n","END",0,""),"terminator is not an editable setting");
  string parts="$TYPE VEHICLETYPE_ROAD\n$DUMPER_DECK_PIVOT\n// deck\nbody\n0 1 2\n$MIXER_DRUM body 1 2 3 4 5 6 // keep\n$UNKNOWN body\n$END\n";
  string renamed=VehicleScript.RenamePart(parts,"body","deck");check(renamed.Contains("// deck\ndeck\n0 1 2")&&renamed.Contains("$MIXER_DRUM deck 1 2 3 4 5 6 // keep")&&renamed.Contains("$UNKNOWN body"),"mesh rename updates inline and multiline mechanism names without changing opaque fields");
  string resources="$TYPE VEHICLETYPE_ROAD\n$RESOURCE_ALLOW_ONLY wood // first\n$RESOURCE_ALLOW_ONLY boards\n$UNKNOWN opaque\n$END\n";
  check(VehicleScript.SetValues(resources,"RESOURCE_ALLOW_ONLY",["wood","boards"])==resources,"unchanged multiple resources retain exact script bytes");
  string resourcesChanged=VehicleScript.SetValues(resources,"RESOURCE_ALLOW_ONLY",["steel","bricks","wood"]);check(VehicleScript.Fields(resourcesChanged).Where(f=>f.Key=="RESOURCE_ALLOW_ONLY").Select(f=>f.Value).SequenceEqual(new[]{"steel","bricks","wood"})&&resourcesChanged.Contains("$UNKNOWN opaque"),"resource list can grow without losing unrelated fields");
  check(!VehicleScript.Fields(VehicleScript.SetValues(resourcesChanged,"RESOURCE_ALLOW_ONLY",[])).Any(f=>f.Key=="RESOURCE_ALLOW_ONLY"),"allow any resource removes every previous restriction");
  string purchase="$TYPE VEHICLETYPE_RAIL_LOCOMOTIVE\n$COUNTRY_STR \"Austria\"\n$AVAILABLE 1900 3000\n$COST_USD 1\n$END\n";
  string purchaseChanged=VehicleCatalog.ApplyPurchase(purchase,"USD",-1,1900,3000,1,"Austria");check(VehicleScript.Fields(purchaseChanged).Single(f=>f.Key=="COUNTRY_STR").Value=="\"Austria\""&&!VehicleScript.Fields(purchaseChanged).Any(f=>f.Key=="COUNTRY")&&purchaseChanged.Contains("$AVAILABLE 1900 3000"),"textual country and unlimited availability survive purchase edits");
  reject(()=>VehicleCatalog.ApplyPurchase(purchase,"USD",39018,1900,2400,1),"new arbitrary out-of-range availability is rejected");
  check(VehicleCatalog.ApplyPurchase(purchase.Replace("3000","2400"),"USD",39018,1900,2400,1).Contains("$AVAILABLE 1900 2400"),"existing native expiry beyond UI range can be retained");
  check(VehicleCatalog.ApplyPurchase(purchase.Replace("1900 3000","1850 1880"),"USD",39018,1850,1880,1).Contains("$AVAILABLE 1850 1880"),"unchanged early-start native years survive without extending manual input range");
  string lamps="$TYPE VEHICLETYPE_ROAD\n$LIGHT_FORWARD 1 2 3\n$END\n";
  check(VehicleLights.Read(VehicleLights.Transform(lamps,Matrix4x4.CreateTranslation(1,0,0)))[0].Position==new Vector3(2,2,3),"pivot accepts dollar terminator without treating it as unknown section");
  string root=Path.Combine(Path.GetTempPath(),"Aurora-cabins-"+Guid.NewGuid().ToString("N"));
  try{
   foreach(string kind in new[]{"LIGHT","HEAVY"}){string folder=Path.Combine(root,"cabins",kind);Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"script.ini"),"$TYPE VEHICLETYPE_CABIN\n$CABIN_"+kind+"\n$RESOURCE_TRANSPORT_TYPE RESOURCE_TRANSPORT_PASSANGER\n$END\n");}
   var catalog=VehicleCatalog.Load(root,false);check(catalog.Classes.Length==2&&catalog.Classes.Select(c=>c.Id).Distinct().Count()==2,"native cabins are scanned and light/heavy identities remain distinct");
   string source="$TYPE VEHICLETYPE_CABIN\n$CABIN_LIGHT\n$UNKNOWN keep\n$END\n";string changed=VehicleCatalog.ApplyClass(source,catalog.Classes.Single(c=>c.Flags.Contains("CABIN_HEAVY")));
   check(changed.Contains("$CABIN_HEAVY")&&!changed.Contains("$CABIN_LIGHT")&&changed.Contains("$UNKNOWN keep"),"cabin subtype change replaces exclusive flag and retains unrelated fields");
  }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
  if(native!=null){string? media=Path.GetFullPath(native);while(media!=null&&Path.GetFileName(media)!="media_soviet")media=Path.GetDirectoryName(media);if(media!=null){
   string[] roots=["vehicles","trains","ships","airplanes","helicopters","cabins"];var bases=new[]{media}.Concat(Directory.GetDirectories(media).Where(d=>Path.GetFileName(d).StartsWith("dlc",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(d).StartsWith("elc",StringComparison.OrdinalIgnoreCase)));int count=0;var failures=new List<string>();
   foreach(string folder in bases.SelectMany(b=>roots.Select(r=>Path.Combine(b,r))).Where(Directory.Exists))foreach(string file in Directory.EnumerateFiles(folder,"script.ini",SearchOption.AllDirectories))try{
    string source=File.ReadAllText(file);var fields=VehicleScript.Fields(source);if(!fields.Any(f=>f.Key=="TYPE"&&f.Value.StartsWith("VEHICLETYPE_",StringComparison.Ordinal)))continue;
    foreach(var field in fields)if(VehicleScript.Set(source,field.Key,field.Occurrence,field.Value)!=source)throw new Exception("unchanged field changed bytes: "+field.Key);
    string added=VehicleScript.Set(source,"AURORA_TEST_INSERT",0,"1");if(!VehicleScript.Fields(added).Any(f=>f.Key=="AURORA_TEST_INSERT"&&f.Value=="1"))throw new Exception("inserted field is outside terminator");
    if(VehicleScript.SetValues(source,"RESOURCE_ALLOW_ONLY",fields.Where(f=>f.Key=="RESOURCE_ALLOW_ONLY").Select(f=>f.Value))!=source)throw new Exception("unchanged restrictions changed bytes");count++;
   }catch(Exception ex){failures.Add(Path.GetRelativePath(media,file)+": "+ex.Message);}
   check(count>1000&&failures.Count==0,"native transport scripts preserve unchanged fields and accept pre-terminator insertion: "+count+" files; "+string.Join("; ",failures.Take(3)));
  }}
 }
}
