using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.ApplicationModel.DataTransfer;
using Aurora.Shared;
namespace Aurora.Editor;
#if !AURORA_EDITOR_MODULE
static class Program{[STAThread]static void Main(){WinRT.ComWrappersSupport.InitializeComWrappers();Application.Start(parameters=>{SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));_=new EditorApp();});}}
sealed partial class EditorApp:Application{Window? window;public EditorApp(){InitializeComponent();}protected override void OnLaunched(LaunchActivatedEventArgs args){window=new EditorWindow();window.Activate();}}
#endif
public static class EditorFactory { public static Window Create() => new EditorWindow(); }

sealed class EditorWindow:Window
{
 readonly WebView2 view=new();readonly StackPanel controls=new(){Spacing=10,Margin=new Thickness(18)};
 readonly ComboBox lodChoice=new(){Header=UiLanguage.Text("Модель / LOD"),PlaceholderText=UiLanguage.Text("Сначала откройте NMF"),HorizontalAlignment=HorizontalAlignment.Stretch};
 readonly ComboBox objects=new(){Header=UiLanguage.Text("Объект"),PlaceholderText=UiLanguage.Text("Выберите объект"),HorizontalAlignment=HorizontalAlignment.Stretch};
 readonly ComboBox materialChoice=new(){Header=UiLanguage.Text("Материал"),PlaceholderText=UiLanguage.Text("Выберите материал"),HorizontalAlignment=HorizontalAlignment.Stretch};
 readonly TextBlock status=new(){Text="Выберите NMF и MTL",TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true};
 readonly Dictionary<string,NativeModel> lods=[];readonly Dictionary<string,HashSet<string>> glassObjects=[];
 readonly Dictionary<string,MaterialSettings> settings=[];
 string? selectedLod,mtl,mask,maskMaterial;bool ready,busy,processing,dirty,closeApproved,closePrompt;
 readonly TextBlock dirtyText=new(){Text=UiLanguage.Text("Есть несохранённые изменения"),FontSize=12,Opacity=.75,Visibility=Visibility.Collapsed};
 readonly ProgressBar busyIndicator=new(){IsIndeterminate=true,Visibility=Visibility.Collapsed};
 readonly List<(Button Button,Func<bool> CanExecute)> commandButtons=[];
 readonly StackPanel emptyView=new(){Spacing=8,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MaxWidth=320};
 bool HasModel=>ready&&selectedLod!=null;
 string? startupModel=Environment.GetCommandLineArgs().Skip(1).FirstOrDefault(x=>x.EndsWith(".nmf",StringComparison.OrdinalIgnoreCase));
 public EditorWindow()
 {
  Title="Aurora Editor";SystemBackdrop=new MicaBackdrop();AppWindow.Resize(new(1250,850));
  var grid=new Grid();grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(350)});grid.Children.Add(view);
  emptyView.Children.Add(new TextBlock{Text=UiLanguage.Text("Откройте модель"),FontSize=22,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
  emptyView.Children.Add(new TextBlock{Text=UiLanguage.Text("Откройте NMF или перетащите его сюда. Затем выберите MTL для настройки PBR и стекла."),TextWrapping=TextWrapping.Wrap,Opacity=.7});
  emptyView.IsHitTestVisible=false;grid.Children.Add(emptyView);
  grid.AllowDrop=true;
  grid.DragOver+=(_,e)=>{if(!busy&&e.DataView.Contains(StandardDataFormats.StorageItems)){e.AcceptedOperation=DataPackageOperation.Copy;e.DragUIOverride.Caption=UiLanguage.Text("Открыть модель");}e.Handled=true;};
  grid.Drop+=async(_,e)=>{e.Handled=true;if(busy||!e.DataView.Contains(StandardDataFormats.StorageItems))return;try{var files=(await e.DataView.GetStorageItemsAsync()).OfType<Windows.Storage.StorageFile>().Where(x=>x.FileType.Equals(".nmf",StringComparison.OrdinalIgnoreCase)).ToArray();if(files.Length!=1){status.Text=UiLanguage.Text("Перетащите один основной NMF. Файлы LOD загрузятся из той же папки.");return;}await RunCommand(async()=>{if(await ConfirmDiscard())await Load(files[0].Path);});}catch(Exception ex){status.Text=ex.Message;}};
  var scroll=new ScrollViewer{Content=controls};Grid.SetColumn(scroll,1);grid.Children.Add(scroll);WindowChrome.Attach(this,grid,"Aurora Editor");
  controls.Children.Add(new TextBlock{Text="Мастер моделей",FontSize=24});
  controls.Children.Add(Button("Открыть NMF…",async()=>{var path=await Pick(".nmf");if(path!=null&&await ConfirmDiscard())await Load(path);}));
  controls.Children.Add(Button("Добавить LOD…",async()=>{var path=await Pick(".nmf");if(path!=null){await AddModel(path);SelectLod(path);dirty=true;await Display();}},()=>HasModel));
  controls.Children.Add(Button("Выбрать MTL…",async()=>{var chosen=await Pick(".mtl");if(chosen!=null){mtl=chosen;dirty=true;status.Text=Path.GetFileName(mtl);await LoadTextures();}},()=>HasModel));
  controls.Children.Add(lodChoice);controls.Children.Add(objects);controls.Children.Add(materialChoice);
  lodChoice.SelectionChanged+=async(_,_)=>{if(!busy&&lodChoice.SelectedItem is ComboBoxItem item&&item.Tag is string path){await RunCommand(async()=>{selectedLod=path;await Display();});}};
  objects.SelectionChanged+=async(_,_)=>{try{if(ready&&objects.SelectedIndex>=0)await view.ExecuteScriptAsync("select("+objects.SelectedIndex+")");}catch(Exception ex){status.Text=ex.Message;}finally{UpdateControls();}};
  materialChoice.SelectionChanged+=(_,_)=>UpdateControls();
  controls.Children.Add(Button("Настроить PBR",ConfigurePbr,()=>HasModel&&materialChoice.SelectedItem!=null));
  controls.Children.Add(Button("Сделать объект стеклом",async()=>{if(selectedLod==null||objects.SelectedItem is not string name)throw new InvalidOperationException("Выберите объект.");glassObjects[selectedLod].Add(name);dirty=true;await view.ExecuteScriptAsync("glassObject("+objects.SelectedIndex+",true)");status.Text=UiLanguage.Text("Стекло")+": "+name+". "+UiLanguage.Text("Настройте соответствующий объект в каждом LOD.");},()=>HasModel&&objects.SelectedItem!=null));
  controls.Children.Add(Button("Стекло через альфа-маску…",async()=>{if(materialChoice.SelectedItem is not string name)throw new InvalidOperationException("Выберите материал кузова.");var chosen=await Pick(".dds",".png");if(chosen!=null){mask=chosen;maskMaterial=name;dirty=true;status.Text=UiLanguage.Text("Альфа-маска")+": "+Path.GetFileName(mask)+" — "+name;}},()=>HasModel&&materialChoice.SelectedItem!=null));
  controls.Children.Add(Button("Сбросить стекло",async()=>{if(selectedLod!=null)glassObjects[selectedLod].Clear();mask=null;maskMaterial=null;dirty=true;await Display();},()=>HasModel&&(mask!=null||(selectedLod!=null&&glassObjects[selectedLod].Count>0))));
  controls.Children.Add(Button("Сохранить мод…",Export,()=>HasModel&&mtl!=null));controls.Children.Add(busyIndicator);controls.Children.Add(dirtyText);controls.Children.Add(status);
  view.CoreWebView2Initialized+=(_,_)=>{view.CoreWebView2.Settings.AreDevToolsEnabled=false;view.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;view.CoreWebView2.WebMessageReceived+=async(_,e)=>{try{using var json=JsonDocument.Parse(e.WebMessageAsJson);if(json.RootElement.TryGetProperty("error",out var viewportError))status.Text="Viewport: "+viewportError.GetString();if(json.RootElement.TryGetProperty("ready",out var readyProperty)){ready=true;await view.ExecuteScriptAsync("document.getElementById('hint').textContent="+JsonSerializer.Serialize(UiLanguage.Text("Перетаскивание: вращение. Колесо: масштаб. Щелчок: выбор объекта.")));if(startupModel is string path){startupModel=null;await RunCommand(()=>Load(path));}else await Display();}if(json.RootElement.TryGetProperty("selected",out var index)&&index.TryGetInt32(out int i)&&i>=0&&i<objects.Items.Count)objects.SelectedIndex=i;}catch{}};};
  AppWindow.Closing+=async(_,e)=>{if(!dirty||closeApproved)return;e.Cancel=true;if(closePrompt||busy)return;closePrompt=true;try{if(await ConfirmDiscard(true)){closeApproved=true;Close();}}finally{closePrompt=false;}};
  UiLanguage.Apply(controls);UpdateControls();LoadedView();
 }
 async void LoadedView(){try{await view.EnsureCoreWebView2Async();string folder=File.Exists(Path.Combine(AppContext.BaseDirectory,"Editor","Assets","viewport.html"))?"Editor":"";view.NavigateToString(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,folder,"Assets","viewport.html")));}catch(Exception ex){status.Text=UiLanguage.Text("Не удалось открыть viewport")+": "+ex.Message;}}
 async Task<bool> ConfirmDiscard(bool closing=false){if(!dirty)return true;return await new ContentDialog{Title=UiLanguage.Text(closing?"Закрыть редактор?":"Открыть другую модель?"),Content=UiLanguage.Text("Несохранённые настройки текущей модели будут потеряны."),PrimaryButtonText=UiLanguage.Text(closing?"Закрыть без сохранения":"Открыть"),CloseButtonText=UiLanguage.Text("Отмена"),DefaultButton=ContentDialogButton.Close,XamlRoot=((FrameworkElement)Content).XamlRoot}.ShowAsync()==ContentDialogResult.Primary;}
 NativeModel CurrentModel()=>lods[selectedLod??throw new InvalidOperationException("Выберите NMF")];
 void UpdateControls(){dirtyText.Visibility=dirty?Visibility.Visible:Visibility.Collapsed;busyIndicator.Visibility=busy&&processing?Visibility.Visible:Visibility.Collapsed;view.IsHitTestVisible=!busy;foreach(var command in commandButtons)command.Button.IsEnabled=!busy&&command.CanExecute();lodChoice.IsEnabled=!busy&&HasModel;objects.IsEnabled=!busy&&HasModel;materialChoice.IsEnabled=!busy&&HasModel;emptyView.Visibility=selectedLod==null?Visibility.Visible:Visibility.Collapsed;}
 Button Button(string label,Func<Task> action,Func<bool>? canExecute=null){var b=new Button{Content=UiLanguage.Text(label),HorizontalAlignment=HorizontalAlignment.Stretch};commandButtons.Add((b,canExecute??(()=>true)));b.Click+=async(_,_)=>await RunCommand(action);return b;}
 async Task RunCommand(Func<Task> action){if(busy)return;busy=true;processing=false;UpdateControls();try{await action();}catch(Exception ex){status.Text=UiLanguage.Text(ex.Message);}finally{busy=false;processing=false;UpdateControls();}}
 void SelectLod(string path){selectedLod=path;lodChoice.SelectedItem=lodChoice.Items.OfType<ComboBoxItem>().First(x=>string.Equals(x.Tag as string,path,StringComparison.OrdinalIgnoreCase));}
 void AddLodChoice(string path){if(lodChoice.Items.OfType<ComboBoxItem>().Any(x=>string.Equals(x.Tag as string,path,StringComparison.OrdinalIgnoreCase)))return;var item=new ComboBoxItem{Content=Path.GetFileName(path),Tag=path};ToolTipService.SetToolTip(item,path);lodChoice.Items.Add(item);}
 async Task<string?> Pick(params string[] extensions){var picker=new Windows.Storage.Pickers.FileOpenPicker();foreach(var ext in extensions)picker.FileTypeFilter.Add(ext);WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));return(await picker.PickSingleFileAsync())?.Path;}
 async Task<string?> Folder(){var picker=new Windows.Storage.Pickers.FolderPicker();picker.FileTypeFilter.Add("*");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));return(await picker.PickSingleFolderAsync())?.Path;}
 async Task AddModel(string path){var model=await Task.Run(()=>new NativeModel(File.ReadAllBytes(path)));lods[path]=model;glassObjects.TryAdd(path,[]);AddLodChoice(path);}
 async Task Load(string path)
 {
  processing=true;UpdateControls();
  // Load siblings so LOD files cannot silently disappear from an export.
  var siblings=Directory.GetFiles(Path.GetDirectoryName(path)!,"*.nmf").Where(p=>p.Equals(path,StringComparison.OrdinalIgnoreCase)||Regex.IsMatch(Path.GetFileName(p),@"^lod.*\.nmf$",RegexOptions.IgnoreCase)).ToArray();
  var parsed=await Task.Run(()=>siblings.ToDictionary(x=>x,x=>new NativeModel(File.ReadAllBytes(x))));
  lods.Clear();glassObjects.Clear();settings.Clear();lodChoice.Items.Clear();mask=null;maskMaterial=null;
  foreach(var pair in parsed){lods[pair.Key]=pair.Value;glassObjects[pair.Key]=[];AddLodChoice(pair.Key);}
  selectedLod=path;SelectLod(path);
  var candidate=Path.ChangeExtension(path,"mtl");var common=Path.Combine(Path.GetDirectoryName(path)!,"material.mtl");mtl=File.Exists(candidate)?candidate:File.Exists(common)?common:null;
  dirty=false;status.Text=UiLanguage.Text("Загружено моделей и LOD")+": "+lods.Count;await Display();
 }
 async Task Display()
 {
  processing=true;UpdateControls();if(!ready||selectedLod==null)return;var model=CurrentModel();objects.Items.Clear();foreach(var node in model.Nodes)objects.Items.Add(node.Name);materialChoice.Items.Clear();foreach(var name in model.Materials)materialChoice.Items.Add(name);if(objects.Items.Count>0)objects.SelectedIndex=0;if(materialChoice.Items.Count>0)materialChoice.SelectedIndex=0;
  var scene=new{model.Materials,Nodes=model.Nodes.Select(n=>new{n.Name,n.Positions,n.Normals,n.Uv,Indices=n.Indices.Select(x=>(int)x).ToArray(),n.Groups})};
  string sceneJson=await Task.Run(()=>JsonSerializer.Serialize(scene));
  await view.ExecuteScriptAsync("load("+sceneJson+")");await LoadTextures();
  foreach(var pair in settings)await ApplyPreview(pair.Key,pair.Value);
  for(int i=0;i<model.Nodes.Count;i++)if(glassObjects[selectedLod].Contains(model.Nodes[i].Name))await view.ExecuteScriptAsync("glassObject("+i+",true)");
 }
 async Task LoadTextures()
 {
  if(!ready||mtl==null||!File.Exists(mtl))return;processing=true;UpdateControls();
  string text=File.ReadAllText(mtl);string? name=null;
  foreach(var line in text.Split('\n'))
  {
   var material=Regex.Match(line,@"^\s*\$SUBMATERIAL\s+(.+?)\s*$");if(material.Success){name=material.Groups[1].Value;continue;}
   var texture=Regex.Match(line,@"^\s*\$TEXTURE(?:_MTL)?\s+0\s+(.+?)\s*$");
   if(name!=null&&texture.Success)
   {
    try{var file=MaterialFiles.Resolve(mtl,texture.Groups[1].Value);var image=await ReadImage(file);await PreviewTexture(name,image);}catch(Exception ex){status.Text=UiLanguage.Text("Текстура")+": "+ex.Message;}
   }
  }
 }
 async Task<RgbaImage> ReadImage(string file)
 {
  if(Path.GetExtension(file).Equals(".dds",StringComparison.OrdinalIgnoreCase))return DdsImage.Decode(File.ReadAllBytes(file));
  var storage=await Windows.Storage.StorageFile.GetFileFromPathAsync(file);using var stream=await storage.OpenReadAsync();var decoder=await BitmapDecoder.CreateAsync(stream);var data=await decoder.GetPixelDataAsync(BitmapPixelFormat.Rgba8,BitmapAlphaMode.Straight,new BitmapTransform(),ExifOrientationMode.IgnoreExifOrientation,ColorManagementMode.DoNotColorManage);return new((int)decoder.PixelWidth,(int)decoder.PixelHeight,data.DetachPixelData());
 }
 async Task PreviewTexture(string material,RgbaImage image,string channel="texture")
 {
  using var stream=new InMemoryRandomAccessStream();var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream);encoder.SetPixelData(BitmapPixelFormat.Rgba8,BitmapAlphaMode.Straight,(uint)image.Width,(uint)image.Height,96,96,image.Pixels);await encoder.FlushAsync();stream.Seek(0);var bytes=new byte[(int)stream.Size];using var reader=new DataReader(stream);await reader.LoadAsync((uint)bytes.Length);reader.ReadBytes(bytes);await view.ExecuteScriptAsync("texture("+JsonSerializer.Serialize(material)+","+JsonSerializer.Serialize("data:image/png;base64,"+Convert.ToBase64String(bytes))+","+JsonSerializer.Serialize(channel)+")");
 }
 async Task ApplyPreview(string name,MaterialSettings s)
 {
  await view.ExecuteScriptAsync("material("+JsonSerializer.Serialize(name)+","+JsonSerializer.Serialize(new{metallic=s.Metallic,roughness=s.Roughness,ao=s.Ao,normalY=s.NormalY,normalStrength=s.NormalStrength,emissionStrength=s.EmissiveStrength,iblStrength=s.Ibl})+")");
  foreach(var map in new[]{("texture",s.Base),("normalMap",s.Normal),("ormMap",s.Orm),("metalMap",s.MetalMap),("roughMap",s.RoughMap),("aoMap",s.AoMap),("emissiveMap",s.Emissive)})if(map.Item2!=null)await PreviewTexture(name,await ReadImage(map.Item2),map.Item1);
 }
 async Task ConfigurePbr()
 {
  if(materialChoice.SelectedItem is not string name)throw new InvalidOperationException("Выберите материал.");
  var dialogError=new InfoBar{Severity=InfoBarSeverity.Error,IsOpen=false,IsClosable=true};
  Button DialogButton(string label,Func<Task> action){var b=new Button{Content=UiLanguage.Text(label),HorizontalAlignment=HorizontalAlignment.Stretch};b.Click+=async(_,_)=>{b.IsEnabled=false;try{await action();}catch(Exception ex){dialogError.Message=ex.Message;dialogError.IsOpen=true;}finally{b.IsEnabled=true;}};return b;}
  async Task<string?> PickMap(string? current)=>await Pick(".dds")??current;
  var s=settings.GetValueOrDefault(name)??new();var panel=new StackPanel{Spacing=12};panel.Children.Add(dialogError);
  var mapRefresh=new List<Action>();
  Slider Slider(string label,double value,double max=1,Panel? target=null){var row=new Grid{ColumnSpacing=10};row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new GridLength(40)});row.Children.Add(new TextBlock{Text=UiLanguage.Text(label),TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center});var number=new TextBlock{Text=value.ToString("0.00",CultureInfo.InvariantCulture),Opacity=.75,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right};Grid.SetColumn(number,2);row.Children.Add(number);var slider=new Slider{Minimum=0,Maximum=max,Value=value,StepFrequency=.01,MinHeight=32};Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(slider,UiLanguage.Text(label));slider.ValueChanged+=(_,e)=>number.Text=e.NewValue.ToString("0.00",CultureInfo.InvariantCulture);Grid.SetColumn(slider,1);row.Children.Add(slider);(target??panel).Children.Add(row);return slider;}
  var metal=Slider("Металличность",s.Metallic);var rough=Slider("Шероховатость",s.Roughness);var ao=Slider("AO",s.Ao);var strength=Slider("Сила normal",s.NormalStrength,3);
  var native=new ToggleSwitch{Header="Сохранять штатную цветовую текстуру",IsOn=s.Native};panel.Children.Add(native);
  string? baseMap=s.Base,normal=s.Normal,orm=s.Orm,metalMap=s.MetalMap,roughMap=s.RoughMap,aoMap=s.AoMap,emissive=s.Emissive;
  var maps=new StackPanel{Spacing=8};var advanced=new StackPanel{Spacing=8};
  void MapRow(string label,Func<string?> current,Action<string?> assign){
   var row=new Grid{ColumnSpacing=12};row.ColumnDefinitions.Add(new(){Width=new GridLength(140)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
   var filename=new TextBlock{VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,Opacity=.75};
   void Refresh(){filename.Text=current()==null?UiLanguage.Text("Не выбрана"):Path.GetFileName(current());ToolTipService.SetToolTip(filename,current());}
   mapRefresh.Add(Refresh);Refresh();row.Children.Add(DialogButton(label,async()=>{assign(await PickMap(current()));Refresh();}));Grid.SetColumn(filename,1);row.Children.Add(filename);maps.Children.Add(row);
  }
  MapRow("Базовый цвет…",()=>baseMap,v=>baseMap=v);MapRow("Normal-карта…",()=>normal,v=>normal=v);MapRow("ORM…",()=>orm,v=>orm=v);
  MapRow("Металличность…",()=>metalMap,v=>metalMap=v);MapRow("Шероховатость…",()=>roughMap,v=>roughMap=v);MapRow("AO…",()=>aoMap,v=>aoMap=v);MapRow("Свечение…",()=>emissive,v=>emissive=v);
  var emission=Slider("Сила свечения",s.EmissiveStrength,8,advanced);var ibl=Slider("Свет окружения",s.Ibl,3,advanced);var normalY=new ComboBox{Header="Normal-карта",ItemsSource=new[]{"DirectX","OpenGL"},SelectedIndex=s.NormalY<0?0:1};advanced.Children.Add(normalY);
  maps.Children.Add(DialogButton("Убрать загруженные карты",()=>{baseMap=normal=orm=metalMap=roughMap=aoMap=emissive=null;foreach(var refresh in mapRefresh)refresh();return Task.CompletedTask;}));
  panel.Children.Add(new Expander{Header=UiLanguage.Text("Карты текстур"),Content=maps,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch});
  panel.Children.Add(new Expander{Header=UiLanguage.Text("Дополнительно"),Content=advanced,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch});
  var dialog=new ContentDialog{Title=name,MinWidth=Math.Min(480,Math.Max(280,((FrameworkElement)Content).ActualWidth-48)),Content=new ScrollViewer{Content=panel,MaxHeight=520},PrimaryButtonText=UiLanguage.Text("Применить"),CloseButtonText=UiLanguage.Text("Отмена"),XamlRoot=((FrameworkElement)Content).XamlRoot};
  UiLanguage.Apply(panel);
  if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
  dirty=true;settings[name]=new(baseMap,normal,orm,(float)metal.Value,(float)rough.Value,(float)ao.Value,(float)strength.Value,native.IsOn,metalMap,roughMap,aoMap,emissive,(float)emission.Value,(float)ibl.Value,normalY.SelectedIndex==0?-1:1);
  await Display();
 }
 async Task Export()
 {
  if(lods.Count==0||mtl==null)throw new InvalidOperationException("Выберите NMF и MTL.");
  var destination=await Folder();if(destination==null)return;
  await ModelExport.Export(lods,glassObjects,settings,mtl,mask,maskMaterial,destination,ReadImage);
  dirty=false;status.Text=UiLanguage.Text("Готово")+": "+destination;
 }
}
