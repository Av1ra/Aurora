using Aurora.Core;
using System.Diagnostics;
using System.Text.Json;
static class CatalogBenchmark
{
 public static void Run(string media,string cache)
 {
  var fresh=VehicleCatalogCache.Load(media,false,cache,true);var baseline=new List<double>();var cached=new List<double>();CatalogLoadResult? last=null;
  for(int i=0;i<5;i++){var watch=Stopwatch.StartNew();_=VehicleCatalog.Load(media,false);baseline.Add(watch.Elapsed.TotalMilliseconds);last=VehicleCatalogCache.Load(media,false,cache);cached.Add(last.Milliseconds);}
  double Median(List<double> values)=>values.Order().ElementAt(values.Count/2);
  Console.WriteLine(JsonSerializer.Serialize(new{Objects=last!.Catalog.Objects.Length,last.SourceFiles,FreshMs=fresh.Milliseconds,RawSamplesMs=baseline,CacheSamplesMs=cached,RawMedianMs=Median(baseline),CacheMedianMs=Median(cached),last.CacheHit,last.ValidationMilliseconds}));
 }
}
