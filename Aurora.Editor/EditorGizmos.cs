using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Numerics;
using System.Text.Json;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 EditorSnapshot? toolStart;
 Vector3 mainPivot=Vector3.Zero,mainRotation=Vector3.Zero;
 readonly VehicleLight?[] lampSlots=new VehicleLight?[4];
 readonly Vector3[] lampRotations=new Vector3[4];
 readonly List<VehicleLight> extraLights=[];
 int activeLamp=-1;bool gizmoVisible,gizmoEditing,gizmoUpdating;string gizmoMode="move";
 readonly StackPanel gizmoActions=new(){Spacing=6,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(12),Visibility=Visibility.Collapsed};
 readonly StackPanel gizmoModes=new(){Spacing=6,Orientation=Orientation.Horizontal,Visibility=Visibility.Collapsed};
 readonly StackPanel gizmoNumbers=new(){Spacing=6,Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(12),Visibility=Visibility.Collapsed};
 readonly NumberBox[] gizmoCoordinates=new NumberBox[3];
 Button? pivotResetButton,gizmoRotateButton,gizmoScaleButton,pivotCentreButton,pivotApplyButton,pivotEditButton;
 readonly ComboBox rotationStep=new(){Header=UiLanguage.Text("Шаг вращения, °"),IsEditable=true,ItemsSource=new[]{"0.1","0.5","1","5","10","45","90"},SelectedIndex=0,MinWidth=120};
 double RotationStep=>double.TryParse(rotationStep.Text.Replace(',','.'),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double v)&&double.IsFinite(v)&&v>0&&v<=360?v:.1;
 readonly StackPanel cargoScaleModes=new(){Orientation=Orientation.Horizontal,Spacing=6,Visibility=Visibility.Collapsed};
 readonly RadioButton cargoUniformButton=new(){Content=UiLanguage.Text("Все оси"),GroupName="cargo-scale"},cargoAxisButton=new(){Content=UiLanguage.Text("По осям"),GroupName="cargo-scale"};
 readonly ComboBox cargoEndChoice=new(){ItemsSource=new[]{UiLanguage.Text("Пустой кузов"),UiLanguage.Text("Полный кузов")},Visibility=Visibility.Collapsed};
 void BuildGizmoControls(Grid viewport)
 {
  Button Action(string label,Func<Task> action){var b=new Button{Content=UiLanguage.Text(label),HorizontalAlignment=HorizontalAlignment.Stretch};b.Click+=async(_,_)=>{if(!busy)await RunCommand(action);};return b;}
  gizmoModes.Children.Add(Action("Move",async()=>{gizmoMode="move";await RefreshGizmo();}));gizmoRotateButton=Action("Rotate",async()=>{gizmoMode="rotate";await RefreshGizmo();});gizmoModes.Children.Add(gizmoRotateButton);gizmoScaleButton=Action("Scale",async()=>{gizmoMode="scale";await RefreshGizmo();});gizmoModes.Children.Add(gizmoScaleButton);gizmoActions.Children.Add(gizmoModes);gizmoActions.Children.Add(rotationStep);rotationStep.SelectionChanged+=async(_,_)=>{if(!gizmoUpdating)await RefreshGizmo();};rotationStep.LostFocus+=async(_,_)=>await RefreshGizmo();
  cargoScaleModes.Children.Add(cargoUniformButton);cargoScaleModes.Children.Add(cargoAxisButton);cargoUniformButton.Checked+=async(_,_)=>{if(!gizmoUpdating){cargoUniform=true;await RefreshGizmo();}};cargoAxisButton.Checked+=async(_,_)=>{if(!gizmoUpdating){cargoUniform=false;await RefreshGizmo();}};gizmoActions.Children.Add(cargoScaleModes);gizmoActions.Children.Add(cargoEndChoice);cargoEndChoice.SelectionChanged+=async(_,_)=>{if(gizmoUpdating||activeCargo<0||busy)return;cargoEndpoint=cargoEndChoice.SelectedIndex;cargoFill=cargoEndpoint;await Display();};
  pivotEditButton=Action("Редактировать pivot",async()=>{gizmoEditing=!gizmoEditing;await RefreshGizmo();});gizmoActions.Children.Add(pivotEditButton);
  pivotCentreButton=Action("Автоцентрирование",async()=>{mainPivot=ModelOrientation.BottomPivot(lods.First().Value);dirty=true;await RefreshGizmo();});gizmoActions.Children.Add(pivotCentreButton);
  gizmoActions.Children.Add(Action("Отменить действие",async()=>{if(history.CanUndo)await TraverseHistory(false);}));
  gizmoActions.Children.Add(Action("Отмена",async()=>{if(toolStart is EditorSnapshot state)await RestoreSnapshot(state);CloseAnimationTimeline();gizmoVisible=false;transportPoint=null;await Display();}));
  pivotResetButton=Action("Сбросить pivot",async()=>{var source=savedSnapshot??toolStart;if(source==null)return;if(transportPoint!=null){string key=transportPoint.Key;int occurrence=transportPoint.Occurrence;var old=source.Vehicle==null?null:VehicleScript.Fields(source.Vehicle).FirstOrDefault(f=>f.Key==key&&f.Occurrence==occurrence);if(old!=null)vehicleConfiguration=VehicleScript.Set(vehicleConfiguration!,key,occurrence,old.Value);var rotations=JsonSerializer.Deserialize<Dictionary<string,float[]>>(source.MechanismRotations)??[];if(rotations.TryGetValue(MechanismIdentity,out var rotation))mechanismRotations[MechanismIdentity]=rotation;else mechanismRotations.Remove(MechanismIdentity);}else{await RestorePivotFrame(source);mainPivot=source.Pivot;mainRotation=source.Rotation;}dirty=true;await RefreshGizmo();});gizmoActions.Children.Add(pivotResetButton);
  pivotApplyButton=Action("Исправить pivot",ApplyPivot);gizmoActions.Children.Add(pivotApplyButton);gizmoActions.Children.Add(Action("Готово",async()=>{CloseAnimationTimeline();gizmoVisible=false;gizmoEditing=false;transportPoint=null;await Display();}));
  for(int i=0;i<3;i++){int axis=i;var box=new NumberBox{Header="XYZ"[i].ToString(),Value=0,Width=108,SmallChange=.1,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Hidden,NumberFormatter=new Windows.Globalization.NumberFormatting.DecimalFormatter{FractionDigits=1,IntegerDigits=1,IsGrouped=false,NumberRounder=new Windows.Globalization.NumberFormatting.IncrementNumberRounder{Increment=.1,RoundingAlgorithm=Windows.Globalization.NumberFormatting.RoundingAlgorithm.RoundHalfUp}}};box.ValueChanged+=async(_,_)=>{if(gizmoUpdating||busy||!gizmoVisible||!double.IsFinite(box.Value))return;if((activeCargo>=0||animationEditing)&&gizmoMode=="scale"&&box.Value<(animationEditing?.0001:0)){await RefreshGizmo();return;}var v=GizmoValue();v=axis switch{0=>new((float)box.Value,v.Y,v.Z),1=>new(v.X,(float)box.Value,v.Z),_=>new(v.X,v.Y,(float)box.Value)};if(!float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z))return;var before=CaptureSnapshot();SetGizmoValue(v);dirty=true;CommitHistory(before);await RefreshGizmo();};gizmoCoordinates[i]=box;gizmoNumbers.Children.Add(box);}
  BuildMechanismControls();BuildAnimationControls(viewport);viewport.Children.Add(cargoPalette);viewport.Children.Add(gizmoActions);viewport.Children.Add(gizmoNumbers);
 }
 Vector3 GizmoPosition()=>animationEditing?AnimationPivot+(animationAxisEditing?Vector3.Zero:AnimationVector(animationPose.Position)):transportPoint!=null?TransportPointPosition():activeSmoke>=0?SmokePosition():activeCargo>=0?CargoPosition():activeLamp<0?mainPivot:lampSlots[activeLamp]?.Position??mainPivot;
 Vector3 GizmoRotation()=>animationEditing?AnimationVector(animationAxisEditing?ActiveTrack?.AxisRotation??[0,0,0]:animationPose.Rotation):transportPoint!=null&&MechanismAxis.Supports(transportPoint.Key)?MechanismRotation():transportPoint!=null?TransportRotation():activeSmoke>=0?Vector3.Zero:activeCargo>=0?CargoRotation():activeLamp<0?mainRotation:lampRotations[activeLamp];
 Vector3 GizmoValue()=>animationEditing&&gizmoMode=="scale"?AnimationVector(animationPose.Scale):activeCargo>=0&&gizmoMode=="scale"?CargoScale():gizmoMode=="move"?GizmoPosition():GizmoRotation();
 void SetGizmoValue(Vector3 value)
 {
  if(animationEditing){SetAnimationTool(value);return;}
  if(transportPoint!=null){if(gizmoMode=="rotate")SetTransportRotation(value);else SetTransportPoint(value);return;}if(activeSmoke>=0){SetSmokePosition(value);return;}
  if(activeCargo>=0){var scale=CargoScale();if(gizmoMode=="scale"){int axis=value.X!=scale.X?0:value.Y!=scale.Y?1:2;float old=axis==0?scale.X:axis==1?scale.Y:scale.Z,next=axis==0?value.X:axis==1?value.Y:value.Z;if(next<0)throw new InvalidDataException(UiLanguage.Text("Масштаб не может быть отрицательным"));if(cargoUniform)value=old>1e-6f?scale*(next/old):new Vector3(next);}SetCargoTool(gizmoMode=="move"?value:CargoPosition(),gizmoMode=="rotate"?value:CargoRotation(),gizmoMode=="scale"?value:scale);return;}
  if(activeLamp<0){if(gizmoMode=="move")mainPivot=value;else mainRotation=value;}
  else if(gizmoMode=="move"){lampSlots[activeLamp]=new(activeLamp>=2,value);StoreLampSlots();}else lampRotations[activeLamp]=value;
 }
 async Task RefreshGizmo()
 {
  RefreshCargoPalette();RefreshMechanismControls();if(animationEditing)await RefreshAnimationPose();else await RefreshMechanismPose();
  gizmoActions.Visibility=gizmoVisible?Visibility.Visible:Visibility.Collapsed;gizmoModes.Visibility=gizmoEditing?Visibility.Visible:Visibility.Collapsed;gizmoNumbers.Visibility=gizmoVisible&&gizmoEditing?Visibility.Visible:Visibility.Collapsed;
  // Native headlights are positional emitters; rotation belongs to model pivot.
  if(activeLamp>=0||activeSmoke>=0)gizmoMode="move";
  if(pivotResetButton!=null)pivotResetButton.Visibility=activeLamp<0&&activeCargo<0&&activeSmoke<0&&!animationEditing?Visibility.Visible:Visibility.Collapsed;
  if(gizmoRotateButton!=null)gizmoRotateButton.Visibility=(transportPoint==null||transportPoint.Rotation>=0||MechanismAxis.Supports(transportPoint.Key))&&!(activeCargo>=0&&VehicleBoxFrame)&&activeLamp<0&&activeSmoke<0?Visibility.Visible:Visibility.Collapsed;
  if(pivotCentreButton!=null)pivotCentreButton.Visibility=!animationEditing&&transportPoint==null&&activeLamp<0&&activeCargo<0&&activeSmoke<0?Visibility.Visible:Visibility.Collapsed;
  if(pivotApplyButton!=null)pivotApplyButton.Visibility=animationEditing?Visibility.Collapsed:Visibility.Visible;if(pivotEditButton!=null)pivotEditButton.Visibility=animationEditing?Visibility.Collapsed:Visibility.Visible;
  if(pivotApplyButton!=null)pivotApplyButton.Content=UiLanguage.Text(transportPoint==null&&activeLamp<0&&activeCargo<0&&activeSmoke<0?"Исправить pivot":"Применить");
  if(gizmoScaleButton!=null)gizmoScaleButton.Visibility=activeCargo>=0||animationEditing&&!animationAxisEditing?Visibility.Visible:Visibility.Collapsed;
  if(pivotEditButton!=null)pivotEditButton.Visibility=transportPoint==null&&activeCargo<0&&activeSmoke<0?Visibility.Visible:Visibility.Collapsed;
  cargoScaleModes.Visibility=activeCargo>=0&&gizmoMode=="scale"?Visibility.Visible:Visibility.Collapsed;cargoEndChoice.Visibility=activeCargo>=0&&!VehicleBoxFrame?Visibility.Visible:Visibility.Collapsed;
  gizmoUpdating=true;foreach(var box in gizmoCoordinates){box.Minimum=(activeCargo>=0||animationEditing)&&gizmoMode=="scale"?(animationEditing?.0001:0):-1e6;box.Maximum=(activeCargo>=0||animationEditing)&&gizmoMode=="scale"?1e6:1e6;}cargoUniformButton.IsChecked=cargoUniform;cargoAxisButton.IsChecked=!cargoUniform;cargoEndChoice.SelectedIndex=cargoEndpoint;var v=GizmoValue();gizmoCoordinates[0].Value=v.X;gizmoCoordinates[1].Value=v.Y;gizmoCoordinates[2].Value=v.Z;gizmoUpdating=false;
  if(!ready)return;await RefreshLighting();var p=GizmoPosition();var r=GizmoRotation();await view.ExecuteScriptAsync("gizmo("+JsonSerializer.Serialize(new{visible=gizmoVisible,editing=gizmoEditing,mode=gizmoMode,position=new[]{p.X,p.Y,p.Z},rotation=new[]{r.X,r.Y,r.Z},scale=animationEditing?animationPose.Scale:activeCargo>=0?new[]{CargoScale().X,CargoScale().Y,CargoScale().Z}:new[]{1f,1f,1f},cargo=activeCargo,uniformScale=cargoUniform,basis=animationEditing&&!animationAxisEditing?ActiveTrack?.AxisRotation??[0,0,0]:new float[]{0,0,0},rotationStep=RotationStep,axis=MechanismAxisPoints()})+")");
 }
 async Task StartPivot(){CloseAnimationTimeline();toolStart=CaptureSnapshot();cabinAssemblyMenu.IsChecked=false;showCargo=false;transportPoint=null;activeSmoke=-1;activeCargo=-1;activeLamp=-1;gizmoVisible=true;gizmoEditing=false;gizmoMode="move";await Display();}
 void ReceiveGizmo(JsonElement element)
 {
  if(busy||!gizmoVisible||!element.TryGetProperty("position",out var p)||!element.TryGetProperty("rotation",out var r)||p.GetArrayLength()!=3||r.GetArrayLength()!=3)return;
  string? phase=element.TryGetProperty("phase",out var phaseValue)?phaseValue.GetString():null;if(phase=="begin"){dragBefore=CaptureSnapshot();return;}
  var position=p.EnumerateArray().Select(x=>x.GetSingle()).ToArray();var rotation=r.EnumerateArray().Select(x=>x.GetSingle()).ToArray();if(position.Concat(rotation).Any(x=>!float.IsFinite(x)||MathF.Abs(x)>1e7f))return;
  float[]? scale=element.TryGetProperty("scale",out var scaleJson)&&scaleJson.GetArrayLength()==3?scaleJson.EnumerateArray().Select(x=>x.GetSingle()).ToArray():null;
  if(animationEditing){if(gizmoMode=="scale"&&(scale==null||scale.Any(x=>!float.IsFinite(x)||x<=0)))return;SetAnimationTool(new(position[0],position[1],position[2]));if(gizmoMode=="rotate")SetAnimationTool(new(rotation[0],rotation[1],rotation[2]));if(gizmoMode=="scale"&&scale!=null)SetAnimationTool(new(scale[0],scale[1],scale[2]));}
  else if(transportPoint!=null){SetTransportPoint(new(position[0],position[1],position[2]));SetTransportRotation(new(rotation[0],rotation[1],rotation[2]));}else if(activeSmoke>=0)SetSmokePosition(new(position[0],position[1],position[2]));
  else if(activeCargo>=0){if(scale==null||scale.Any(x=>!float.IsFinite(x)||x<0||x>1e6f))return;SetCargoTool(new(position[0],position[1],position[2]),new(rotation[0],rotation[1],rotation[2]),new(scale[0],scale[1],scale[2]));}
  else if(activeLamp<0){mainPivot=new(position[0],position[1],position[2]);mainRotation=new(rotation[0],rotation[1],rotation[2]);}else{lampSlots[activeLamp]=new(activeLamp>=2,new(position[0],position[1],position[2]));lampRotations[activeLamp]=new(rotation[0],rotation[1],rotation[2]);StoreLampSlots();}
  gizmoUpdating=true;var values=gizmoMode=="scale"&&scale!=null?scale:gizmoMode=="move"?position:rotation;for(int i=0;i<3;i++)gizmoCoordinates[i].Value=values[i];gizmoUpdating=false;dirty=true;if(phase=="end"&&dragBefore!=null){CommitHistory(dragBefore);dragBefore=null;}
 }
 Matrix4x4 PivotFrame()=>Matrix4x4.CreateRotationX(mainRotation.X*MathF.PI/180)*Matrix4x4.CreateRotationY(mainRotation.Y*MathF.PI/180)*Matrix4x4.CreateRotationZ(mainRotation.Z*MathF.PI/180);
 async Task ApplyPivot()
 {
  if(transportPoint!=null||activeCargo>=0||activeLamp>=0||activeSmoke>=0){gizmoVisible=false;transportPoint=null;await Display();return;}
  if(animationClips.Length>0)throw new InvalidDataException("Настройте общий pivot до создания анимаций; ось отдельной анимации меняется в её редакторе");
  if(!Matrix4x4.Invert(PivotFrame(),out var inverse))throw new InvalidDataException("Invalid pivot");var transform=Matrix4x4.CreateTranslation(-mainPivot)*inverse;
  await TransformDocumentFrame(transform);mainPivot=mainRotation=Vector3.Zero;dirty=true;await Display();await RefreshGizmo();
 }
 async Task RestorePivotFrame(EditorSnapshot source)
 {
  if(documentTransform==source.Transform)return;
  if(animationClips.Length>0)throw new InvalidDataException("Reset the vehicle pivot before creating animation clips, or undo the pivot correction in history");
  if(!Matrix4x4.Invert(documentTransform,out var inverse))throw new InvalidDataException("Invalid document frame");
  await TransformDocumentFrame(inverse*source.Transform);documentTransform=source.Transform;await Display();
 }
 async Task TransformDocumentFrame(Matrix4x4 transform)
 {
  string? configuration=vehicleConfiguration==null?null:VehicleLights.Transform(vehicleConfiguration,transform);
  var rotations=mechanismRotations.ToDictionary(p=>p.Key,p=>{var r=p.Value;var matrix=Matrix4x4.CreateRotationX(r[0]*MathF.PI/180)*Matrix4x4.CreateRotationY(r[1]*MathF.PI/180)*Matrix4x4.CreateRotationZ(r[2]*MathF.PI/180)*transform;var d=Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitX,matrix));return new[]{0f,-MathF.Asin(Math.Clamp(d.Z,-1,1))*180/MathF.PI,MathF.Atan2(d.Y,d.X)*180/MathF.PI};});
  var models=await Task.Run(()=>lods.ToDictionary(p=>p.Key,p=>ModelOrientation.Apply(p.Value,transform)));
  foreach(var p in models)lods[p.Key]=p.Value;vehicleConfiguration=configuration;vehicleConfigurationEdited|=configuration!=null;mechanismRotations=rotations;documentTransform*=transform;ReadLampSlots();

 }
 void ReadLampSlots()
 {
  Array.Clear(lampSlots);Array.Clear(lampRotations);extraLights.Clear();if(vehicleConfiguration==null)return;
  foreach(bool rear in new[]{false,true}){var values=VehicleLights.Read(vehicleConfiguration).Where(l=>l.Rear==rear).OrderByDescending(l=>VehicleLights.IsLeft(l.Position,mainPivot,PivotFrame())).ThenByDescending(l=>l.Position.X).ToArray();for(int i=0;i<Math.Min(2,values.Length);i++)lampSlots[(rear?2:0)+i]=values[i];extraLights.AddRange(values.Skip(2));}
 }
 // A script can contain more than four indicator emitters. Preserve all
 // existing occurrences and create only the missing corner requested by user.
 async Task SelectIndicator(bool front,bool left)
 {
  if(!await EnsureVehicleScript())return;
  var tokens=VehicleScript.Tokens(vehicleConfiguration!).ToArray();
  var candidates=VehicleScript.Fields(vehicleConfiguration!).Where(f=>f.Key=="LIGHT_BLINKER").ToArray();
  int occurrence=-1;
  foreach(var field in candidates){
   var coords=tokens.Where(t=>t.Key==field.Key&&t.Occurrence==field.Occurrence).Take(3).Select(t=>t.Value).ToArray();
   if(coords.Length!=3||coords.Any(x=>!float.TryParse(x,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out _)))continue;
   var v=new Vector3(float.Parse(coords[0],System.Globalization.CultureInfo.InvariantCulture),float.Parse(coords[1],System.Globalization.CultureInfo.InvariantCulture),float.Parse(coords[2],System.Globalization.CultureInfo.InvariantCulture));
   if(VehicleLights.IsLeft(v,mainPivot,PivotFrame())!=left)continue;
   if(!Matrix4x4.Invert(PivotFrame(),out var inverse))continue;
   if((Vector3.TransformNormal(v-mainPivot,inverse).Z>=0)==front){occurrence=field.Occurrence;break;}
  }
  toolStart=CaptureSnapshot();
  if(occurrence<0){
   occurrence=candidates.Length;
   var bounds=ModelOrientation.Bounds(lods.First().Value);
   var v=new Vector3(left?bounds.Max.X:bounds.Min.X,bounds.Min.Y+(bounds.Max.Y-bounds.Min.Y)*.55f,front?bounds.Max.Z:bounds.Min.Z);
   vehicleConfiguration=VehicleScript.SetArguments(vehicleConfiguration!,"LIGHT_BLINKER",occurrence,new[]{v.X,v.Y,v.Z}.Select(n=>n.ToString("G9",System.Globalization.CultureInfo.InvariantCulture)).ToArray());
   vehicleConfigurationEdited=true;dirty=true;
  }
  transportPoint=new("LIGHT_BLINKER",occurrence,0);activeLamp=activeCargo=activeSmoke=-1;
  gizmoMode="move";gizmoVisible=gizmoEditing=true;await RefreshGizmo();
 }
 void StoreLampSlots(){if(vehicleConfiguration==null)return;vehicleConfiguration=VehicleLights.Write(vehicleConfiguration,lampSlots.Where(l=>l!=null).Select(l=>l!).Concat(extraLights));vehicleConfigurationEdited=true;}
 async Task ChooseLamp()
 {
  CloseAnimationTimeline();toolStart=CaptureSnapshot();transportPoint=null;
  if(!await EnsureVehicleScript())return;ReadLampSlots();activeSmoke=-1;activeCargo=-1;var panel=new StackPanel{Spacing=8,Width=280};var dialog=VehicleDialog("Фары и фонари",panel);dialog.PrimaryButtonText="";int selected=-1;
  string[] labels=["Левая фара","Правая фара","Левый фонарь","Правый фонарь"];
  for(int i=0;i<4;i++){int index=i;var button=new Button{Content=UiLanguage.Text(labels[i]),HorizontalAlignment=HorizontalAlignment.Stretch};button.Click+=(_,_)=>{selected=index;dialog.Hide();};panel.Children.Add(button);}
  (bool Front,bool Left)? indicatorSlot=null;
  foreach(var spec in new[]{(true,true,"Передний левый поворотник"),(true,false,"Передний правый поворотник"),(false,true,"Задний левый поворотник"),(false,false,"Задний правый поворотник")}){
   var button=new Button{Content=UiLanguage.Text(spec.Item3),HorizontalAlignment=HorizontalAlignment.Stretch};
   button.Click+=(_,_)=>{indicatorSlot=(spec.Item1,spec.Item2);dialog.Hide();};panel.Children.Add(button);
  }
  var more=new Button{Content=UiLanguage.Text("Другие огни"),HorizontalAlignment=HorizontalAlignment.Stretch};bool advanced=false;more.Click+=(_,_)=>{advanced=true;dialog.Hide();};panel.Children.Add(more);await ShowDialog(dialog);if(indicatorSlot is { } indicator){await SelectIndicator(indicator.Front,indicator.Left);return;}if(advanced){await ConfigureTransport("lights");return;}if(selected<0)return;
  int paired=selected^1;if(lampSlots[paired]!=null){
   var axes=new ComboBox{ItemsSource=new[]{"X","Y","Z"},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};var mirror=VehicleDialog("Настроить симметрию?",axes);mirror.PrimaryButtonText=UiLanguage.Text("Отразить");mirror.CloseButtonText=UiLanguage.Text("Вручную");
   if(await ShowDialog(mirror)==ContentDialogResult.Primary){var frame=PivotFrame();Matrix4x4.Invert(frame,out var inv);var local=Vector3.TransformNormal(lampSlots[paired]!.Position-mainPivot,inv);local=axes.SelectedIndex switch{0=>new(-local.X,local.Y,local.Z),1=>new(local.X,-local.Y,local.Z),_=>new(local.X,local.Y,-local.Z)};lampSlots[selected]=new(selected>=2,mainPivot+Vector3.TransformNormal(local,frame));}
  }
  lampSlots[selected]??=new(selected>=2,mainPivot);activeLamp=selected;gizmoVisible=gizmoEditing=true;gizmoMode="move";dirty=true;StoreLampSlots();await RefreshGizmo();
 }
}
