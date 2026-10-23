using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.Globalization.NumberFormatting;
namespace Aurora.Editor;

// AuoUI v1: editor-owned visual rules, native WinUI input/focus/accessibility.
static class AuoUI
{
 public const double ButtonHeight=26,FieldHeight=28,FontSize=12,InspectorWidth=232;
 public const string CoordinateTag="AuoUI.Coordinate";
 public static Color Colour(byte r,byte g,byte b)=>Color.FromArgb(255,r,g,b);
 public static Color Surface(bool dark)=>dark?Colour(41,44,48):Colour(242,243,245);
 public static Color ControlSurface(bool dark)=>dark?Colour(55,59,65):Colour(228,231,235);
 public static Color Edge(bool dark)=>dark?Colour(74,80,88):Colour(184,190,198);
 public static void Apply(FrameworkElement root)
 {
  var resources=Application.Current.Resources;
  resources["ControlCornerRadius"]=new CornerRadius(2);resources["OverlayCornerRadius"]=new CornerRadius(2);
  resources["MenuFlyoutPresenterPadding"]=new Thickness(4);
  var owned=new HashSet<string>();var accessibility=new Windows.UI.ViewManagement.AccessibilitySettings();
  void Refresh(){
   if(accessibility.HighContrast){foreach(string key in owned)resources.Remove(key);owned.Clear();if(root is Panel highContrastPanel)highContrastPanel.Background=new SolidColorBrush(new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Background));return;}
   bool dark=root.ActualTheme==ElementTheme.Dark;
   var accent=dark?Colour(114,164,205):Colour(34,91,139);
   var text=dark?Colour(232,234,237):Colour(30,33,38);
   void Brush(string key,Color colour){resources[key]=new SolidColorBrush(colour);owned.Add(key);}
   Brush("ControlFillColorDefaultBrush",ControlSurface(dark));Brush("ControlFillColorSecondaryBrush",dark?Colour(66,72,80):Colour(217,222,229));Brush("ControlFillColorTertiaryBrush",Surface(dark));
   Brush("ControlStrokeColorDefaultBrush",Edge(dark));Brush("TextFillColorPrimaryBrush",text);
   Brush("AccentFillColorDefaultBrush",accent);Brush("AccentFillColorSecondaryBrush",accent);Brush("AccentFillColorTertiaryBrush",accent);Brush("AccentTextFillColorPrimaryBrush",accent);Brush("SystemControlHighlightAccentBrush",accent);
   Brush("TextOnAccentFillColorPrimaryBrush",dark?Colour(17,25,33):Colour(255,255,255));
   root.Resources["AuoUI.Edge"]=new SolidColorBrush(Edge(dark));
   if(root is Microsoft.UI.Xaml.Controls.Panel panel)panel.Background=new SolidColorBrush(dark?Colour(30,33,37):Colour(231,234,238));
  }
  void ContrastChanged(Windows.UI.ViewManagement.AccessibilitySettings sender,object args){root.DispatcherQueue.TryEnqueue(Refresh);}
  accessibility.HighContrastChanged+=ContrastChanged;root.Unloaded+=(_,_)=>accessibility.HighContrastChanged-=ContrastChanged;
  root.Loaded+=(_,_)=>Refresh();root.ActualThemeChanged+=(_,_)=>Refresh();Refresh();
 }
 public static void Coordinate(NumberBox number){number.Tag=CoordinateTag;FormatNumber(number);}
 public static void FormatNumber(NumberBox number)
 {
  bool coordinate=number.Tag as string==CoordinateTag;int digits=coordinate?2:1;double increment=coordinate?.01:.1;
  number.SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Hidden;
  if(coordinate){number.SmallChange=.01;number.LargeChange=.1;}
  number.NumberFormatter=new DecimalFormatter{FractionDigits=digits,IntegerDigits=1,IsGrouped=false,NumberRounder=new IncrementNumberRounder{Increment=increment,RoundingAlgorithm=RoundingAlgorithm.RoundHalfUp}};
 }
}
