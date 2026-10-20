using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Numerics;
using System.Text.Json;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 Vector3 mainPivot=Vector3.Zero,mainRotation=Vector3.Zero;
 readonly VehicleLight?[] lampSlots=new VehicleLight?[4];
 readonly Vector3[] lampRotations=new Vector3[4];
 readonly List<VehicleLight> extraLights=[];
 int activeLamp=-1;bool gizmoVisible,gizmoEditing,gizmoUpdating;string gizmoMode="move";
 readonly StackPanel gizmoActions=new(){Spacing=6,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(12),Visibility=Visibility.Collapsed};
 readonly StackPanel gizmoModes=new(){Spacing=6,Orientation=Orientation.Horizontal,Visibility=Visibility.Collapsed};
 readonly StackPanel gizmoNumbers=new(){Spacing=6,Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(12),Visibility=Visibility.Collapsed};
 readonly NumberBox[] gizmoCoordinates=new NumberBox[3];
 Button? gizmoRotateButton,pivotCentreButton,pivotApplyButton;
 void BuildGizmoControls(Grid viewport)
 {
  Button Action(string label,Func<Task> action){var b=new Button{Content=UiLanguage.Text(label),HorizontalAlignment=HorizontalAlignment.Stretch};b.Click+=async(_,_)=>{if(!busy)await RunCommand(action);};return b;}
  gizmoModes.Children.Add(Action("Move",async()=>{gizmoMode="move";await RefreshGizmo();}));gizmoRotateButton=Action("Rotate",async()=>{gizmoMode="rotate";await RefreshGizmo();});gizmoModes.Children.Add(gizmoRotateButton);gizmoActions.Children.Add(gizmoModes);
  gizmoActions.Children.Add(Action("Редактировать pivot",async()=>{gizmoEditing=!gizmoEditing;await RefreshGizmo();}));
  pivotCentreButton=Action("Автоцентрирование",async()=>{mainPivot=ModelOrientation.BottomPivot(lods.First().Value);dirty=true;await RefreshGizmo();});gizmoActions.Children.Add(pivotCentreButton);
  pivotApplyButton=Action("Исправить pivot",ApplyPivot);gizmoActions.Children.Add(pivotApplyButton);gizmoActions.Children.Add(Action("Готово",async()=>{gizmoVisible=false;gizmoEditing=false;await RefreshGizmo();}));
  for(int i=0;i<3;i++){int axis=i;var box=new NumberBox{Header="XYZ"[i].ToString(),Value=0,Width=108,SmallChange=.01,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact};box.ValueChanged+=async(_,_)=>{if(gizmoUpdating||busy||!gizmoVisible||!double.IsFinite(box.Value))return;var v=GizmoValue();v=axis switch{0=>new((float)box.Value,v.Y,v.Z),1=>new(v.X,(float)box.Value,v.Z),_=>new(v.X,v.Y,(float)box.Value)};if(!float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z))return;var before=CaptureSnapshot();SetGizmoValue(v);dirty=true;CommitHistory(before);await RefreshGizmo();};gizmoCoordinates[i]=box;gizmoNumbers.Children.Add(box);}
  viewport.Children.Add(gizmoActions);viewport.Children.Add(gizmoNumbers);
 }
 Vector3 GizmoPosition()=>activeLamp<0?mainPivot:lampSlots[activeLamp]?.Position??mainPivot;
 Vector3 GizmoRotation()=>activeLamp<0?mainRotation:lampRotations[activeLamp];
 Vector3 GizmoValue()=>gizmoMode=="move"?GizmoPosition():GizmoRotation();
 void SetGizmoValue(Vector3 value)
 {
  if(activeLamp<0){if(gizmoMode=="move")mainPivot=value;else mainRotation=value;}
  else if(gizmoMode=="move"){lampSlots[activeLamp]=new(activeLamp>=2,value);StoreLampSlots();}else lampRotations[activeLamp]=value;
 }
 async Task RefreshGizmo()
 {
  gizmoActions.Visibility=gizmoVisible?Visibility.Visible:Visibility.Collapsed;gizmoModes.Visibility=gizmoEditing?Visibility.Visible:Visibility.Collapsed;gizmoNumbers.Visibility=gizmoVisible&&gizmoEditing?Visibility.Visible:Visibility.Collapsed;
  // Native headlights are positional emitters; rotation belongs to model pivot.
  if(activeLamp>=0)gizmoMode="move";
  if(gizmoRotateButton!=null)gizmoRotateButton.Visibility=activeLamp<0?Visibility.Visible:Visibility.Collapsed;
  if(pivotCentreButton!=null)pivotCentreButton.Visibility=activeLamp<0?Visibility.Visible:Visibility.Collapsed;
  if(pivotApplyButton!=null)pivotApplyButton.Content=UiLanguage.Text(activeLamp<0?"Исправить pivot":"Применить");
  gizmoUpdating=true;var v=GizmoValue();gizmoCoordinates[0].Value=v.X;gizmoCoordinates[1].Value=v.Y;gizmoCoordinates[2].Value=v.Z;gizmoUpdating=false;
  if(!ready)return;var p=GizmoPosition();var r=GizmoRotation();await view.ExecuteScriptAsync("gizmo("+JsonSerializer.Serialize(new{visible=gizmoVisible,editing=gizmoEditing,mode=gizmoMode,position=new[]{p.X,p.Y,p.Z},rotation=new[]{r.X,r.Y,r.Z}})+")");
 }
 async Task StartPivot(){activeLamp=-1;gizmoVisible=true;gizmoEditing=false;gizmoMode="move";await RefreshGizmo();}
 void ReceiveGizmo(JsonElement element)
 {
  if(busy||!gizmoVisible||!element.TryGetProperty("position",out var p)||!element.TryGetProperty("rotation",out var r)||p.GetArrayLength()!=3||r.GetArrayLength()!=3)return;
  string? phase=element.TryGetProperty("phase",out var phaseValue)?phaseValue.GetString():null;if(phase=="begin"){dragBefore=CaptureSnapshot();return;}
  var position=p.EnumerateArray().Select(x=>x.GetSingle()).ToArray();var rotation=r.EnumerateArray().Select(x=>x.GetSingle()).ToArray();if(position.Concat(rotation).Any(x=>!float.IsFinite(x)||MathF.Abs(x)>1e7f))return;
  if(activeLamp<0){mainPivot=new(position[0],position[1],position[2]);mainRotation=new(rotation[0],rotation[1],rotation[2]);}else{lampSlots[activeLamp]=new(activeLamp>=2,new(position[0],position[1],position[2]));lampRotations[activeLamp]=new(rotation[0],rotation[1],rotation[2]);StoreLampSlots();}
  gizmoUpdating=true;var values=gizmoMode=="move"?position:rotation;for(int i=0;i<3;i++)gizmoCoordinates[i].Value=values[i];gizmoUpdating=false;dirty=true;if(phase=="end"&&dragBefore!=null){CommitHistory(dragBefore);dragBefore=null;}
 }
 Matrix4x4 PivotFrame()=>Matrix4x4.CreateRotationX(mainRotation.X*MathF.PI/180)*Matrix4x4.CreateRotationY(mainRotation.Y*MathF.PI/180)*Matrix4x4.CreateRotationZ(mainRotation.Z*MathF.PI/180);
 async Task ApplyPivot()
 {
  if(activeLamp>=0){gizmoVisible=false;await RefreshGizmo();return;}
  if(!Matrix4x4.Invert(PivotFrame(),out var inverse))throw new InvalidDataException("Invalid pivot");var transform=Matrix4x4.CreateTranslation(-mainPivot)*inverse;
  string? configuration=vehicleConfiguration==null?null:VehicleLights.Transform(vehicleConfiguration,transform);var models=await Task.Run(()=>lods.ToDictionary(p=>p.Key,p=>ModelOrientation.Apply(p.Value,transform)));
  foreach(var p in models)lods[p.Key]=p.Value;vehicleConfiguration=configuration;vehicleConfigurationEdited|=configuration!=null;documentTransform*=transform;mainPivot=mainRotation=Vector3.Zero;ReadLampSlots();dirty=true;await Display();await RefreshGizmo();
 }
 void ReadLampSlots()
 {
  Array.Clear(lampSlots);Array.Clear(lampRotations);extraLights.Clear();if(vehicleConfiguration==null)return;
  foreach(bool rear in new[]{false,true}){var values=VehicleLights.Read(vehicleConfiguration).Where(l=>l.Rear==rear).OrderBy(l=>l.Position.X).ToArray();for(int i=0;i<Math.Min(2,values.Length);i++)lampSlots[(rear?2:0)+i]=values[i];extraLights.AddRange(values.Skip(2));}
 }
 void StoreLampSlots(){if(vehicleConfiguration==null)return;vehicleConfiguration=VehicleLights.Write(vehicleConfiguration,lampSlots.Where(l=>l!=null).Select(l=>l!).Concat(extraLights));vehicleConfigurationEdited=true;}
 async Task ChooseLamp()
 {
  if(!await EnsureVehicleScript())return;var panel=new StackPanel{Spacing=8,Width=280};var dialog=VehicleDialog("Фары и фонари",panel);dialog.PrimaryButtonText="";int selected=-1;
  string[] labels=["Левая фара","Правая фара","Левый фонарь","Правый фонарь"];
  for(int i=0;i<4;i++){int index=i;var button=new Button{Content=UiLanguage.Text(labels[i]),HorizontalAlignment=HorizontalAlignment.Stretch};button.Click+=(_,_)=>{selected=index;dialog.Hide();};panel.Children.Add(button);}
  await ShowDialog(dialog);if(selected<0)return;
  int paired=selected^1;if(lampSlots[paired]!=null){
   var axes=new ComboBox{ItemsSource=new[]{"X","Y","Z"},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};var mirror=VehicleDialog("Настроить симметрию?",axes);mirror.PrimaryButtonText=UiLanguage.Text("Отразить");mirror.CloseButtonText=UiLanguage.Text("Вручную");
   if(await ShowDialog(mirror)==ContentDialogResult.Primary){var frame=PivotFrame();Matrix4x4.Invert(frame,out var inv);var local=Vector3.TransformNormal(lampSlots[paired]!.Position-mainPivot,inv);local=axes.SelectedIndex switch{0=>new(-local.X,local.Y,local.Z),1=>new(local.X,-local.Y,local.Z),_=>new(local.X,local.Y,-local.Z)};lampSlots[selected]=new(selected>=2,mainPivot+Vector3.TransformNormal(local,frame));}
  }
  lampSlots[selected]??=new(selected>=2,mainPivot);activeLamp=selected;gizmoVisible=gizmoEditing=true;gizmoMode="move";dirty=true;StoreLampSlots();await RefreshGizmo();
 }
}
