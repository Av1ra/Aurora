using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace Aurora.Launcher;

sealed class ConsoleWindow : Window
{
    const uint GetSetting = 0x83a0, SetSetting = 0x83a1;
    readonly int pid;
    readonly nint host;
    readonly Grid root;
    readonly ToggleSwitch enabled, mirror;
    readonly Slider[] sliders;
    readonly TextBlock[] percentages;
    readonly ComboBox view;
    readonly DispatcherTimer timer;
    bool updating;
    delegate bool EnumProc(nint hwnd, nint data);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, nint data);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(nint hwnd, StringBuilder text, int size);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32.dll", SetLastError=true)] static extern nint SendMessageTimeout(nint hwnd, uint message, nuint index, nint value, uint flags, uint timeout, out nuint result);

    public ConsoleWindow()
    {
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--pid");
        if(at<0||at+1>=args.Length||!int.TryParse(args[at+1],out pid)||pid<=0)throw new ArgumentException("Missing game PID");
        nint found=0;
        EnumWindows((hwnd,_)=>{GetWindowThreadProcessId(hwnd,out uint owner);if(owner!=(uint)pid)return true;var name=new StringBuilder(128);GetClassName(hwnd,name,128);if(name.ToString()!="PBRDebugControlsWindow")return true;found=hwnd;return false;},0);
        host=found;if(host==0||Read(10)!=1)throw new InvalidOperationException("PBR runtime console protocol unavailable. Update the complete package.");
        Title="PBR — эффекты в игре";SystemBackdrop=new MicaBackdrop();root=(Grid)XamlReader.Load(Layout);Content=root;
        ExtendsContentIntoTitleBar=true;SetTitleBar((UIElement)root.FindName("TitleBar"));
        Aurora.Shared.WindowChrome.MatchCaption(this,root);
        var area=Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id,Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(Math.Min(540,area.Width-32),Math.Min(440,area.Height-32)));
        AppWindow.Move(new Windows.Graphics.PointInt32(area.X+32,area.Y+32));
        enabled=(ToggleSwitch)root.FindName("Enabled");mirror=(ToggleSwitch)root.FindName("Mirror");
        sliders=Enumerable.Range(0,6).Select(i=>(Slider)root.FindName("Slider"+i)).ToArray();
        percentages=Enumerable.Range(0,6).Select(i=>(TextBlock)root.FindName("Percent"+i)).ToArray();view=(ComboBox)root.FindName("View");
        for(int i=0;i<6;i++){int setting=i+2;sliders[i].ValueChanged+=(_,e)=>{percentages[setting-2].Text=$"{(int)e.NewValue}%";if(!updating)Write(setting,(int)e.NewValue);};}
        enabled.Toggled+=(_,_)=>{if(!updating)Write(0,enabled.IsOn?1:0);};
        mirror.Toggled+=(_,_)=>{if(!updating){Write(1,mirror.IsOn?1:0);Refresh();}};
        view.SelectionChanged+=(_,_)=>{if(!updating&&view.SelectedIndex>=0)Write(8,view.SelectedIndex);};
        ((Button)root.FindName("Close")).Click+=(_,_)=>Close();
        timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};timer.Tick+=(_,_)=>{try{GetWindowThreadProcessId(host,out uint current);if(current!=(uint)pid){Close();return;}Refresh();}catch{Close();}};
        Closed+=(_,_)=>timer.Stop();UiLanguage.Bind((ComboBox)root.FindName("Language"),root,()=>Title=UiLanguage.Text("PBR — эффекты в игре"));Refresh();timer.Start();
    }
    int Read(int index){if(SendMessageTimeout(host,GetSetting,(nuint)index,0,3,250,out var value)==0)throw new InvalidOperationException("Game settings host is unavailable");return unchecked((int)value);}
    void Write(int index,int value){if(SendMessageTimeout(host,SetSetting,(nuint)index,(nint)value,3,250,out var result)==0||result!=1){((InfoBar)root.FindName("Status")).Message=UiLanguage.Text("Не удалось применить настройку. Проверьте, что игра отвечает.");((InfoBar)root.FindName("Status")).IsOpen=true;}}
    void Refresh(){updating=true;try{enabled.IsOn=Read(0)!=0;mirror.IsOn=Read(1)!=0;for(int i=0;i<6;i++){sliders[i].Value=Read(i+2);percentages[i].Text=$"{(int)sliders[i].Value}%";}view.SelectedIndex=Read(8);bool allowed=Read(9)!=0;enabled.IsEnabled=allowed;mirror.IsEnabled=allowed;((FrameworkElement)root.FindName("Values")).IsHitTestVisible=allowed;if(!allowed){var status=(InfoBar)root.FindName("Status");status.Message=UiLanguage.Text("Общий PBR выключен в лаунчере. Включите его перед следующим запуском игры.");status.IsOpen=true;}}finally{updating=false;}}
    const string Layout="""
    <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
      <Grid.Resources><Style x:Key="Label" TargetType="TextBlock"><Setter Property="FontSize" Value="12"/><Setter Property="VerticalAlignment" Value="Center"/></Style></Grid.Resources>
      <Grid.RowDefinitions><RowDefinition Height="32"/><RowDefinition Height="*"/><RowDefinition Height="38"/></Grid.RowDefinitions>
      <Grid x:Name="TitleBar" Margin="16,0,140,0"><TextBlock Text="PBR" FontSize="12" VerticalAlignment="Center"/></Grid>
      <StackPanel Grid.Row="1" Margin="16,4,16,8" Spacing="6">
        <Grid><TextBlock Text="Эффекты" FontSize="18" FontWeight="SemiBold" VerticalAlignment="Center"/><ComboBox x:Name="Language" AutomationProperties.Name="Language / Язык" HorizontalAlignment="Right" Width="112" MinHeight="28"><ComboBoxItem Content="Русский"/><ComboBoxItem Content="English"/></ComboBox></Grid>
        <Grid><ToggleSwitch x:Name="Enabled" Header="PBR" OnContent="Вкл." OffContent="Выкл." MinWidth="100"/><ToggleSwitch x:Name="Mirror" Header="Зеркало" OnContent="Вкл." OffContent="Выкл." HorizontalAlignment="Right" MinWidth="100"/></Grid>
        <Border Background="{ThemeResource CardBackgroundFillColorDefaultBrush}" BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}" BorderThickness="1" CornerRadius="8" Padding="12,4"><Grid x:Name="Values" ColumnSpacing="10">
          <Grid.ColumnDefinitions><ColumnDefinition Width="136"/><ColumnDefinition Width="*"/><ColumnDefinition Width="40"/></Grid.ColumnDefinitions>
          <Grid.RowDefinitions><RowDefinition Height="30"/><RowDefinition Height="30"/><RowDefinition Height="30"/><RowDefinition Height="30"/><RowDefinition Height="30"/><RowDefinition Height="30"/></Grid.RowDefinitions>
          <TextBlock Text="Интенсивность PBR" Style="{StaticResource Label}"/><Slider x:Name="Slider0" Grid.Column="1" Minimum="0" Maximum="100" StepFrequency="1" Height="30"/><TextBlock x:Name="Percent0" Grid.Column="2" Style="{StaticResource Label}" HorizontalAlignment="Right"/>
          <TextBlock Grid.Row="1" Text="Металличность" Style="{StaticResource Label}"/><Slider x:Name="Slider1" Grid.Row="1" Grid.Column="1" Minimum="0" Maximum="100" StepFrequency="1" Height="30"/><TextBlock x:Name="Percent1" Grid.Row="1" Grid.Column="2" Style="{StaticResource Label}" HorizontalAlignment="Right"/>
          <TextBlock Grid.Row="2" Text="Шероховатость" Style="{StaticResource Label}"/><Slider x:Name="Slider2" Grid.Row="2" Grid.Column="1" Minimum="4" Maximum="100" StepFrequency="1" Height="30"/><TextBlock x:Name="Percent2" Grid.Row="2" Grid.Column="2" Style="{StaticResource Label}" HorizontalAlignment="Right"/>
          <TextBlock Grid.Row="3" Text="Затенение AO" Style="{StaticResource Label}"/><Slider x:Name="Slider3" Grid.Row="3" Grid.Column="1" Minimum="0" Maximum="100" StepFrequency="1" Height="30"/><TextBlock x:Name="Percent3" Grid.Row="3" Grid.Column="2" Style="{StaticResource Label}" HorizontalAlignment="Right"/>
          <TextBlock Grid.Row="4" Text="Отражения" Style="{StaticResource Label}"/><Slider x:Name="Slider4" Grid.Row="4" Grid.Column="1" Minimum="0" Maximum="200" StepFrequency="1" Height="30"/><TextBlock x:Name="Percent4" Grid.Row="4" Grid.Column="2" Style="{StaticResource Label}" HorizontalAlignment="Right"/>
          <TextBlock Grid.Row="5" Text="Свет окружения" Style="{StaticResource Label}"/><Slider x:Name="Slider5" Grid.Row="5" Grid.Column="1" Minimum="0" Maximum="200" StepFrequency="1" Height="30"/><TextBlock x:Name="Percent5" Grid.Row="5" Grid.Column="2" Style="{StaticResource Label}" HorizontalAlignment="Right"/>
        </Grid></Border>
        <ComboBox x:Name="View" HorizontalAlignment="Stretch" HorizontalContentAlignment="Stretch" MinHeight="30"><ComboBoxItem Content="Полный PBR"/><ComboBoxItem Content="Карта металличности"/><ComboBoxItem Content="Карта шероховатости"/><ComboBoxItem Content="Карта AO"/><ComboBoxItem Content="Отражения"/><ComboBoxItem Content="Карта нормалей"/><ComboBoxItem Content="Базовый цвет"/></ComboBox>
        <InfoBar x:Name="Status" IsOpen="False" Severity="Informational" IsClosable="True"/>
      </StackPanel>
      <Border Grid.Row="2" Background="{ThemeResource LayerFillColorDefaultBrush}" Padding="16,3"><Grid><TextBlock Text="F9 — показать / скрыть" FontSize="11" Foreground="{ThemeResource TextFillColorSecondaryBrush}" VerticalAlignment="Center"/><Button x:Name="Close" Content="Закрыть" HorizontalAlignment="Right" MinWidth="88" MinHeight="28" Padding="10,2"/></Grid></Border>
    </Grid>

    """;
}
