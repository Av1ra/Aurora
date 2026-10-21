using System.Text.Json;
namespace Aurora.Core;

public static class VehicleProfiles
{
 sealed record Template(string Type,string? Transport,string[] Flags,Dictionary<string,string> Defaults);
 static readonly Lazy<Template[]> templates=new(()=>{
  using var stream=typeof(VehicleProfiles).Assembly.GetManifestResourceStream("Aurora.Core.TransportProfiles.json")??throw new InvalidDataException("Transport profiles missing");return JsonSerializer.Deserialize<Template[]>(stream)??throw new InvalidDataException("Invalid transport profiles");
 });
 public static VehicleClass[] All(bool english)=>templates.Value.Select(t=>{
  string script="$TYPE "+t.Type+"\n"+(string.IsNullOrEmpty(t.Transport)?"":"$RESOURCE_TRANSPORT_TYPE "+t.Transport+"\n")+string.Concat(t.Flags.Select(f=>"$"+f+" "+t.Defaults.GetValueOrDefault(f,"")+"\n"))+"$END\n";return VehicleCatalog.DescribeClass(script,english);
 }).ToArray();
}
