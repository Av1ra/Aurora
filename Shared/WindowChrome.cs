using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace Aurora.Shared;
static class WindowChrome
{
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern uint GetDpiForWindow(nint hwnd);
 public static void ResizeLogical(Window window,int width,int height)
 {
  var area=Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(window.AppWindow.Id,Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
  var size=WindowSizing.Fit(width,height,GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(window)),area.Width,area.Height);
  window.AppWindow.Resize(new(size.Width,size.Height));
  var position=window.AppWindow.Position;
  window.AppWindow.Move(new(Math.Clamp(position.X,area.X,area.X+area.Width-size.Width),Math.Clamp(position.Y,area.Y,area.Y+area.Height-size.Height)));
 }
 public static void Attach(Window window,FrameworkElement content,string title)
 {
  var root=new Grid();root.RowDefinitions.Add(new(){Height=new GridLength(40)});root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
  var bar=new Grid{Padding=new Thickness(16,0,144,0)};
  bar.Children.Add(new TextBlock{Text=title,FontSize=12,VerticalAlignment=VerticalAlignment.Center});
  root.Children.Add(bar);Grid.SetRow(content,1);root.Children.Add(content);window.Content=root;
  window.ExtendsContentIntoTitleBar=true;window.SetTitleBar(bar);MatchCaption(window,root);
 }
 public static void MatchCaption(Window window,FrameworkElement root)
 {
  void Apply()
  {
   bool dark=root.ActualTheme==ElementTheme.Dark;var caption=window.AppWindow.TitleBar;
   caption.ButtonBackgroundColor=Colors.Transparent;caption.ButtonInactiveBackgroundColor=Colors.Transparent;
   caption.ButtonForegroundColor=dark?Colors.White:Colors.Black;
   caption.ButtonInactiveForegroundColor=dark?Windows.UI.Color.FromArgb(255,153,153,153):Windows.UI.Color.FromArgb(255,112,112,112);
   caption.ButtonHoverBackgroundColor=dark?Windows.UI.Color.FromArgb(255,64,64,64):Windows.UI.Color.FromArgb(255,230,230,230);
   caption.ButtonHoverForegroundColor=dark?Colors.White:Colors.Black;
   caption.ButtonPressedBackgroundColor=dark?Windows.UI.Color.FromArgb(255,80,80,80):Windows.UI.Color.FromArgb(255,210,210,210);
   caption.ButtonPressedForegroundColor=dark?Colors.White:Colors.Black;
  }
  root.Loaded+=(_,_)=>Apply();root.ActualThemeChanged+=(_,_)=>Apply();Apply();
 }
}
