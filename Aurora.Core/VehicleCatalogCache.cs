using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Aurora.Core;

public sealed record CatalogLoadResult(VehicleCatalog Catalog,bool CacheHit,int SourceFiles,double Milliseconds,double ValidationMilliseconds,bool Persisted);
sealed record CatalogData(Dictionary<int,string> Texts,Dictionary<string,string> ModelNames,VehicleCountry[] Countries,VehicleClass[] Classes,VehicleSound[] Sounds,Dictionary<string,string[]> Values,Dictionary<string,string[]> ClassFields,VehicleObject[] Objects);
public sealed partial class VehicleCatalog
{
 internal CatalogData CacheData()=>new CatalogData(Texts,ModelNames,Countries,Classes,Sounds,Values,ClassFields,Objects);
 internal static VehicleCatalog FromCache(CatalogData d)
 {
  if(d.Texts==null||d.ModelNames==null||d.Countries==null||d.Classes==null||d.Sounds==null||d.Values==null||d.ClassFields==null||d.Objects==null||d.Values.Values.Any(v=>v==null)||d.ClassFields.Values.Any(v=>v==null)||d.Objects.Any(o=>o==null||string.IsNullOrWhiteSpace(o.Script))||d.Classes.Any(c=>c==null||c.Flags==null)||d.Sounds.Any(s=>s==null||s.Files==null)||d.Countries.Any(c=>c==null||c.Currencies==null))throw new InvalidDataException("Invalid catalog cache");
  return new(){Texts=d.Texts,ModelNames=d.ModelNames,Countries=d.Countries,Classes=d.Classes,Sounds=d.Sounds,Values=d.Values,ClassFields=d.ClassFields,Objects=d.Objects};
 }
}

// Cache only derived lookup data. Game resources and project drafts are never rewritten.
public static class VehicleCatalogCache
{
 sealed record Envelope(int Schema,string Module,string Media,bool English,string Fingerprint,CatalogData Data);
 static readonly string module=typeof(VehicleCatalog).Module.ModuleVersionId.ToString();
 static string[] Sources(string media,bool english)
 {
  string[] roots=["vehicles","trains","ships","airplanes","helicopters","cabins"];
  var bases=new[]{media}.Concat(Directory.GetDirectories(media).Where(d=>Path.GetFileName(d).StartsWith("dlc",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(d).StartsWith("elc",StringComparison.OrdinalIgnoreCase)));
  return bases.SelectMany(b=>roots.Select(r=>Path.Combine(b,r))).Where(Directory.Exists).SelectMany(d=>Directory.EnumerateFiles(d,"script.ini",SearchOption.AllDirectories)).Concat(new[]{Path.Combine(media,"sovietEnglish.btf")}.Concat(english?[]:[Path.Combine(media,"sovietRussian.btf")])).Where(File.Exists).Order(StringComparer.OrdinalIgnoreCase).ToArray();
 }
 static string Fingerprint(string media,string[] files)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",files.Select(p=>{var f=new FileInfo(p);return Path.GetRelativePath(media,p)+"|"+f.Length+"|"+f.LastWriteTimeUtc.Ticks;})))));
 public static CatalogLoadResult Load(string media,bool english,string cacheDirectory,bool forceRebuild=false)
 {
  var watch=Stopwatch.StartNew();media=Path.TrimEndingDirectorySeparator(Path.GetFullPath(media));var files=Sources(media,english);string fingerprint=Fingerprint(media,files);double validation=watch.Elapsed.TotalMilliseconds;
  string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(media.ToUpperInvariant()+"|"+english)));
  string file=Path.Combine(cacheDirectory,key+".json");
  try{
   if(!forceRebuild&&File.Exists(file)&&new FileInfo(file).Length<=64L*1024*1024){using var stream=File.OpenRead(file);var e=JsonSerializer.Deserialize<Envelope>(stream);if(e is {Schema:1}&&e.Module==module&&e.English==english&&e.Media.Equals(media,StringComparison.OrdinalIgnoreCase)&&e.Fingerprint==fingerprint)return new(VehicleCatalog.FromCache(e.Data),true,files.Length,watch.Elapsed.TotalMilliseconds,validation,true);}
  }catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or NullReferenceException){/* Rebuild disposable derived data. */}
  var catalog=VehicleCatalog.Load(media,english);bool persisted=false;string temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{
   // Do not publish a snapshot if installed scripts changed during its construction.
   var after=Sources(media,english);if(fingerprint==Fingerprint(media,after)){
    Directory.CreateDirectory(cacheDirectory);using(var stream=File.Create(temporary))JsonSerializer.Serialize(stream,new Envelope(1,module,media,english,fingerprint,catalog.CacheData()));File.Move(temporary,file,true);persisted=true;
   }
  }catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){/* Read-only cache storage must not prevent editing. */}
  finally{try{if(File.Exists(temporary))File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
  return new(catalog,false,files.Length,watch.Elapsed.TotalMilliseconds,validation,persisted);
 }
}
