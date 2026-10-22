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
 bool IndicatorLeft(string script,string key,int occurrence){var args=VehicleScript.Tokens(script).Where(t=>t.Key==key&&t.Occurrence==occurrence).Take(3).Select(t=>float.Parse(t.Value,System.Globalization.CultureInfo.InvariantCulture)).ToArray();if(args.Length!=3)throw new InvalidDataException("Invalid indicator coordinates");var p=new Vector3(args[0],args[1],args[2]);Matrix4x4.Invert(PivotFrame(),out var inverse);return Vector3.TransformNormal(p-mainPivot,inverse).X<=0;}
 StackPanel EffectButtons()
 {
  var bar=new StackPanel{Orientation=Orientation.Horizontal,Spacing=5,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(12)};
  effectBar=bar;nightButton.Margin=new Thickness(0);bar.Children.Add(nightButton);
  void Add(string glyph,string label,Action<bool> set){var b=new Microsoft.UI.Xaml.Controls.Primitives.ToggleButton{Content=glyph,Width=34,Height=32,Padding=new Thickness(0),IsChecked=false};effectToggles.Add(b);ToolTipService.SetToolTip(b,UiLanguage.Text(label));Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b,UiLanguage.Text(label));b.Click+=async(_,_)=>{if(!HasModel){b.IsChecked=false;return;}set(b.IsChecked==true);await RefreshLighting();};bar.Children.Add(b);}
  Add("←","Левые поворотники",v=>testLeft=v);Add("→","Правые поворотники",v=>testRight=v);Add("◉","Проблесковые маяки",v=>testBeacon=v);
  var wheels=new Button{Content="↻",Width=34,Height=32,Padding=new Thickness(0)};ToolTipService.SetToolTip(wheels,UiLanguage.Text("Проверка колёс"));var wheelPanel=new StackPanel{Spacing=6,MinWidth=230};var turn=new Slider{Header=UiLanguage.Text("Поворот передних колёс"),Minimum=-45,Maximum=45,StepFrequency=.1};var spin=new Slider{Header=UiLanguage.Text("Вращение колёс"),Minimum=0,Maximum=360,StepFrequency=.1};wheelPanel.Children.Add(turn);wheelPanel.Children.Add(spin);async Task Pose(){if(!HasModel)return;var axes=WheelDiagnostics.Read(ViewportModel());if(axes.Length==0){editorNotice.Message=UiLanguage.Text("Не найдены колёса со штатными именами");editorNotice.Severity=InfoBarSeverity.Warning;editorNotice.IsOpen=true;return;}await view.ExecuteScriptAsync("poseWheels("+System.Text.Json.JsonSerializer.Serialize(axes)+","+turn.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+spin.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)+")");}turn.ValueChanged+=async(_,_)=>await Pose();spin.ValueChanged+=async(_,_)=>await Pose();var reset=new Button{Content=UiLanguage.Text("Сбросить")};reset.Click+=async(_,_)=>{turn.Value=spin.Value=0;await Pose();};wheelPanel.Children.Add(reset);wheels.Flyout=new Microsoft.UI.Xaml.Controls.Flyout{Content=wheelPanel};bar.Children.Add(wheels);
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
