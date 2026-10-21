using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;
using System.Numerics;
namespace Aurora.Editor;

sealed record TransportPoint(string Key,int Occurrence,int Start);
sealed partial class EditorWindow
{
 TransportPoint? transportPoint;
 readonly Dictionary<string,(DateTime Stamp,NativeModel Model)> frameModels=new(StringComparer.OrdinalIgnoreCase);
 bool JointFrameRequested=>gizmoVisible&&(transportPoint?.Key.Contains("JOINT",StringComparison.Ordinal)==true||transportPoint?.Key=="LIGHT_BLINKER_TRAILER"||activeCargo>=0&&cargoFrame.Contains("JOINT",StringComparison.Ordinal));
 NativeModel JointModel(string? script=null){script??=vehicleConfiguration;var reference=script==null?null:VehicleScript.Tokens(script).FirstOrDefault(t=>t.Key=="JOINT"&&t.Index==0)?.Value;if(reference==null)throw new InvalidDataException(UiLanguage.Text("Сначала выберите модель присоединённой части"));string path=VehicleAssets.Resolve(reference,Path.GetDirectoryName(lods.Keys.First())!,MediaFolder());var stamp=File.GetLastWriteTimeUtc(path);if(!frameModels.TryGetValue(path,out var cached)||cached.Stamp!=stamp){cached=(stamp,new NativeModel(File.ReadAllBytes(path)));frameModels[path]=cached;}return cached.Model;}
 NativeModel ViewportModel()=>JointFrameRequested?JointModel():CurrentModel();
 async Task ConfigureConsist()
 {
  await LoadVehicleChoices();string script=VehicleTemplate();string type=VehicleScript.Fields(script).First(f=>f.Key=="TYPE").Value;bool rail=type is "VEHICLETYPE_RAIL_LOCOMOTIVE" or "VEHICLETYPE_RAIL_VAGON";
  var panel=new StackPanel{Spacing=10,Width=500};var family=new ComboBox{HorizontalAlignment=HorizontalAlignment.Stretch};var originalFamily=VehicleScript.Fields(script).FirstOrDefault(f=>f.Key=="FAMILY")?.Value??"";var objects=vehicleCatalog!.Objects.Where(o=>o.Type==type).ToArray();var familyRefs=new[]{""}.Concat(objects.Select(o=>o.Id)).Concat(new[]{originalFamily}).Distinct().ToArray();family.ItemsSource=familyRefs.Select(r=>r.Length==0?UiLanguage.Text("Без базовой модели"):objects.FirstOrDefault(o=>o.Id==r)?.Name??r).ToArray();family.SelectedIndex=Array.IndexOf(familyRefs,originalFamily);SettingRow(panel,"Базовая модель",family);
  var excluded=new CheckBox{Content=UiLanguage.Text("Не продавать отдельно"),IsChecked=VehicleScript.Fields(script).Any(f=>f.Key=="PURCHASE_EXCLUDE")};panel.Children.Add(excluded);
  var units=VehicleConsist.Read(script).ToList();var list=new ListBox{MaxHeight=240};var source=new ComboBox{HorizontalAlignment=HorizontalAlignment.Stretch,IsEditable=true};var candidates=vehicleCatalog.Objects.Where(o=>o.Type is "VEHICLETYPE_RAIL_LOCOMOTIVE" or "VEHICLETYPE_RAIL_VAGON").ToArray();source.ItemsSource=candidates.Select(o=>o.Name).ToArray();var reversed=new CheckBox{Content=UiLanguage.Text("Развернуть секцию")};
  if(rail){panel.Children.Add(new TextBlock{Text=UiLanguage.Text("После головной секции"),FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});panel.Children.Add(list);panel.Children.Add(source);panel.Children.Add(reversed);var buttons=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6};Button Action(string title,Action action){var button=new Button{Content=UiLanguage.Text(title)};button.Click+=(_,_)=>action();buttons.Children.Add(button);return button;}
   void Refresh(int index=-1){list.ItemsSource=units.Select((u,i)=>(i+1)+". "+(candidates.FirstOrDefault(o=>o.Id==u.Reference)?.Name??u.Reference)+(u.Reverse?" ↶":"")).ToArray();list.SelectedIndex=index;}
   Action("Добавить",()=>{string reference=source.SelectedIndex>=0?candidates[source.SelectedIndex].Id:source.Text.Trim();if(reference.Length==0)return;units.Add(new(reference,reversed.IsChecked==true));Refresh(units.Count-1);});
   Action("Удалить",()=>{int i=list.SelectedIndex;if(i<0)return;units.RemoveAt(i);Refresh(Math.Min(i,units.Count-1));});
   Action("Выше",()=>{int i=list.SelectedIndex;if(i<=0)return;(units[i-1],units[i])=(units[i],units[i-1]);Refresh(i-1);});Action("Ниже",()=>{int i=list.SelectedIndex;if(i<0||i>=units.Count-1)return;(units[i+1],units[i])=(units[i],units[i+1]);Refresh(i+1);});
   Action("Развернуть секцию",()=>{int i=list.SelectedIndex;if(i<0)return;units[i]=units[i] with{Reverse=!units[i].Reverse};Refresh(i);});panel.Children.Add(buttons);Refresh();
  }
  var dialog=VehicleDialog("Связи и состав",panel);string? result=null;ValidateVehicleDialog(dialog,panel,()=>{result=VehicleScript.Set(script,"FAMILY",0,family.SelectedIndex>0?familyRefs[family.SelectedIndex]:null);result=VehicleScript.Set(result,"PURCHASE_EXCLUDE",0,excluded.IsChecked==true?"":null);if(rail)result=VehicleConsist.Write(result,units);});if(await ShowDialog(dialog)!=ContentDialogResult.Primary||result==null)return;vehicleConfiguration=result;vehicleConfigurationEdited=true;dirty=true;await Display();
 }
 Vector3 TransportPointPosition(){if(transportPoint==null||vehicleConfiguration==null)return Vector3.Zero;var tokens=VehicleScript.Tokens(vehicleConfiguration).Where(t=>t.Key==transportPoint.Key&&t.Occurrence==transportPoint.Occurrence).ToArray();if(tokens.Length<transportPoint.Start+3){transportPoint=null;return Vector3.Zero;}return new(float.Parse(tokens[transportPoint.Start].Value,CultureInfo.InvariantCulture),float.Parse(tokens[transportPoint.Start+1].Value,CultureInfo.InvariantCulture),float.Parse(tokens[transportPoint.Start+2].Value,CultureInfo.InvariantCulture));}
 void SetTransportPoint(Vector3 value){if(transportPoint==null||vehicleConfiguration==null)return;var args=VehicleScript.Tokens(vehicleConfiguration).Where(t=>t.Key==transportPoint.Key&&t.Occurrence==transportPoint.Occurrence).Select(t=>t.Value).ToArray();if(args.Length<transportPoint.Start+3)return;var p=new[]{value.X,value.Y,value.Z};for(int i=0;i<3;i++)args[transportPoint.Start+i]=p[i].ToString("G9",CultureInfo.InvariantCulture);vehicleConfiguration=VehicleScript.SetArguments(vehicleConfiguration,transportPoint.Key,transportPoint.Occurrence,args);vehicleConfigurationEdited=true;}
 async Task ConfigureTransport()
 {
  string draft=VehicleTemplate();string type=VehicleScript.Fields(draft).First(f=>f.Key=="TYPE").Value;
  var specs=TransportParameters.All.Where(p=>p.Applies(type)||VehicleScript.Fields(draft).Any(f=>f.Key==p.Key)).ToArray();
  var panel=new StackPanel{Spacing=10,Width=500};var list=new ComboBox{HorizontalAlignment=HorizontalAlignment.Stretch};panel.Children.Add(list);
  var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6};var addType=new ComboBox{ItemsSource=specs.Select(p=>UiLanguage.Text(p.Name)).ToArray(),SelectedIndex=0,MinWidth=0,MaxWidth=350};var add=new Button{Content="+"};var remove=new Button{Content=UiLanguage.Text("Удалить")};actions.Children.Add(addType);actions.Children.Add(add);actions.Children.Add(remove);panel.Children.Add(actions);
  var controls=new StackPanel{Spacing=8};panel.Children.Add(controls);var error=new InfoBar{Severity=InfoBarSeverity.Error,IsClosable=false};panel.Children.Add(error);ContentDialog? dialog=null;
  var entries=new List<(TransportParameter Spec,int Occurrence)>();TransportParameter? current=null;int occurrence=0;string[] values=[];Func<string>[] readers=[];TransportPoint? requestedPoint=null;
  void Store(){if(current==null)return;var updated=readers.Select(r=>r()).ToArray();draft=current.Write(draft,occurrence,updated);values=updated;}
  void ValidateDraft(){Store();foreach(var reference in VehicleScript.Tokens(draft).Where(VehicleAssets.IsModelReference))VehicleAssets.Resolve(reference.Value,Path.GetDirectoryName(lods.Keys.First())!,MediaFolder());}
  void Populate(string? key=null,int index=0){entries.Clear();foreach(var field in VehicleScript.Fields(draft)){var spec=specs.FirstOrDefault(p=>p.Key==field.Key);if(spec!=null)entries.Add((spec,field.Occurrence));}current=null;readers=[];list.ItemsSource=entries.Select(e=>UiLanguage.Text(e.Spec.Name)+(e.Spec.Repeated?" "+(e.Occurrence+1):"")).ToArray();list.SelectedIndex=entries.FindIndex(e=>e.Spec.Key==key&&e.Occurrence==index);if(list.SelectedIndex<0&&entries.Count>0)list.SelectedIndex=0;remove.IsEnabled=entries.Count>0;}
  void Show(){controls.Children.Clear();current=null;readers=[];if(list.SelectedIndex<0||list.SelectedIndex>=entries.Count)return;var entry=entries[list.SelectedIndex];var spec=entry.Spec.For(draft,entry.Occurrence);occurrence=entry.Occurrence;
   try{values=spec.Read(draft,occurrence);}catch(Exception ex){controls.Children.Add(new TextBlock{Text=spec.Key+": "+ex.Message,TextWrapping=TextWrapping.Wrap});return;}
   current=spec;var getters=new List<Func<string>>();for(int i=0;i<spec.Arguments.Length;i++){int at=i;var argument=spec.Arguments[i];if(argument.Fixed){getters.Add(()=>argument.Default);continue;}
    if(argument.Extension!=null){var file=new Button{Content=Path.GetFileName(values[i]),HorizontalAlignment=HorizontalAlignment.Stretch};ToolTipService.SetToolTip(file,values[i]);file.Click+=async(_,_)=>{try{string? path=argument.Extension==".nmf"?await Pick(".nmf",".obj"):await Pick(argument.Extension);if(path==null)return;if(Path.GetExtension(path).Equals(".obj",StringComparison.OrdinalIgnoreCase)){var imported=await Task.Run(()=>ObjImporter.Parse(File.ReadAllText(path)));if(imported.Model.Materials.Except(lods.First().Value.Materials,StringComparer.Ordinal).Any()&&!spec.Arguments.Any(a=>a.Extension==".mtl"))throw new InvalidDataException(UiLanguage.Text("Материалы части должны присутствовать в материале машины"));string native=SessionFile("part-"+Guid.NewGuid().ToString("N")+".nmf");File.WriteAllBytes(native,imported.Model.Data);path=native;}values[at]=path;file.Content=Path.GetFileName(path);ToolTipService.SetToolTip(file,path);}catch(Exception ex){error.Message=ex.Message;error.IsOpen=true;}};SettingRow(controls,argument.Label,file);getters.Add(()=>values[at]);}
    else if(argument.Mesh){var mesh=new ComboBox{IsEditable=true,Text=values[i],ItemsSource=lods.Values.SelectMany(m=>m.Nodes.Select(n=>n.Name)).Distinct().ToArray(),HorizontalAlignment=HorizontalAlignment.Stretch};SettingRow(controls,argument.Label,mesh);getters.Add(()=>mesh.Text);}
    else{var number=new NumberBox{Value=double.Parse(values[i],CultureInfo.InvariantCulture),SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Hidden,SmallChange=.1};SettingRow(controls,argument.Label,number);getters.Add(()=>number.Value.ToString("G9",CultureInfo.InvariantCulture));}
   }
   readers=getters.ToArray();FormatEditorNumbers(controls);
   // The main viewport uses the main body's coordinates. Joint-local points
   // retain numerical editing until their parent view is selected explicitly.
   for(int i=0;i+2<spec.Arguments.Length;i++){int start=i;if(!spec.Arguments[i].Label.EndsWith("X",StringComparison.Ordinal)||!spec.Arguments[i+1].Label.EndsWith("Y",StringComparison.Ordinal)||!spec.Arguments[i+2].Label.EndsWith("Z",StringComparison.Ordinal))continue;var place=new Button{Content=UiLanguage.Text("Разместить в окне модели")+(spec.Key.StartsWith("MIXER",StringComparison.Ordinal)?" "+(i==1?"A":"B"):"")};place.Click+=(_,_)=>{try{ValidateDraft();if(spec.Key.Contains("JOINT",StringComparison.Ordinal)||spec.Key=="LIGHT_BLINKER_TRAILER")_=JointModel(draft);requestedPoint=new(spec.Key,occurrence,start);dialog!.Hide();}catch(Exception ex){error.Message=ex.Message;error.IsOpen=true;}};controls.Children.Add(place);i+=2;}
  }
  list.SelectionChanged+=(_,_)=>{try{Store();Show();error.IsOpen=false;}catch(Exception ex){error.Message=ex.Message;error.IsOpen=true;}};
  add.Click+=(_,_)=>{try{Store();var spec=specs[addType.SelectedIndex];int count=VehicleScript.Fields(draft).Count(f=>f.Key==spec.Key);if(!spec.Repeated&&count>0){Populate(spec.Key);return;}draft=VehicleScript.Set(draft,spec.Key,count,string.Join(" ",spec.Arguments.Select(a=>a.Default.Length>0?a.Default:"select"+a.Extension)));Populate(spec.Key,count);}catch(Exception ex){error.Message=ex.Message;error.IsOpen=true;}};
  remove.Click+=(_,_)=>{if(list.SelectedIndex<0)return;var entry=entries[list.SelectedIndex];current=null;draft=VehicleScript.RemoveEntry(draft,entry.Spec.Key,entry.Occurrence);Populate();};
  dialog=VehicleDialog("Сборка и механизмы",panel);dialog.PrimaryButtonClick+=(_,e)=>{try{ValidateDraft();}catch(Exception ex){e.Cancel=true;error.Message=ex.Message;error.IsOpen=true;}};Populate();var result=await ShowDialog(dialog);if(result!=ContentDialogResult.Primary&&requestedPoint==null)return;vehicleConfiguration=draft;vehicleConfigurationEdited=true;dirty=true;
  if(requestedPoint!=null){transportPoint=requestedPoint;activeSmoke=activeCargo=activeLamp=-1;gizmoMode="move";gizmoVisible=gizmoEditing=true;}
  await Display();
 }
}
