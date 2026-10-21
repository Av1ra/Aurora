using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 async Task ConfigureSoundLayers()
 {
  await LoadVehicleChoices();string script=VehicleTemplate();var layers=VehicleSoundFiles.Layers(script).ToList();string media=MediaFolder()!;var registry=await Task.Run(()=>VehicleSoundFiles.Registry(media));
  var panel=new StackPanel{Spacing=8,Width=470};var list=new ListView{MaxHeight=220,SelectionMode=ListViewSelectionMode.Single};panel.Children.Add(list);
  var group=new ComboBox{HorizontalAlignment=HorizontalAlignment.Stretch};var native=new ComboBox{HorizontalAlignment=HorizontalAlignment.Stretch,MaxDropDownHeight=240};var presets=vehicleCatalog!.Sounds;string[] groups=presets.Select(s=>s.Name.Split(" / ")[0]).Distinct().Order().ToArray();group.ItemsSource=groups;panel.Children.Add(group);panel.Children.Add(native);string[] files=[];
  void Choices(){files=presets.Where(s=>s.Name.Split(" / ")[0]==group.SelectedItem as string).SelectMany(s=>s.Files).Distinct().Order().ToArray();native.ItemsSource=files;native.SelectedIndex=files.Length>0?0:-1;}
  group.SelectionChanged+=(_,_)=>Choices();string currentClass=VehicleCatalog.DescribeClass(script,UiLanguage.English).Name.Split(" / ")[0];group.SelectedIndex=Math.Max(0,Array.IndexOf(groups,currentClass));
  var row=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6};Button Button(string name){var b=new Button{Content=UiLanguage.Text(name)};row.Children.Add(b);return b;}var add=Button("+");var replace=Button("Заменить");var remove=Button("Удалить");var up=Button("↑");var down=Button("↓");var import=Button("Загрузить INI…");panel.Children.Add(row);var info=new TextBlock{TextWrapping=TextWrapping.Wrap};panel.Children.Add(info);var error=new InfoBar{IsClosable=false,Severity=InfoBarSeverity.Warning};panel.Children.Add(error);
  string PathFor(VehicleSoundFiles.Layer layer)=>layer.Workshop?VehicleSoundFiles.Resolve(layer.Reference,Path.GetDirectoryName(lods.Keys.First())!):VehicleAssets.Resolve(layer.Reference,Path.GetDirectoryName(lods.Keys.First())!,media);
  string[] Validate(){var texts=layers.Select(l=>VehicleSoundFiles.Validate(PathFor(l))).ToArray();return texts.SelectMany(t=>VehicleSoundFiles.MissingEvents(t,registry)).Distinct().ToArray();}
  void Refresh(int selected=-1){list.ItemsSource=layers.Select((l,i)=>(i+1)+". "+Path.GetFileName(l.Reference)).ToArray();list.SelectedIndex=selected;try{var missing=Validate();var overlaps=VehicleSoundFiles.Overlaps(layers.Select(l=>VehicleSoundFiles.Validate(PathFor(l))));info.Text=overlaps.Length==0?"":UiLanguage.Text("Повторяющиеся звуковые параметры")+": "+string.Join(", ",overlaps);error.Message=missing.Length==0?"":UiLanguage.Text("Нет в реестре звуков игры")+": "+string.Join(", ",missing);error.IsOpen=missing.Length>0;}catch(Exception ex){error.Message=ex.Message;error.IsOpen=true;}}
  add.Click+=(_,_)=>{if(native.SelectedIndex<0)return;layers.Add(new(files[native.SelectedIndex]));Refresh(layers.Count-1);};replace.Click+=(_,_)=>{int at=list.SelectedIndex;if(at<0||native.SelectedIndex<0)return;layers[at]=new(files[native.SelectedIndex]);Refresh(at);};remove.Click+=(_,_)=>{int at=list.SelectedIndex;if(at<0)return;layers.RemoveAt(at);Refresh(Math.Min(at,layers.Count-1));};void Move(int delta){int at=list.SelectedIndex,to=at+delta;if(at<0||to<0||to>=layers.Count)return;(layers[at],layers[to])=(layers[to],layers[at]);Refresh(to);}up.Click+=(_,_)=>Move(-1);down.Click+=(_,_)=>Move(1);
  import.Click+=async(_,_)=>{try{string? path=await Pick(".ini");if(path==null)return;string text=VehicleSoundFiles.Validate(path);string copy=SessionFile("sound-"+Guid.NewGuid().ToString("N")+".ini");File.WriteAllText(copy,text);layers.Add(new(copy,true));Refresh(layers.Count-1);}catch(Exception ex){error.Message=ex.Message;error.IsOpen=true;}};
  var dialog=VehicleDialog("Звуковые слои",panel);dialog.PrimaryButtonClick+=(_,e)=>{try{Validate();}catch(Exception ex){e.Cancel=true;error.Message=ex.Message;error.IsOpen=true;}};Refresh();if(await ShowDialog(dialog)!=ContentDialogResult.Primary)return;string updated=VehicleSoundFiles.SetLayers(script,layers);if(updated==script)return;vehicleConfiguration=updated;vehicleConfigurationEdited=true;dirty=true;
 }
}
