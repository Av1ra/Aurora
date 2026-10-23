using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 async Task ConfigureEditorPreferences()
 {
  var panel=new StackPanel{Spacing=12,Width=360};var diagnostics=new Button{Content=UiLanguage.Text("Сохранить диагностику…"),HorizontalAlignment=HorizontalAlignment.Stretch};diagnostics.Click+=async(_,_)=>{try{await ExportViewportDiagnostics();}catch(Exception ex){SetStatus(ex.Message);}};panel.Children.Add(diagnostics);var refreshCatalog=new Button{Content=UiLanguage.Text("Обновить справочники"),HorizontalAlignment=HorizontalAlignment.Stretch};refreshCatalog.Click+=async(_,_)=>{refreshCatalog.IsEnabled=false;try{await LoadVehicleChoices(true);ToolTipService.SetToolTip(refreshCatalog,UiLanguage.Text("Справочники обновлены"));}catch(Exception ex){SetStatus(ex.Message);}finally{refreshCatalog.IsEnabled=true;}};panel.Children.Add(refreshCatalog);
  var enabled=new CheckBox{Content=UiLanguage.Text("Полигональная сетка"),IsChecked=wireframeMenu.IsChecked};panel.Children.Add(enabled);
  var sample=new Border{Height=28,Background=new SolidColorBrush(wireframeColour)};panel.Children.Add(sample);
  var hex=new TextBox{Header=UiLanguage.Text("Цвет сетки"),Text=$"#{wireframeColour.R:X2}{wireframeColour.G:X2}{wireframeColour.B:X2}",MaxLength=7};
  var sliders=new Slider[3];bool updating=false;
  void Changed(){if(updating)return;var c=Windows.UI.Color.FromArgb(255,(byte)sliders[0].Value,(byte)sliders[1].Value,(byte)sliders[2].Value);sample.Background=new SolidColorBrush(c);hex.Text=$"#{c.R:X2}{c.G:X2}{c.B:X2}";}
  byte[] channels=[wireframeColour.R,wireframeColour.G,wireframeColour.B];
  for(int i=0;i<3;i++){sliders[i]=new Slider{Header="RGB"[i].ToString(),Minimum=0,Maximum=255,StepFrequency=1,Value=channels[i]};panel.Children.Add(sliders[i]);}
  foreach(var slider in sliders)slider.ValueChanged+=(_,_)=>Changed();panel.Children.Add(hex);
  hex.LostFocus+=(_,_)=>{string s=hex.Text.Trim().TrimStart('#');if(s.Length!=6||!uint.TryParse(s,System.Globalization.NumberStyles.HexNumber,null,out uint rgb))return;updating=true;sliders[0].Value=(rgb>>16)&255;sliders[1].Value=(rgb>>8)&255;sliders[2].Value=rgb&255;updating=false;Changed();};
  if(await ShowDialog(VehicleDialog("Настройки",panel))!=ContentDialogResult.Primary)return;
  wireframeColour=Windows.UI.Color.FromArgb(255,(byte)sliders[0].Value,(byte)sliders[1].Value,(byte)sliders[2].Value);wireframeMenu.IsChecked=enabled.IsChecked==true;await ApplyWireframe();
 }
}
