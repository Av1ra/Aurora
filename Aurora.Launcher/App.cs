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
  var launchCommand=Environment.GetCommandLineArgs();int startupCheck=Array.IndexOf(launchCommand,"--editor-startup-check");if(startupCheck>=0){if(startupCheck+1<launchCommand.Length)CheckEditorStartup(launchCommand[startupCheck+1]);Exit();return;}
  try{var command=Environment.GetCommandLineArgs();if(command.Contains("--game-console")){int i=Array.IndexOf(command,"--pid");if(i<0||i+1>=command.Length||!int.TryParse(command[i+1],out int pid))throw new ArgumentException("Missing game PID");gameConsole=new GameConsoleHost(pid);return;}if(command.Contains("--editor")){if(!File.Exists(Path.Combine(AppContext.BaseDirectory,"Editor","acode.lnchr")))throw new FileNotFoundException("Aurora Editor отсутствует. Переустановите полный комплект Aurora.");window=EditorHost.Create();}else window=command.Contains("--console")?new ConsoleWindow():new MainWindow();window.Activate();if(command.Contains("--open-editor")&&window is MainWindow main)_=main.OpenEditorFromEntry();}
  catch(Exception ex){string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aurora");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"editor-startup-error.txt"),ex.ToString());if(launchCommand.Contains("--editor")){window=new Window{Title="Aurora Editor"};var panel=new StackPanel{Padding=new Thickness(20),Spacing=12,Background=new SolidColorBrush(Windows.UI.Color.FromArgb(255,32,32,32))};panel.Children.Add(new TextBlock{Text=UiLanguage.Text("Не удалось открыть Aurora Editor"),FontSize=20,TextWrapping=TextWrapping.Wrap});panel.Children.Add(new TextBlock{Text=ex.GetBaseException().Message,TextWrapping=TextWrapping.Wrap});var close=new Button{Content=UiLanguage.Text("Закрыть")};close.Click+=(_,_)=>window.Close();panel.Children.Add(close);WindowChrome.Attach(window,panel,"Aurora Editor");WindowChrome.ResizeLogical(window,620,280);}else window=new MainWindow();window.Activate();}
 }
 void CheckEditorStartup(string report)
 {
  try{var editor=EditorHost.Create();FrameworkElement? Find(DependencyObject node){if(node is FrameworkElement e&&e.Name=="EditorToolbar")return e;IEnumerable<DependencyObject> children=node switch{Panel p=>p.Children.Cast<DependencyObject>(),Border b when b.Child!=null=>[b.Child],ContentControl c when c.Content is DependencyObject d=>[d],_=>[]};foreach(var child in children){var found=Find(child);if(found!=null)return found;}return null;}
   var toolbarPanel=editor.Content is DependencyObject root?Find(root) as Panel:null;bool toolbar=toolbarPanel!=null&&toolbarPanel.Children.OfType<Button>().Count(b=>b.Flyout!=null)>=5;var menu=toolbarPanel?.Children.OfType<Button>().Select(b=>b.Flyout).OfType<MenuFlyout>().FirstOrDefault();if(menu==null)throw new InvalidDataException("Editor file menu is missing");var presenter=new MenuFlyoutPresenter{Style=menu.MenuFlyoutPresenterStyle};presenter.ApplyTemplate();presenter.SystemBackdrop=null;if(presenter.Background is not SolidColorBrush brush||brush.Color.A!=255)throw new InvalidDataException("Editor menu surface is not opaque");if(!toolbar)throw new InvalidDataException("Editor toolbar is missing from the window content");File.WriteAllText(report,JsonSerializer.Serialize(new{Success=true,Window=editor.GetType().FullName,Toolbar=toolbar}));editor.Close();
  }catch(Exception ex){File.WriteAllText(report,JsonSerializer.Serialize(new{Success=false,Error=ex.ToString()}));}
 }
}
record Preferences(string Game="",bool Console=false);
sealed class MainWindow:Window
{
 readonly NavigationView navigation=new(){IsBackButtonVisible=NavigationViewBackButtonVisible.Collapsed,PaneDisplayMode=NavigationViewPaneDisplayMode.Left,OpenPaneLength=200,CompactPaneLength=48,IsSettingsVisible=false};
 readonly NavigationViewItem settingsItem=new(){Tag="settings",Icon=new SymbolIcon(Symbol.Setting)};
 readonly StackPanel body=new(){Spacing=14,Margin=new Thickness(28)};
 TextBlock status=new(){TextWrapping=TextWrapping.Wrap};
 readonly InfoBar notice=new(){IsOpen=false,IsClosable=true,Margin=new Thickness(24,12,24,0)};
 TextBlock? versionText;Button? installUpdateButton;bool updateAvailable;string versionMessage="Проверка обновлений…";
 readonly string stateRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aurora");
 Preferences prefs;
 List<VerifiedMod> mods=[];
 List<WorkshopUpdateOffer> workshopOffers=[];
 bool workshopPromptActive,applyingDiscovery,discovering,discoverAgain,launching;
 Button? gameLaunchButton;TextBlock? gameStateText;
 readonly UpdateService updates=new();
 readonly string publicKey=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Assets","publisher.pem"));
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern uint GetPrivateProfileInt(string section,string key,int fallback,string file);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool WritePrivateProfileString(string section,string key,string value,string file);
 public MainWindow()
 {
  Title="Aurora Launcher";SystemBackdrop=new MicaBackdrop();WindowChrome.ResizeLogical(this,1000,720);
  Directory.CreateDirectory(stateRoot);
  try{prefs=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(Path.Combine(stateRoot,"settings.json")))??new();}catch{prefs=new();}
  string gamePath=Path.Combine(stateRoot,"game-path.txt");if(File.Exists(gamePath)){string selected=File.ReadAllText(gamePath).Trim();if(File.Exists(Path.Combine(selected,"SOVIET64.exe")))prefs=prefs with{Game=selected};}
  if(!File.Exists(Path.Combine(prefs.Game,"SOVIET64.exe"))&&File.Exists(Path.Combine(InstallationPaths.SuggestedGame,"SOVIET64.exe")))prefs=prefs with{Game=InstallationPaths.SuggestedGame};
  var page=new Grid();page.RowDefinitions.Add(new(){Height=GridLength.Auto});page.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});page.Children.Add(notice);var pageScroll=new ScrollViewer{Content=body,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(pageScroll,1);page.Children.Add(pageScroll);navigation.Content=page;WindowChrome.Attach(this,navigation,"Aurora Launcher");
  var language=new NavigationViewItem{Content=UiLanguage.English?"English":"Русский",Icon=new FontIcon{Glyph="\uE774"},SelectsOnInvoked=false};
  ToolTipService.SetToolTip(language,"Язык / Language");navigation.FooterMenuItems.Add(language);navigation.FooterMenuItems.Add(settingsItem);
  var languageMenu=new MenuFlyout();
  var russian=new MenuFlyoutItem{Text="Русский"};var english=new MenuFlyoutItem{Text="English"};
  languageMenu.Items.Add(russian);languageMenu.Items.Add(english);
  void ChangeLanguage(bool en){UiLanguage.SetEnglish(en);UiLanguage.Apply((FrameworkElement)Content);language.Content=en?"English":"Русский";ToolTipService.SetToolTip(language,en?"Language":"Язык");settingsItem.Content=en?"Settings":"Настройки";ToolTipService.SetToolTip(settingsItem,en?"Settings":"Настройки");foreach(var item in navigation.MenuItems.OfType<NavigationViewItem>())if(item.Content is string text)item.Content=UiLanguage.Text(text);if(ReferenceEquals(navigation.SelectedItem,settingsItem))Settings();else if(navigation.SelectedItem is NavigationViewItem selected&&selected.Tag is VerifiedMod module)ShowMod(module);else Home();}
  russian.Click+=(_,_)=>ChangeLanguage(false);english.Click+=(_,_)=>ChangeLanguage(true);
  navigation.ItemInvoked+=(_,e)=>{if(e.InvokedItemContainer==language)languageMenu.ShowAt(language);};
  navigation.Loaded+=(_,_)=>ChangeLanguage(UiLanguage.English);
  navigation.SelectionChanged+=(_,e)=>{if(applyingDiscovery)return;if(ReferenceEquals(e.SelectedItem,settingsItem))Settings();else if(e.SelectedItem is NavigationViewItem item&&item.Tag is VerifiedMod mod)ShowMod(mod);else Home();};
  Home();navigation.Loaded+=(_,_)=>Discover();
  Activated+=(_,e)=>{if(e.WindowActivationState!=WindowActivationState.Deactivated&&UiLanguage.Reload())ChangeLanguage(UiLanguage.English);RefreshLaunchState();};
  _=CheckUpdates();
 }
 void Save(){File.WriteAllText(Path.Combine(stateRoot,"settings.json"),JsonSerializer.Serialize(prefs));File.WriteAllText(Path.Combine(stateRoot,"game-path.txt"),prefs.Game);}
 void Discover(){_=DiscoverAsync();}
 void ApplyDiscovery(List<VerifiedMod> found,List<string> errors,List<WorkshopUpdateOffer> offers)
 {
  bool wasSettings=ReferenceEquals(navigation.SelectedItem,settingsItem);
  string? selectedId=(navigation.SelectedItem as NavigationViewItem)?.Tag is VerifiedMod previous?previous.Manifest.Id:null;
  applyingDiscovery=true;
  mods=found;workshopOffers=offers;navigation.MenuItems.Clear();navigation.MenuItems.Add(new NavigationViewItem{Content=UiLanguage.Text("Главная"),Icon=new SymbolIcon(Symbol.Home)});
  foreach(var mod in mods.Where(m=>m.Manifest.Placement=="navigation"))navigation.MenuItems.Add(new NavigationViewItem{Content=UiLanguage.Text(mod.Manifest.Name),Tag=mod,Icon=new FontIcon{Glyph=mod.Manifest.Category=="graphics"?"\uE790":mod.Manifest.Category=="camera"?"\uE722":"\uE8F1"}});
  status.Text=string.Join("\n",errors);
  navigation.SelectedItem=wasSettings?settingsItem:navigation.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x=>x.Tag is VerifiedMod m&&m.Manifest.Id==selectedId)??navigation.MenuItems[0];
  applyingDiscovery=false;
  if(wasSettings)Settings();else if(navigation.SelectedItem is NavigationViewItem current&&current.Tag is VerifiedMod module)ShowMod(module);else Home();
 }
 TextBlock Heading(string text)=>new(){Text=UiLanguage.Text(text),FontSize=28,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold};
 void ClearBody(){body.Spacing=14;string previous=status.Text;body.Children.Clear();status=new TextBlock{Text=previous,TextWrapping=TextWrapping.Wrap,Opacity=.8};}
 Button Button(string label,Action action){var button=new Button{Content=UiLanguage.Text(label)};button.Click+=(_,_)=>{try{action();}catch(Exception ex){Notice(ex.Message);}};return button;}
 Button AsyncButton(string label,Func<Task> action,Func<bool>? canExecute=null){var button=new Button{Content=UiLanguage.Text(label),IsEnabled=canExecute?.Invoke()??true};button.Click+=async(_,_)=>{button.IsEnabled=false;try{await action();}catch(Exception ex){Notice(ex.Message);}finally{button.IsEnabled=canExecute?.Invoke()??true;}};return button;}
 internal static Border Badge(string text)
 {
  var border=(Border)XamlReader.Load("<Border xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Background=\"{ThemeResource SubtleFillColorSecondaryBrush}\" BorderBrush=\"{ThemeResource CardStrokeColorDefaultBrush}\" BorderThickness=\"1\" CornerRadius=\"4\" Padding=\"8,2\" VerticalAlignment=\"Center\"/>");
  border.Child=new TextBlock{Text=UiLanguage.Text(text),FontSize=11,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold};return border;
 }
 static Grid RowTitle(string title,string? badge=null){var panel=new Grid{ColumnSpacing=10};panel.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});panel.ColumnDefinitions.Add(new(){Width=GridLength.Auto});panel.Children.Add(new TextBlock{Text=UiLanguage.Text(title),TextWrapping=TextWrapping.Wrap,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});if(badge!=null){var tag=Badge(badge);Grid.SetColumn(tag,1);panel.Children.Add(tag);}return panel;}
 static Grid ResponsiveActions(){var panel=new Grid{ColumnSpacing=8,RowSpacing=8};bool? stacked=null;panel.SizeChanged+=(_,e)=>{bool next=e.NewSize.Width<500;if(stacked==next&&panel.ColumnDefinitions.Count>0)return;stacked=next;panel.ColumnDefinitions.Clear();panel.RowDefinitions.Clear();for(int i=0;i<panel.Children.Count;i++){if(next)panel.RowDefinitions.Add(new(){Height=GridLength.Auto});else panel.ColumnDefinitions.Add(new(){Width=GridLength.Auto});var child=(FrameworkElement)panel.Children[i];Grid.SetRow(child,next?i:0);Grid.SetColumn(child,next?0:i);child.HorizontalAlignment=HorizontalAlignment.Left;}};return panel;}
 static Border Card(UIElement content){var border=(Border)XamlReader.Load("<Border xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Background=\"{ThemeResource CardBackgroundFillColorDefaultBrush}\" BorderBrush=\"{ThemeResource CardStrokeColorDefaultBrush}\" BorderThickness=\"1\" CornerRadius=\"8\" Padding=\"14,6\" HorizontalAlignment=\"Stretch\"/>");border.Child=content;return border;}
 static Border FeatureCard(string title,ToggleSwitch toggle,string? badge=null)
 {
  var row=new Grid{ColumnSpacing=16,MinHeight=32};row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new GridLength(90)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});row.Children.Add(new TextBlock{Text=UiLanguage.Text(title),FontSize=14,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap});toggle.OnContent="";toggle.OffContent="";toggle.MinWidth=40;toggle.HorizontalAlignment=HorizontalAlignment.Center;toggle.VerticalAlignment=VerticalAlignment.Center;Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle,UiLanguage.Text(title));Grid.SetColumn(toggle,1);row.Children.Add(toggle);if(badge!=null){var tag=Badge(badge);Grid.SetColumn(tag,2);row.Children.Add(tag);}return Card(row);
 }
 void Home()
 {
  ClearBody();
  body.Children.Add(new TextBlock{Text="Workers & Resources: Soviet Republic",FontSize=25,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
  bool found=File.Exists(Path.Combine(prefs.Game,"SOVIET64.exe"));
  gameStateText=new TextBlock{Text=UiLanguage.Text(found?"Игра найдена":"Выберите папку игры в настройках"),Opacity=.75};body.Children.Add(gameStateText);
  var actions=ResponsiveActions();
  var play=AsyncButton("Запустить игру",LaunchGame,()=>File.Exists(Path.Combine(prefs.Game,"SOVIET64.exe"))&&!launching&&!IsGameRunning());gameLaunchButton=play;play.IsEnabled=found&&!launching&&!IsGameRunning();actions.Children.Add(play);
  actions.Children.Add(Button("Настройки игры и модулей",Settings));body.Children.Add(actions);
  var moduleState=new StackPanel{Spacing=8};moduleState.Children.Add(RowTitle("Модули"));
  if(mods.Count==0)moduleState.Children.Add(new TextBlock{Text=UiLanguage.Text("Совместимые модули не найдены."),TextWrapping=TextWrapping.Wrap,Opacity=.75});
  foreach(var module in mods){var line=new Grid{ColumnSpacing=12};line.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});line.ColumnDefinitions.Add(new(){Width=GridLength.Auto});var link=new HyperlinkButton{Content=UiLanguage.Text(module.Manifest.Name),HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(0,3,0,3)};link.Click+=(_,_)=>{navigation.SelectedItem=module.Manifest.Placement=="settings"?settingsItem:navigation.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x=>x.Tag is VerifiedMod m&&m.Manifest.Id==module.Manifest.Id);};line.Children.Add(link);var version=new TextBlock{Text=module.Manifest.Version,Opacity=.6,VerticalAlignment=VerticalAlignment.Center};ToolTipService.SetToolTip(version,UiLanguage.Text("Версия модуля"));Grid.SetColumn(version,1);line.Children.Add(version);moduleState.Children.Add(line);}
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
    entry.Children.Add(AsyncButton(ModuleRuntimeCompatibility.OfferMatchesSignedRuntime(offer,mods)?(offer.FirstInstall?"Установить модуль":"Обновить модуль"):"Подробнее",()=>ShowWorkshopUpdatesAsync([offer])));
    workshopState.Children.Add(entry);
   }
   body.Children.Add(Card(workshopState));
  }
  var editor=new StackPanel{Orientation=Orientation.Horizontal,Spacing=12};editor.Children.Add(new TextBlock{Text="Aurora Editor",VerticalAlignment=VerticalAlignment.Center,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
  editor.Children.Add(AsyncButton("Открыть редактор",OpenEditor));body.Children.Add(Card(editor));
  RefreshLaunchState();body.Children.Add(status);versionText=new TextBlock{Text=UiLanguage.Text(versionMessage),Opacity=.65};body.Children.Add(versionText);UiLanguage.Apply(body);
 }
 async Task DiscoverAsync()
 {
  if(discovering){discoverAgain=true;return;}
  discovering=true;string gamePath=prefs.Game;
  status.Text=UiLanguage.Text("Поиск модулей…");
  try
  {
   if(!Directory.Exists(gamePath)){ApplyDiscovery([],[],[]);return;}
   var result=await Task.Run(()=>{
    var modules=new ModCatalog(publicKey).Discover(new[]{Path.Combine(AppContext.BaseDirectory,"Modules")}.Concat(SteamDiscovery.FindRoots(gamePath)));
    var updates=WorkshopUpdates.Scan(gamePath);
    return (modules.Mods,Errors:modules.Errors.Concat(updates.Errors).ToList(),updates.Offers);
   });
   if(!string.Equals(gamePath,prefs.Game,StringComparison.OrdinalIgnoreCase)){discoverAgain=true;return;}
   ApplyDiscovery(result.Mods,result.Errors,result.Offers);
   if(result.Offers.Count>0)await ShowWorkshopUpdatesAsync(result.Offers);
  }
  catch(Exception ex){Notice(ex.Message);}
  finally{discovering=false;if(discoverAgain){discoverAgain=false;_=DiscoverAsync();}}
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
    bool compatible=ModuleRuntimeCompatibility.OfferMatchesSignedRuntime(offer,mods);
    var details=new StackPanel{Spacing=8,MinWidth=390};
    details.Children.Add(new TextBlock{Text=UiLanguage.Text("Установлено")+": "+(offer.Installed==null?UiLanguage.Text("Не установлен"):offer.Installed.Version+" ("+offer.Installed.ReleaseDate+")"),TextWrapping=TextWrapping.Wrap});
    details.Children.Add(new TextBlock{Text=UiLanguage.Text("Доступно")+": "+offer.Release.Version+" ("+offer.Release.ReleaseDate+")",TextWrapping=TextWrapping.Wrap,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
    details.Children.Add(new TextBlock{Text="Steam Workshop ID: "+offer.Registration.SteamId,Opacity=.7});
    if(!compatible)details.Children.Add(new TextBlock{Text=UiLanguage.Text("Для запуска этого обновления нужен совместимый подписанный комплект модуля. Установка пока недоступна."),TextWrapping=TextWrapping.Wrap});
    var dialog=new ContentDialog{
     IsPrimaryButtonEnabled=compatible,Title=UiLanguage.Text(offer.FirstInstall?"Установка модуля":"Обновление модуля")+": "+offer.Release.Name,
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

 static void RefreshDependencies(ModManifest manifest,Dictionary<string,(Control Control,Func<bool> Value)> controls){
  var options=manifest.Options.ToDictionary(x=>x.Id,StringComparer.Ordinal);
  bool Active(string id)=>controls.TryGetValue(id,out var state)&&state.Value()&&(options[id].Requires??[]).All(Active);
  foreach(var option in manifest.Options){if(!controls.TryGetValue(option.Id,out var state))continue;var required=option.Requires??[];state.Control.IsEnabled=required.All(Active);string hint=required.Length==0?"":UiLanguage.Text("Требуется включить")+": "+string.Join(", ",required.Select(x=>UiLanguage.Text(options[x].Label)));ToolTipService.SetToolTip(state.Control,hint.Length==0?null:hint);Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(state.Control,hint);}
 }
 void ShowMod(VerifiedMod mod)
 {
  ClearBody();body.Children.Add(Heading(mod.Manifest.Name));
  body.Children.Add(new TextBlock{Text="Изменения применяются при следующем запуске игры.",TextWrapping=TextWrapping.Wrap});body.Spacing=8;
  AddOptionRows(body,mod,true);
  body.Children.Add(Button("Установить / обновить модуль",()=>Install(mod)));UiLanguage.Apply(body);
 }
 void AddOptionRows(StackPanel target,VerifiedMod module,bool wrapCards)
 {
  string file=ModCatalog.SafePath(prefs.Game,"mods/"+module.Manifest.Config);
  var states=new Dictionary<string,(Control Control,Func<bool> Value)>();var beta=new StackPanel{Spacing=6};
  void Write(ModOption option,int value){VerifyCurrent(module);Directory.CreateDirectory(Path.GetDirectoryName(file)!);if(!WritePrivateProfileString(option.Section,option.Key,value.ToString(System.Globalization.CultureInfo.InvariantCulture),file))throw new System.ComponentModel.Win32Exception();}
  foreach(var option in module.Manifest.Options)
  {
   var row=new Grid{ColumnSpacing=12};row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
   row.Children.Add(wrapCards?RowTitle(option.Label,option.Beta?"Бета":null):new TextBlock{Text=UiLanguage.Text(option.Label),VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap});
   bool restoring=false;
   if(option.Kind=="toggle")
   {
    var toggle=new ToggleSwitch{IsOn=GetPrivateProfileInt(option.Section,option.Key,option.Default?1:0,file)!=0,VerticalAlignment=VerticalAlignment.Center,OnContent=UiLanguage.Text("Вкл."),OffContent=UiLanguage.Text("Выкл.")};
    Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle,UiLanguage.Text(option.Label));states[option.Id]=(toggle,()=>toggle.IsOn);
    toggle.Toggled+=(_,_)=>{if(restoring)return;try{Write(option,toggle.IsOn?1:0);RefreshDependencies(module.Manifest,states);}catch(Exception ex){restoring=true;toggle.IsOn=GetPrivateProfileInt(option.Section,option.Key,option.Default?1:0,file)!=0;restoring=false;RefreshDependencies(module.Manifest,states);Notice(ex.Message);}};
    Grid.SetColumn(toggle,1);row.Children.Add(toggle);
   }
   else
   {
    row.ColumnDefinitions[1].Width=new GridLength(1,GridUnitType.Star);var right=new Grid{ColumnSpacing=8};right.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});right.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
    var slider=new Slider{Minimum=option.Minimum,Maximum=option.Maximum,StepFrequency=1,Value=GetPrivateProfileInt(option.Section,option.Key,option.DefaultValue,file),MaxWidth=230,HorizontalAlignment=HorizontalAlignment.Stretch};
    Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(slider,UiLanguage.Text(option.Label));states[option.Id]=(slider,()=>true);
    var value=new TextBlock{Text=((int)slider.Value).ToString(),Width=34,VerticalAlignment=VerticalAlignment.Center};
    slider.ValueChanged+=(_,e)=>{value.Text=((int)e.NewValue).ToString();if(restoring)return;try{Write(option,(int)e.NewValue);}catch(Exception ex){restoring=true;slider.Value=GetPrivateProfileInt(option.Section,option.Key,option.DefaultValue,file);restoring=false;Notice(ex.Message);}};
    Grid.SetColumn(value,1);right.Children.Add(slider);right.Children.Add(value);Grid.SetColumn(right,1);row.Children.Add(right);
   }
   UIElement content=wrapCards?Card(row):row;if(option.Beta)beta.Children.Add(content);else target.Children.Add(content);
  }
  RefreshDependencies(module.Manifest,states);
  if(beta.Children.Count>0)target.Children.Add(new Expander{Header=RowTitle("Экспериментальные функции","Бета"),Content=beta,IsExpanded=false,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch});
 }
 Border SettingsModuleCard(VerifiedMod module)
 {
  if(module.Manifest.Id=="aurora.fov"){
   var option=module.Manifest.Options.Single(o=>o.Id=="enabled");string file=ModCatalog.SafePath(prefs.Game,"mods/"+module.Manifest.Config);bool restoring=false;var enabled=new ToggleSwitch{IsOn=GetPrivateProfileInt(option.Section,option.Key,option.Default?1:0,file)!=0};enabled.Toggled+=(_,_)=>{if(restoring)return;try{VerifyCurrent(module);Directory.CreateDirectory(Path.GetDirectoryName(file)!);if(!WritePrivateProfileString(option.Section,option.Key,enabled.IsOn?"1":"0",file))throw new System.ComponentModel.Win32Exception();}catch(Exception ex){restoring=true;enabled.IsOn=GetPrivateProfileInt(option.Section,option.Key,option.Default?1:0,file)!=0;restoring=false;Notice(ex.Message);}};return FeatureCard(module.Manifest.Name,enabled,"Альфа");
  }
  var controls=new StackPanel{Spacing=8};controls.Children.Add(RowTitle(module.Manifest.Name,module.Manifest.Stage=="alpha"?"Альфа":module.Manifest.Stage=="beta"?"Бета":null));AddOptionRows(controls,module,false);return Card(controls);
 }
 void VerifyCurrent(VerifiedMod mod){_=new ModCatalog(publicKey).VerifySnapshot(mod);}
 void Install(VerifiedMod mod)
 {
  VerifyCurrent(mod);
  if(Process.GetProcessesByName("SOVIET64").Length>0)throw new InvalidOperationException("Закройте игру перед установкой.");
  EnsureCompatibleRuntime([mod]);
  ModuleInstaller.Install(mod,prefs.Game,publicKey);
  Notice("Модуль установлен. Настройки сохранены.",InfoBarSeverity.Success);
 }
 void EnsureCompatibleRuntime(IEnumerable<VerifiedMod> modules,string? game=null){try{ModuleRuntimeCompatibility.EnsureWorkshopCompatible(game??prefs.Game,modules);}catch(WorkshopRuntimeConflictException ex){throw new InvalidOperationException(UiLanguage.Text("Установленный модуль Workshop отличается от подписанного комплекта. Обновите комплект модуля. Файлы сохранены.")+" ("+ex.SteamId+", "+ex.Version+")");}}
 static bool IsGameRunning(){var processes=Process.GetProcessesByName("SOVIET64");try{return processes.Length>0;}finally{foreach(var process in processes)process.Dispose();}}
 void RefreshLaunchState(){if(gameLaunchButton==null)return;bool found=File.Exists(Path.Combine(prefs.Game,"SOVIET64.exe")),running=IsGameRunning();gameLaunchButton.IsEnabled=found&&!running&&!launching;if(gameStateText!=null)gameStateText.Text=UiLanguage.Text(launching?"Запуск игры…":running?"Игра уже запущена":found?"Игра найдена":"Выберите папку игры в настройках");}
 async Task LaunchGame(){if(launching)return;launching=true;RefreshLaunchState();try{await LaunchGameCore();}finally{launching=false;RefreshLaunchState();}}
 async Task LaunchGameCore()
 {
  if(IsGameRunning())throw new InvalidOperationException(UiLanguage.Text("Игра уже запущена"));
  string game=prefs.Game;bool consoleEnabled=prefs.Console;
  if(!File.Exists(Path.Combine(game,"SOVIET64.exe")))throw new FileNotFoundException("Выберите папку игры в настройках.");
  // Steam owns Workshop downloads; re-read signed Workshop manifests before each launch.
  var refreshed=await Task.Run(()=>new ModCatalog(publicKey).Discover(new[]{Path.Combine(AppContext.BaseDirectory,"Modules")}.Concat(SteamDiscovery.FindRoots(game))));
  var launchModules=refreshed.Mods;
  foreach(var mod in launchModules)VerifyCurrent(mod);
  var start=new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"Aurora.Bootstrap.exe")){WorkingDirectory=game,UseShellExecute=false,RedirectStandardOutput=true};
  start.ArgumentList.Add(game);
  var libraries=launchModules.SelectMany(x=>x.Manifest.Files).Where(x=>x.Path.StartsWith("Payload/")&&x.Path.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)).GroupBy(x=>x.Path,StringComparer.OrdinalIgnoreCase).ToArray();
  if(libraries.Any(x=>x.Select(f=>f.Sha256.ToLowerInvariant()).Distinct().Count()>1))throw new InvalidDataException("Конфликт библиотек модов. Установите совместимые версии.");
  await Task.Run(()=>{EnsureCompatibleRuntime(launchModules,game);foreach(var module in launchModules){bool installed=module.Manifest.Files.Where(f=>f.Path.StartsWith("Payload/")).All(f=>{string path=ModCatalog.SafePath(game,f.Path[8..]);if(!File.Exists(path))return false;using var stream=File.OpenRead(path);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).Equals(f.Sha256,StringComparison.OrdinalIgnoreCase);});if(!installed)ModuleInstaller.Install(module,game,publicKey);}});
  foreach(var file in libraries.Select(x=>x.First()).OrderBy(x=>x.Path.EndsWith("/m.dll")?0:1))
  {
   string installed=ModCatalog.SafePath(game,file.Path[8..]);
   using var input=File.OpenRead(installed);if(!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input)).Equals(file.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Установленная библиотека отличается. Установите модуль заново: "+file.Path);
   start.ArgumentList.Add(installed);start.ArgumentList.Add(file.Sha256.ToLowerInvariant());
  }
  using var bootstrap=Process.Start(start)??throw new InvalidOperationException("Cannot start game bootstrap");
  string output=await bootstrap.StandardOutput.ReadToEndAsync();await bootstrap.WaitForExitAsync();
  if(bootstrap.ExitCode!=0)throw new InvalidOperationException("Game launch failed");
  if(consoleEnabled&&int.TryParse(output.Trim(),out int pid)){var host=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false};host.ArgumentList.Add("--game-console");host.ArgumentList.Add("--pid");host.ArgumentList.Add(pid.ToString());Process.Start(host);}
 }
 void Settings()
 {
  ClearBody();body.Children.Add(Heading("Настройки"));
  var locations=new StackPanel{Spacing=12};locations.Children.Add(RowTitle("Игра и модули"));
  var path=new TextBox{Header="Папка SovietRepublic",Text=prefs.Game};locations.Children.Add(path);
  var destination=new TextBox{Header=UiLanguage.Text("Папка установки модулей"),IsReadOnly=true,Text=string.IsNullOrWhiteSpace(path.Text)?"":Path.Combine(path.Text,"mods")};path.TextChanged+=(_,_)=>{try{destination.Text=string.IsNullOrWhiteSpace(path.Text)?"":Path.Combine(path.Text,"mods");}catch{destination.Text="";}};locations.Children.Add(destination);
  var locateButtons=ResponsiveActions();
  locateButtons.Children.Add(AsyncButton("Выбрать папку…",async()=>{var picker=new Windows.Storage.Pickers.FolderPicker();picker.FileTypeFilter.Add("*");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));var folder=await picker.PickSingleFolderAsync();if(folder!=null)path.Text=folder.Path;}));
  locateButtons.Children.Add(Button("Сохранить папку",()=>{if(!File.Exists(Path.Combine(path.Text,"SOVIET64.exe")))throw new InvalidDataException("В выбранной папке нет SOVIET64.exe.");prefs=prefs with{Game=Path.GetFullPath(path.Text)};Save();Notice("Папка сохранена.",InfoBarSeverity.Success);Discover();}));
  locateButtons.Children.Add(Button("Workshop",()=>Process.Start(new ProcessStartInfo("https://steamcommunity.com/app/784150/workshop/"){UseShellExecute=true})));locations.Children.Add(locateButtons);body.Children.Add(Card(locations));
  foreach(var module in mods.Where(x=>x.Manifest.Placement=="settings"))body.Children.Add(SettingsModuleCard(module));
  var console=new ToggleSwitch{IsOn=prefs.Console};console.Toggled+=(_,_)=>{prefs=prefs with{Console=console.IsOn};Save();};body.Children.Add(FeatureCard("Консоль разработчика",console,"Альфа"));
  var updater=new StackPanel{Spacing=10};updater.Children.Add(new TextBox{Header=UiLanguage.Text("Папка лаунчера"),Text=InstallationPaths.Root,IsReadOnly=true});var updateButtons=ResponsiveActions();
  updateButtons.Children.Add(AsyncButton("Проверить обновления",CheckUpdates));
  installUpdateButton=AsyncButton("Установить обновление",async()=>{var folder=await updates.DownloadVerified(publicKey);if(folder==null){Notice("Обновлений нет.");return;}updates.StartReplacement(folder);Application.Current.Exit();},()=>updateAvailable);installUpdateButton.IsEnabled=updateAvailable;updateButtons.Children.Add(installUpdateButton);updater.Children.Add(updateButtons);versionText=new TextBlock{Text=UiLanguage.Text(versionMessage),Opacity=.65};updater.Children.Add(versionText);updater.Children.Add(status);body.Children.Add(Card(updater));
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
 async Task CheckUpdates(){versionMessage="Проверка обновлений…";if(versionText!=null)versionText.Text=UiLanguage.Text(versionMessage);try{var release=await updates.Check();updateAvailable=release!=null;if(installUpdateButton!=null)installUpdateButton.IsEnabled=updateAvailable;versionMessage=release==null?"Установлена актуальная версия 0.2":"Устаревшая версия 0.2";if(versionText!=null)versionText.Text=UiLanguage.Text(versionMessage);}catch(Exception ex){updateAvailable=false;if(installUpdateButton!=null)installUpdateButton.IsEnabled=false;versionMessage="Не удалось проверить обновления.";if(versionText!=null)versionText.Text=UiLanguage.Text(versionMessage);status.Text=UiLanguage.Text(versionMessage)+" "+ex.Message;}}
 void Notice(string message,InfoBarSeverity severity=InfoBarSeverity.Error){notice.Severity=severity;notice.Title=UiLanguage.Text(severity==InfoBarSeverity.Success?"Готово":"Не удалось выполнить действие");notice.Message=UiLanguage.Text(message);notice.IsOpen=true;}
}
sealed class AlphaConsole:Window
{
 public AlphaConsole()
 {
  Title="Aurora Console";SystemBackdrop=new MicaBackdrop();WindowChrome.ResizeLogical(this,520,260);
  var panel=new Grid{Margin=new Thickness(14),RowSpacing=8};panel.RowDefinitions.Add(new(){Height=GridLength.Auto});panel.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});panel.RowDefinitions.Add(new(){Height=GridLength.Auto});
  var log=new TextBox{IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Text="In development",MinHeight=40};Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(log,UiLanguage.Text("Вывод консоли"));
  var input=new TextBox{PlaceholderText=UiLanguage.Text("Сообщение")};Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(input,UiLanguage.Text("Сообщение"));
  void Reply(){if(string.IsNullOrWhiteSpace(input.Text))return;log.Text+="\r\n> "+input.Text+"\r\nIn development";log.SelectionStart=log.Text.Length;input.Text="";input.Focus(FocusState.Programmatic);}
  input.KeyDown+=(_,e)=>{if(e.Key==Windows.System.VirtualKey.Enter){Reply();e.Handled=true;}};
  var send=new Button{Content=UiLanguage.Text("Отправить"),VerticalAlignment=VerticalAlignment.Stretch};send.Click+=(_,_)=>Reply();
  var heading=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};heading.Children.Add(new TextBlock{Text=UiLanguage.Text("Консоль"),FontSize=20});heading.Children.Add(MainWindow.Badge("Альфа"));
  var entry=new Grid{ColumnSpacing=8};entry.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});entry.ColumnDefinitions.Add(new(){Width=GridLength.Auto});entry.Children.Add(input);Grid.SetColumn(send,1);entry.Children.Add(send);
  panel.Children.Add(heading);Grid.SetRow(log,1);panel.Children.Add(log);Grid.SetRow(entry,2);panel.Children.Add(entry);WindowChrome.Attach(this,panel,Title);
 }

}





