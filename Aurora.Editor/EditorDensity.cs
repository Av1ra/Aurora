using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 void ConfigureCompactEditor()
 {
  // This host process displays the Editor. Use resources from the pinned SDK,
  // retaining native control templates, keyboard input and accessibility.
  var resources=Application.Current.Resources;
  resources["TextControlThemeMinHeight"]=AuoUI.FieldHeight;
  resources["TextControlThemePadding"]=new Thickness(5,1,5,1);
  resources["ButtonPadding"]=new Thickness(6,1,6,1);
  resources["ListViewItemMinHeight"]=AuoUI.ButtonHeight;
  foreach(var type in new[]{typeof(Button),typeof(ToggleButton),typeof(TextBox),typeof(ComboBox),typeof(NumberBox),typeof(ListViewItem),typeof(CheckBox),typeof(RadioButton),typeof(ToggleSwitch),typeof(MenuFlyoutItem),typeof(MenuFlyoutSubItem)})
  {
   var style=new Style(type);
   if(resources.TryGetValue(type,out var previous)&&previous is Style inherited)style.BasedOn=inherited;
   style.Setters.Add(new Setter(Control.MinHeightProperty,type==typeof(Button)||type==typeof(ToggleButton)||type==typeof(ListViewItem)?AuoUI.ButtonHeight:AuoUI.FieldHeight));
   style.Setters.Add(new Setter(Control.MinWidthProperty,0d));
   style.Setters.Add(new Setter(Control.FontSizeProperty,AuoUI.FontSize));
   style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(5,1,5,1)));
   style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(0)));
   resources[type]=style;
  }
 }
}
