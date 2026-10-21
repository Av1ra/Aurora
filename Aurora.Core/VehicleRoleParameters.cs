namespace Aurora.Core;

public static class VehicleRoleParameters
{
 // These are role switches, not editable efficiency coefficients. Their raw values stay in script.ini.
 public static bool FlagOnly(string key)=>key is "SKILL_PERSONAL" or "SKILL_FORKLIFT" or "SKILL_POLICECAR" or "SKILL_POLICEBUS";
 public static string Label(string key,int argument,int count)=>key switch{
  "SKILL_CONSTRUCTION_RAIL_CRANE" when argument==0=>"Производительность",
  "SKILL_CONSTRUCTION_RAIL_CRANE" when argument==1=>"Рабочие",
  "SKILL_CONSTRUCTION_RAIL_TRACK_LAYER" when argument==0=>"Производительность",
  "SKILL_CONSTRUCTION_RAIL_TRACK_LAYER" when argument==1=>"Рабочие",
  "SKILL_CONSTRUCTION_RAIL_TRACK_LAYER" when argument==2=>"Коэффициент скорости",
  "SKILL_SNOWPLOW" when argument==0=>"Скорость уборки снега",
  "SKILL_CONSTRUCTION_RAIL_TUNNEL_BORING" when argument==0=>"Производительность",
  _=>count>1?"Параметр "+(argument+1):""
 };
}
