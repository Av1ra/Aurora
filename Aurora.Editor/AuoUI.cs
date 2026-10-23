using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.Globalization.NumberFormatting;
using Microsoft.UI.Xaml.Markup;
namespace Aurora.Editor;

// AuoUI owns composition and control chrome; WinUI owns input and accessibility.
static class AuoUI
{
 public const double ButtonHeight=22,FieldHeight=22,FontSize=12,InspectorWidth=208;
 public const string CoordinateTag="AuoUI.Coordinate";
 public static Color Colour(byte r,byte g,byte b)=>Color.FromArgb(255,r,g,b);
 public static Color Surface(bool dark)=>dark?Colour(45,48,49):Colour(232,234,232);
 public static Color ControlSurface(bool dark)=>dark?Colour(64,68,69):Colour(211,216,213);
 public static Color Edge(bool dark)=>dark?Colour(74,79,80):Colour(184,190,186);
 public static void Apply(FrameworkElement root)
 {
  var resources=Application.Current.Resources;
  resources["ControlCornerRadius"]=new CornerRadius(2);resources["OverlayCornerRadius"]=new CornerRadius(2);
  resources["MenuFlyoutPresenterPadding"]=new Thickness(4);
  resources["ListViewItemSelectionIndicatorVisualEnabled"]=false;
  resources["ContentDialogPadding"]=new Thickness(8);
  resources["ContentDialogTitleMargin"]=new Thickness(0,0,0,6);
  resources["ContentDialogContentMargin"]=new Thickness(0);
  resources["ContentDialogCommandSpacePadding"]=new Thickness(8,4,8,6);
  resources["ContentDialogTitleFontSize"]=14d;
  resources["ContentDialogBorderThickness"]=new Thickness(0);
  resources["ContentDialogMinHeight"]=0d;
  resources["ContentDialogMinWidth"]=0d;
  resources["TextControlBorderThemeThickness"]=new Thickness(0);
  resources["TextControlBorderThemeThicknessFocused"]=new Thickness(0);
  resources["TextControlThemeMinHeight"]=FieldHeight;
  resources["ComboBoxMinHeight"]=FieldHeight;
  resources["ComboBoxPadding"]=new Thickness(5,1,24,1);
  resources["ComboBoxBorderThemeThickness"]=new Thickness(0);
  resources["CheckBoxMinHeight"]=22d;resources["CheckBoxMinWidth"]=0d;
  resources["CheckBoxBorderThemeThickness"]=new Thickness(0);
  resources["MenuFlyoutItemMinHeight"]=24d;
  var owned=new HashSet<string>();var accessibility=new Windows.UI.ViewManagement.AccessibilitySettings();
  void Refresh(){
   if(accessibility.HighContrast){foreach(string key in owned)resources.Remove(key);owned.Clear();var ui=new Windows.UI.ViewManagement.UISettings();var background=ui.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);var foreground=ui.GetColorValue(Windows.UI.ViewManagement.UIColorType.Foreground);foreach(string key in new[]{"AuoUI.Field","AuoUI.Hover","AuoUI.Panel"})resources[key]=new SolidColorBrush(background);resources["AuoUI.Text"]=new SolidColorBrush(foreground);if(root is Panel highContrastPanel)highContrastPanel.Background=new SolidColorBrush(background);return;}
   bool dark=root.ActualTheme==ElementTheme.Dark;
   var accent=dark?Colour(159,174,165):Colour(75,94,83);
   var text=dark?Colour(213,216,214):Colour(34,39,36);
   void Brush(string key,Color colour){resources[key]=new SolidColorBrush(colour);owned.Add(key);}
   Brush("ControlFillColorDefaultBrush",ControlSurface(dark));Brush("ControlFillColorSecondaryBrush",Edge(dark));Brush("ControlFillColorTertiaryBrush",Surface(dark));
   Brush("ControlStrokeColorDefaultBrush",Colour(0,0,0) with{A=0});Brush("TextFillColorPrimaryBrush",text);
   Brush("AuoUI.Field",ControlSurface(dark));Brush("AuoUI.Hover",Edge(dark));Brush("AuoUI.Text",text);Brush("AuoUI.Panel",Surface(dark));
   foreach(string key in new[]{"TextControlBackground","TextControlBackgroundPointerOver","TextControlBackgroundFocused","ComboBoxBackground","ComboBoxBackgroundPointerOver","ComboBoxBackgroundPressed","ComboBoxBackgroundUnfocused"})Brush(key,ControlSurface(dark));
   foreach(string key in new[]{"TextControlBorderBrush","TextControlBorderBrushPointerOver","TextControlBorderBrushFocused","ComboBoxBorderBrush","ComboBoxBorderBrushPointerOver","ComboBoxBorderBrushPressed","ContentDialogBorderBrush"})Brush(key,Color.FromArgb(0,0,0,0));
   foreach(string key in new[]{"ContentDialogBackground","ContentDialogTopOverlay","ContentDialogSmokeFill","ContentDialogCommandSpaceBackground","MenuFlyoutPresenterBackground","FlyoutPresenterBackground"})Brush(key,Surface(dark));
   Brush("TextControlForeground",text);Brush("ComboBoxForeground",text);
   Brush("AccentFillColorDefaultBrush",accent);Brush("AccentFillColorSecondaryBrush",accent);Brush("AccentFillColorTertiaryBrush",accent);Brush("AccentTextFillColorPrimaryBrush",accent);Brush("SystemControlHighlightAccentBrush",accent);
   Brush("TextOnAccentFillColorPrimaryBrush",dark?Colour(17,25,33):Colour(255,255,255));
   root.Resources["AuoUI.Edge"]=new SolidColorBrush(Edge(dark));
   if(root is Microsoft.UI.Xaml.Controls.Panel panel)panel.Background=new SolidColorBrush(dark?Colour(39,42,43):Colour(223,226,224));
  }
  void ContrastChanged(Windows.UI.ViewManagement.AccessibilitySettings sender,object args){root.DispatcherQueue.TryEnqueue(Refresh);}
  bool subscribed=false;
  root.Loaded+=(_,_)=>{Refresh();if(!subscribed)try{accessibility.HighContrastChanged+=ContrastChanged;subscribed=true;}catch(System.Runtime.InteropServices.COMException){/* Some hosts do not expose this event; theme changes still refresh the palette. */}};
  root.Unloaded+=(_,_)=>{if(subscribed){try{accessibility.HighContrastChanged-=ContrastChanged;}catch(System.Runtime.InteropServices.COMException){}subscribed=false;}};
  root.ActualThemeChanged+=(_,_)=>Refresh();Refresh();
  var chrome=(ResourceDictionary)XamlReader.Load(ButtonChrome);foreach(var key in chrome.Keys)resources[key]=chrome[key];
 }
 public static StackPanel Section(string title,FrameworkElement content,bool expanded=true)
 {
  var panel=new StackPanel{Spacing=4};var header=new Button{Content=(expanded?"▾ ":"▸ ")+title,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(5,1,5,1)};
  content.Visibility=expanded?Visibility.Visible:Visibility.Collapsed;header.Click+=(_,_)=>{expanded=!expanded;content.Visibility=expanded?Visibility.Visible:Visibility.Collapsed;header.Content=(expanded?"▾ ":"▸ ")+title;Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(header,title+(expanded?" expanded":" collapsed"));};panel.Children.Add(header);panel.Children.Add(content);return panel;
 }
 public static void Compact(DependencyObject element)
 {
  if(element is Control control){control.FontSize=FontSize;control.MinWidth=0;if(control is Button or Microsoft.UI.Xaml.Controls.Primitives.ToggleButton or TextBox or ComboBox or NumberBox or CheckBox or RadioButton){control.MinHeight=22;control.Padding=new Thickness(5,1,5,1);control.BorderThickness=new Thickness(0);}if(control is TextBox or ComboBox or NumberBox)control.HorizontalContentAlignment=HorizontalAlignment.Stretch;}
  if(element is StackPanel stack)stack.Spacing=Math.Min(stack.Spacing,4);
  if(element is Grid grid){grid.ColumnSpacing=Math.Min(grid.ColumnSpacing,6);grid.RowSpacing=Math.Min(grid.RowSpacing,3);}
  if(element is TextBlock text&&text.FontSize>12)text.FontSize=12;
  if(element is Panel panel)foreach(var child in panel.Children)Compact(child);
  else if(element is ContentControl content&&content.Content is DependencyObject child)Compact(child);
  else if(element is Border border&&border.Child!=null)Compact(border.Child);
 }
 const string ButtonChrome="""
 <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style TargetType="Button">
   <Setter Property="Background" Value="{ThemeResource AuoUI.Field}"/><Setter Property="Foreground" Value="{ThemeResource AuoUI.Text}"/>
   <Setter Property="BorderThickness" Value="0"/><Setter Property="CornerRadius" Value="2"/><Setter Property="MinHeight" Value="22"/><Setter Property="MinWidth" Value="0"/><Setter Property="FontSize" Value="12"/><Setter Property="Padding" Value="6,1"/>
   <Setter Property="HorizontalContentAlignment" Value="Center"/><Setter Property="VerticalContentAlignment" Value="Center"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
    <Grid x:Name="Root" Background="{TemplateBinding Background}" CornerRadius="{TemplateBinding CornerRadius}">
     <VisualStateManager.VisualStateGroups><VisualStateGroup x:Name="CommonStates">
      <VisualState x:Name="Normal"/><VisualState x:Name="PointerOver"><VisualState.Setters><Setter Target="Root.Background" Value="{ThemeResource AuoUI.Hover}"/></VisualState.Setters></VisualState>
      <VisualState x:Name="Pressed"><VisualState.Setters><Setter Target="Root.Background" Value="{ThemeResource AuoUI.Hover}"/><Setter Target="Presenter.Opacity" Value="0.8"/></VisualState.Setters></VisualState>
      <VisualState x:Name="Disabled"><VisualState.Setters><Setter Target="Presenter.Opacity" Value="0.45"/></VisualState.Setters></VisualState>
     </VisualStateGroup></VisualStateManager.VisualStateGroups>
     <ContentPresenter x:Name="Presenter" Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}" Foreground="{TemplateBinding Foreground}" Padding="{TemplateBinding Padding}" HorizontalContentAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalContentAlignment="{TemplateBinding VerticalContentAlignment}"/>
    </Grid>
   </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="ContentDialog">
   <Setter Property="Background" Value="{ThemeResource AuoUI.Panel}"/><Setter Property="Foreground" Value="{ThemeResource AuoUI.Text}"/>
   <Setter Property="BorderThickness" Value="0"/><Setter Property="IsTabStop" Value="False"/><Setter Property="CornerRadius" Value="2"/>
   <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ContentDialog">
    <Border x:Name="Container">
     <VisualStateManager.VisualStateGroups>
      <VisualStateGroup x:Name="DialogShowingStates">
       <VisualState x:Name="DialogHidden"/>
       <VisualState x:Name="DialogShowing"><VisualState.Setters><Setter Target="LayoutRoot.Visibility" Value="Visible"/><Setter Target="BackgroundElement.TabFocusNavigation" Value="Cycle"/></VisualState.Setters></VisualState>
       <VisualState x:Name="DialogShowingWithoutSmokeLayer"><VisualState.Setters><Setter Target="LayoutRoot.Visibility" Value="Visible"/></VisualState.Setters></VisualState>
      </VisualStateGroup>
      <VisualStateGroup x:Name="DialogSizingStates"><VisualState x:Name="DefaultDialogSizing"/><VisualState x:Name="FullDialogSizing"/></VisualStateGroup>
      <VisualStateGroup x:Name="ButtonsVisibilityStates">
       <VisualState x:Name="AllVisible"><VisualState.Setters><Setter Target="FirstSpacer.Width" Value="4"/><Setter Target="SecondaryColumn.Width" Value="Auto"/><Setter Target="SecondaryButton.(Grid.Column)" Value="2"/></VisualState.Setters></VisualState>
       <VisualState x:Name="NoneVisible"><VisualState.Setters><Setter Target="CommandSpace.Visibility" Value="Collapsed"/></VisualState.Setters></VisualState>
       <VisualState x:Name="PrimaryVisible"><VisualState.Setters><Setter Target="SecondaryButton.Visibility" Value="Collapsed"/><Setter Target="CloseButton.Visibility" Value="Collapsed"/></VisualState.Setters></VisualState>
       <VisualState x:Name="SecondaryVisible"><VisualState.Setters><Setter Target="PrimaryButton.Visibility" Value="Collapsed"/><Setter Target="CloseButton.Visibility" Value="Collapsed"/><Setter Target="SecondaryColumn.Width" Value="Auto"/></VisualState.Setters></VisualState>
       <VisualState x:Name="CloseVisible"><VisualState.Setters><Setter Target="PrimaryButton.Visibility" Value="Collapsed"/><Setter Target="SecondaryButton.Visibility" Value="Collapsed"/></VisualState.Setters></VisualState>
       <VisualState x:Name="PrimaryAndSecondaryVisible"><VisualState.Setters><Setter Target="SecondaryColumn.Width" Value="Auto"/><Setter Target="FirstSpacer.Width" Value="4"/><Setter Target="SecondaryButton.(Grid.Column)" Value="2"/><Setter Target="CloseButton.Visibility" Value="Collapsed"/></VisualState.Setters></VisualState>
       <VisualState x:Name="PrimaryAndCloseVisible"><VisualState.Setters><Setter Target="SecondaryButton.Visibility" Value="Collapsed"/></VisualState.Setters></VisualState>
       <VisualState x:Name="SecondaryAndCloseVisible"><VisualState.Setters><Setter Target="PrimaryButton.Visibility" Value="Collapsed"/><Setter Target="SecondaryColumn.Width" Value="Auto"/></VisualState.Setters></VisualState>
      </VisualStateGroup>
      <VisualStateGroup x:Name="DefaultButtonStates"><VisualState x:Name="NoDefaultButton"/><VisualState x:Name="PrimaryAsDefaultButton"/><VisualState x:Name="SecondaryAsDefaultButton"/><VisualState x:Name="CloseAsDefaultButton"/></VisualStateGroup>
      <VisualStateGroup x:Name="DialogBorderStates"><VisualState x:Name="NoBorder"/><VisualState x:Name="AccentColorBorder"/></VisualStateGroup>
     </VisualStateManager.VisualStateGroups>
     <Grid x:Name="LayoutRoot" Visibility="Collapsed">
      <Border x:Name="BackgroundElement" Background="{TemplateBinding Background}" CornerRadius="2" HorizontalAlignment="Stretch" VerticalAlignment="Top">
       <Grid x:Name="DialogSpace"><Grid.RowDefinitions><RowDefinition Height="*"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
        <ScrollViewer x:Name="ContentScrollViewer" HorizontalScrollBarVisibility="Disabled" VerticalScrollBarVisibility="Disabled" IsTabStop="False">
         <Grid Padding="6"><Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
          <ContentControl x:Name="Title" Content="{TemplateBinding Title}" ContentTemplate="{TemplateBinding TitleTemplate}" FontSize="12" FontWeight="Medium" Foreground="{TemplateBinding Foreground}" IsTabStop="False" Margin="0,0,0,6"/>
          <ContentPresenter x:Name="Content" Grid.Row="1" Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}" Foreground="{TemplateBinding Foreground}" FontSize="12" TextWrapping="Wrap"/>
         </Grid>
        </ScrollViewer>
        <Grid x:Name="CommandSpace" Grid.Row="1" HorizontalAlignment="Right" Padding="6,2,6,6">
         <Grid.ColumnDefinitions><ColumnDefinition x:Name="PrimaryColumn" Width="Auto"/><ColumnDefinition x:Name="FirstSpacer" Width="0"/><ColumnDefinition x:Name="SecondaryColumn" Width="0"/><ColumnDefinition x:Name="SecondSpacer" Width="4"/><ColumnDefinition x:Name="CloseColumn" Width="Auto"/></Grid.ColumnDefinitions>
         <Button x:Name="PrimaryButton" Content="{TemplateBinding PrimaryButtonText}" IsEnabled="{TemplateBinding IsPrimaryButtonEnabled}" MinWidth="70"/>
         <Button x:Name="SecondaryButton" Grid.Column="2" Content="{TemplateBinding SecondaryButtonText}" IsEnabled="{TemplateBinding IsSecondaryButtonEnabled}" MinWidth="70"/>
         <Button x:Name="CloseButton" Grid.Column="4" Content="{TemplateBinding CloseButtonText}" MinWidth="70"/>
        </Grid>
       </Grid>
      </Border>
     </Grid>
    </Border>
   </ControlTemplate></Setter.Value></Setter>
  </Style>
 </ResourceDictionary>
 """;
 public static void Coordinate(NumberBox number){number.Tag=CoordinateTag;FormatNumber(number);}
 public static void FormatNumber(NumberBox number)
 {
  bool coordinate=number.Tag as string==CoordinateTag;int digits=coordinate?2:1;double increment=coordinate?.01:.1;
  number.SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Hidden;
  if(coordinate){number.SmallChange=.01;number.LargeChange=.1;}
  number.NumberFormatter=new DecimalFormatter{FractionDigits=digits,IntegerDigits=1,IsGrouped=false,NumberRounder=new IncrementNumberRounder{Increment=increment,RoundingAlgorithm=RoundingAlgorithm.RoundHalfUp}};
 }
}
