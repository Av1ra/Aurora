namespace Aurora.Core;
public static class VehicleName
{
 public static string Write(string script,bool localized,string value)
 {
  value=value.Trim();
  if(localized){if(!int.TryParse(value,out int id)||id<0)throw new InvalidDataException("Invalid localization ID");script=VehicleScript.Set(script,"NAME_STR",0,null);return VehicleScript.Set(script,"NAME",0,id.ToString(System.Globalization.CultureInfo.InvariantCulture));}
  if(value.Length is <1 or >256||value.Any(char.IsControl))throw new InvalidDataException("Invalid vehicle name");
  script=VehicleScript.Set(script,"NAME",0,null);return VehicleScript.Set(script,"NAME_STR",0,"\""+value.Replace('"','\'')+"\"");
 }
}
