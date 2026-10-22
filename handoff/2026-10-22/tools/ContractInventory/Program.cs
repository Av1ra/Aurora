using Aurora.Core;
using System.Text.Json;
if(args.Length!=1)throw new ArgumentException("ContractInventory <output.json>");
var types=VehicleProfiles.All(false).Select(p=>p.Type).Distinct().Order().ToArray();
var value=new {
 Schema=1,
 Note="Compiled Core API snapshot, not proof of in-game acceptance",
 Parameters=TransportParameters.All.Select(p=>new {p.Key,p.Name,p.Group,p.Types,p.Arguments,p.Repeated,Section=TransportParameters.Section(p)}),
 AnimationActions=AnimationBindings.Actions.Select(a=>new {a.Key,a.Label,Attached=AnimationBindings.Attached(a.Key),ApplicableTypes=types.Where(t=>AnimationBindings.Applies(a.Key,t)).ToArray()}),
 ProfilesRu=VehicleProfiles.All(false),ProfilesEn=VehicleProfiles.All(true)
};
File.WriteAllText(Path.GetFullPath(args[0]),JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS contract inventory: {TransportParameters.All.Length} parameters, {AnimationBindings.Actions.Length} actions, {VehicleProfiles.All(false).Length} profiles");
