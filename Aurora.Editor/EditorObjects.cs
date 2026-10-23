using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 void RegisterObjectCommands()
 {
  editorCommands["Переименовать объект…"]=(RenameObject,()=>HasModel&&objects.SelectedItem!=null);
  editorCommands["Разделить несвязанные меши"]=(SplitObject,()=>HasModel&&objects.SelectedItem!=null);
  editorCommands["Трансформация…"]=(BeginMeshEditing,()=>HasModel&&objects.SelectedItem!=null);
  editorCommands["Свойства объекта…"]=(ConfigureObjectProperties,()=>HasModel&&objects.SelectedItem!=null);
  editorCommands["Pivot объекта…"]=(EditSelectedObjectPivot,()=>HasModel&&objects.SelectedItem!=null);
  editorCommands["Удалить объект"]=(DeleteMeshSelection,()=>HasModel&&objects.SelectedItem!=null);
 }
 async Task RenameObject()
 {
  if(objects.SelectedItem is not string oldName)return;
  var box=new TextBox{Header=UiLanguage.Text("Своё имя"),Text=oldName,MinWidth=0};var panel=new StackPanel{Spacing=10};panel.Children.Add(box);
  var presets=new ComboBox{Header=UiLanguage.Text("Штатное имя"),HorizontalAlignment=HorizontalAlignment.Stretch};
  foreach(var preset in new[]{("body","Основной корпус; механизм настраивается отдельно"),("tire_leftShape","Колесо левой стороны транспорта"),("tire_rightShape","Колесо правой стороны транспорта"),("tire2Shape","Дополнительная колёсная группа"),("tireShape","Колёсная группа с общим именем")}){var content=new StackPanel{Spacing=3};content.Children.Add(new TextBlock{Text=preset.Item1});content.Children.Add(new TextBlock{Text=UiLanguage.Text(preset.Item2),TextWrapping=TextWrapping.Wrap,MaxWidth=340,Opacity=.7});presets.Items.Add(new ComboBoxItem{Content=content,Tag=preset.Item1});}
  presets.SelectionChanged+=(_,_)=>{if(presets.SelectedItem is ComboBoxItem item&&item.Tag is string value)box.Text=value;};panel.Children.Add(presets);
  var dialog=VehicleDialog("Переименовать объект…",panel);if(await ShowDialog(dialog)!=ContentDialogResult.Primary)return;string name=box.Text.Trim();
  await RenameObjectTo(oldName,name);
 }
 async Task RenameObjectTo(string oldName,string name){var changed=lods.ToDictionary(p=>p.Key,p=>ModelEditing.Rename(p.Value,oldName,name));string? script=vehicleConfiguration==null?null:VehicleScript.RenamePart(vehicleConfiguration,oldName,name);foreach(var p in changed){var ids=ObjectIds(p.Key);if(ids.Remove(oldName,out var id))ids[name]=id;lods[p.Key]=p.Value;if(glassObjects[p.Key].Remove(oldName))glassObjects[p.Key].Add(name);}RenameAnimationMesh(oldName,name);if(interchange!=null)interchange=interchange with{Nodes=interchange.Nodes.Select(n=>n.Name==oldName?n with{Name=name}:n).ToArray()};vehicleConfiguration=script;vehicleConfigurationEdited|=script!=null;dirty=true;await Display();objects.SelectedItem=name;}
 async Task SplitObject()
 {
  var names=objects.SelectedItems.OfType<string>().ToArray();if(names.Length==0)return;var scope=new ComboBox{ItemsSource=new[]{UiLanguage.Text("Текущая модель"),UiLanguage.Text("Все LOD")},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};if(await ShowDialog(VehicleDialog("Разделить несвязанные меши",scope))!=ContentDialogResult.Primary)return;bool all=scope.SelectedIndex==1;processing=true;UpdateControls();
  var changed=new Dictionary<string,NativeModel>(lods);var clips=animationClips;bool any=false;
  foreach(string name in names)foreach(string path in lods.Keys){if(!all&&path!=selectedLod)continue;var before=changed[path];if(!before.Nodes.Any(n=>n.Name==name))continue;if(before.Nodes.Count(n=>n.Name==name)>1)throw new InvalidDataException("Duplicate mesh name: "+name);NativeModel split;try{split=ModelEditing.Split(before,name);}catch(InvalidDataException ex)when(ex.Message=="Объект уже связан."){continue;}changed[path]=split;any=true;if(path==lods.Keys.First()){var original=before.Nodes.Select(n=>n.Name).ToHashSet();clips=AnimationEditing.SplitMesh(clips,name,split.Nodes.Where(n=>!original.Contains(n.Name)).Select(n=>n.Name).ToArray());}}
  if(!any)throw new InvalidDataException(UiLanguage.Text("Объект уже связан."));
  foreach(var pair in changed){var old=lods[pair.Key];var glass=glassObjects[pair.Key];foreach(string name in names)if(glass.Contains(name)&&!pair.Value.Nodes.Any(n=>n.Name==name)){glass.Remove(name);foreach(var node in pair.Value.Nodes.Where(n=>!old.Nodes.Any(o=>o.Name==n.Name)&&n.Name.StartsWith(name[..Math.Min(52,name.Length)]+"_")))glass.Add(node.Name);}lods[pair.Key]=pair.Value;}
  animationClips=clips;if(animationEditing)RefreshAnimationTracks();dirty=true;await Display();
 }
}
