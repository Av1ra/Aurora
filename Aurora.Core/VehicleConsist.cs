namespace Aurora.Core;
public sealed record TrainUnit(string Reference,bool Reverse=false);
public static class VehicleConsist
{
 public static TrainUnit[] Read(string script)=>VehicleScript.Fields(script).Where(f=>f.Key is "TRAINSET" or "TRAINSET_REVERSE").Select(f=>new TrainUnit(f.Value,f.Key=="TRAINSET_REVERSE")).ToArray();
 public static string Write(string script,IEnumerable<TrainUnit> units)
 {
  var values=units.ToArray();if(values.Any(u=>string.IsNullOrWhiteSpace(u.Reference)||u.Reference.Any(char.IsWhiteSpace)||u.Reference.Contains('"')))throw new InvalidDataException("Invalid train unit reference");if(Read(script).SequenceEqual(values))return script;
  foreach(var field in VehicleScript.Fields(script).Where(f=>f.Key is "TRAINSET" or "TRAINSET_REVERSE").Reverse())script=VehicleScript.RemoveEntry(script,field.Key,field.Occurrence);
  int normal=0,reverse=0;foreach(var unit in values)script=VehicleScript.Set(script,unit.Reverse?"TRAINSET_REVERSE":"TRAINSET",unit.Reverse?reverse++:normal++,unit.Reference);return script;
 }
}
