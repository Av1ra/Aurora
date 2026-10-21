using Aurora.Core;
static class VehicleProfileChecks
{
 public static void Run(Action<bool,string> check)
 {
  string root=Path.Combine(Path.GetTempPath(),"Aurora-donor-free-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   var catalog=VehicleCatalog.Load(root,true);var profiles=VehicleProfiles.All(true);check(catalog.Objects.Length==0&&catalog.Sounds.Length==0&&profiles.Length>=88&&catalog.Classes.Select(c=>c.Type).Distinct().Count()==7,"all seven transport types and native class profiles exist with no donor models, sounds or DLC files");
   var roles=catalog.Classes.SelectMany(c=>c.Flags).Where(f=>f.StartsWith("SKILL_",StringComparison.Ordinal)).Distinct().ToArray();check(roles.Length>=22&&roles.All(r=>VehicleCatalog.Label(r,true)!=r)&&roles.All(r=>VehicleCatalog.Label(r,false)!=r),"all known transport roles remain available and localized without installed examples");
   foreach(var profile in profiles){string script=VehicleCatalog.ApplyClass("$TYPE VEHICLETYPE_ROAD\n$CUSTOM keep\n$END\n",profile);if(VehicleCatalog.ClassId(script)!=profile.Id||!script.Contains("$CUSTOM keep"))throw new Exception("Donor-free profile failed: "+profile.Id);foreach(string flag in profile.Flags){string expected=profile.Defaults?.GetValueOrDefault(flag)??"";if(VehicleScript.Fields(script).Single(f=>f.Key==flag).Value!=expected)throw new Exception("Role arguments changed: "+flag);}}
   check(profiles.Any(p=>p.Defaults?.GetValueOrDefault("SKILL_CONSTRUCTION_RAIL_CRANE")?.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Length==2)&&catalog.Values["RESOURCE_TRANSPORT_TYPE"].Contains("RESOURCE_TRANSPORT_CEMENT"),"donor-free presets retain multiple rail-crane arguments and transport cargo categories");
   string folder=Path.Combine(root,"vehicles","custom");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"script.ini"),"$TYPE VEHICLETYPE_ROAD\n$SKILL_FIRETRUCK 123\n$END\n");var withDonor=VehicleCatalog.Load(root,true);var selected=withDonor.Classes.Single(c=>c.Id==VehicleCatalog.ClassId("$TYPE VEHICLETYPE_ROAD\n$SKILL_FIRETRUCK 123\n$END\n"));check(selected.Defaults!["SKILL_FIRETRUCK"]=="123","installed role profile takes priority over embedded starting values");
  }finally{Directory.Delete(root,true);}
 }
}
