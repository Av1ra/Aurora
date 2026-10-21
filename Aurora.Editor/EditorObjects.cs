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
  var changed=lods.ToDictionary(p=>p.Key,p=>ModelEditing.Rename(p.Value,oldName,name));string? script=vehicleConfiguration==null?null:VehicleScript.RenamePart(vehicleConfiguration,oldName,name);foreach(var p in changed){lods[p.Key]=p.Value;if(glassObjects[p.Key].Remove(oldName))glassObjects[p.Key].Add(name);}vehicleConfiguration=script;vehicleConfigurationEdited|=script!=null;dirty=true;await Display();objects.SelectedItem=name;
 }
 async Task SplitObject()
 {
  if(objects.SelectedItem is not string name)return;processing=true;UpdateControls();
  var changed=await Task.Run(()=>lods.ToDictionary(p=>p.Key,p=>p.Value.Nodes.Any(n=>n.Name==name)?ModelEditing.Split(p.Value,name):p.Value));
  var counts=changed.Where(p=>lods[p.Key].Nodes.Any(n=>n.Name==name)).Select(p=>p.Value.Nodes.Count-lods[p.Key].Nodes.Count).Distinct().ToArray();if(counts.Length!=1)throw new InvalidDataException(UiLanguage.Text("Число частей должно совпадать во всех LOD."));
  foreach(var p in changed){if(glassObjects[p.Key].Remove(name)){var original=lods[p.Key].Nodes.Select(n=>n.Name).ToHashSet();foreach(var n in p.Value.Nodes.Where(n=>!original.Contains(n.Name)))glassObjects[p.Key].Add(n.Name);}lods[p.Key]=p.Value;}
  dirty=true;await Display();
 }
}
