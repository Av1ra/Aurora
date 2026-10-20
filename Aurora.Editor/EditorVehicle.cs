using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Numerics;

namespace Aurora.Editor;
sealed partial class EditorWindow
{
 string? vehicleConfiguration;
 bool vehicleConfigurationEdited;
 Matrix4x4 documentTransform=Matrix4x4.Identity;
 void ResetVehicle(string? script)
 {
  NewHistoryDocument();
  activeSmoke=-1;activeCargo=-1;cargoModel=null;cargoMtl=cargoModelPath=null;showCargo=false;cargoFill=1;previewSettings=new();
  vehicleConfiguration=script!=null&&File.Exists(script)?File.ReadAllText(script):null;
  vehicleConfigurationEdited=false;documentTransform=Matrix4x4.Identity;
  mainPivot=mainRotation=Vector3.Zero;gizmoVisible=false;ReadLampSlots();
 }
 void RegisterVehicleCommands()
 {
  editorCommands["Починить ориентацию…"]=(StartPivot,()=>HasModel);
  editorCommands["Фары и фонари"]=(ChooseLamp,()=>HasModel);
  editorCommands["Выбрать script.ini…"]=(ChooseVehicleScript,()=>HasModel);
  editorCommands["Назначить фары…"]=(async()=>await AssignLight(false),()=>HasModel&&objects.SelectedItem!=null);
  editorCommands["Назначить задние фонари…"]=(async()=>await AssignLight(true),()=>HasModel&&objects.SelectedItem!=null);
  editorCommands["Настроить огни…"]=(EditLights,()=>HasModel&&vehicleConfiguration!=null);
 }
 ContentDialog VehicleDialog(string title,FrameworkElement panel)=>new(){Title=UiLanguage.Text(title),Content=new ScrollViewer{Content=panel,MaxHeight=Math.Max(180,((FrameworkElement)Content).ActualHeight-220)},PrimaryButtonText=UiLanguage.Text("Применить"),CloseButtonText=UiLanguage.Text("Отмена"),DefaultButton=ContentDialogButton.Close,XamlRoot=((FrameworkElement)Content).XamlRoot};
 void ValidateVehicleDialog(ContentDialog dialog,Panel panel,Action validate)
 {
  var error=new InfoBar{Severity=InfoBarSeverity.Error,IsClosable=false};panel.Children.Add(error);
  dialog.PrimaryButtonClick+=(_,e)=>{try{validate();}catch(Exception ex){e.Cancel=true;error.Message=UiLanguage.Text(ex.Message);error.IsOpen=true;}};
 }
 async Task ChooseVehicleScript()
 {
  var path=await Pick(".ini");if(path==null)return;
  string source=File.ReadAllText(path);source=VehicleLights.Write(source,VehicleLights.Read(source));
  vehicleConfiguration=VehicleLights.Transform(source,documentTransform);ReadLampSlots();vehicleConfigurationEdited=true;dirty=true;SetStatus("Конфигурация огней загружена");
 }
 async Task<bool> EnsureVehicleScript()
 {
  if(vehicleConfiguration!=null)return true;
  SetStatus("Выберите script.ini транспортного средства.");await ChooseVehicleScript();return vehicleConfiguration!=null;
 }
 NumberBox Coordinate(string label,float value,Panel panel)
 {
  var box=new NumberBox{Header=label,Value=value,SmallChange=.01,LargeChange=.1,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact,MinWidth=100,HorizontalAlignment=HorizontalAlignment.Stretch};
  panel.Children.Add(box);return box;
 }
 static Vector3 LightPosition(NumberBox x,NumberBox y,NumberBox z)
 {
  var p=new Vector3((float)x.Value,(float)y.Value,(float)z.Value);
  if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z))throw new InvalidDataException("Некорректные координаты огня.");return p;
 }
 async Task AssignLight(bool rear)
 {
  if(objects.SelectedItem is not string name)return;
  var node=CurrentModel().Nodes.Single(n=>n.Name==name);
  float Centre(int axis)=> (node.Positions.Where((_,i)=>i%3==axis).Min()+node.Positions.Where((_,i)=>i%3==axis).Max())/2;
  if(!await EnsureVehicleScript())return;
  var panel=new StackPanel{Spacing=10,Width=340};panel.Children.Add(new TextBlock{Text=name,TextWrapping=TextWrapping.Wrap});
  var row=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};panel.Children.Add(row);
  var x=Coordinate("X",Centre(0),row);var y=Coordinate("Y",Centre(1),row);var z=Coordinate("Z",Centre(2),row);
  var mirror=new CheckBox{Content=UiLanguage.Text("Добавить симметричный огонь по X")};panel.Children.Add(mirror);
  var dialog=VehicleDialog(rear?"Назначить задние фонари…":"Назначить фары…",panel);
  ValidateVehicleDialog(dialog,panel,()=>LightPosition(x,y,z));
  if(await ShowDialog(dialog)!=ContentDialogResult.Primary)return;
  var position=LightPosition(x,y,z);var values=VehicleLights.Read(vehicleConfiguration!);values.Add(new(rear,position));
  if(mirror.IsChecked==true&&MathF.Abs(position.X)>.00001f)values.Add(new(rear,new(-position.X,position.Y,position.Z)));
  vehicleConfiguration=VehicleLights.Write(vehicleConfiguration!,values);vehicleConfigurationEdited=true;dirty=true;SetStatus("Игровые огни назначены",values.Count.ToString());
 }
 async Task EditLights()
 {
  if(vehicleConfiguration==null)return;
  var panel=new StackPanel{Spacing=8,Width=390};var entries=new List<(bool Rear,NumberBox X,NumberBox Y,NumberBox Z,CheckBox Remove)>();
  foreach(var light in VehicleLights.Read(vehicleConfiguration)){
   var group=new StackPanel{Spacing=4};panel.Children.Add(group);
   group.Children.Add(new TextBlock{Text=UiLanguage.Text(light.Rear?"Задний фонарь":"Фара")});
   var row=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6};group.Children.Add(row);
   var x=Coordinate("X",light.Position.X,row);var y=Coordinate("Y",light.Position.Y,row);var z=Coordinate("Z",light.Position.Z,row);
   var remove=new CheckBox{Content=UiLanguage.Text("Удалить")};group.Children.Add(remove);entries.Add((light.Rear,x,y,z,remove));
  }
  if(entries.Count==0){SetStatus("Огни ещё не назначены.");return;}
  var dialog=VehicleDialog("Настроить огни…",panel);ValidateVehicleDialog(dialog,panel,()=>{foreach(var e in entries.Where(e=>e.Remove.IsChecked!=true))LightPosition(e.X,e.Y,e.Z);});
  if(await ShowDialog(dialog)!=ContentDialogResult.Primary)return;
  vehicleConfiguration=VehicleLights.Write(vehicleConfiguration,entries.Where(e=>e.Remove.IsChecked!=true).Select(e=>new VehicleLight(e.Rear,LightPosition(e.X,e.Y,e.Z))).ToArray());vehicleConfigurationEdited=true;dirty=true;SetStatus("Игровые огни сохранены");
 }
 async Task FixOrientation()
 {
  var panel=new StackPanel{Spacing=10,Width=360};
  panel.Children.Add(new TextBlock{Text=UiLanguage.Text("В игре: перед +Z, верх +Y. Изменение применяется ко всем LOD."),TextWrapping=TextWrapping.Wrap});
  var labels=new[]{"+Z","−Z","+X","−X","+Y","−Y"};
  var front=new ComboBox{Header=UiLanguage.Text("Сейчас перед модели"),ItemsSource=labels,SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};
  var up=new ComboBox{Header=UiLanguage.Text("Сейчас верх модели"),ItemsSource=labels,SelectedIndex=4,HorizontalAlignment=HorizontalAlignment.Stretch};panel.Children.Add(front);panel.Children.Add(up);
  var centre=new CheckBox{Content=UiLanguage.Text("Центр pivot по X и Z"),IsChecked=true};var ground=new CheckBox{Content=UiLanguage.Text("Поставить нижнюю точку на Y = 0")};panel.Children.Add(centre);panel.Children.Add(ground);
  var error=new InfoBar{Severity=InfoBarSeverity.Error,IsClosable=false};panel.Children.Add(error);
  var dialog=VehicleDialog("Починить ориентацию…",panel);Matrix4x4 transform=default;Dictionary<string,NativeModel>? transformed=null;string? script=null;
  dialog.PrimaryButtonClick+=async(_,e)=>{
   var deferral=e.GetDeferral();
   try{
    int f=front.SelectedIndex,u=up.SelectedIndex;bool c=centre.IsChecked==true,g=ground.IsChecked==true;
    await Task.Run(()=>{
     transform=ModelOrientation.Transform(lods.First().Value,f,u,c,g);
     script=vehicleConfiguration==null?null:VehicleLights.Transform(vehicleConfiguration,transform);
     transformed=lods.ToDictionary(pair=>pair.Key,pair=>ModelOrientation.Apply(pair.Value,transform));
    });
   }catch(Exception ex){e.Cancel=true;error.Message=UiLanguage.Text(ex.Message);error.IsOpen=true;}
   finally{deferral.Complete();}
  };
  if(await ShowDialog(dialog)!=ContentDialogResult.Primary||transformed==null)return;
  foreach(var pair in transformed)lods[pair.Key]=pair.Value;
  vehicleConfiguration=script;vehicleConfigurationEdited|=script!=null;documentTransform*=transform;dirty=true;SetStatus("Ориентация и pivot исправлены");await Display();
 }
}
