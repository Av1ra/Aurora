using Aurora.Core;
using System.Text.Json;
if(args[0]=="--checks"){
 string source=File.ReadAllText(Path.Combine(args[1],"dlc3/vehicles/bbo214/script.ini"));string edited=VehicleScript.Set(source,"FAMILY",0,"audit-example");
 string rail=File.ReadAllText(Path.Combine(args[1],"trains/russian_service_agmu/script.ini"));string skill=VehicleScript.Fields(rail).Single(f=>f.Key=="SKILL_CONSTRUCTION_RAIL_CRANE").Value;
 var findings=new{dollarEnd=new{example="dlc3/vehicles/bbo214/script.ini",parsedAsOrdinaryField=VehicleScript.Fields(source).Any(f=>f.Key=="END"),newFieldAfterDollarEnd=edited.IndexOf("$FAMILY audit-example",StringComparison.Ordinal)>edited.IndexOf("$END",StringComparison.Ordinal)},multiArgumentSkill=new{example="trains/russian_service_agmu/script.ini",value=skill,acceptedByScalarUi=double.TryParse(skill,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out _)}};
 File.WriteAllText(args[2],JsonSerializer.Serialize(findings,new JsonSerializerOptions{WriteIndented=true})+"\n");Console.WriteLine(JsonSerializer.Serialize(findings));return;
}
var media=args[0];var roots=new[]{"vehicles","trains","ships","airplanes","helicopters","cabins","buildings","resources","containers","trees","fences","paths","buildingeditor"};
var bases=new[]{media}.Concat(Directory.GetDirectories(media).Where(p=>System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(p),@"^(dlc|elc)\d+$"))).ToArray();
var report=new Dictionary<string,object>();
foreach(string root in roots){int total=0,opened=0;var errors=new Dictionary<string,List<string>>();var meshNames=new Dictionary<string,int>();
 foreach(string folder in bases.Select(b=>Path.Combine(b,root)).Where(Directory.Exists))foreach(string path in Directory.EnumerateFiles(folder,"*.nmf",SearchOption.AllDirectories)){
  total++;try{var model=new NativeModel(File.ReadAllBytes(path));opened++;foreach(string name in model.Nodes.Select(n=>n.Name).Distinct())if(name.Contains("tire",StringComparison.OrdinalIgnoreCase)||name.Contains("wheel",StringComparison.OrdinalIgnoreCase)||name.Contains("rotor",StringComparison.OrdinalIgnoreCase))meshNames[name]=meshNames.GetValueOrDefault(name)+1;}
  catch(Exception ex){string reason=ex.Message;if(!errors.TryGetValue(reason,out var paths))errors[reason]=paths=[];paths.Add(Path.GetRelativePath(media,path).Replace('\\','/'));}
 }
 report[root]=new{total,opened,rejected=total-opened,errors=errors.ToDictionary(p=>p.Key,p=>new{count=p.Value.Count,examples=p.Value.Take(3)}),specialMeshNames=meshNames.OrderByDescending(p=>p.Value).Take(30).ToDictionary()};Console.WriteLine($"{root}: {opened}/{total} opened");
}
File.WriteAllText(args[1],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true})+"\n");
