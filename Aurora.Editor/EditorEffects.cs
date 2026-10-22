using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Numerics;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 StackPanel? effectBar;
 bool testLeft,testRight,testBeacon;Microsoft.UI.Xaml.Controls.Primitives.ToggleButton? wheelRun;
 readonly List<Microsoft.UI.Xaml.Controls.Primitives.ToggleButton> effectToggles=[];
 void ResetEffectTests(){testLeft=testRight=testBeacon=false;foreach(var button in effectToggles)button.IsChecked=false;if(wheelRun!=null)wheelRun.IsChecked=false;if(ready)_=view.ExecuteScriptAsync("stopWheels(true)");}
 bool IndicatorLeft(string script,string key,int occurrence){var args=VehicleScript.Tokens(script).Where(t=>t.Key==key&&t.Occurrence==occurrence).Take(3).Select(t=>float.Parse(t.Value,System.Globalization.CultureInfo.InvariantCulture)).ToArray();if(args.Length!=3)throw new InvalidDataException("Invalid indicator coordinates");var p=new Vector3(args[0],args[1],args[2]);return VehicleLights.IsLeft(p,mainPivot,PivotFrame());}
 StackPanel EffectButtons()
 {
  var bar=new StackPanel{Orientation=Orientation.Horizontal,Spacing=5,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(12)};
  effectBar=bar;nightButton.Margin=new Thickness(0);bar.Children.Add(nightButton);
  void Add(string glyph,string label,Action<bool> set){var b=new Microsoft.UI.Xaml.Controls.Primitives.ToggleButton{Content=glyph,Width=34,Height=32,Padding=new Thickness(0),IsChecked=false};effectToggles.Add(b);ToolTipService.SetToolTip(b,UiLanguage.Text(label));Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b,UiLanguage.Text(label));b.Click+=async(_,_)=>{if(!HasModel){b.IsChecked=false;return;}set(b.IsChecked==true);await RefreshLighting();};bar.Children.Add(b);}
  Add("←","Левые поворотники",v=>testLeft=v);Add("→","Правые поворотники",v=>testRight=v);Add("◉","Проблесковые маяки",v=>testBeacon=v);
  wheelRun=new Microsoft.UI.Xaml.Controls.Primitives.ToggleButton{Content="↻",Width=34,Height=32,Padding=new Thickness(0)};ToolTipService.SetToolTip(wheelRun,UiLanguage.Text("Вращать колёса: 20 км/ч"));Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(wheelRun,UiLanguage.Text("Вращать колёса: 20 км/ч"));wheelRun.Click+=async(_,_)=>{if(!HasModel){wheelRun.IsChecked=false;return;}if(wheelRun.IsChecked==true){var wheels=WheelDiagnostics.Read(CurrentModel());if(wheels.Length==0){wheelRun.IsChecked=false;SetStatus("Не найдены колёса со штатными именами");return;}await view.ExecuteScriptAsync("playWheels("+System.Text.Json.JsonSerializer.Serialize(wheels)+",20)");}else await view.ExecuteScriptAsync("stopWheels(false)");};bar.Children.Add(wheelRun);
  editorCommands["Поворот колёс…"]=(ConfigureWheelSteering,()=>HasModel);

  return bar;
 }
 async Task ConfigureWheelSteering()
 {
  if(wheelRun!=null)wheelRun.IsChecked=false;await view.ExecuteScriptAsync("stopWheels(true)");var axes=WheelDiagnostics.Read(CurrentModel());var panel=new StackPanel{Spacing=8,Width=300};var choice=new ComboBox{ItemsSource=new[]{UiLanguage.Text("Все колёса")}.Concat(axes.Select(a=>a.Mesh+" "+(a.Component+1))).ToArray(),SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};var turn=new Slider{Header=UiLanguage.Text("Поворот передних колёс"),Minimum=-45,Maximum=45,StepFrequency=.1};panel.Children.Add(choice);panel.Children.Add(turn);async Task Pose(){var selected=choice.SelectedIndex>0?axes[choice.SelectedIndex-1]:null;turn.IsEnabled=selected?.Steering??axes.Any(a=>a.Steering);await view.ExecuteScriptAsync("poseWheels("+System.Text.Json.JsonSerializer.Serialize(axes)+","+turn.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)+",0,"+System.Text.Json.JsonSerializer.Serialize(selected==null?null:selected.Mesh+"/"+selected.Component)+")");}turn.ValueChanged+=async(_,_)=>await Pose();choice.SelectionChanged+=async(_,_)=>await Pose();try{await ShowDialog(VehicleDialog("Поворот колёс…",panel));}finally{await view.ExecuteScriptAsync("poseWheels("+System.Text.Json.JsonSerializer.Serialize(axes)+",0,0)");}
 }
 IEnumerable<object> DiagnosticLights()
 {
  if(vehicleConfiguration==null)yield break;
  foreach(var field in VehicleScript.Fields(vehicleConfiguration)){
   bool blink=field.Key==(JointFrameRequested?"LIGHT_BLINKER_TRAILER":"LIGHT_BLINKER"),beacon=field.Key=="SIREN_POINT";
   if(JointFrameRequested&&beacon)continue;if(!(blink&&(testLeft||testRight||AllLightPreview)||beacon&&(testBeacon||AllLightPreview)))continue;
   var args=VehicleScript.Tokens(vehicleConfiguration).Where(t=>t.Key==field.Key&&t.Occurrence==field.Occurrence).Select(t=>t.Value).Take(3).ToArray();
   if(args.Length!=3||args.Any(a=>!float.TryParse(a,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out _)))continue;
   var p=args.Select(a=>float.Parse(a,System.Globalization.CultureInfo.InvariantCulture)).ToArray();if(blink&&!AllLightPreview&&!(IndicatorLeft(vehicleConfiguration,field.Key,field.Occurrence)?testLeft:testRight))continue;
   var forward=Vector3.TransformNormal(Vector3.UnitZ,PivotFrame());yield return new{position=p,direction=new[]{forward.X,forward.Y,forward.Z},rear=false,kind=beacon?"beacon":"blinker",animate=beacon?testBeacon:testLeft||testRight,phase=beacon?field.Occurrence*MathF.PI:0};
  }
 }
}
