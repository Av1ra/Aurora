using Aurora.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
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
 readonly ComboBox lodChoice=new(){Header="Модель / LOD",HorizontalAlignment=HorizontalAlignment.Stretch};
 readonly ComboBox objects=new(){Header="Объект",HorizontalAlignment=HorizontalAlignment.Stretch};
 readonly ComboBox materialChoice=new(){Header="Материал",HorizontalAlignment=HorizontalAlignment.Stretch};
 readonly TextBlock status=new(){Text="Выберите NMF и MTL",TextWrapping=TextWrapping.Wrap};
 readonly Dictionary<string,NativeModel> lods=[];readonly Dictionary<string,HashSet<string>> glassObjects=[];
 readonly Dictionary<string,MaterialSettings> settings=[];
 string? selectedLod,mtl,mask,maskMaterial;bool ready;
 public EditorWindow()
 {
  Title="Aurora Editor";SystemBackdrop=new MicaBackdrop();AppWindow.Resize(new(1250,850));
  var grid=new Grid();grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(350)});grid.Children.Add(view);
  var scroll=new ScrollViewer{Content=controls};Grid.SetColumn(scroll,1);grid.Children.Add(scroll);WindowChrome.Attach(this,grid,"Aurora Editor");
  controls.Children.Add(new TextBlock{Text="Мастер моделей",FontSize=24});
  controls.Children.Add(Button("Открыть NMF…",async()=>{var path=await Pick(".nmf");if(path!=null)await Load(path);}));
  controls.Children.Add(Button("Добавить LOD…",async()=>{var path=await Pick(".nmf");if(path!=null){AddModel(path);lodChoice.SelectedItem=path;await Display();}}));
  controls.Children.Add(Button("Выбрать MTL…",async()=>{mtl=await Pick(".mtl");if(mtl!=null){status.Text=mtl;await LoadTextures();}}));
  controls.Children.Add(lodChoice);controls.Children.Add(objects);controls.Children.Add(materialChoice);
  lodChoice.SelectionChanged+=async(_,_)=>{if(lodChoice.SelectedItem is string path){selectedLod=path;await Display();}};
  objects.SelectionChanged+=async(_,_)=>{if(ready&&objects.SelectedIndex>=0)await view.ExecuteScriptAsync("select("+objects.SelectedIndex+")");};
  controls.Children.Add(Button("Настроить PBR",ConfigurePbr));
  controls.Children.Add(Button("Сделать объект стеклом",async()=>{if(selectedLod==null||objects.SelectedItem is not string name)throw new InvalidOperationException("Выберите объект.");glassObjects[selectedLod].Add(name);await view.ExecuteScriptAsync("glassObject("+objects.SelectedIndex+",true)");status.Text="Стекло: "+name+". Отметьте соответствующий объект в каждом LOD.";}));
  controls.Children.Add(Button("Стекло через альфа-маску…",async()=>{if(materialChoice.SelectedItem is not string name)throw new InvalidOperationException("Выберите материал кузова.");mask=await Pick(".dds",".png");if(mask!=null){maskMaterial=name;status.Text="Альфа-маска: "+Path.GetFileName(mask)+" — "+name;}}));
  controls.Children.Add(Button("Сбросить стекло",async()=>{if(selectedLod!=null)glassObjects[selectedLod].Clear();mask=null;maskMaterial=null;await Display();}));
  controls.Children.Add(Button("Сохранить мод…",Export));controls.Children.Add(status);
  view.CoreWebView2Initialized+=(_,_)=>{view.CoreWebView2.Settings.AreDevToolsEnabled=false;view.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;view.CoreWebView2.WebMessageReceived+=async(_,e)=>{try{using var json=JsonDocument.Parse(e.WebMessageAsJson);if(json.RootElement.TryGetProperty("error",out var viewportError))status.Text="Viewport: "+viewportError.GetString();if(json.RootElement.TryGetProperty("ready",out var readyProperty)){ready=true;await Display();}if(json.RootElement.TryGetProperty("selected",out var index)&&index.TryGetInt32(out int i)&&i>=0&&i<objects.Items.Count)objects.SelectedIndex=i;}catch{}};};
  LoadedView();
 }
 async void LoadedView(){try{await view.EnsureCoreWebView2Async();string folder=File.Exists(Path.Combine(AppContext.BaseDirectory,"Editor","Assets","viewport.html"))?"Editor":"";view.NavigateToString(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,folder,"Assets","viewport.html")));}catch(Exception ex){status.Text="Не удалось открыть viewport: "+ex.Message;}}
 NativeModel CurrentModel()=>lods[selectedLod??throw new InvalidOperationException("Выберите NMF")];
 Button Button(string label,Func<Task> action){var b=new Button{Content=label,HorizontalAlignment=HorizontalAlignment.Stretch};b.Click+=async(_,_)=>{b.IsEnabled=false;try{await action();}catch(Exception ex){status.Text=ex.Message;}finally{b.IsEnabled=true;}};return b;}
 async Task<string?> Pick(params string[] extensions){var picker=new Windows.Storage.Pickers.FileOpenPicker();foreach(var ext in extensions)picker.FileTypeFilter.Add(ext);WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));return(await picker.PickSingleFileAsync())?.Path;}
 async Task<string?> Folder(){var picker=new Windows.Storage.Pickers.FolderPicker();picker.FileTypeFilter.Add("*");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));return(await picker.PickSingleFolderAsync())?.Path;}
 void AddModel(string path){var model=new NativeModel(File.ReadAllBytes(path));lods[path]=model;glassObjects.TryAdd(path,[]);if(!lodChoice.Items.Contains(path))lodChoice.Items.Add(path);}
 async Task Load(string path)
 {
  // Load siblings so LOD files cannot silently disappear from an export.
  var siblings=Directory.GetFiles(Path.GetDirectoryName(path)!,"*.nmf").Where(p=>p.Equals(path,StringComparison.OrdinalIgnoreCase)||Regex.IsMatch(Path.GetFileName(p),@"^lod.*\.nmf$",RegexOptions.IgnoreCase)).ToArray();
  var parsed=siblings.ToDictionary(x=>x,x=>new NativeModel(File.ReadAllBytes(x)));
  lods.Clear();glassObjects.Clear();settings.Clear();lodChoice.Items.Clear();mask=null;maskMaterial=null;
  foreach(var pair in parsed){lods[pair.Key]=pair.Value;glassObjects[pair.Key]=[];lodChoice.Items.Add(pair.Key);}
  selectedLod=path;lodChoice.SelectedItem=path;
  var candidate=Path.ChangeExtension(path,"mtl");var common=Path.Combine(Path.GetDirectoryName(path)!,"material.mtl");mtl=File.Exists(candidate)?candidate:File.Exists(common)?common:null;
  status.Text=$"NMF: {lods.Count} — LOD загружены";await Display();
 }
 async Task Display()
 {
  if(!ready||selectedLod==null)return;var model=CurrentModel();objects.Items.Clear();foreach(var node in model.Nodes)objects.Items.Add(node.Name);materialChoice.Items.Clear();foreach(var name in model.Materials)materialChoice.Items.Add(name);if(objects.Items.Count>0)objects.SelectedIndex=0;if(materialChoice.Items.Count>0)materialChoice.SelectedIndex=0;
  var scene=new{model.Materials,Nodes=model.Nodes.Select(n=>new{n.Name,n.Positions,n.Normals,n.Uv,Indices=n.Indices.Select(x=>(int)x).ToArray(),n.Groups})};
  await view.ExecuteScriptAsync("load("+JsonSerializer.Serialize(scene)+")");await LoadTextures();
  foreach(var pair in settings)await ApplyPreview(pair.Key,pair.Value);
  for(int i=0;i<model.Nodes.Count;i++)if(glassObjects[selectedLod].Contains(model.Nodes[i].Name))await view.ExecuteScriptAsync("glassObject("+i+",true)");
 }
 async Task LoadTextures()
 {
  if(!ready||mtl==null||!File.Exists(mtl))return;
  string text=File.ReadAllText(mtl);string? name=null;
  foreach(var line in text.Split('\n'))
  {
   var material=Regex.Match(line,@"^\s*\$SUBMATERIAL\s+(.+?)\s*$");if(material.Success){name=material.Groups[1].Value;continue;}
   var texture=Regex.Match(line,@"^\s*\$TEXTURE(?:_MTL)?\s+0\s+(.+?)\s*$");
   if(name!=null&&texture.Success)
   {
    try{var file=MaterialFiles.Resolve(mtl,texture.Groups[1].Value);var image=await ReadImage(file);await PreviewTexture(name,image);}catch(Exception ex){status.Text="Текстура: "+ex.Message;}
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
  var s=settings.GetValueOrDefault(name)??new();var panel=new StackPanel{Spacing=12};
  Slider Slider(string label,double value,double max=1){var slider=new Slider{Header=label,Minimum=0,Maximum=max,Value=value,StepFrequency=.01};panel.Children.Add(slider);return slider;}
  var metal=Slider("Металличность",s.Metallic);var rough=Slider("Шероховатость",s.Roughness);var ao=Slider("AO",s.Ao);var strength=Slider("Сила normal",s.NormalStrength,3);
  var native=new ToggleSwitch{Header="Сохранять штатную цветовую текстуру",IsOn=s.Native};panel.Children.Add(native);
  string? baseMap=s.Base,normal=s.Normal,orm=s.Orm,metalMap=s.MetalMap,roughMap=s.RoughMap,aoMap=s.AoMap,emissive=s.Emissive;
  panel.Children.Add(Button("Base color…",async()=>{baseMap=await Pick(".dds");}));panel.Children.Add(Button("Normal…",async()=>{normal=await Pick(".dds");}));panel.Children.Add(Button("ORM (R=AO G=rough B=metal)…",async()=>{orm=await Pick(".dds");}));
  panel.Children.Add(Button("Metallic…",async()=>{metalMap=await Pick(".dds");}));panel.Children.Add(Button("Roughness…",async()=>{roughMap=await Pick(".dds");}));panel.Children.Add(Button("AO…",async()=>{aoMap=await Pick(".dds");}));panel.Children.Add(Button("Emissive…",async()=>{emissive=await Pick(".dds");}));
  var emission=Slider("Сила свечения",s.EmissiveStrength,8);var ibl=Slider("Свет окружения",s.Ibl,3);var normalY=new ComboBox{Header="Normal-карта",ItemsSource=new[]{"DirectX","OpenGL"},SelectedIndex=s.NormalY<0?0:1};panel.Children.Add(normalY);
  panel.Children.Add(Button("Убрать загруженные карты",()=>{baseMap=normal=orm=metalMap=roughMap=aoMap=emissive=null;return Task.CompletedTask;}));
  var dialog=new ContentDialog{Title=name,Content=new ScrollViewer{Content=panel,MaxHeight=520},PrimaryButtonText="Применить",CloseButtonText="Отмена",XamlRoot=((FrameworkElement)Content).XamlRoot};
  if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
  settings[name]=new(baseMap,normal,orm,(float)metal.Value,(float)rough.Value,(float)ao.Value,(float)strength.Value,native.IsOn,metalMap,roughMap,aoMap,emissive,(float)emission.Value,(float)ibl.Value,normalY.SelectedIndex==0?-1:1);
  await Display();
 }
 async Task Export()
 {
  if(lods.Count==0||mtl==null)throw new InvalidOperationException("Выберите NMF и MTL.");
  var destination=await Folder();if(destination==null)return;
  await ModelExport.Export(lods,glassObjects,settings,mtl,mask,maskMaterial,destination,ReadImage);
  status.Text="Готово: "+destination;
 }
}
