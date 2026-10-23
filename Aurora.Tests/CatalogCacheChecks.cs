using Aurora.Core;
using System.Text.Json;
static class CatalogCacheChecks
{
 public static void Run(Action<bool,string> check)
 {
  string temp=Path.Combine(Path.GetTempPath(),"Aurora-catalog-"+Guid.NewGuid().ToString("N"));
  string media=Path.Combine(temp,"media"),cache=Path.Combine(temp,"cache"),folder=Path.Combine(media,"vehicles","first");Directory.CreateDirectory(folder);
  try{
   string script=Path.Combine(folder,"script.ini");File.WriteAllText(script,"$TYPE VEHICLETYPE_ROAD\n$NAME_STR First\n$MOVEMENT_SPEED 60\n$END\n");
   var cold=VehicleCatalogCache.Load(media,false,cache);var warm=VehicleCatalogCache.Load(media,false,cache);
   check(!cold.CacheHit&&cold.Persisted&&warm.CacheHit&&JsonSerializer.Serialize(cold.Catalog)==JsonSerializer.Serialize(warm.Catalog),"catalog cache restores every lookup table and object reference without reparsing");
   check(!VehicleCatalogCache.Load(media,false,cache,true).CacheHit,"explicit refresh can rebuild even an otherwise valid cache");
   File.WriteAllText(script,"$TYPE VEHICLETYPE_ROAD\n$NAME_STR Changed\n$MOVEMENT_SPEED 75\n$END\n");var changed=VehicleCatalogCache.Load(media,false,cache);check(!changed.CacheHit&&changed.Catalog.Objects.Single().Name=="Changed","catalog invalidates changed script");
   string second=Path.Combine(media,"dlc_test","ships","second");Directory.CreateDirectory(second);File.WriteAllText(Path.Combine(second,"script.ini"),"$TYPE VEHICLETYPE_SHIP\n$NAME_STR Boat\n$END\n");var added=VehicleCatalogCache.Load(media,false,cache);check(!added.CacheHit&&added.Catalog.Objects.Length==2,"catalog invalidates newly installed DLC transport");
   File.Delete(Path.Combine(second,"script.ini"));var removed=VehicleCatalogCache.Load(media,false,cache);check(!removed.CacheHit&&removed.Catalog.Objects.Length==1,"catalog invalidates removed transport");
   var english=VehicleCatalogCache.Load(media,true,cache);check(!english.CacheHit&&VehicleCatalogCache.Load(media,true,cache).CacheHit&&VehicleCatalogCache.Load(media,false,cache).CacheHit,"catalog languages have independent cache identities");
   foreach(var file in Directory.GetFiles(cache,"*.json"))File.WriteAllText(file,"{broken");check(!VehicleCatalogCache.Load(media,false,cache).CacheHit,"corrupted derived cache rebuilds without blocking editor");
   File.WriteAllText(Path.Combine(media,"sovietEnglish.btf"),"bad table");bool rejected=false;try{_=VehicleCatalogCache.Load(media,false,cache);}catch(InvalidDataException){rejected=true;}check(rejected,"changed localization cannot silently reuse stale catalog");File.Delete(Path.Combine(media,"sovietEnglish.btf"));
   string blocked=Path.Combine(temp,"blocked");File.WriteAllText(blocked,"file");var uncached=VehicleCatalogCache.Load(media,false,blocked);check(!uncached.CacheHit&&!uncached.Persisted&&uncached.Catalog.Objects.Length==1,"unwritable cache falls back to fresh catalog without modifying game");
   check(File.ReadAllText(script).Contains("$MOVEMENT_SPEED 75"),"preparation leaves source scripts intact");
  }finally{Directory.Delete(temp,true);}
 }
}
