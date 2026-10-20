using System.Globalization;
using System.Text.RegularExpressions;
namespace Aurora.Core;
public sealed record VehicleLod(string Key,string Model,double? Distance);
public static class VehicleLods
{
 public static VehicleLod[] Read(string script){var fields=VehicleScript.Fields(script);return fields.Where(f=>Regex.IsMatch(f.Key,@"^LOD\d*$")).Select(f=>{string key="LODDISTANCE"+f.Key[3..];string? text=fields.FirstOrDefault(v=>v.Key==key)?.Value;return new VehicleLod(f.Key,f.Value,double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out double d)&&double.IsFinite(d)?d:null);}).ToArray();}
 public static string Write(string script,IEnumerable<VehicleLod> items){var entries=items.ToArray();if(entries.Select(e=>e.Key).Distinct().Count()!=entries.Length||entries.Any(e=>!Regex.IsMatch(e.Key,@"^LOD\d*$")||string.IsNullOrWhiteSpace(e.Model)||e.Distance is double d&&(!double.IsFinite(d)||d<=0)))throw new InvalidDataException("Invalid LOD settings");foreach(var field in VehicleScript.Fields(script).Where(f=>Regex.IsMatch(f.Key,@"^LOD(?:DISTANCE)?\d*$")).Reverse())script=VehicleScript.Set(script,field.Key,field.Occurrence,null);foreach(var entry in entries){script=VehicleScript.Set(script,entry.Key,0,entry.Model);if(entry.Distance is double distance)script=VehicleScript.Set(script,"LODDISTANCE"+entry.Key[3..],0,distance.ToString("G9",CultureInfo.InvariantCulture));}return script;}
}
