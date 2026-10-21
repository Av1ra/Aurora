using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
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

sealed partial class EditorWindow:Window
{
 readonly WebView2 view=new();
 readonly ComboBox lodChoice=new(){Header=UiLanguage.Text("Модель / LOD"),PlaceholderText=UiLanguage.Text("Сначала откройте NMF"),HorizontalAlignment=HorizontalAlignment.Stretch};
 readonly ComboBox objects=new(){Header=UiLanguage.Text("Объект"),PlaceholderText=UiLanguage.Text("Выберите объект"),HorizontalAlignment=HorizontalAlignment.Stretch};
 readonly ComboBox materialChoice=new(){Header=UiLanguage.Text("Материал"),PlaceholderText=UiLanguage.Text("Выберите материал"),HorizontalAlignment=HorizontalAlignment.Stretch};
 readonly TextBlock materialFile=new(){FontSize=12,Opacity=.75,TextTrimming=TextTrimming.CharacterEllipsis};
 readonly TextBlock status=new(){Text="Выберите NMF и MTL",TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true};
 string statusKey="Выберите NMF и MTL";string? statusDetail,statusHint;
 readonly Dictionary<string,NativeModel> lods=[];readonly Dictionary<string,HashSet<string>> glassObjects=[];
 readonly Dictionary<string,MaterialSettings> settings=[];
 string? selectedLod,mtl,mask,maskMaterial;bool ready,busy,processing,dirty,closeApproved,closePrompt;
 ContentDialog? activeDialog;
 string? viewportHtml;
 readonly TextBlock dirtyText=new(){Text=UiLanguage.Text("Есть несохранённые изменения"),FontSize=12,Opacity=.75,Visibility=Visibility.Collapsed};
 readonly ProgressBar busyIndicator=new(){IsIndeterminate=true,Visibility=Visibility.Collapsed};
 readonly List<(Button Button,Func<bool> CanExecute)> commandButtons=[];
 readonly StackPanel emptyView=new(){Spacing=8,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MaxWidth=320};
 bool HasModel=>ready&&selectedLod!=null;
 string? startupModel=Environment.GetCommandLineArgs().Skip(1).FirstOrDefault(x=>x.EndsWith(".nmf",StringComparison.OrdinalIgnoreCase)||x.EndsWith(".obj",StringComparison.OrdinalIgnoreCase)||x.EndsWith(".aur",StringComparison.OrdinalIgnoreCase));
 public EditorWindow()
 {
  Title="Aurora Editor";SystemBackdrop=new MicaBackdrop();WindowChrome.ResizeLogical(this,1250,850);
  var grid=new Grid();grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
  var viewport=new Grid();viewport.Children.Add(view);Grid.SetRow(viewport,1);grid.Children.Add(viewport);
  emptyView.Children.Add(new TextBlock{Text=UiLanguage.Text("Откройте модель"),FontSize=22,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
  emptyView.Children.Add(new TextBlock{Text=UiLanguage.Text("Откройте NMF или импортируйте OBJ. Перетащить файл сюда тоже можно."),TextWrapping=TextWrapping.Wrap,Opacity=.7});
  emptyView.IsHitTestVisible=false;viewport.Children.Add(emptyView);BuildGizmoControls(viewport);BuildPreviewWorkspace(viewport);
  grid.AllowDrop=true;
  grid.DragOver+=(_,e)=>{if(!busy&&e.DataView.Contains(StandardDataFormats.StorageItems)){e.AcceptedOperation=DataPackageOperation.Copy;e.DragUIOverride.Caption=UiLanguage.Text("Открыть модель");}e.Handled=true;};
  grid.Drop+=async(_,e)=>{e.Handled=true;if(busy||!e.DataView.Contains(StandardDataFormats.StorageItems))return;try{var files=(await e.DataView.GetStorageItemsAsync()).OfType<Windows.Storage.StorageFile>().Where(x=>x.FileType.Equals(".nmf",StringComparison.OrdinalIgnoreCase)||x.FileType.Equals(".obj",StringComparison.OrdinalIgnoreCase)||x.FileType.Equals(".aur",StringComparison.OrdinalIgnoreCase)).ToArray();if(files.Length!=1){SetStatus("Перетащите один NMF или OBJ.");return;}await RunCommand(async()=>{if(await ConfirmDiscard()){await OpenDocument(files[0].Path);}});}catch(Exception ex){SetStatus(ex.Message);}};
  WindowChrome.Attach(this,grid,"Aurora Editor");

  RegisterCommand("Открыть NMF…",async()=>{var path=await Pick(".nmf");if(path!=null&&await ConfirmDiscard())await Load(path);});
  RegisterCommand("Добавить LOD…",async()=>{var path=await Pick(".nmf",".obj");if(path!=null){if(Path.GetExtension(path).Equals(".obj",StringComparison.OrdinalIgnoreCase))await ImportObj(path,true);else{await AddModel(path);SelectLod(path);dirty=true;await Display();}}},()=>HasModel);
  RegisterCommand("Выбрать MTL…",async()=>{var chosen=await Pick(".mtl");if(chosen!=null){mtl=chosen;dirty=true;SetStatus(Path.GetFileName(mtl));await LoadTextures();}},()=>HasModel);


  lodChoice.SelectionChanged+=async(_,_)=>{if(!busy&&lodChoice.SelectedItem is ComboBoxItem item&&item.Tag is string path){await RunCommand(async()=>{selectedLod=path;await Display();});}};
  objects.SelectionChanged+=async(_,_)=>{try{if(ready&&!busy){int index=objects.SelectedIndex;if(index>=0&&selectedLod!=null&&CurrentModel().Nodes[index].Groups.FirstOrDefault() is DrawGroup group&&group.Material<materialChoice.Items.Count)materialChoice.SelectedIndex=group.Material;await view.ExecuteScriptAsync("select("+index+")");}}catch(Exception ex){SetStatus(ex.Message);}finally{UpdateControls();}};
  materialChoice.SelectionChanged+=(_,_)=>UpdateControls();
  RegisterCommand("Настроить PBR",ConfigurePbr,()=>HasModel&&materialChoice.SelectedItem!=null);
  RegisterCommand("Сделать объект стеклом",async()=>{if(selectedLod==null||objects.SelectedItem is not string name)throw new InvalidOperationException("Выберите объект.");glassObjects[selectedLod].Add(name);dirty=true;await view.ExecuteScriptAsync("glassObject("+objects.SelectedIndex+",true)");SetStatus("Стекло",name,"Настройте соответствующий объект в каждом LOD.");},()=>HasModel&&objects.SelectedItem!=null);
  RegisterCommand("Стекло через альфа-маску…",async()=>{if(materialChoice.SelectedItem is not string name)throw new InvalidOperationException("Выберите материал кузова.");var chosen=await Pick(".dds",".png");if(chosen!=null){mask=chosen;maskMaterial=name;dirty=true;SetStatus("Альфа-маска",Path.GetFileName(mask)+" — "+name);}},()=>HasModel&&materialChoice.SelectedItem!=null);
  RegisterCommand("Сбросить стекло",async()=>{if(selectedLod!=null)glassObjects[selectedLod].Clear();mask=null;maskMaterial=null;dirty=true;await Display();},()=>HasModel&&(mask!=null||(selectedLod!=null&&glassObjects[selectedLod].Count>0)));
  RegisterCommand("Сохранить мод…",Export,()=>HasModel&&mtl!=null);
  BuildMenuLayout(grid);
  view.CoreWebView2Initialized+=(_,_)=>{view.CoreWebView2.NavigationStarting+=(_,e)=>{if(!ViewportNavigation.IsAllowed(e.Uri,viewportHtml))e.Cancel=true;};view.CoreWebView2.Settings.AreDevToolsEnabled=false;view.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;view.CoreWebView2.WebMessageReceived+=async(_,e)=>{try{using var json=JsonDocument.Parse(e.WebMessageAsJson);if(json.RootElement.TryGetProperty("error",out var viewportError))SetStatus("Viewport",viewportError.GetString());if(json.RootElement.TryGetProperty("ready",out var readyProperty)){ready=true;await view.ExecuteScriptAsync("document.getElementById('hint').textContent="+JsonSerializer.Serialize(UiLanguage.Text("Перетаскивание: вращение. Колесо: масштаб. Щелчок: выбор объекта.")));if(startupModel is string path){startupModel=null;await RunCommand(()=>OpenDocument(path));}else await Display();}if(json.RootElement.TryGetProperty("cameraFov",out var cf))cameraFov.Text="FOV: "+cf.GetInt32();ReceivePreview(json.RootElement);await ReceiveHistory(json.RootElement);if(json.RootElement.TryGetProperty("gizmo",out var gizmoMessage)){ReceiveGizmo(gizmoMessage);if(transportPoint!=null&&gizmoMessage.TryGetProperty("phase",out var phase)&&phase.GetString()=="end")await Display();else await RefreshLighting();}if(!JointFrameRequested&&json.RootElement.TryGetProperty("selected",out var index)&&index.TryGetInt32(out int i)&&i>=-1&&i<objects.Items.Count)objects.SelectedIndex=i;}catch{}};};
  AppWindow.Closing+=async(_,e)=>{if(!dirty||closeApproved)return;e.Cancel=true;if(closePrompt||busy)return;closePrompt=true;try{if(await ConfirmDiscard(true)){closeApproved=true;Close();}}finally{closePrompt=false;}};
  Activated+=async(_,e)=>{if(e.WindowActivationState==WindowActivationState.Deactivated||!UiLanguage.Reload())return;UiLanguage.Apply(grid);RefreshMenuLanguage();if(activeDialog!=null)UiLanguage.Apply(activeDialog);RefreshStatus();UpdateControls();if(ready)try{await UpdateViewportLanguage();}catch{}};
  RefreshMenuLanguage();UpdateControls();LoadedView();
 }
 async void LoadedView(){try{
  // Keep writable browser state outside the installation, including read-only installs.
  string userData=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aurora","Editor","WebView2");
  Directory.CreateDirectory(userData);
  var environment=await CoreWebView2Environment.CreateWithOptionsAsync(null,userData,new CoreWebView2EnvironmentOptions());
  await view.EnsureCoreWebView2Async(environment);
  string folder=File.Exists(Path.Combine(AppContext.BaseDirectory,"Editor","Assets","viewport.html"))?"Editor":"";viewportHtml=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,folder,"Assets","viewport.html")).Replace("/*AURORA_GIZMO*/",File.ReadAllText(Path.Combine(AppContext.BaseDirectory,folder,"Assets","gizmo.js"))).Replace("/*AURORA_PREVIEWS*/",File.ReadAllText(Path.Combine(AppContext.BaseDirectory,folder,"Assets","previews.js")));view.CoreWebView2.NavigateToString(viewportHtml);
 }catch(Exception ex){SetStatus("Не удалось открыть viewport",ex.Message);}}
 Task UpdateViewportLanguage()=>view.ExecuteScriptAsync("document.getElementById('hint').textContent="+JsonSerializer.Serialize(UiLanguage.Text("Перетаскивание: вращение. Колесо: масштаб. Щелчок: выбор объекта."))).AsTask();
 void SetStatus(string key,string? detail=null,string? hint=null){statusKey=key;statusDetail=detail;statusHint=hint;RefreshStatus();}
 void RefreshStatus()=>status.Text=UiLanguage.Text(statusKey)+(statusDetail==null?"":": "+statusDetail)+(statusHint==null?"":". "+UiLanguage.Text(statusHint));
 async Task<ContentDialogResult> ShowDialog(ContentDialog dialog){FormatEditorNumbers(dialog);activeDialog=dialog;try{return await dialog.ShowAsync();}finally{activeDialog=null;}}
 async Task<bool> ConfirmDiscard(bool closing=false){if(!dirty)return true;return await ShowDialog(new ContentDialog{Title=UiLanguage.Text(closing?"Закрыть редактор?":"Открыть другую модель?"),Content=UiLanguage.Text("Несохранённые настройки текущей модели будут потеряны."),PrimaryButtonText=UiLanguage.Text(closing?"Закрыть без сохранения":"Открыть"),CloseButtonText=UiLanguage.Text("Отмена"),DefaultButton=ContentDialogButton.Close,XamlRoot=((FrameworkElement)Content).XamlRoot})==ContentDialogResult.Primary;}
 NativeModel CurrentModel()=>lods[selectedLod??throw new InvalidOperationException("Выберите NMF")];
 void UpdateControls(){materialFile.Text=mtl==null?UiLanguage.Text("MTL не выбран"):Path.GetFileName(mtl);ToolTipService.SetToolTip(materialFile,mtl);dirtyText.Visibility=dirty?Visibility.Visible:Visibility.Collapsed;busyIndicator.Visibility=busy&&processing?Visibility.Visible:Visibility.Collapsed;view.IsHitTestVisible=!busy;foreach(var command in commandButtons)command.Button.IsEnabled=!busy&&!cameraEditing&&command.CanExecute();lodChoice.IsEnabled=!busy&&HasModel;objects.IsEnabled=!busy&&HasModel;materialChoice.IsEnabled=!busy&&HasModel;variantChoice.IsEnabled=!busy&&variants.Count>1;gizmoActions.IsHitTestVisible=!busy;gizmoNumbers.IsHitTestVisible=!busy;foreach(var box in gizmoCoordinates)if(box!=null)box.IsEnabled=!busy;emptyView.Visibility=selectedLod==null?Visibility.Visible:Visibility.Collapsed;UpdateMenuState();}
 void RegisterCommand(string label,Func<Task> action,Func<bool>? canExecute=null)=>editorCommands[label]=(action,canExecute??(()=>true));
 Task RunCommand(Func<Task> action)=>RunHistoryCommand(action);
 void SelectLod(string path){selectedLod=path;lodChoice.SelectedItem=lodChoice.Items.OfType<ComboBoxItem>().First(x=>string.Equals(x.Tag as string,path,StringComparison.OrdinalIgnoreCase));}
 void AddLodChoice(string path){if(lodChoice.Items.OfType<ComboBoxItem>().Any(x=>string.Equals(x.Tag as string,path,StringComparison.OrdinalIgnoreCase)))return;var item=new ComboBoxItem{Content=Path.GetFileName(path),Tag=path};ToolTipService.SetToolTip(item,path);lodChoice.Items.Add(item);}
 async Task<string?> Pick(params string[] extensions){var picker=new Windows.Storage.Pickers.FileOpenPicker();foreach(var ext in extensions)picker.FileTypeFilter.Add(ext);WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));return(await picker.PickSingleFileAsync())?.Path;}
 async Task<string?> Folder(){var picker=new Windows.Storage.Pickers.FolderPicker();picker.FileTypeFilter.Add("*");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));return(await picker.PickSingleFolderAsync())?.Path;}
 async Task AddModel(string path){if(lods.ContainsKey(path))throw new InvalidDataException(UiLanguage.Text("Модель с таким именем уже загружена."));var model=await Task.Run(()=>new NativeModel(File.ReadAllBytes(path)));if(documentTransform!=System.Numerics.Matrix4x4.Identity)model=ModelOrientation.Apply(model,documentTransform);lods[path]=model;glassObjects.TryAdd(path,[]);AddLodChoice(path);}
 async Task Load(string path)
 {
  processing=true;UpdateControls();
  // The primary model can still be inspected when a separate LOD is missing
  // or unsupported. Its script reference stays intact for replacement/export.
  var loadErrors=new List<string>();string folder=Path.GetDirectoryName(path)!;string scriptFile=Path.Combine(folder,"script.ini");
  var siblings=Directory.GetFiles(folder,"*.nmf").Where(p=>p.Equals(path,StringComparison.OrdinalIgnoreCase)||Regex.IsMatch(Path.GetFileName(p),@"^lod.*\.nmf$",RegexOptions.IgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
  if(File.Exists(scriptFile)){string rawScript=File.ReadAllText(scriptFile);loadErrors.AddRange(VehicleLights.Warnings(rawScript).Concat(VehicleCargoBox.Warnings(rawScript)).Select(w=>UiLanguage.Text("Нестандартная запись в исходном скрипте")+": "+w));}
  if(File.Exists(scriptFile))foreach(var lod in VehicleLods.Read(File.ReadAllText(scriptFile)))try{siblings.Add(VehicleAssets.Resolve(lod.Model,folder,VehicleAssets.Media(folder)??MediaFolder()));}catch(Exception ex){loadErrors.Add(lod.Model+": "+ex.Message);}
  var parsed=await Task.Run(()=>{var result=new Dictionary<string,NativeModel>{{path,new NativeModel(File.ReadAllBytes(path))}};foreach(string file in siblings.Where(p=>!p.Equals(path,StringComparison.OrdinalIgnoreCase)))try{result[file]=new NativeModel(File.ReadAllBytes(file));}catch(Exception ex){loadErrors.Add(Path.GetFileName(file)+": "+ex.Message);}return result;});
  lods.Clear();glassObjects.Clear();settings.Clear();lodChoice.Items.Clear();mask=null;maskMaterial=null;
  ResetVehicle(Path.Combine(Path.GetDirectoryName(path)!,"script.ini"));
  foreach(var pair in parsed.OrderBy(p=>p.Key.Equals(path,StringComparison.OrdinalIgnoreCase)?0:1)){lods[pair.Key]=pair.Value;glassObjects[pair.Key]=[];AddLodChoice(pair.Key);}
  selectedLod=path;SelectLod(path);
  await LoadMaterialVariants(path);
  dirty=false;MarkHistorySaved();await Display();loadErrors.AddRange(partWarnings);SetStatus("Загружено моделей и LOD",lods.Count.ToString(CultureInfo.InvariantCulture)+(loadErrors.Count>0?"\n"+string.Join("\n",loadErrors):""));
 }
 async Task Display()
 {
  processing=true;UpdateControls();if(!ready||selectedLod==null)return;string? selectedObject=objects.SelectedItem as string,selectedMaterial=materialChoice.SelectedItem as string;var model=ViewportModel();objects.IsEnabled=!JointFrameRequested;objects.Items.Clear();foreach(var node in model.Nodes)objects.Items.Add(node.Name);materialChoice.Items.Clear();foreach(var name in model.Materials)materialChoice.Items.Add(name);objects.SelectedItem=!JointFrameRequested&&selectedObject!=null&&objects.Items.Contains(selectedObject)?selectedObject:null;if(materialChoice.Items.Count>0)materialChoice.SelectedItem=selectedMaterial!=null&&materialChoice.Items.Contains(selectedMaterial)?selectedMaterial:materialChoice.Items[0];
  var scene=BuildViewportScene(model);
  string sceneJson=await Task.Run(()=>JsonSerializer.Serialize(scene));
  await view.ExecuteScriptAsync("load("+sceneJson+")");await view.ExecuteScriptAsync("wireframe("+(wireframeMenu.IsChecked?"true":"false")+")");await view.ExecuteScriptAsync("select("+objects.SelectedIndex+")");await LoadTextures();
  foreach(var pair in settings)await ApplyPreview(pair.Key,pair.Value);
  for(int i=0;i<model.Nodes.Count;i++)if(!JointFrameRequested&&glassObjects[selectedLod].Contains(model.Nodes[i].Name))await view.ExecuteScriptAsync("glassObject("+i+",true)");
  await LoadCargoTextures();await LoadPartTextures();await RefreshGizmo();if(partWarnings.Count>0)SetStatus("Просмотр сборки",string.Join("\n",partWarnings));
 }
 async Task LoadTextures()
 {
  if(!ready||mtl==null||!File.Exists(mtl))return;processing=true;UpdateControls();
  string text=File.ReadAllText(mtl);string? name=null;
  foreach(var line in text.Split('\n'))
  {
   var material=Regex.Match(line,@"^\s*\$SUBMATERIAL\s+(.+?)\s*$");if(material.Success){name=material.Groups[1].Value;continue;}
   var texture=Regex.Match(line,@"^\s*\$TEXTURE(?:_MTL)?\s+([012])\s+(.+?)\s*$");
   if(name!=null&&texture.Success)
   {
    try{var file=MaterialFiles.Resolve(mtl,texture.Groups[2].Value);var image=await ReadImage(file);string channel=texture.Groups[1].Value switch{"1"=>"specularMap","2"=>"normalMap",_=>"texture"};await PreviewTexture(name,image,channel);}catch(Exception ex){SetStatus("Текстура",ex.Message);}
   }
  }
 }
 async Task<RgbaImage> ReadImage(string file)
 {
  if(Path.GetExtension(file).Equals(".dds",StringComparison.OrdinalIgnoreCase))return DdsImage.Decode(File.ReadAllBytes(file));
  var storage=await Windows.Storage.StorageFile.GetFileFromPathAsync(file);using var stream=await storage.OpenReadAsync();var decoder=await BitmapDecoder.CreateAsync(stream);if(decoder.PixelWidth>8192||decoder.PixelHeight>8192)throw new InvalidDataException(UiLanguage.Text("Максимальный размер текстуры — 8192 × 8192."));var data=await decoder.GetPixelDataAsync(BitmapPixelFormat.Rgba8,BitmapAlphaMode.Straight,new BitmapTransform(),ExifOrientationMode.IgnoreExifOrientation,ColorManagementMode.DoNotColorManage);return new((int)decoder.PixelWidth,(int)decoder.PixelHeight,data.DetachPixelData());
 }
 async Task PreviewTexture(string material,RgbaImage image,string channel="texture")
 {
  using var stream=new InMemoryRandomAccessStream();var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream);encoder.SetPixelData(BitmapPixelFormat.Rgba8,BitmapAlphaMode.Straight,(uint)image.Width,(uint)image.Height,96,96,image.Pixels);await encoder.FlushAsync();stream.Seek(0);var bytes=new byte[(int)stream.Size];using var reader=new DataReader(stream);await reader.LoadAsync((uint)bytes.Length);reader.ReadBytes(bytes);await view.ExecuteScriptAsync("texture("+JsonSerializer.Serialize(material)+","+JsonSerializer.Serialize("data:image/png;base64,"+Convert.ToBase64String(bytes))+","+JsonSerializer.Serialize(channel)+")");
 }
 async Task ApplyPreview(string name,MaterialSettings s)
 {
  await view.ExecuteScriptAsync("material("+JsonSerializer.Serialize(name)+","+JsonSerializer.Serialize(new{metallic=s.Metallic,roughness=s.Roughness,ao=s.Ao,normalY=s.NormalY,normalStrength=s.NormalStrength,emissionStrength=s.EmissiveStrength,iblStrength=s.Ibl,diffuseColour=s.NativeDiffuse,ambientColour=s.NativeAmbient,specularColour=s.NativeSpecular})+")");
  foreach(var map in new[]{("texture",s.PbrBase??s.Base),("normalMap",s.PbrNormal??s.Normal),("ormMap",s.Orm),("metalMap",s.MetalMap),("roughMap",s.RoughMap),("aoMap",s.AoMap),("emissiveMap",s.Emissive),("specularMap",s.Specular)})if(map.Item2!=null)await PreviewTexture(name,await ReadImage(map.Item2),map.Item1);
 }
 async Task ConfigurePbr()
 {
  if(materialChoice.SelectedItem is not string name)throw new InvalidOperationException("Выберите материал.");
  var dialogError=new InfoBar{Severity=InfoBarSeverity.Error,IsOpen=false,IsClosable=true};
  Button DialogButton(string label,Func<Task> action){var b=new Button{Content=UiLanguage.Text(label),HorizontalAlignment=HorizontalAlignment.Stretch};b.Click+=async(_,_)=>{b.IsEnabled=false;try{await action();}catch(Exception ex){dialogError.Message=ex.Message;dialogError.IsOpen=true;}finally{b.IsEnabled=true;}};return b;}
  async Task<string?> PickMap(string? current)=>await Pick(".dds",".png",".jpg",".jpeg",".bmp",".tif",".tiff")??current;
  var s=settings.GetValueOrDefault(name)??new();var panel=new StackPanel{Spacing=12};panel.Children.Add(dialogError);
  var mapRefresh=new List<Action>();
  Slider Slider(string label,double value,double max=1,Panel? target=null){var row=new Grid{ColumnSpacing=10};row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new GridLength(40)});row.Children.Add(new TextBlock{Text=UiLanguage.Text(label),TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center});var number=new TextBlock{Text=value.ToString("0.0",CultureInfo.InvariantCulture),Opacity=.75,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right};Grid.SetColumn(number,2);row.Children.Add(number);var slider=new Slider{Minimum=0,Maximum=max,Value=value,StepFrequency=.1,MinHeight=32};Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(slider,UiLanguage.Text(label));slider.ValueChanged+=(_,e)=>number.Text=e.NewValue.ToString("0.0",CultureInfo.InvariantCulture);Grid.SetColumn(slider,1);row.Children.Add(slider);(target??panel).Children.Add(row);return slider;}
  var metal=Slider("Металличность",s.Metallic);var rough=Slider("Шероховатость",s.Roughness);var ao=Slider("AO",s.Ao);var strength=Slider("Сила normal",s.NormalStrength,3);
  var native=new ToggleSwitch{Header=new TextBlock{Text=UiLanguage.Text("Сохранять штатную цветовую текстуру"),TextWrapping=TextWrapping.Wrap},IsOn=s.Native};panel.Children.Add(native);
  string? baseMap=s.Base,normal=s.Normal,orm=s.Orm,metalMap=s.MetalMap,roughMap=s.RoughMap,aoMap=s.AoMap,emissive=s.Emissive,specular=s.Specular,pbrBase=s.PbrBase,pbrNormal=s.PbrNormal;
  var maps=new StackPanel{Spacing=8};var nativeMaps=new StackPanel{Spacing=8};var advanced=new StackPanel{Spacing=8};
  NumberBox[] ColorRow(string label,float[]? value){var row=new StackPanel{Orientation=Orientation.Horizontal,Spacing=5};var boxes=Enumerable.Range(0,4).Select(i=>new NumberBox{Header="RGBA"[i].ToString(),Value=value?.ElementAtOrDefault(i)??1,Width=65,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Hidden}).ToArray();foreach(var box in boxes)row.Children.Add(box);SettingRow(nativeMaps,label,row);return boxes;}
  var diffuseColor=ColorRow("Цвет поверхности",s.NativeDiffuse);var ambientColor=ColorRow("Фоновый цвет",s.NativeAmbient);var specularColor=ColorRow("Цвет блика",s.NativeSpecular);var nativePower=new NumberBox{Value=s.NativePower??16,Minimum=0,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Hidden};SettingRow(nativeMaps,"Сила блика",nativePower);

  void MapRow(string label,Func<string?> current,Action<string?> assign,Panel? target=null){
   var row=new Grid{ColumnSpacing=12};row.ColumnDefinitions.Add(new(){Width=new GridLength(140)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
   var filename=new TextBlock{VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,Opacity=.75};
   void Refresh(){filename.Text=current()==null?UiLanguage.Text("Не выбрана"):Path.GetFileName(current());ToolTipService.SetToolTip(filename,current());}
   mapRefresh.Add(Refresh);Refresh();row.Children.Add(DialogButton(label,async()=>{assign(await PickMap(current()));Refresh();}));Grid.SetColumn(filename,1);row.Children.Add(filename);(target??maps).Children.Add(row);
  }
  MapRow("Базовый цвет…",()=>baseMap,v=>baseMap=v,nativeMaps);MapRow("Normal-карта…",()=>normal,v=>normal=v,nativeMaps);MapRow("Specular…",()=>specular,v=>specular=v,nativeMaps);
  MapRow("Базовый цвет…",()=>pbrBase,v=>pbrBase=v);MapRow("Normal-карта…",()=>pbrNormal,v=>pbrNormal=v);MapRow("ORM…",()=>orm,v=>orm=v);
  MapRow("Металличность…",()=>metalMap,v=>metalMap=v);MapRow("Шероховатость…",()=>roughMap,v=>roughMap=v);MapRow("AO…",()=>aoMap,v=>aoMap=v);MapRow("Свечение…",()=>emissive,v=>emissive=v);
  var emission=Slider("Сила свечения",s.EmissiveStrength,8,advanced);var ibl=Slider("Свет окружения",s.Ibl,3,advanced);var normalY=new ComboBox{Header="Normal-карта",ItemsSource=new[]{"DirectX","OpenGL"},SelectedIndex=s.NormalY<0?0:1};advanced.Children.Add(normalY);
  maps.Children.Add(DialogButton("Убрать загруженные карты",()=>{pbrBase=pbrNormal=orm=metalMap=roughMap=aoMap=emissive=null;foreach(var refresh in mapRefresh)refresh();return Task.CompletedTask;}));
  panel.Children.Add(new Expander{Header=UiLanguage.Text("Стандартный материал"),Content=nativeMaps,IsExpanded=true,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch});
  panel.Children.Add(new Expander{Header="PBR",Content=maps,IsExpanded=true,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch});
  panel.Children.Add(new Expander{Header=UiLanguage.Text("Дополнительно"),Content=advanced,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch});
  var dialogBody=new ScrollViewer{Content=panel,MinWidth=Math.Max(0,Math.Min(432,((FrameworkElement)Content).ActualWidth-96)),MaxHeight=Math.Min(520,Math.Max(180,((FrameworkElement)Content).ActualHeight-200)),HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
  var dialog=new ContentDialog{Title=UiLanguage.Text("Настройки материала")+": "+name,Content=dialogBody,PrimaryButtonText=UiLanguage.Text("Применить"),CloseButtonText=UiLanguage.Text("Отмена"),XamlRoot=((FrameworkElement)Content).XamlRoot};
  string? reload=null;foreach(var entry in new[]{("Загрузить стандартный MTL…","native"),("Загрузить PBR MTL…","pbr")}){var button=new Button{Content=UiLanguage.Text(entry.Item1),HorizontalAlignment=HorizontalAlignment.Stretch};button.Click+=(_,_)=>{reload=entry.Item2;dialog.Hide();};panel.Children.Insert(0,button);}
  UiLanguage.Apply(panel);
  dialog.PrimaryButtonClick+=(_,e)=>{if(diffuseColor.Concat(ambientColor).Concat(specularColor).Append(nativePower).Any(b=>!double.IsFinite(b.Value))){e.Cancel=true;dialogError.Message=UiLanguage.Text("Проверьте значения материала");dialogError.IsOpen=true;}};var choice=await ShowDialog(dialog);if(reload!=null){if(reload=="native")await LoadNativeMaterial();else await LoadPbrMaterial();await ConfigurePbr();return;}if(choice!=ContentDialogResult.Primary)return;
  var updated=new Dictionary<string,MaterialSettings>{{name,s with{Base=baseMap,Normal=normal,Orm=orm,Metallic=(float)metal.Value,Roughness=(float)rough.Value,Ao=(float)ao.Value,NormalStrength=(float)strength.Value,Native=native.IsOn,MetalMap=metalMap,RoughMap=roughMap,AoMap=aoMap,Emissive=emissive,EmissiveStrength=(float)emission.Value,Ibl=(float)ibl.Value,NormalY=normalY.SelectedIndex==0?-1:1,Specular=specular,PbrBase=pbrBase,PbrNormal=pbrNormal,NativeDiffuse=diffuseColor.Select(b=>(float)b.Value).ToArray(),NativeAmbient=ambientColor.Select(b=>(float)b.Value).ToArray(),NativeSpecular=specularColor.Select(b=>(float)b.Value).ToArray(),NativePower=(float)nativePower.Value}}};await NormalizeMaterialMaps(updated);dirty=true;settings[name]=updated[name];StoreVariant();
  await Display();
 }
 async Task Export()
 {
  if(lods.Count==0||mtl==null)throw new InvalidOperationException("Выберите NMF и MTL.");
  var destination=await Folder();if(destination==null)return;
  await ModelExport.Export(lods,glassObjects,settings,mtl,mask,maskMaterial,destination,ReadImage,vehicleConfiguration,useSourceScript:false);
  dirty=false;SetStatus("Готово",destination);
 }
}
