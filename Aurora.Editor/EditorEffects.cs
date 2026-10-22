using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Numerics;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 StackPanel? effectBar;
 bool testLeft,testRight,testBeacon;
 readonly List<Microsoft.UI.Xaml.Controls.Primitives.ToggleButton> effectToggles=[];
 void ResetEffectTests(){testLeft=testRight=testBeacon=false;foreach(var button in effectToggles)button.IsChecked=false;}
 bool IndicatorLeft(string script,string key,int occurrence){var args=VehicleScript.Tokens(script).Where(t=>t.Key==key&&t.Occurrence==occurrence).Take(3).Select(t=>float.Parse(t.Value,System.Globalization.CultureInfo.InvariantCulture)).ToArray();if(args.Length!=3)throw new InvalidDataException("Invalid indicator coordinates");var p=new Vector3(args[0],args[1],args[2]);return VehicleLights.IsLeft(p,mainPivot,PivotFrame());}
 StackPanel EffectButtons()
 {
  var bar=new StackPanel{Orientation=Orientation.Horizontal,Spacing=5,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(12)};
  effectBar=bar;nightButton.Margin=new Thickness(0);bar.Children.Add(nightButton);
  void Add(string glyph,string label,Action<bool> set){var b=new Microsoft.UI.Xaml.Controls.Primitives.ToggleButton{Content=glyph,Width=34,Height=32,Padding=new Thickness(0),IsChecked=false};effectToggles.Add(b);ToolTipService.SetToolTip(b,UiLanguage.Text(label));Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b,UiLanguage.Text(label));b.Click+=async(_,_)=>{if(!HasModel){b.IsChecked=false;return;}set(b.IsChecked==true);await RefreshLighting();};bar.Children.Add(b);}
  Add("←","Левые поворотники",v=>testLeft=v);Add("→","Правые поворотники",v=>testRight=v);Add("◉","Проблесковые маяки",v=>testBeacon=v);
  var wheels=new Button{Content="↻",Width=34,Height=32,Padding=new Thickness(0)};ToolTipService.SetToolTip(wheels,UiLanguage.Text("Проверка колёс"));var wheelPanel=new StackPanel{Spacing=6,MinWidth=230};var choice=new ComboBox{Header=UiLanguage.Text("Колесо"),HorizontalAlignment=HorizontalAlignment.Stretch};wheelPanel.Children.Add(choice);WheelDiagnostic[] axes=[];bool updating=false;
  var turn=new Slider{Header=UiLanguage.Text("Поворот передних колёс"),Minimum=-45,Maximum=45,StepFrequency=.1};var spin=new Slider{Header=UiLanguage.Text("Вращение колёс"),Minimum=0,Maximum=360,StepFrequency=.1};wheelPanel.Children.Add(turn);wheelPanel.Children.Add(spin);
  async Task Pose(bool resetAll=false){if(!HasModel||updating)return;var selected=choice.SelectedIndex>0?axes.ElementAtOrDefault(choice.SelectedIndex-1):null;turn.IsEnabled=selected?.Steering??axes.Any(a=>a.Steering);await view.ExecuteScriptAsync("poseWheels("+System.Text.Json.JsonSerializer.Serialize(axes)+","+(resetAll?"0":turn.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))+","+(resetAll?"0":spin.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))+","+System.Text.Json.JsonSerializer.Serialize(resetAll||selected==null?null:selected.Mesh+"/"+selected.Component)+")");}
  turn.ValueChanged+=async(_,_)=>await Pose();spin.ValueChanged+=async(_,_)=>await Pose();choice.SelectionChanged+=async(_,_)=>await Pose();var reset=new Button{Content=UiLanguage.Text("Сбросить")};reset.Click+=async(_,_)=>{updating=true;turn.Value=spin.Value=0;updating=false;await Pose(true);};wheelPanel.Children.Add(reset);var wheelFlyout=new Flyout{Content=wheelPanel};wheelFlyout.Opening+=async(_,_)=>{wheelFlyout.FlyoutPresenterStyle=opaqueFlyoutStyle;if(!HasModel)return;updating=true;try{axes=WheelDiagnostics.Read(ViewportModel());choice.ItemsSource=new[]{UiLanguage.Text("Все колёса")}.Concat(axes.Select(a=>a.Mesh+" "+(a.Component+1))).ToArray();choice.SelectedIndex=axes.Length>0?1:0;turn.Value=spin.Value=0;turn.IsEnabled=axes.FirstOrDefault()?.Steering==true;if(axes.Length==0){editorNotice.Message=UiLanguage.Text("Не найдены колёса со штатными именами");editorNotice.Severity=InfoBarSeverity.Warning;editorNotice.IsOpen=true;}}catch(Exception ex){axes=[];editorNotice.Message=ex.Message;editorNotice.Severity=InfoBarSeverity.Error;editorNotice.IsOpen=true;}finally{updating=false;}await Pose(true);};wheels.Flyout=wheelFlyout;bar.Children.Add(wheels);

  return bar;
 }
 IEnumerable<object> DiagnosticLights()
 {
  if(vehicleConfiguration==null)yield break;
  foreach(var field in VehicleScript.Fields(vehicleConfiguration)){
   bool blink=field.Key==(JointFrameRequested?"LIGHT_BLINKER_TRAILER":"LIGHT_BLINKER"),beacon=field.Key=="SIREN_POINT";
   if(JointFrameRequested&&beacon)continue;if(!(blink&&(testLeft||testRight)||beacon&&testBeacon))continue;
   var args=VehicleScript.Tokens(vehicleConfiguration).Where(t=>t.Key==field.Key&&t.Occurrence==field.Occurrence).Select(t=>t.Value).Take(3).ToArray();
   if(args.Length!=3||args.Any(a=>!float.TryParse(a,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out _)))continue;
   var p=args.Select(a=>float.Parse(a,System.Globalization.CultureInfo.InvariantCulture)).ToArray();if(blink&&!(IndicatorLeft(vehicleConfiguration,field.Key,field.Occurrence)?testLeft:testRight))continue;
   var forward=Vector3.TransformNormal(Vector3.UnitZ,PivotFrame());yield return new{position=p,direction=new[]{forward.X,forward.Y,forward.Z},rear=false,kind=beacon?"beacon":"blinker"};
  }
 }
}
