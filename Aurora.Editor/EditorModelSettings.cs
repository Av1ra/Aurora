using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;
using System.Text.RegularExpressions;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 static readonly Dictionary<string,string> vehicleLabels=new(){["TYPE"]="Тип транспорта",["DESCRIPTION"]="Описание (ID)",["COUNTRY"]="Страна (ID)",["COST_RUB"]="Цена, рубли",["COST_USD"]="Цена, доллары",["AVAILABLE"]="Годы выпуска",["LIFESPAN"]="Срок службы",["MOVEMENT_SPEED"]="Скорость",["MOVEMENT_POWER_KW"]="Мощность, кВт",["MOVEMENT_EMPTY_WEIGHT"]="Масса без груза",["MOVEMENT_CONSPUMPTION"]="Расход",["RESOURCE_CAPACITY"]="Вместимость",["RESOURCE_TRANSPORT_TYPE"]="Тип перевозимого груза",["RESOURCE_ALLOW_ONLY"]="Разрешённый ресурс",["FAMILY"]="Семейство",["SOUND_PARAMS"]="Звуки",["DRIVER"]="Положение водителя",["SKILL_PERSONAL"]="Личный автомобиль",["SKILL_FIRETRUCK"]="Пожарная машина",["SKILL_AMBULANCE"]="Скорая помощь",["SKILL_POLICECAR"]="Полиция",["SKILL_POLICEBUS"]="Полицейский автобус",["SKILL_CONSTRUCTION_CRANE"]="Строительный кран",["SKILL_FORKLIFT"]="Погрузчик"};
 Dictionary<string,string[]>? vehicleChoices;
 string? MediaFolder()
 {
  foreach(string? start in new[]{selectedLod==null?null:Path.GetDirectoryName(selectedLod),AppContext.BaseDirectory,Environment.CurrentDirectory})for(string? folder=start;folder!=null;folder=Path.GetDirectoryName(folder)){string media=Path.Combine(folder,"media_soviet");if(Directory.Exists(Path.Combine(media,"vehicles")))return media;}return null;
 }
 async Task LoadVehicleChoices()
 {
  if(vehicleChoices!=null)return;string? media=MediaFolder();vehicleChoices=await Task.Run(()=>{
   var result=new Dictionary<string,HashSet<string>>();if(media!=null)foreach(string file in Directory.EnumerateFiles(Path.Combine(media,"vehicles"),"script.ini",SearchOption.AllDirectories))foreach(var field in VehicleScript.Fields(File.ReadAllText(file))){if(field.Key.StartsWith("LIGHT_",StringComparison.Ordinal)||field.Key.Contains("VISUALIZATION",StringComparison.Ordinal))continue;if(!result.TryGetValue(field.Key,out var set))result[field.Key]=set=[];if(field.Value.Length>0)set.Add(field.Value);}
   return result.ToDictionary(p=>p.Key,p=>p.Value.Order().ToArray());
  });
 }
 void RegisterModelSettingsCommands()
 {
  editorCommands["Настройки модели"]=(ConfigureModel,()=>HasModel);
  editorCommands["Добавить параметр…"]=(AddVehicleSetting,()=>HasModel);
  editorCommands["Настроить груз…"]=(ConfigureCargo,()=>HasModel);
  editorCommands["Настройки превью…"]=(ConfigurePreviews,()=>HasModel);
 }
 string VehicleTemplate()=>vehicleConfiguration??"$TYPE VEHICLETYPE_ROAD\n$NAME_STR \"Vehicle\"\n$COST_RUB 1000\n$MOVEMENT_SPEED 60\n$MOVEMENT_POWER_KW 100\n$MOVEMENT_EMPTY_WEIGHT 1\n$RESOURCE_CAPACITY 1\n$RESOURCE_TRANSPORT_TYPE RESOURCE_TRANSPORT_COVERED\n$AVAILABLE 1950 2050\nend\n";
 async Task ConfigureModel()
 {
  await LoadVehicleChoices();string original=VehicleTemplate();var fields=VehicleScript.Fields(original);var panel=new StackPanel{Spacing=10,Width=500};var edits=new List<Func<string,string>>();
  var literal=fields.FirstOrDefault(f=>f.Key=="NAME_STR");var localized=fields.FirstOrDefault(f=>f.Key=="NAME");
  var nameMode=new ComboBox{Header=UiLanguage.Text("Название модели"),ItemsSource=new[]{UiLanguage.Text("Текст"),"ID"},SelectedIndex=literal==null&&localized!=null?1:0};var name=new TextBox{Text=(literal?.Value??localized?.Value??"Vehicle").Trim('"')};panel.Children.Add(nameMode);panel.Children.Add(name);
  var groups=new Dictionary<string,StackPanel>();string Group(string key)=>key.StartsWith("COST_",StringComparison.Ordinal)||key=="AVAILABLE"?"Покупка":key.StartsWith("MOVEMENT_",StringComparison.Ordinal)?"Движение":key.StartsWith("RESOURCE_",StringComparison.Ordinal)||key.StartsWith("SKILL_",StringComparison.Ordinal)?"Транспорт и функции":key.StartsWith("LOD",StringComparison.Ordinal)||key.Contains("ANIM",StringComparison.Ordinal)||key.Contains("JOINT",StringComparison.Ordinal)?"Модели и анимации":key.StartsWith("SOUND",StringComparison.Ordinal)||key.StartsWith("PARTICLE",StringComparison.Ordinal)?"Звук и эффекты":"Общее";
  foreach(var field in fields.Where(f=>f.Key is not "NAME" and not "NAME_STR" and not "RESOURCE_VISUALIZATION"&&!f.Key.StartsWith("LIGHT_",StringComparison.Ordinal))){
   string group=Group(field.Key);if(!groups.TryGetValue(group,out var rows)){groups[group]=rows=new StackPanel{Spacing=8};panel.Children.Add(new Expander{Header=UiLanguage.Text(group),Content=rows,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch,IsExpanded=group=="Общее"});}
   var row=new StackPanel{Spacing=4};rows.Children.Add(row);row.Children.Add(new TextBlock{Text=UiLanguage.Text(vehicleLabels.GetValueOrDefault(field.Key,field.Key))+(field.Occurrence>0?" "+(field.Occurrence+1):""),TextWrapping=TextWrapping.Wrap});
   string[] tokens=field.Value.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
   bool numbers=tokens.Length is >0 and <=4&&tokens.All(t=>double.TryParse(t,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)&&double.IsFinite(value));
   if(numbers){
    var values=tokens.Select(t=>double.Parse(t,CultureInfo.InvariantCulture)).ToArray();var line=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6};row.Children.Add(line);var boxes=values.Select(v=>new NumberBox{Value=v,Width=Math.Max(98,Math.Min(210,450/values.Length)),SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact,SmallChange=field.Key=="AVAILABLE"?1:.1}).ToArray();foreach(var box in boxes)line.Children.Add(box);
    edits.Add(text=>{if(boxes.Any(b=>!double.IsFinite(b.Value)))throw new InvalidDataException(UiLanguage.Text("Некорректное значение"));if(field.Key=="AVAILABLE"&&(boxes.Length!=2||boxes[0].Value>boxes[1].Value||boxes.Any(b=>b.Value!=Math.Truncate(b.Value))))throw new InvalidDataException(UiLanguage.Text("Проверьте годы выпуска"));return VehicleScript.Set(text,field.Key,field.Occurrence,string.Join(" ",boxes.Select(b=>b.Value.ToString("G9",CultureInfo.InvariantCulture))));});
   }else if(tokens.Length==0&&string.IsNullOrWhiteSpace(field.Body)){var enabled=new CheckBox{Content=UiLanguage.Text("Включено"),IsChecked=true};row.Children.Add(enabled);edits.Add(text=>VehicleScript.Set(text,field.Key,field.Occurrence,enabled.IsChecked==true?"":null));}
   else{
    if(vehicleChoices!.TryGetValue(field.Key,out var choices)&&choices.Length>0&&field.Key is "TYPE" or "RESOURCE_TRANSPORT_TYPE" or "RESOURCE_ALLOW_ONLY"){
     var choice=new ComboBox{IsEditable=true,ItemsSource=choices,Text=field.Value,HorizontalAlignment=HorizontalAlignment.Stretch};row.Children.Add(choice);edits.Add(text=>VehicleScript.Set(text,field.Key,field.Occurrence,choice.Text));
    }else{var box=new TextBox{Text=field.Value};row.Children.Add(box);edits.Add(text=>VehicleScript.Set(text,field.Key,field.Occurrence,box.Text));}
   }
   if(!string.IsNullOrWhiteSpace(field.Body)&&field.Body.Split('\n').Any(l=>l.Trim().Length>0&&!l.TrimStart().StartsWith("--",StringComparison.Ordinal))){var body=new TextBox{Text=field.Body.Trim(),AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=64};row.Children.Add(body);edits.Add(text=>VehicleScript.SetBody(text,field.Key,field.Occurrence,body.Text));}
  }
  string? updated=null;var dialog=VehicleDialog("Настройки модели",panel);ValidateVehicleDialog(dialog,panel,()=>{
   string text=original;foreach(var edit in edits.AsEnumerable().Reverse())text=edit(text);foreach(var key in new[]{"NAME","NAME_STR"})foreach(var field in VehicleScript.Fields(text).Where(f=>f.Key==key).OrderByDescending(f=>f.Occurrence))text=VehicleScript.Set(text,key,field.Occurrence,null);
   string value=name.Text.Trim();if(value.Length==0)throw new InvalidDataException(UiLanguage.Text("Введите название модели"));if(nameMode.SelectedIndex==1){if(!int.TryParse(value,out var id)||id<0)throw new InvalidDataException("Invalid name ID");text=VehicleScript.Set(text,"NAME",0,value);}else text=VehicleScript.Set(text,"NAME_STR",0,"\""+value.Replace('"','\'')+"\"");updated=text;
  });
  if(await ShowDialog(dialog)!=ContentDialogResult.Primary||updated==null)return;vehicleConfiguration=updated;vehicleConfigurationEdited=true;ReadLampSlots();dirty=true;SetStatus("Настройки модели сохранены");
 }
 async Task AddVehicleSetting()
 {
  await LoadVehicleChoices();var panel=new StackPanel{Spacing=8,Width=420};var key=new ComboBox{Header=UiLanguage.Text("Параметр"),IsEditable=true,ItemsSource=VehicleScript.NewFields.Concat(vehicleChoices!.Keys).Where(k=>!k.StartsWith("LIGHT_",StringComparison.Ordinal)&&!k.Contains("VISUALIZATION",StringComparison.Ordinal)).Distinct().Order().ToArray(),HorizontalAlignment=HorizontalAlignment.Stretch};var value=new TextBox{Header=UiLanguage.Text("Значение")};panel.Children.Add(key);panel.Children.Add(value);string? script=null;var dialog=VehicleDialog("Добавить параметр…",panel);ValidateVehicleDialog(dialog,panel,()=>{string text=VehicleTemplate(),name=key.Text.Trim().TrimStart('$').ToUpperInvariant();script=VehicleScript.Set(text,name,VehicleScript.Fields(text).Count(f=>f.Key==name),value.Text);});if(await ShowDialog(dialog)!=ContentDialogResult.Primary)return;vehicleConfiguration=script;vehicleConfigurationEdited=true;dirty=true;
 }
}
