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
  resources["TextControlThemeMinHeight"]=28d;
  resources["TextControlThemePadding"]=new Thickness(6,2,6,2);
  resources["ButtonPadding"]=new Thickness(7,2,7,2);
  resources["ListViewItemMinHeight"]=26d;
  foreach(var type in new[]{typeof(Button),typeof(ToggleButton),typeof(TextBox),typeof(ComboBox),typeof(NumberBox),typeof(ListViewItem)})
  {
   var style=new Style(type);
   if(resources.TryGetValue(type,out var previous)&&previous is Style inherited)style.BasedOn=inherited;
   style.Setters.Add(new Setter(Control.MinHeightProperty,type==typeof(Button)||type==typeof(ToggleButton)||type==typeof(ListViewItem)?26d:28d));
   style.Setters.Add(new Setter(Control.MinWidthProperty,0d));
   style.Setters.Add(new Setter(Control.FontSizeProperty,12d));
   style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(6,2,6,2)));
   resources[type]=style;
  }
 }
}
