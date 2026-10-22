using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Numerics;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 StackPanel? effectBar;
 bool testLeft,testRight,testBeacon,testWater;
 StackPanel EffectButtons()
 {
  var bar=new StackPanel{Orientation=Orientation.Horizontal,Spacing=5,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(12)};
  effectBar=bar;nightButton.Margin=new Thickness(0);bar.Children.Add(nightButton);
  void Add(string glyph,string label,Func<bool> value,Action<bool> set){var b=new Button{Content=glyph,Width=34,Height=32,Padding=new Thickness(0)};ToolTipService.SetToolTip(b,UiLanguage.Text(label));Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b,UiLanguage.Text(label));b.Click+=async(_,_)=>{if(!HasModel)return;set(!value());b.Opacity=value()?1:.55;await RefreshLighting();};bar.Children.Add(b);}
  Add("←","Левые поворотники",()=>testLeft,v=>testLeft=v);Add("→","Правые поворотники",()=>testRight,v=>testRight=v);Add("◉","Проблесковые маяки",()=>testBeacon,v=>testBeacon=v);Add("≋","Проверка направления струи",()=>testWater,v=>testWater=v);
  var wheels=new Button{Content="↻",Width=34,Height=32,Padding=new Thickness(0)};ToolTipService.SetToolTip(wheels,UiLanguage.Text("Проверка колёс"));var wheelPanel=new StackPanel{Spacing=6,MinWidth=230};var turn=new Slider{Header=UiLanguage.Text("Поворот передних колёс"),Minimum=-45,Maximum=45,StepFrequency=.1};var spin=new Slider{Header=UiLanguage.Text("Вращение колёс"),Minimum=0,Maximum=360,StepFrequency=.1};wheelPanel.Children.Add(turn);wheelPanel.Children.Add(spin);async Task Pose(){if(!HasModel)return;var axes=WheelDiagnostics.Read(ViewportModel());if(axes.Length==0){editorNotice.Message=UiLanguage.Text("Не найдены колёса со штатными именами");editorNotice.Severity=InfoBarSeverity.Warning;editorNotice.IsOpen=true;return;}await view.ExecuteScriptAsync("poseWheels("+System.Text.Json.JsonSerializer.Serialize(axes)+","+turn.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+spin.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)+")");}turn.ValueChanged+=async(_,_)=>await Pose();spin.ValueChanged+=async(_,_)=>await Pose();var reset=new Button{Content=UiLanguage.Text("Сбросить")};reset.Click+=async(_,_)=>{turn.Value=spin.Value=0;await Pose();};wheelPanel.Children.Add(reset);wheels.Flyout=new Microsoft.UI.Xaml.Controls.Flyout{Content=wheelPanel};bar.Children.Add(wheels);
  return bar;
 }
 IEnumerable<object> DiagnosticLights()
 {
  if(vehicleConfiguration==null)yield break;
  foreach(var field in VehicleScript.Fields(vehicleConfiguration)){
   bool blink=field.Key==(JointFrameRequested?"LIGHT_BLINKER_TRAILER":"LIGHT_BLINKER"),beacon=field.Key=="SIREN_POINT",water=field.Key=="WATER_POINT";
   if(JointFrameRequested&&(beacon||water))continue;if(!(blink&&(testLeft||testRight)||beacon&&testBeacon||water&&testWater))continue;
   var args=VehicleScript.Tokens(vehicleConfiguration).Where(t=>t.Key==field.Key&&t.Occurrence==field.Occurrence).Select(t=>t.Value).Take(3).ToArray();
   if(args.Length!=3||args.Any(a=>!float.TryParse(a,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out _)))continue;
   var p=args.Select(a=>float.Parse(a,System.Globalization.CultureInfo.InvariantCulture)).ToArray();if(blink&&!(p[0]<=mainPivot.X?testLeft:testRight))continue;
   var forward=Vector3.TransformNormal(Vector3.UnitZ,PivotFrame());yield return new{position=p,direction=new[]{forward.X,forward.Y,forward.Z},rear=false,kind=water?"water":beacon?"beacon":"blinker"};
  }
 }
}
