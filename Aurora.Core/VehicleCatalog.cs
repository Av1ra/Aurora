using System.Buffers.Binary;
using System.Globalization;
using System.Text;
namespace Aurora.Core;

public sealed record VehicleCountry(int Id,string Name,string[] Currencies);
public sealed record VehicleClass(string Id,string Type,string? Transport,string[] Flags,string Name,Dictionary<string,string>? Defaults=null);
public sealed record VehicleSound(string Name,string ClassId,string[] Files);
public sealed record CargoResource(string File,string Name);
public sealed class VehicleCatalog
{
 public Dictionary<int,string> Texts {get;private set;}=[];
 public Dictionary<string,string> ModelNames {get;private set;}=[];
 public VehicleCountry[] Countries {get;private set;}=[];
 public VehicleClass[] Classes {get;private set;}=[];
 public VehicleSound[] Sounds {get;private set;}=[];
 public Dictionary<string,string[]> Values {get;private set;}=[];
 public Dictionary<string,string[]> ClassFields {get;private set;}=[];
 public static Dictionary<int,string> ReadTexts(string file)
 {
  if(!File.Exists(file))return [];byte[] data=File.ReadAllBytes(file);if(data.Length<12)throw new InvalidDataException("Invalid game text table");int count=BinaryPrimitives.ReadInt32BigEndian(data);long start=12L+count*10L;if(count<0||start>data.Length)throw new InvalidDataException("Invalid game text table");var result=new Dictionary<int,string>();
  for(int i=0;i<count;i++){var row=data.AsSpan(12+i*10,10);int id=BinaryPrimitives.ReadInt32BigEndian(row),offset=BinaryPrimitives.ReadInt32BigEndian(row[4..]);int length=BinaryPrimitives.ReadUInt16BigEndian(row[8..]);long at=start+offset*2L;if(offset<0||at+length*2L>data.Length)throw new InvalidDataException("Invalid game text entry");result[id]=Encoding.BigEndianUnicode.GetString(data,(int)at,length*2).TrimEnd('\0');}return result;
 }
 static string Value(VehicleField[] fields,string key)=>fields.FirstOrDefault(f=>f.Key==key)?.Value??"";
 static string[] ClassFlags(VehicleField[] fields)=>fields.Where(f=>f.Key.StartsWith("SKILL_",StringComparison.Ordinal)||f.Key.StartsWith("TRAINGROUP_",StringComparison.Ordinal)).Select(f=>f.Key).Order().ToArray();
 public static string ClassId(string script){var f=VehicleScript.Fields(script);return string.Join("|",new[]{Value(f,"TYPE"),Value(f,"RESOURCE_TRANSPORT_TYPE")}.Concat(ClassFlags(f)));}
 public static string Label(string key,bool english)=>labels.TryGetValue(key,out var text)?(english?text.En:text.Ru):key;
 static string ClassName(VehicleField[] f,bool en)
 {
  string type=Value(f,"TYPE"),transport=Value(f,"RESOURCE_TRANSPORT_TYPE");var flags=ClassFlags(f);string? special=flags.FirstOrDefault(k=>k.StartsWith("SKILL_",StringComparison.Ordinal));string? train=flags.FirstOrDefault(k=>k.StartsWith("TRAINGROUP_",StringComparison.Ordinal));
  string name=special!=null?Label(special,en):train!=null?Label(train,en):type=="VEHICLETYPE_ROAD"&&transport=="RESOURCE_TRANSPORT_PASSANGER"?(en?"Bus":"Автобус"):Label(type,en);
  if(transport.Length>0&&transport!="RESOURCE_TRANSPORT_PASSANGER")name+=" / "+Label(transport,en);return name;
 }
 public static VehicleClass DescribeClass(string script,bool english){var f=VehicleScript.Fields(script);return new(ClassId(script),Value(f,"TYPE"),Value(f,"RESOURCE_TRANSPORT_TYPE"),ClassFlags(f),ClassName(f,english),f.Where(x=>ClassFlags(f).Contains(x.Key)).ToDictionary(x=>x.Key,x=>x.Value));}
 public static VehicleCatalog Load(string media,bool english)
 {
  var englishTexts=ReadTexts(Path.Combine(media,"sovietEnglish.btf"));var texts=new Dictionary<int,string>(englishTexts);if(!english)foreach(var p in ReadTexts(Path.Combine(media,"sovietRussian.btf")))texts[p.Key]=p.Value;
  var modelNames=new Dictionary<string,string>();var classes=new Dictionary<string,VehicleClass>();var sounds=new Dictionary<string,VehicleSound>();var values=new Dictionary<string,HashSet<string>>();var classFields=new Dictionary<string,HashSet<string>>();
  string[] roots=["vehicles","trains","ships","airplanes","helicopters"];
  var bases=new[]{media}.Concat(Directory.GetDirectories(media).Where(d=>Path.GetFileName(d).StartsWith("dlc",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(d).StartsWith("elc",StringComparison.OrdinalIgnoreCase)));
  foreach(string folder in bases.SelectMany(b=>roots.Select(r=>Path.Combine(b,r))).Where(Directory.Exists))foreach(string path in Directory.EnumerateFiles(folder,"script.ini",SearchOption.AllDirectories)){
   string script=File.ReadAllText(path);var f=VehicleScript.Fields(script);string type=Value(f,"TYPE");if(type.Length==0)continue;string id=ClassId(script);
   if(!classFields.TryGetValue(id,out var keys))classFields[id]=keys=[];foreach(var field in f)keys.Add(field.Key);
   classes.TryAdd(id,new(id,type,Value(f,"RESOURCE_TRANSPORT_TYPE"),ClassFlags(f),ClassName(f,english),f.Where(x=>ClassFlags(f).Contains(x.Key)).ToDictionary(x=>x.Key,x=>x.Value)));
   foreach(var field in f){if(!values.TryGetValue(field.Key,out var set))values[field.Key]=set=[];if(field.Value.Length>0)set.Add(field.Value);}
   string modelFolder=Path.GetFileName(Path.GetDirectoryName(path))!;string modelName=Value(f,"NAME_STR").Trim('"');if(modelName.Length==0&&int.TryParse(Value(f,"NAME"),out int modelId))modelName=texts.GetValueOrDefault(modelId,modelFolder);modelNames.TryAdd(modelFolder,modelName.Length>0?modelName:modelFolder);
   var files=f.Where(x=>x.Key=="SOUND_PARAMS").Select(x=>x.Value).ToArray();if(files.Length>0){string name=Value(f,"NAME_STR").Trim('"');if(name.Length==0&&int.TryParse(Value(f,"NAME"),out int nameId))name=texts.GetValueOrDefault(nameId,"");if(name.Length==0)name=Path.GetFileName(Path.GetDirectoryName(path))!;string signature=id+"\n"+string.Join("\n",files);sounds.TryAdd(signature,new(ClassName(f,english)+" / "+name,id,files));}
  }
  var entries=texts.Where(p=>p.Key is >=38998 and <=39044&&englishTexts.ContainsKey(p.Key)).Select(p=>new VehicleCountry(p.Key,p.Value,CountryCurrencies(p.Key))).ToArray();
  return new(){Texts=texts,ModelNames=modelNames,Countries=entries,Classes=classes.Values.OrderBy(c=>c.Name).ToArray(),Sounds=sounds.Values.OrderBy(s=>s.Name).ToArray(),Values=values.ToDictionary(p=>p.Key,p=>p.Value.Order().ToArray()),ClassFields=classFields.ToDictionary(p=>p.Key,p=>p.Value.Order().ToArray())};
 }
 // Bloc is an explicit country classification, not inferred from a vehicle's price currency.
 public static string[] CountryCurrencies(int id)=>id is 38998 or 38999?["RUB","USD"]:new[]{39000,39004,39006,39007,39008,39009,39010,39011,39012,39013,39014,39015,39019,39020,39021,39025,39026,39027,39028,39029,39042}.Contains(id)?["RUB"]:["USD"];
 public static string ApplyClass(string script,VehicleClass selected)
 {
  if(ClassId(script)==selected.Id)return script;var fields=VehicleScript.Fields(script);foreach(var field in fields.Where(f=>f.Key.StartsWith("SKILL_",StringComparison.Ordinal)||f.Key.StartsWith("TRAINGROUP_",StringComparison.Ordinal)).Reverse())script=VehicleScript.Set(script,field.Key,field.Occurrence,null);
  script=VehicleScript.Set(script,"TYPE",0,selected.Type);script=VehicleScript.Set(script,"RESOURCE_TRANSPORT_TYPE",0,string.IsNullOrEmpty(selected.Transport)?null:selected.Transport);foreach(string flag in selected.Flags)script=VehicleScript.Set(script,flag,0,fields.FirstOrDefault(f=>f.Key==flag)?.Value??selected.Defaults?.GetValueOrDefault(flag)??"");return script;
 }
 public static string ApplyPurchase(string script,string currency,int country,int first,int last,double price)
 {
  if(currency is not ("RUB" or "USD")||first<1900||last>2100||first>last||price<=0||!double.IsFinite(price))throw new InvalidDataException("Invalid purchase settings");
  script=VehicleScript.Set(script,currency=="RUB"?"COST_USD":"COST_RUB",0,null);script=VehicleScript.Set(script,"COUNTRY_STR",0,null);script=VehicleScript.Set(script,"COUNTRY",0,country.ToString(CultureInfo.InvariantCulture));script=VehicleScript.Set(script,"AVAILABLE",0,$"{first} {last}");return VehicleScript.Set(script,"COST_"+currency,0,price.ToString("G12",CultureInfo.InvariantCulture));
 }
 public static string ApplySounds(string script,VehicleSound sound)
 {
  foreach(var f in VehicleScript.Fields(script).Where(f=>f.Key is "SOUND_PARAMS" or "SOUND_PARAMS_WORKSHOP").Reverse())script=VehicleScript.Set(script,f.Key,f.Occurrence,null);for(int i=0;i<sound.Files.Length;i++)script=VehicleScript.Set(script,"SOUND_PARAMS",i,sound.Files[i]);return script;
 }
 public static string[] ResourceOptions(string transport)=>transport switch{
  "RESOURCE_TRANSPORT_COVERED"=>["crops","food","alcohol","fabric","clothes","chemicals","plastics","eletronics","mechanicalcomponents","electrocomponents","yellowcake","uf6","nuclearfuel","nuclearwaste"],
  "RESOURCE_TRANSPORT_OPEN"=>["wood","boards","bricks","steel","prefabpanels","aluminium","vehicles","cargovehicles"],
  "RESOURCE_TRANSPORT_GENERAL"=>ResourceOptions("RESOURCE_TRANSPORT_COVERED").Concat(ResourceOptions("RESOURCE_TRANSPORT_OPEN")).Append("meat").ToArray(),
  "RESOURCE_TRANSPORT_GRAVEL"=>["gravel","rawgravel","coal","rawcoal","iron","rawiron","bauxite","rawbauxite","uranium"],
  "RESOURCE_TRANSPORT_OIL"=>["oil","fuel","bitumen"],"RESOURCE_TRANSPORT_COOLER"=>["meat"],"RESOURCE_TRANSPORT_LIVESTOCK"=>["livestock"],"RESOURCE_TRANSPORT_CEMENT"=>["cement"],"RESOURCE_TRANSPORT_CONCRETE"=>["concrete"],"RESOURCE_TRANSPORT_WATER"=>["water"],"RESOURCE_TRANSPORT_SEWAGE"=>["sewage"],"RESOURCE_TRANSPORT_WASTE"=>["waste","waste_gravel","waste_steel","waste_aluminium","waste_plastic"],_=>[]};
 public static CargoResource[] CargoResources(string transport,string? only,bool english)
 {
  var names=transport switch{
   "RESOURCE_TRANSPORT_GRAVEL"=>new[]{"gravel_vehicle","rawgravel_vehicle","coal_vehicle","rawcoal_vehicle","iron_vehicle","rawiron_vehicle","bauxite_vehicle","rawbauxite_vehicle","uranium_vehicle"},
   "RESOURCE_TRANSPORT_COVERED"=>["generalcargo_vehicle","yellowcake"],
   "RESOURCE_TRANSPORT_GENERAL"=>["generalcargo_vehicle","boards","bricks","steel","prefabpanels","wood","aluminium"],
   "RESOURCE_TRANSPORT_OPEN"=>["boards","bricks","steel","prefabpanels","wood","aluminium"],
   "RESOURCE_TRANSPORT_LIVESTOCK"=>["livestock_vehicle"],
   "RESOURCE_TRANSPORT_WASTE"=>["waste1","waste_gravel_vehicle","waste_steel_vehicle","waste_aluminium_vehicle","waste_plastic"],_=>Array.Empty<string>()};
  if(!string.IsNullOrEmpty(only)&&only!="*"){string resource=only switch{"cargovehicles"=>"vehicles","eletronics"=>"electronics",_=>only};if(names.Contains(resource))names=[resource];else if(names.Contains(resource+"_vehicle"))names=[resource+"_vehicle"];else if(resource=="waste"&&transport=="RESOURCE_TRANSPORT_WASTE")names=["waste1"];else if(transport=="RESOURCE_TRANSPORT_COVERED"||transport=="RESOURCE_TRANSPORT_GENERAL"&&(ResourceOptions("RESOURCE_TRANSPORT_COVERED").Contains(resource)||resource is "meat" or "electronics"))names=["generalcargo_vehicle"];else names=[];}
  return names.Select(n=>new CargoResource(n+".nmf",Label(n,english))).ToArray();
 }
 static readonly Dictionary<string,(string Ru,string En)> labels=new()
 {
  ["VEHICLETYPE_ROAD"]=("Грузовик","Truck"),["VEHICLETYPE_RAIL_LOCOMOTIVE"]=("Локомотив","Locomotive"),["VEHICLETYPE_RAIL_VAGON"]=("Вагон","Wagon"),["VEHICLETYPE_SHIP"]=("Судно","Ship"),["VEHICLETYPE_AIRPLANE"]=("Самолёт","Airplane"),["VEHICLETYPE_HELICOPTER"]=("Вертолёт","Helicopter"),
  ["RESOURCE_TRANSPORT_PASSANGER"]=("Пассажиры","Passengers"),["RESOURCE_TRANSPORT_COVERED"]=("Крытый кузов","Covered cargo"),["RESOURCE_TRANSPORT_OPEN"]=("Открытый кузов","Open cargo"),["RESOURCE_TRANSPORT_GENERAL"]=("Смешанные грузы","General cargo"),["RESOURCE_TRANSPORT_GRAVEL"]=("Сыпучие грузы","Aggregates"),["RESOURCE_TRANSPORT_CEMENT"]=("Цемент","Cement"),["RESOURCE_TRANSPORT_OIL"]=("Нефтепродукты","Oil products"),["RESOURCE_TRANSPORT_SEWAGE"]=("Сточные воды","Sewage"),["RESOURCE_TRANSPORT_WATER"]=("Вода","Water"),["RESOURCE_TRANSPORT_COOLER"]=("Рефрижератор","Refrigerated cargo"),["RESOURCE_TRANSPORT_CONCRETE"]=("Бетон","Concrete"),["RESOURCE_TRANSPORT_LIVESTOCK"]=("Скот","Livestock"),["RESOURCE_TRANSPORT_WASTE"]=("Отходы","Waste"),
  ["SKILL_PERSONAL"]=("Легковой автомобиль","Personal car"),["SKILL_FIRETRUCK"]=("Пожарная машина","Fire engine"),["SKILL_AMBULANCE"]=("Скорая помощь","Ambulance"),["SKILL_POLICECAR"]=("Полицейская машина","Police car"),["SKILL_POLICEBUS"]=("Полицейский автобус","Police bus"),["SKILL_FORKLIFT"]=("Погрузчик","Forklift"),["SKILL_SNOWPLOW"]=("Снегоуборочная машина","Snowplow"),["SKILL_CONSTRUCTION_CRANE"]=("Кран","Crane"),["SKILL_CONSTRUCTION_GROUNDWORKS"]=("Экскаватор","Excavator"),["SKILL_CONSTRUCTION_BULLDOZER"]=("Бульдозер","Bulldozer"),["SKILL_CONSTRUCTION_ROLLING"]=("Каток","Road roller"),["SKILL_CONSTRUCTION_ASPHALT_LAYING"]=("Асфальтоукладчик","Asphalt paver"),["SKILL_GRAVELMINING"]=("Карьерный экскаватор","Quarry excavator"),["SKILL_SEEDING"]=("Трактор","Tractor"),["SKILL_HARVESTING"]=("Комбайн","Harvester"),["SKILL_WOODCARRING"]=("Лесовоз","Logging truck"),["SKILL_WASTE_CONTAINER_BIG"]=("Мусоровоз больших контейнеров","Large container truck"),["SKILL_WASTE_CONTAINER_SMALL"]=("Мусоровоз","Garbage truck"),["SKILL_CONSTRUCTION_RAIL_CRANE"]=("Железнодорожный кран","Rail crane"),["SKILL_CONSTRUCTION_RAIL_CRANE2"]=("Железнодорожный кран","Rail crane"),["SKILL_CONSTRUCTION_RAIL_TRACK_LAYER"]=("Путеукладчик","Track layer"),["SKILL_CONSTRUCTION_RAIL_TUNNEL_BORING"]=("Тоннелепроходческая машина","Tunnel boring machine"),
  ["TRAINGROUP_LOCOMOTIVE"]=("Локомотив","Locomotive"),["TRAINGROUP_LOCOMOTIVE_STEAM"]=("Паровоз","Steam locomotive"),["TRAINGROUP_METRO"]=("Метро","Metro"),["TRAINGROUP_MOTORVAGON"]=("Моторвагон","Motor wagon"),["TRAINGROUP_TRACKBUILDER"]=("Путеукладчик","Track builder"),["TRAINGROUP_TRACKBUILDER_STEAM"]=("Паровой путеукладчик","Steam track builder"),["TRAINGROUP_TRAINSET"]=("Поезд","Train set"),["TRAINGROUP_TRAM"]=("Трамвай","Tram"),["TRAINGROUP_VAGON"]=("Вагон","Wagon"),
  ["generalcargo_vehicle"]=("Упакованный груз","Packaged cargo"),["yellowcake"]=("Оксид урана","Uranium oxide"),["boards"]=("Доски","Boards"),["bricks"]=("Кирпичи","Bricks"),["steel"]=("Сталь","Steel"),["prefabpanels"]=("Панели","Prefab panels"),["wood"]=("Древесина","Wood"),["aluminium"]=("Алюминий","Aluminium"),["livestock_vehicle"]=("Скот","Livestock"),["gravel_vehicle"]=("Гравий","Gravel"),["rawgravel_vehicle"]=("Камень","Quarried stone"),["coal_vehicle"]=("Уголь","Coal"),["rawcoal_vehicle"]=("Уголь-сырец","Coal ore"),["iron_vehicle"]=("Железо","Iron"),["rawiron_vehicle"]=("Железная руда","Iron ore"),["bauxite_vehicle"]=("Бокситы","Bauxite"),["rawbauxite_vehicle"]=("Бокситы-сырцы","Raw bauxite"),["uranium_vehicle"]=("Урановая руда","Uranium ore"),["waste1"]=("Смешанные отходы","Mixed waste"),["waste_gravel_vehicle"]=("Строительный мусор","Construction waste"),["waste_steel_vehicle"]=("Металлолом","Metal scrap"),["waste_aluminium_vehicle"]=("Алюминиевый лом","Aluminium scrap"),["waste_plastic"]=("Пластиковые отходы","Plastic waste")
 };
}
