using Aurora.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Aurora.Shared;

namespace Aurora.Launcher;
internal static class Program
{
 [STAThread] static void Main(){WinRT.ComWrappersSupport.InitializeComWrappers();Application.Start(parameters=>{SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));_=new AuroraApp();});}
}
sealed partial class AuroraApp:Application
{
 Window? window;
 public AuroraApp(){InitializeComponent();}
 protected override void OnLaunched(LaunchActivatedEventArgs args){window=Environment.GetCommandLineArgs().Contains("--console")?new ConsoleWindow():new MainWindow();window.Activate();}
}
record Preferences(string Game="",bool Console=false);
sealed class MainWindow:Window
{
 readonly NavigationView navigation=new(){IsBackButtonVisible=NavigationViewBackButtonVisible.Collapsed,PaneDisplayMode=NavigationViewPaneDisplayMode.Left,IsSettingsVisible=true};
 readonly StackPanel body=new(){Spacing=14,Margin=new Thickness(28)};
 readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap};
 readonly string stateRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aurora");
 Preferences prefs;
 List<VerifiedMod> mods=[];
 readonly UpdateService updates=new();
 readonly string publicKey=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Assets","publisher.pem"));
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern uint GetPrivateProfileInt(string section,string key,int fallback,string file);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool WritePrivateProfileString(string section,string key,string value,string file);
 public MainWindow()
 {
  Title="Аврора";SystemBackdrop=new MicaBackdrop();AppWindow.Resize(new(1000,720));
  Directory.CreateDirectory(stateRoot);
  try{prefs=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(Path.Combine(stateRoot,"settings.json")))??new();}catch{prefs=new();}
  navigation.Content=new ScrollViewer{Content=body};WindowChrome.Attach(this,navigation,"Аврора");
  var language=new NavigationViewItem{Content=UiLanguage.English?"English":"Русский",Icon=new FontIcon{Glyph="\uE774"},SelectsOnInvoked=false};
  ToolTipService.SetToolTip(language,"Язык / Language");navigation.FooterMenuItems.Add(language);
  var languageMenu=new MenuFlyout();
  var russian=new RadioMenuFlyoutItem{Text="Русский",IsChecked=!UiLanguage.English};var english=new RadioMenuFlyoutItem{Text="English",IsChecked=UiLanguage.English};
  languageMenu.Items.Add(russian);languageMenu.Items.Add(english);
  void ChangeLanguage(bool en){UiLanguage.SetEnglish(en);UiLanguage.Apply((FrameworkElement)Content);language.Content=en?"English":"Русский";if(navigation.SettingsItem is NavigationViewItem settings)settings.Content=UiLanguage.Text("Настройки");foreach(var item in navigation.MenuItems.OfType<NavigationViewItem>())if(item.Content is string text)item.Content=UiLanguage.Text(text);}
  russian.Click+=(_,_)=>ChangeLanguage(false);english.Click+=(_,_)=>ChangeLanguage(true);
  navigation.ItemInvoked+=(_,e)=>{if(e.InvokedItemContainer==language)languageMenu.ShowAt(language);};
  navigation.Loaded+=(_,_)=>ChangeLanguage(UiLanguage.English);
  navigation.SelectionChanged+=(_,e)=>{if(e.IsSettingsSelected)Settings();else if(e.SelectedItem is NavigationViewItem item&&item.Tag is VerifiedMod mod)ShowMod(mod);else Home();};
  Discover();
  _=CheckUpdates();
 }
 void Save(){File.WriteAllText(Path.Combine(stateRoot,"settings.json"),JsonSerializer.Serialize(prefs));}
 void Discover()
 {
  navigation.MenuItems.Clear();navigation.MenuItems.Add(new NavigationViewItem{Content="Главная",Icon=new SymbolIcon(Symbol.Home)});
  if(Directory.Exists(prefs.Game))
  {
   var result=new ModCatalog(publicKey).Discover(SteamDiscovery.FindRoots(prefs.Game));mods=result.Mods;
   foreach(var mod in mods)navigation.MenuItems.Add(new NavigationViewItem{Content=mod.Manifest.Name,Tag=mod,Icon=new SymbolIcon(Symbol.Library)});
   status.Text=result.Errors.Count==0?"":string.Join("\n",result.Errors);
  }
  else mods=[];
  navigation.SelectedItem=navigation.MenuItems[0];Home();
 }
 TextBlock Heading(string text)=>new(){Text=UiLanguage.Text(text),FontSize=28,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold};
 Button Button(string label,Action action){var button=new Button{Content=UiLanguage.Text(label)};button.Click+=(_,_)=>{try{action();}catch(Exception ex){Notice(ex.Message);}};return button;}
 Button AsyncButton(string label,Func<Task> action){var button=new Button{Content=UiLanguage.Text(label)};button.Click+=async(_,_)=>{button.IsEnabled=false;try{await action();}catch(Exception ex){Notice(ex.Message);}finally{button.IsEnabled=true;}};return button;}
 void Home()
 {
  body.Children.Clear();body.Children.Add(Heading("Аврора"));
  var image=Path.Combine(AppContext.BaseDirectory,"Assets","game.jpg");
  if(File.Exists(image))body.Children.Add(new Image{Source=new BitmapImage(new Uri(image)),Width=560,Height=300,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left});
  body.Children.Add(new TextBlock{Text="Workers & Resources: Soviet Republic",FontSize=20});
  body.Children.Add(new TextBlock{Text=mods.Count==0?"Моды не найдены. Откройте настройки и выполните поиск.":"Выберите мод в меню слева.",TextWrapping=TextWrapping.Wrap});
  body.Children.Add(Button("Запустить игру",LaunchGame));body.Children.Add(status);
  body.Children.Add(Button("Мастер моделей",()=>{var editor=Path.Combine(AppContext.BaseDirectory,"Editor","Aurora.Editor.exe");Process.Start(new ProcessStartInfo(editor){UseShellExecute=true});}));
  UiLanguage.Apply(body);
 }
 Border StageBadge(string stage)
 {
  bool alpha=stage.Equals("alpha",StringComparison.OrdinalIgnoreCase);
  var stroke=new SolidColorBrush(alpha?Windows.UI.Color.FromArgb(135,190,135,67):Windows.UI.Color.FromArgb(125,121,142,161));
  var fill=new SolidColorBrush(alpha?Windows.UI.Color.FromArgb(35,190,135,67):Windows.UI.Color.FromArgb(31,121,142,161));
  var text=new TextBlock{Text=UiLanguage.Text(alpha?"Альфа":"Бета"),FontSize=11,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold};
  return new Border{Child=text,Background=fill,BorderBrush=stroke,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(3),Padding=new Thickness(7,2,7,2),VerticalAlignment=VerticalAlignment.Center};
 }
 Border FeatureSurface(UIElement content)
 {
  return new Border{
   Child=content,Padding=new Thickness(16,13,16,13),CornerRadius=new CornerRadius(4),
   Background=(Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
   BorderBrush=(Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
   BorderThickness=new Thickness(1),HorizontalAlignment=HorizontalAlignment.Stretch};
 }
 void ShowMod(VerifiedMod mod)
 {
  body.Children.Clear();body.Children.Add(Heading(mod.Manifest.Name));
  body.Children.Add(new TextBlock{Text="Изменения применяются при следующем запуске игры.",TextWrapping=TextWrapping.Wrap});
  var experimental=new StackPanel{Spacing=9};
  string config=ModCatalog.SafePath(prefs.Game,"mods/"+mod.Manifest.Config);
  foreach(var option in mod.Manifest.Options)
  {
   bool isAlpha=option.Stage.Equals("alpha",StringComparison.OrdinalIgnoreCase);
   bool isBeta=option.Beta || option.Stage.Equals("beta",StringComparison.OrdinalIgnoreCase);
   var row=new Grid{ColumnSpacing=20};
   row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
   row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var copy=new StackPanel{Spacing=5,VerticalAlignment=VerticalAlignment.Center};
   var line=new Grid{ColumnSpacing=10};
   line.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
   line.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   line.Children.Add(new TextBlock{Text=UiLanguage.Text(option.Label),FontSize=14,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center});
   if(isAlpha||isBeta){var badge=StageBadge(isAlpha?"alpha":"beta");Grid.SetColumn(badge,1);line.Children.Add(badge);}
   copy.Children.Add(line);
   if(!string.IsNullOrWhiteSpace(option.Description))
    copy.Children.Add(new TextBlock{Text=UiLanguage.Text(option.Description),FontSize=12,TextWrapping=TextWrapping.Wrap,Opacity=0.78});
   row.Children.Add(copy);
   bool oldValue=GetPrivateProfileInt(option.Section,option.Key,option.Default?1:0,config)!=0;
   var toggle=new ToggleSwitch{IsOn=oldValue,OnContent=UiLanguage.Text("Вкл."),OffContent=UiLanguage.Text("Выкл."),VerticalAlignment=VerticalAlignment.Center,MinWidth=94};
   Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle,UiLanguage.Text(option.Label));
   Grid.SetColumn(toggle,1);row.Children.Add(toggle);
   bool writing=false;
   toggle.Toggled+=async(_,_)=>{
    if(writing)return;
    bool next=toggle.IsOn;
    writing=true;toggle.IsEnabled=false;
    try{
     await Task.Run(()=>VerifyCurrent(mod));
     Directory.CreateDirectory(Path.GetDirectoryName(config)!);
     if(!WritePrivateProfileString(option.Section,option.Key,next?"1":"0",config))
      throw new System.ComponentModel.Win32Exception();
    }
    catch(Exception ex){toggle.IsOn=!next;Notice(ex.Message);}
    finally{toggle.IsEnabled=true;writing=false;}
   };
   if(isAlpha||isBeta)experimental.Children.Add(FeatureSurface(row));
   else body.Children.Add(FeatureSurface(row));
  }
  if(experimental.Children.Count>0)
   body.Children.Add(new Expander{Header="Экспериментальные функции",Content=experimental,IsExpanded=false,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch});
  body.Children.Add(Button("Установить / обновить мод",()=>Install(mod)));
  body.Children.Add(Button("Запустить игру",LaunchGame));
  UiLanguage.Apply(body);
 }
 void VerifyCurrent(VerifiedMod mod){var current=new ModCatalog(publicKey).Verify(mod.Folder);if(current.Id!=mod.Manifest.Id||current.Version!=mod.Manifest.Version)throw new InvalidDataException("Мод обновился. Повторите поиск.");}
 void Install(VerifiedMod mod)
 {
  VerifyCurrent(mod);
  if(Process.GetProcessesByName("SOVIET64").Length>0)throw new InvalidOperationException("Закройте игру перед установкой.");
  ModuleInstaller.Install(mod,prefs.Game,publicKey);
  Notice("Мод установлен. Настройки сохранены.");
 }
 void LaunchGame()
 {
  if(!File.Exists(Path.Combine(prefs.Game,"SOVIET64.exe")))throw new FileNotFoundException("Выберите папку игры в настройках.");
  foreach(var mod in mods)VerifyCurrent(mod);
  var start=new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"Aurora.Bootstrap.exe")){WorkingDirectory=prefs.Game,UseShellExecute=false};
  start.ArgumentList.Add(prefs.Game);
  var libraries=mods.SelectMany(x=>x.Manifest.Files).Where(x=>x.Path.StartsWith("Payload/")&&x.Path.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)).GroupBy(x=>x.Path,StringComparer.OrdinalIgnoreCase).ToArray();
  if(libraries.Any(x=>x.Select(f=>f.Sha256.ToLowerInvariant()).Distinct().Count()>1))throw new InvalidDataException("Конфликт библиотек модов. Установите совместимые версии.");
  foreach(var file in libraries.Select(x=>x.First()).OrderBy(x=>x.Path.EndsWith("/m.dll")?0:1))
  {
   string installed=ModCatalog.SafePath(prefs.Game,file.Path[8..]);
   using var input=File.OpenRead(installed);if(!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input)).Equals(file.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Установленная библиотека отличается. Установите мод заново: "+file.Path);
   start.ArgumentList.Add(installed);start.ArgumentList.Add(file.Sha256.ToLowerInvariant());
  }
  Process.Start(start);
  if(prefs.Console){var console=new AlphaConsole();console.Activate();}
 }
 void Settings()
 {
  body.Children.Clear();body.Children.Add(Heading("Настройки"));
  var path=new TextBox{Header="Папка игры SovietRepublic",Text=prefs.Game};body.Children.Add(path);
  body.Children.Add(AsyncButton("Выбрать папку…",async()=>{var picker=new Windows.Storage.Pickers.FolderPicker();picker.FileTypeFilter.Add("*");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));var folder=await picker.PickSingleFolderAsync();if(folder!=null)path.Text=folder.Path;}));
  body.Children.Add(Button("Найти моды",()=>{if(!File.Exists(Path.Combine(path.Text,"SOVIET64.exe")))throw new InvalidDataException("В выбранной папке нет SOVIET64.exe.");prefs=prefs with{Game=Path.GetFullPath(path.Text)};Save();Discover();}));
  var consoleRow=new Grid{ColumnSpacing=20};
  consoleRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
  consoleRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  var consoleHeading=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};
  consoleHeading.Children.Add(new TextBlock{Text=UiLanguage.Text("Консоль"),FontSize=14,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
  consoleHeading.Children.Add(StageBadge("alpha"));
  consoleRow.Children.Add(consoleHeading);
  var console=new ToggleSwitch{IsOn=prefs.Console,OnContent=UiLanguage.Text("Вкл."),OffContent=UiLanguage.Text("Выкл.")};
  Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(console,UiLanguage.Text("Консоль"));
  Grid.SetColumn(console,1);consoleRow.Children.Add(console);
  console.Toggled+=(_,_)=>{prefs=prefs with{Console=console.IsOn};Save();};
  body.Children.Add(FeatureSurface(consoleRow));
  body.Children.Add(Button("Открыть консоль",()=>{var w=new AlphaConsole();w.Activate();}));
  body.Children.Add(Button("Проверить обновления",()=>_=CheckUpdates()));
  body.Children.Add(AsyncButton("Установить обновление",async()=>{var folder=await updates.DownloadVerified(publicKey);if(folder==null){Notice("Обновлений нет.");return;}updates.StartReplacement(folder);Application.Current.Exit();}));
  body.Children.Add(status);
  UiLanguage.Apply(body);
 }
 async Task CheckUpdates(){try{var release=await updates.Check();status.Text=release==null?"Установлена актуальная версия.":"Доступно обновление Авроры. Установка — в настройках.";}catch(Exception ex){status.Text="Не удалось проверить обновления: "+ex.Message;}}
 async void Notice(string message){await new ContentDialog{Title="Аврора",Content=message,CloseButtonText="ОК",XamlRoot=((FrameworkElement)Content).XamlRoot}.ShowAsync();}
}
sealed class AlphaConsole:Window
{
 public AlphaConsole(){Title="Аврора — Консоль";SystemBackdrop=new MicaBackdrop();AppWindow.Resize(new(640,440));var panel=new StackPanel{Margin=new Thickness(20),Spacing=12};var log=new TextBox{IsReadOnly=true,AcceptsReturn=true,Height=230,Text="In development"};var input=new TextBox{PlaceholderText="Message"};void Reply(){if(string.IsNullOrWhiteSpace(input.Text))return;log.Text+="\r\n> "+input.Text+"\r\nIn development";input.Text="";}input.KeyDown+=(_,e)=>{if(e.Key==Windows.System.VirtualKey.Enter){Reply();e.Handled=true;}};var send=new Button{Content="Send"};send.Click+=(_,_)=>Reply();panel.Children.Add(log);panel.Children.Add(input);panel.Children.Add(send);WindowChrome.Attach(this,panel,Title);}
}
