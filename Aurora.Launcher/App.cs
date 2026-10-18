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
using Microsoft.UI.Xaml.Markup;

namespace Aurora.Launcher;
internal static class Program
{
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SetCurrentProcessExplicitAppUserModelID(string id);
 [STAThread] static void Main(){var args=Environment.GetCommandLineArgs();SetCurrentProcessExplicitAppUserModelID(args.Contains("--editor")?"Av1ra.Aurora.ModelEditor":args.Contains("--console")?"Av1ra.Aurora.GraphicControls":"Av1ra.Aurora.Launcher");WinRT.ComWrappersSupport.InitializeComWrappers();Application.Start(parameters=>{SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));_=new AuroraApp();});}
}
sealed partial class AuroraApp:Application
{
 Window? window; GameConsoleHost? gameConsole;
 public AuroraApp(){InitializeComponent();}
 protected override void OnLaunched(LaunchActivatedEventArgs args)
 {
  try{var command=Environment.GetCommandLineArgs();if(command.Contains("--game-console")){int i=Array.IndexOf(command,"--pid");if(i<0||i+1>=command.Length||!int.TryParse(command[i+1],out int pid))throw new ArgumentException("Missing game PID");gameConsole=new GameConsoleHost(pid);return;}window=command.Contains("--console")?new ConsoleWindow():command.Contains("--editor")&&File.Exists(Path.Combine(AppContext.BaseDirectory,"Editor","acode.lnchr"))?EditorHost.Create():new MainWindow();window.Activate();if(command.Contains("--open-editor")&&window is MainWindow main)_=main.OpenEditorFromEntry();}
  catch(Exception ex){window=new MainWindow();window.Activate();File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aurora","editor-startup-error.txt"),ex.ToString());}
 }
}
record Preferences(string Game="",bool Console=false);
sealed class MainWindow:Window
{
 readonly NavigationView navigation=new(){IsBackButtonVisible=NavigationViewBackButtonVisible.Collapsed,PaneDisplayMode=NavigationViewPaneDisplayMode.Left,OpenPaneLength=200,CompactPaneLength=48,IsSettingsVisible=false};
 readonly NavigationViewItem settingsItem=new(){Tag="settings",Icon=new SymbolIcon(Symbol.Setting)};
 readonly StackPanel body=new(){Spacing=14,Margin=new Thickness(28)};
 TextBlock status=new(){TextWrapping=TextWrapping.Wrap};
 TextBlock? versionText;string versionMessage="Установлена актуальная версия 0.2";
 readonly string stateRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aurora");
 Preferences prefs;
 List<VerifiedMod> mods=[];
 List<WorkshopUpdateOffer> workshopOffers=[];
 bool workshopPromptActive;
 readonly UpdateService updates=new();
 readonly string publicKey=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Assets","publisher.pem"));
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern uint GetPrivateProfileInt(string section,string key,int fallback,string file);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool WritePrivateProfileString(string section,string key,string value,string file);
 public MainWindow()
 {
  Title="Aurora Launcher";SystemBackdrop=new MicaBackdrop();AppWindow.Resize(new(1000,720));
  Directory.CreateDirectory(stateRoot);
  try{prefs=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(Path.Combine(stateRoot,"settings.json")))??new();}catch{prefs=new();}
  string gamePath=Path.Combine(stateRoot,"game-path.txt");if(File.Exists(gamePath)){string selected=File.ReadAllText(gamePath).Trim();if(File.Exists(Path.Combine(selected,"SOVIET64.exe")))prefs=prefs with{Game=selected};}
  if(!File.Exists(Path.Combine(prefs.Game,"SOVIET64.exe"))&&File.Exists(Path.Combine(InstallationPaths.SuggestedGame,"SOVIET64.exe")))prefs=prefs with{Game=InstallationPaths.SuggestedGame};
  navigation.Content=new ScrollViewer{Content=body};WindowChrome.Attach(this,navigation,"Aurora Launcher");
  var language=new NavigationViewItem{Content=UiLanguage.English?"English":"Русский",Icon=new FontIcon{Glyph="\uE774"},SelectsOnInvoked=false};
  ToolTipService.SetToolTip(language,"Язык / Language");navigation.FooterMenuItems.Add(language);navigation.FooterMenuItems.Add(settingsItem);
  var languageMenu=new MenuFlyout();
  var russian=new MenuFlyoutItem{Text="Русский"};var english=new MenuFlyoutItem{Text="English"};
  languageMenu.Items.Add(russian);languageMenu.Items.Add(english);
  void ChangeLanguage(bool en){UiLanguage.SetEnglish(en);UiLanguage.Apply((FrameworkElement)Content);language.Content=en?"English":"Русский";ToolTipService.SetToolTip(language,en?"Language":"Язык");settingsItem.Content=en?"Settings":"Настройки";ToolTipService.SetToolTip(settingsItem,en?"Settings":"Настройки");foreach(var item in navigation.MenuItems.OfType<NavigationViewItem>())if(item.Content is string text)item.Content=UiLanguage.Text(text);if(navigation.SelectedItem==settingsItem)Settings();else if(navigation.SelectedItem is NavigationViewItem selected&&selected.Tag is VerifiedMod module)ShowMod(module);else Home();}
  russian.Click+=(_,_)=>ChangeLanguage(false);english.Click+=(_,_)=>ChangeLanguage(true);
  navigation.ItemInvoked+=(_,e)=>{if(e.InvokedItemContainer==language)languageMenu.ShowAt(language);};
  navigation.Loaded+=(_,_)=>ChangeLanguage(UiLanguage.English);
  navigation.SelectionChanged+=(_,e)=>{if(e.SelectedItem==settingsItem)Settings();else if(e.SelectedItem is NavigationViewItem item&&item.Tag is VerifiedMod mod)ShowMod(mod);else Home();};
  Home();navigation.Loaded+=(_,_)=>Discover();
  _=CheckUpdates();
 }
 void Save(){File.WriteAllText(Path.Combine(stateRoot,"settings.json"),JsonSerializer.Serialize(prefs));File.WriteAllText(Path.Combine(stateRoot,"game-path.txt"),prefs.Game);}
 void Discover(){_=DiscoverAsync();}
 void ApplyDiscovery(List<VerifiedMod> found,List<string> errors,List<WorkshopUpdateOffer> offers)
 {
  mods=found;workshopOffers=offers;navigation.MenuItems.Clear();navigation.MenuItems.Add(new NavigationViewItem{Content=UiLanguage.Text("Главная"),Icon=new SymbolIcon(Symbol.Home)});
  foreach(var mod in mods.Where(m=>m.Manifest.Placement=="navigation"))navigation.MenuItems.Add(new NavigationViewItem{Content=UiLanguage.Text(mod.Manifest.Name),Tag=mod,Icon=new FontIcon{Glyph=mod.Manifest.Category=="graphics"?"\uE790":mod.Manifest.Category=="camera"?"\uE722":"\uE8F1"}});
  status.Text=string.Join("\n",errors);navigation.SelectedItem=navigation.MenuItems[0];Home();
 }
 TextBlock Heading(string text)=>new(){Text=UiLanguage.Text(text),FontSize=28,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold};
 void ClearBody(){body.Spacing=14;string previous=status.Text;body.Children.Clear();status=new TextBlock{Text=previous,TextWrapping=TextWrapping.Wrap,Opacity=.8};}
 Button Button(string label,Action action){var button=new Button{Content=UiLanguage.Text(label)};button.Click+=(_,_)=>{try{action();}catch(Exception ex){Notice(ex.Message);}};return button;}
 Button AsyncButton(string label,Func<Task> action){var button=new Button{Content=UiLanguage.Text(label)};button.Click+=async(_,_)=>{button.IsEnabled=false;try{await action();}catch(Exception ex){Notice(ex.Message);}finally{button.IsEnabled=true;}};return button;}
 internal static Border Badge(string text)
 {
  var border=(Border)XamlReader.Load("<Border xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Background=\"{ThemeResource SubtleFillColorSecondaryBrush}\" BorderBrush=\"{ThemeResource CardStrokeColorDefaultBrush}\" BorderThickness=\"1\" CornerRadius=\"4\" Padding=\"8,2\" VerticalAlignment=\"Center\"/>");
  border.Child=new TextBlock{Text=UiLanguage.Text(text),FontSize=11,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold};return border;
 }
 static StackPanel RowTitle(string title,string? badge=null){var panel=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};panel.Children.Add(new TextBlock{Text=UiLanguage.Text(title),FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});if(badge!=null)panel.Children.Add(Badge(badge));return panel;}
 static Border Card(UIElement content){var border=(Border)XamlReader.Load("<Border xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Background=\"{ThemeResource CardBackgroundFillColorDefaultBrush}\" BorderBrush=\"{ThemeResource CardStrokeColorDefaultBrush}\" BorderThickness=\"1\" CornerRadius=\"8\" Padding=\"14,6\" HorizontalAlignment=\"Stretch\"/>");border.Child=content;return border;}
 static Border FeatureCard(string title,ToggleSwitch toggle,string? badge=null,string? description=null,UIElement? action=null)
 {
  var grid=new Grid{ColumnSpacing=18};grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
  var text=new StackPanel{Spacing=5};text.Children.Add(RowTitle(title,badge));if(description!=null)text.Children.Add(new TextBlock{Text=UiLanguage.Text(description),FontSize=12,Opacity=.72,TextWrapping=TextWrapping.Wrap});
  if(action!=null)text.Children.Add(action);
  toggle.VerticalAlignment=VerticalAlignment.Center;toggle.OnContent=UiLanguage.Text("Вкл.");toggle.OffContent=UiLanguage.Text("Выкл.");Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle,title);Grid.SetColumn(toggle,1);grid.Children.Add(text);grid.Children.Add(toggle);return Card(grid);
 }
 void Home()
 {
  ClearBody();
  body.Children.Add(new TextBlock{Text="Workers & Resources: Soviet Republic",FontSize=25,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
  bool found=File.Exists(Path.Combine(prefs.Game,"SOVIET64.exe"));
  body.Children.Add(new TextBlock{Text=UiLanguage.Text(found?"Игра найдена":"Выберите папку игры в настройках"),Opacity=.75});
  var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
  var play=AsyncButton("Запустить игру",LaunchGame);play.IsEnabled=found;actions.Children.Add(play);
  actions.Children.Add(Button("Настройки игры и модулей",Settings));body.Children.Add(actions);
  var moduleState=new StackPanel{Spacing=8};moduleState.Children.Add(RowTitle("Модули"));
  moduleState.Children.Add(new TextBlock{Text=mods.Count==0?UiLanguage.Text("Совместимые модули не найдены."):string.Join("\n",mods.Select(x=>UiLanguage.Text(x.Manifest.Name))),TextWrapping=TextWrapping.Wrap,Opacity=.75});
  moduleState.Children.Add(AsyncButton("Проверить обновления модулей",DiscoverAsync));body.Children.Add(Card(moduleState));
  // A registered ID is an internal source filter, not a permanent home-screen item.
  // Display a Workshop card only when an install or update is actually available.
  if(workshopOffers.Count>0)
  {
   var workshopState=new StackPanel{Spacing=8};
   workshopState.Children.Add(RowTitle("Обновления Workshop"));
   foreach(var offer in workshopOffers)
   {
    var entry=new StackPanel{Spacing=5};
    entry.Children.Add(new TextBlock{Text=offer.Release.Name,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
    string local=offer.Installed==null?UiLanguage.Text("Не установлен"):offer.Installed.Version+" ("+offer.Installed.ReleaseDate+")";
    entry.Children.Add(new TextBlock{Text=UiLanguage.Text("Установлено")+": "+local+"  →  "+UiLanguage.Text("Доступно")+": "+offer.Release.Version+" ("+offer.Release.ReleaseDate+")",TextWrapping=TextWrapping.Wrap,FontSize=12});
    entry.Children.Add(AsyncButton(offer.FirstInstall?"Установить модуль":"Обновить модуль",()=>ShowWorkshopUpdatesAsync([offer])));
    workshopState.Children.Add(entry);
   }
   body.Children.Add(Card(workshopState));
  }
  var editor=new StackPanel{Orientation=Orientation.Horizontal,Spacing=12};editor.Children.Add(new TextBlock{Text="Aurora Editor",VerticalAlignment=VerticalAlignment.Center,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
  editor.Children.Add(AsyncButton("Открыть редактор",OpenEditor));body.Children.Add(Card(editor));
  body.Children.Add(status);versionText=new TextBlock{Text=UiLanguage.Text(versionMessage),Opacity=.65};body.Children.Add(versionText);UiLanguage.Apply(body);
 }
 async Task DiscoverAsync()
 {
  if(!Directory.Exists(prefs.Game)){ApplyDiscovery([],[],[]);return;}
  status.Text=UiLanguage.Text("Поиск модулей…");
  try
  {
   var result=await Task.Run(()=>{
    var modules=new ModCatalog(publicKey).Discover(new[]{Path.Combine(AppContext.BaseDirectory,"Modules")}.Concat(SteamDiscovery.FindRoots(prefs.Game)));
    var updates=WorkshopUpdates.Scan(prefs.Game);
    return (modules.Mods,Errors:modules.Errors.Concat(updates.Errors).ToList(),updates.Offers);
   });
   ApplyDiscovery(result.Mods,result.Errors,result.Offers);
   if(result.Offers.Count>0)await ShowWorkshopUpdatesAsync(result.Offers);
  }
  catch(Exception ex){Notice(ex.Message);}
 }
 async Task ShowWorkshopUpdatesAsync(List<WorkshopUpdateOffer> offers)
 {
  if(workshopPromptActive || ((FrameworkElement)Content).XamlRoot==null)return;
  workshopPromptActive=true;
  bool changed=false;
  try
  {
   foreach(var offer in offers)
   {
    var details=new StackPanel{Spacing=8,MinWidth=390};
    details.Children.Add(new TextBlock{Text=UiLanguage.Text("Установлено")+": "+(offer.Installed==null?UiLanguage.Text("Не установлен"):offer.Installed.Version+" ("+offer.Installed.ReleaseDate+")"),TextWrapping=TextWrapping.Wrap});
    details.Children.Add(new TextBlock{Text=UiLanguage.Text("Доступно")+": "+offer.Release.Version+" ("+offer.Release.ReleaseDate+")",TextWrapping=TextWrapping.Wrap,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
    details.Children.Add(new TextBlock{Text="Steam Workshop ID: "+offer.Registration.SteamId,Opacity=.7});
    var dialog=new ContentDialog{
     Title=UiLanguage.Text(offer.FirstInstall?"Установка модуля":"Обновление модуля")+": "+offer.Release.Name,
     Content=details,PrimaryButtonText=UiLanguage.Text(offer.FirstInstall?"Установить модуль":"Обновить модуль"),
     CloseButtonText=UiLanguage.Text("Позже"),DefaultButton=ContentDialogButton.Primary,
     XamlRoot=((FrameworkElement)Content).XamlRoot
    };
    if(await dialog.ShowAsync()!=ContentDialogResult.Primary)continue;
    if(Process.GetProcessesByName("SOVIET64").Length>0){Notice(UiLanguage.Text("Закройте игру перед установкой."));continue;}
    await Task.Run(()=>WorkshopUpdates.Install(prefs.Game,offer));
    changed=true;
   }
  }
  catch(Exception ex){Notice(ex.Message);}
  finally
  {
   workshopPromptActive=false;
   if(changed)_=DiscoverAsync();
  }
 }

 void ShowMod(VerifiedMod mod)
 {
  ClearBody();body.Children.Add(Heading(mod.Manifest.Name));
  body.Children.Add(new TextBlock{Text="Изменения применяются при следующем запуске игры.",TextWrapping=TextWrapping.Wrap});
  body.Spacing=8;
  var beta=new StackPanel{Spacing=6};
  string config=ModCatalog.SafePath(prefs.Game,"mods/"+mod.Manifest.Config);
  foreach(var option in mod.Manifest.Options)
  {
   var toggle=new ToggleSwitch{IsOn=GetPrivateProfileInt(option.Section,option.Key,option.Default?1:0,config)!=0,OnContent="Вкл.",OffContent="Выкл."};
   toggle.Toggled+=(_,_)=>{try{VerifyCurrent(mod);Directory.CreateDirectory(Path.GetDirectoryName(config)!);if(!WritePrivateProfileString(option.Section,option.Key,toggle.IsOn?"1":"0",config))throw new System.ComponentModel.Win32Exception();}catch(Exception ex){Notice(ex.Message);}};
   var card=FeatureCard(option.Label,toggle,option.Beta?"Бета":null);if(option.Beta)beta.Children.Add(card);else body.Children.Add(card);
  }
  if(beta.Children.Count>0)body.Children.Add(new Expander{Header=RowTitle("Экспериментальные функции","Бета"),Content=beta,IsExpanded=false,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch});
  body.Children.Add(Button("Установить / обновить модуль",()=>Install(mod)));
  UiLanguage.Apply(body);
 }
 Border SettingsModuleCard(VerifiedMod module)
 {
  string file=ModCatalog.SafePath(prefs.Game,"mods/"+module.Manifest.Config);
  void Write(ModOption option,int value){VerifyCurrent(module);Directory.CreateDirectory(Path.GetDirectoryName(file)!);if(!WritePrivateProfileString(option.Section,option.Key,value.ToString(System.Globalization.CultureInfo.InvariantCulture),file))throw new System.ComponentModel.Win32Exception();}
  var controls=new StackPanel{Spacing=8};
  controls.Children.Add(RowTitle(module.Manifest.Name,module.Manifest.Stage=="alpha"?"Альфа":module.Manifest.Stage=="beta"?"Бета":null));
  // Settings modules may contain only sliders, only toggles, or both.
  foreach(var option in module.Manifest.Options)
  {
   var row=new Grid{ColumnSpacing=12};
   row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
   row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
   row.Children.Add(new TextBlock{Text=UiLanguage.Text(option.Label),VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap});
   if(option.Kind=="toggle")
   {
    var toggle=new ToggleSwitch{IsOn=GetPrivateProfileInt(option.Section,option.Key,option.Default?1:0,file)!=0,VerticalAlignment=VerticalAlignment.Center,OnContent=UiLanguage.Text("Вкл."),OffContent=UiLanguage.Text("Выкл.")};
    Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle,option.Label);
    toggle.Toggled+=(_,_)=>{try{Write(option,toggle.IsOn?1:0);}catch(Exception ex){Notice(ex.Message);}};
    Grid.SetColumn(toggle,1);row.Children.Add(toggle);
   }
   else if(option.Kind=="slider")
   {
    var right=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
    var slider=new Slider{Minimum=option.Minimum,Maximum=option.Maximum,StepFrequency=1,Value=GetPrivateProfileInt(option.Section,option.Key,option.DefaultValue,file),Width=230};
    Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(slider,option.Label);
    var value=new TextBlock{Text=((int)slider.Value).ToString(),Width=34,VerticalAlignment=VerticalAlignment.Center};
    slider.ValueChanged+=(_,e)=>{value.Text=((int)e.NewValue).ToString();try{Write(option,(int)e.NewValue);}catch(Exception ex){Notice(ex.Message);}};
    right.Children.Add(slider);right.Children.Add(value);
    Grid.SetColumn(right,1);row.Children.Add(right);
   }
   controls.Children.Add(row);
  }
  return Card(controls);
 }
 void VerifyCurrent(VerifiedMod mod){var current=new ModCatalog(publicKey).Verify(mod.Folder);if(current.Id!=mod.Manifest.Id||current.Version!=mod.Manifest.Version)throw new InvalidDataException("Модуль обновился. Повторите поиск.");}
 void Install(VerifiedMod mod)
 {
  VerifyCurrent(mod);
  if(Process.GetProcessesByName("SOVIET64").Length>0)throw new InvalidOperationException("Закройте игру перед установкой.");
  ModuleInstaller.Install(mod,prefs.Game,publicKey);
  Notice("Модуль установлен. Настройки сохранены.");
 }
 async Task LaunchGame()
 {
  if(!File.Exists(Path.Combine(prefs.Game,"SOVIET64.exe")))throw new FileNotFoundException("Выберите папку игры в настройках.");
  // Steam owns Workshop downloads; re-read signed Workshop manifests before each launch.
  var refreshed=await Task.Run(()=>new ModCatalog(publicKey).Discover(new[]{Path.Combine(AppContext.BaseDirectory,"Modules")}.Concat(SteamDiscovery.FindRoots(prefs.Game))));
  mods=refreshed.Mods;
  foreach(var mod in mods)VerifyCurrent(mod);
  var start=new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"Aurora.Bootstrap.exe")){WorkingDirectory=prefs.Game,UseShellExecute=false,RedirectStandardOutput=true};
  start.ArgumentList.Add(prefs.Game);
  var libraries=mods.SelectMany(x=>x.Manifest.Files).Where(x=>x.Path.StartsWith("Payload/")&&x.Path.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)).GroupBy(x=>x.Path,StringComparer.OrdinalIgnoreCase).ToArray();
  if(libraries.Any(x=>x.Select(f=>f.Sha256.ToLowerInvariant()).Distinct().Count()>1))throw new InvalidDataException("Конфликт библиотек модов. Установите совместимые версии.");
  await Task.Run(()=>{foreach(var module in mods){bool installed=module.Manifest.Files.Where(f=>f.Path.StartsWith("Payload/")).All(f=>{string path=ModCatalog.SafePath(prefs.Game,f.Path[8..]);if(!File.Exists(path))return false;using var stream=File.OpenRead(path);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).Equals(f.Sha256,StringComparison.OrdinalIgnoreCase);});if(!installed)ModuleInstaller.Install(module,prefs.Game,publicKey);}});
  foreach(var file in libraries.Select(x=>x.First()).OrderBy(x=>x.Path.EndsWith("/m.dll")?0:1))
  {
   string installed=ModCatalog.SafePath(prefs.Game,file.Path[8..]);
   using var input=File.OpenRead(installed);if(!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input)).Equals(file.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Установленная библиотека отличается. Установите модуль заново: "+file.Path);
   start.ArgumentList.Add(installed);start.ArgumentList.Add(file.Sha256.ToLowerInvariant());
  }
  using var bootstrap=Process.Start(start)??throw new InvalidOperationException("Cannot start game bootstrap");
  string output=await bootstrap.StandardOutput.ReadToEndAsync();await bootstrap.WaitForExitAsync();
  if(bootstrap.ExitCode!=0)throw new InvalidOperationException("Game launch failed");
  if(prefs.Console&&int.TryParse(output.Trim(),out int pid)){var host=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false};host.ArgumentList.Add("--game-console");host.ArgumentList.Add("--pid");host.ArgumentList.Add(pid.ToString());Process.Start(host);}
 }
 void Settings()
 {
  ClearBody();body.Children.Add(Heading("Настройки"));
  var locations=new StackPanel{Spacing=12};locations.Children.Add(RowTitle("Игра и модули"));
  var path=new TextBox{Header="Папка SovietRepublic",Text=prefs.Game};locations.Children.Add(path);
  var destination=new TextBox{Header=UiLanguage.Text("Папка установки модулей"),IsReadOnly=true,Text=string.IsNullOrWhiteSpace(path.Text)?"":Path.Combine(path.Text,"mods")};path.TextChanged+=(_,_)=>{try{destination.Text=string.IsNullOrWhiteSpace(path.Text)?"":Path.Combine(path.Text,"mods");}catch{destination.Text="";}};locations.Children.Add(destination);
  var locateButtons=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
  locateButtons.Children.Add(AsyncButton("Выбрать папку…",async()=>{var picker=new Windows.Storage.Pickers.FolderPicker();picker.FileTypeFilter.Add("*");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));var folder=await picker.PickSingleFolderAsync();if(folder!=null)path.Text=folder.Path;}));
  locateButtons.Children.Add(Button("Сохранить папку",()=>{if(!File.Exists(Path.Combine(path.Text,"SOVIET64.exe")))throw new InvalidDataException("В выбранной папке нет SOVIET64.exe.");prefs=prefs with{Game=Path.GetFullPath(path.Text)};Save();Notice(UiLanguage.Text("Папка сохранена."));}));
  locateButtons.Children.Add(Button("Workshop",()=>Process.Start(new ProcessStartInfo("https://steamcommunity.com/app/784150/workshop/"){UseShellExecute=true})));locations.Children.Add(locateButtons);body.Children.Add(Card(locations));
  foreach(var module in mods.Where(x=>x.Manifest.Placement=="settings"))body.Children.Add(SettingsModuleCard(module));
  var console=new ToggleSwitch{IsOn=prefs.Console};console.Toggled+=(_,_)=>{prefs=prefs with{Console=console.IsOn};Save();};body.Children.Add(FeatureCard("Консоль",console,"Альфа","Открыть в игре: ~"));
  var updater=new StackPanel{Spacing=10};updater.Children.Add(new TextBox{Header=UiLanguage.Text("Папка лаунчера"),Text=InstallationPaths.Root,IsReadOnly=true});var updateButtons=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
  updateButtons.Children.Add(Button("Проверить обновления",()=>_=CheckUpdates()));
  updateButtons.Children.Add(AsyncButton("Установить обновление",async()=>{var folder=await updates.DownloadVerified(publicKey);if(folder==null){Notice("Обновлений нет.");return;}updates.StartReplacement(folder);Application.Current.Exit();}));updater.Children.Add(updateButtons);versionText=new TextBlock{Text=UiLanguage.Text(versionMessage),Opacity=.65};updater.Children.Add(versionText);updater.Children.Add(status);body.Children.Add(Card(updater));
  UiLanguage.Apply(body);
 }
 internal async Task OpenEditorFromEntry(){try{await OpenEditor();}catch(Exception ex){Notice(ex.Message);}}
 Task OpenEditor()
 {
  var editor=Path.Combine(AppContext.BaseDirectory,"Editor");
  if(!File.Exists(Path.Combine(editor,"acode.lnchr")))
   throw new FileNotFoundException("Aurora Editor отсутствует. Переустановите полный комплект Aurora.");
  var manifest=new ModCatalog(publicKey).Verify(editor);
  if(manifest.Id!="aurora.editor")throw new InvalidDataException("Неверный модуль Aurora Editor.");
  var start=new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"Aurora.Launcher.exe")){UseShellExecute=false};
  start.ArgumentList.Add("--editor");Process.Start(start);
  return Task.CompletedTask;
 }
 async Task CheckUpdates(){try{var release=await updates.Check();versionMessage=release==null?"Установлена актуальная версия 0.2":"Устаревшая версия 0.2";if(versionText!=null)versionText.Text=UiLanguage.Text(versionMessage);}catch(Exception ex){status.Text="Не удалось проверить обновления: "+ex.Message;}}
 async void Notice(string message){await new ContentDialog{Title="Aurora",Content=message,CloseButtonText="ОК",XamlRoot=((FrameworkElement)Content).XamlRoot}.ShowAsync();}
}
sealed class AlphaConsole:Window
{
 public AlphaConsole(){Title="Aurora — Console";SystemBackdrop=new MicaBackdrop();AppWindow.Resize(new(520,260));var panel=new StackPanel{Margin=new Thickness(14),Spacing=8};var log=new TextBox{IsReadOnly=true,AcceptsReturn=true,Height=80,Text="In development"};var input=new TextBox{PlaceholderText="Message"};void Reply(){if(string.IsNullOrWhiteSpace(input.Text))return;log.Text+="\r\n> "+input.Text+"\r\nIn development";input.Text="";}input.KeyDown+=(_,e)=>{if(e.Key==Windows.System.VirtualKey.Enter){Reply();e.Handled=true;}};var send=new Button{Content="Send"};send.Click+=(_,_)=>Reply();var heading=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};heading.Children.Add(new TextBlock{Text=UiLanguage.Text("Консоль"),FontSize=20});heading.Children.Add(MainWindow.Badge("Альфа"));panel.Children.Add(heading);panel.Children.Add(log);panel.Children.Add(input);panel.Children.Add(send);WindowChrome.Attach(this,panel,Title);}
}





