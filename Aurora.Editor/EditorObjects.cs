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
 }
 async Task RenameObject()
 {
  if(objects.SelectedItem is not string oldName)return;
  var box=new ComboBox{Header=UiLanguage.Text("Имя"),IsEditable=true,Text=oldName,HorizontalAlignment=HorizontalAlignment.Stretch,MinWidth=300};foreach(string preset in new[]{"tire_leftShape","tire_rightShape","tire2Shape","tireShape","body"})box.Items.Add(preset);
  var dialog=VehicleDialog("Переименовать объект…",box);if(await ShowDialog(dialog)!=ContentDialogResult.Primary)return;string name=box.Text.Trim();
  var changed=lods.ToDictionary(p=>p.Key,p=>ModelEditing.Rename(p.Value,oldName,name));string? script=vehicleConfiguration==null?null:VehicleScript.RenamePart(vehicleConfiguration,oldName,name);foreach(var p in changed){lods[p.Key]=p.Value;if(glassObjects[p.Key].Remove(oldName))glassObjects[p.Key].Add(name);}RenameAnimationMesh(oldName,name);vehicleConfiguration=script;vehicleConfigurationEdited|=script!=null;dirty=true;await Display();objects.SelectedItem=name;
 }
 async Task SplitObject()
 {
  if(objects.SelectedItem is not string name)return;var scope=new ComboBox{ItemsSource=new[]{UiLanguage.Text("Текущая модель"),UiLanguage.Text("Все LOD")},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};if(await ShowDialog(VehicleDialog("Разделить несвязанные меши",scope))!=ContentDialogResult.Primary)return;bool all=scope.SelectedIndex==1;processing=true;UpdateControls();
  var changed=await Task.Run(()=>lods.ToDictionary(p=>p.Key,p=>{if((!all&&p.Key!=selectedLod)||!p.Value.Nodes.Any(n=>n.Name==name))return p.Value;try{return ModelEditing.Split(p.Value,name);}catch(InvalidDataException ex)when(all&&ex.Message=="Объект уже связан."){return p.Value;}}));
  var clips=animationClips;var primary=lods.First();if(changed.TryGetValue(primary.Key,out var split)&&!ReferenceEquals(split,primary.Value)){var original=primary.Value.Nodes.Select(n=>n.Name).ToHashSet();string[] pieces=split.Nodes.Where(n=>!original.Contains(n.Name)).Select(n=>n.Name).ToArray();clips=AnimationEditing.SplitMesh(clips,name,pieces);}
  foreach(var p in changed){if(glassObjects[p.Key].Remove(name)){var original=lods[p.Key].Nodes.Select(n=>n.Name).ToHashSet();foreach(var n in p.Value.Nodes.Where(n=>!original.Contains(n.Name)))glassObjects[p.Key].Add(n.Name);}lods[p.Key]=p.Value;}
  animationClips=clips;if(animationEditing)RefreshAnimationTracks();dirty=true;await Display();
 }
}
