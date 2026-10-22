using System.Globalization;
namespace Aurora.Core;

public sealed record TransportArgument(string Label,string Default="0",string? Extension=null,bool Mesh=false,bool Fixed=false,bool Text=false,bool Hidden=false,double UiScale=1);
public sealed record TransportParameter(string Key,string Name,string Group,string[] Types,TransportArgument[] Arguments,bool Repeated=false)
{
 public bool Applies(string type)=>Types.Length==0||Types.Contains(type);
 public TransportParameter For(string script,int occurrence){int count=VehicleScript.Tokens(script).Count(t=>t.Key==Key&&t.Occurrence==occurrence);return Key=="CRANE_STATIC"&&count==4?this with{Arguments=[Arguments[0],new("X"),new("Y"),new("Z")]}:Key is "CRANE_DYNAMIC2" or "CARGO_PLATFORM"&&count==1?this with{Arguments=[Arguments[0]]}:this;}
 public string[] Read(string script,int occurrence){var tokens=VehicleScript.Tokens(script).Where(t=>t.Key==Key&&t.Occurrence==occurrence).ToArray();if(tokens.Length<Arguments.Length)throw new InvalidDataException("Unsupported vehicle argument layout: "+Key);return tokens.Take(Arguments.Length).Select(t=>t.Value).ToArray();}
 public string Write(string script,int occurrence,IReadOnlyList<string> values){if(values.Count!=Arguments.Length)throw new InvalidDataException("Invalid argument count: "+Key);var original=VehicleScript.Tokens(script).Where(t=>t.Key==Key&&t.Occurrence==occurrence).Take(Arguments.Length).Select(t=>t.Value).ToArray();for(int i=0;i<values.Count;i++){if(i<original.Length&&values[i]==original[i])continue;var a=Arguments[i];if(a.Fixed&&values[i]!=a.Default)throw new InvalidDataException("Invalid parameter marker: "+Key);if(a.Extension==null&&!a.Mesh&&!a.Fixed&&!a.Text&&(!double.TryParse(values[i],NumberStyles.Float,CultureInfo.InvariantCulture,out double n)||!double.IsFinite(n)||a.Label=="Масштаб"&&n<=0))throw new InvalidDataException("Invalid number: "+a.Label);}return VehicleScript.SetArguments(script,Key,occurrence,values);}
}
public static class TransportParameters
{
 public static string Section(TransportParameter p)=>p.Key.StartsWith("LODJOINT",StringComparison.Ordinal)?"jointLods":p.Key.StartsWith("LIGHT_",StringComparison.Ordinal)||p.Key=="SIREN_POINT"?"lights":p.Key is "WATER_POINT" or "PARTICLE_JETPOINT" or "PARTICLE_MOVEMENT_JOINT"?"effects":p.Group switch{"Сборка"=>"assembly","Механизмы"=>"mechanisms","Конная тяга"=>"horses","Авиация"=>"aviation","Поезда"=>"rail","Суда"=>"waves","Кабины"=>"cabins",_=>"points"};
 public static bool Available(TransportParameter p,string script)
 {
  var fields=VehicleScript.Fields(script);if(fields.Any(f=>f.Key==p.Key))return true;string type=fields.FirstOrDefault(f=>f.Key=="TYPE")?.Value??"";if(!p.Applies(type))return false;
  return p.Key switch{"WATER_POINT"=>fields.Any(f=>f.Key=="SKILL_FIRETRUCK"),"PARTICLE_MOVEMENT_JOINT" or "DRIVER_JOINT" or "LIGHT_BLINKER_TRAILER"=>fields.Any(f=>f.Key=="JOINT"),_=>true};
 }
 static TransportArgument N(string name,string value="0")=>new(name,value);
 static TransportArgument F(string name,string ext=".nmf")=>new(name,"",ext);
 static TransportArgument[] Point(string prefix="")=>[N(prefix+"X"),N(prefix+"Y"),N(prefix+"Z")];
 static readonly string[] road=["VEHICLETYPE_ROAD"],rail=["VEHICLETYPE_RAIL_LOCOMOTIVE","VEHICLETYPE_RAIL_VAGON"],plane=["VEHICLETYPE_AIRPLANE"],heli=["VEHICLETYPE_HELICOPTER"],ship=["VEHICLETYPE_SHIP"];
 public static readonly TransportParameter[] All=[
  new("JOINT","Сочленённая часть","Сборка",road,[F("Модель"),N("Смещение"),N("Смещение при повороте")]),
  new("LODJOINT1","LOD 1 присоединённой части","Сборка",road,[F("Модель")]),new("LODJOINT2","LOD 2 присоединённой части","Сборка",road,[F("Модель")]),
  new("ALPHA","Дополнительная модель ALPHA","Сборка",[],[F("Модель")]),
  new("CRANE_STATIC","Неподвижная часть механизма","Механизмы",[],[F("Модель")]),
  new("CRANE_DYNAMIC","Подвижная часть механизма","Механизмы",[],[F("Модель"),..Point()]),
  new("CRANE_DYNAMIC2","Вторая подвижная часть","Механизмы",rail,[F("Модель"),..Point()]),
  new("DUMPER_DECK_PIVOT","Кузов самосвала","Механизмы",road,[new("Объект","body",Mesh:true),..Point()]),
  new("DUMPER_JOINTDECK_PIVOT","Кузов самосвального прицепа","Механизмы",road,[new("Объект","body",Mesh:true),..Point()]),
  new("MIXER_DRUM","Барабан бетономешалки","Механизмы",road,[new("Объект","body",Mesh:true),..Point("A "),..Point("B ")]),
  new("MIXER_JOINTDRUM","Барабан на прицепе","Механизмы",road,[new("Объект","body",Mesh:true),..Point("A "),..Point("B ")]),
  new("PROPELER_STATIC","Неподвижный винт","Авиация",plane,[F("Модель")]),new("PROPELER_DYNAMIC","Винт на высоких оборотах","Авиация",plane,[F("Модель")]),
  new("PROPELER_LOW","Винт на низких оборотах","Авиация",plane,[N("Масштаб","1"),F("Модель"),F("Материал",".mtl")]),
  new("PROPELER_POINT","Положение винта","Авиация",plane,Point(),true),
  new("PROPELER_HELICOPTER","Винт вертолёта","Авиация",heli,[N("Масштаб","1"),F("Неподвижный винт"),F("Высокие обороты"),F("Низкие обороты"),F("Материал",".mtl")],true),
  new("PROPELER_HELICOPTER_POINT_DIR","Положение и поворот винта","Авиация",heli,[..Point(),N("Поворот X, рад"),N("Поворот Y, рад"),N("Поворот Z, рад")],true),
  new("MOVEMENT_WHEEL_FRONT","Передняя опора шасси","Авиация",["VEHICLETYPE_AIRPLANE","VEHICLETYPE_HELICOPTER"],Point()),
  new("MOVEMENT_WHEEL_BACK","Задняя опора шасси","Авиация",["VEHICLETYPE_AIRPLANE","VEHICLETYPE_HELICOPTER"],Point()),
  new("DRIVER","Водитель","Точки",road,Point()),new("DRIVER_JOINT","Водитель присоединённой части","Точки",road,Point()),
  new("TRACKTOR_TOOL_POINT","Крепление навесного инструмента","Точки",road,Point()),
  new("WATER_POINT","Пожарная струя","Точки",road,Point(),true),new("SIREN_POINT","Проблесковый маяк","Точки",road,Point(),true),
  new("LIGHT_FORWARD","Передний огонь","Точки",[],Point(),true),new("LIGHT_BACKWARD","Задний огонь","Точки",[],Point(),true),
  new("LIGHT_BLINKER","Поворотник","Точки",[],Point(),true),new("LIGHT_BLINKER_TRAILER","Поворотник прицепа","Точки",road,Point(),true),
  new("PARTICLE_JETPOINT","Реактивное сопло","Авиация",plane,Point(),true),
  new("PARTICLE_MOVEMENT_JOINT","Дым присоединённой части","Точки",road,[new("Эффект","vehicle_medium",Text:true),..Point()],true),
  new("HORSE","Лошадь","Конная тяга",road,[..Point(),new("Упряжь","DLC3_h_single",Text:true)],true),
  new("HORSE_MATERIAL","Материал лошади","Конная тяга",road,[F("Материал",".mtl")]),
  new("SINGLE_HORSE_POWER","Мощность одной лошади","Конная тяга",road,[N("Мощность","2")]),
  new("CARGO_PLATFORM","Грузовая платформа вертолёта","Авиация",heli,[F("Модель"),new("","0",Hidden:true),new("","0",Hidden:true)]),
  new("TUNNELING_TEETH","Режущая головка","Поезда",rail,[]),
  new("TRAIN_FORWARD_AXIS_DISTANCE","Передняя ось","Поезда",rail,[N("Расстояние, м")]),new("TRAIN_BACKWARD_AXIS_DISTANCE","Задняя ось","Поезда",rail,[N("Расстояние, м")]),
  new("TRAIN_BBOX_FORWARD_CORRECTION","Передняя граница стыковки","Поезда",rail,[N("Поправка, м")]),new("TRAIN_BBOX_BACKWARD_CORRECTION","Задняя граница стыковки","Поезда",rail,[N("Поправка, м")]),
  new("PREVIEW_OFFSET","Смещение превью кабины","Кабины",["VEHICLETYPE_CABIN"],[N("Смещение")]),
  new("PREVIEW_ZOOM","Масштаб превью кабины","Кабины",["VEHICLETYPE_CABIN"],[N("Масштаб","1")]),
  Wave("WAVEPOINTFRONT","Носовая волна"),Wave("WAVEPOINTBACK","Кормовая волна")
 ];
 static TransportParameter Wave(string key,string name)=>new(key,name,"Суда",ship,[new("","position",Fixed:true),..Point(),new("","coordstep",Fixed:true),new("Шаг волны","0.001",UiScale:1000),new("","bumpfactor",Fixed:true),N("Высота волны","1"),new("","srcsize",Fixed:true),N("Начальный размер","1"),new("","dstsize",Fixed:true),N("Конечный размер","10"),new("","maxtime",Fixed:true),N("Время жизни, с","10")],true);
}
