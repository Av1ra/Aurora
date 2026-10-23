using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Numerics;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 PreviewSettings previewSettings=new();
 readonly Dictionary<string,TaskCompletionSource<string>> pendingPreviews=[];
 readonly Grid previewWorkspace=new(){Visibility=Visibility.Collapsed,Background=new SolidColorBrush(Windows.UI.Color.FromArgb(255,32,32,32))};
 readonly Image mainPreview=new(){Stretch=Stretch.Uniform},sidePreview=new(){Stretch=Stretch.Uniform};
 readonly Border cameraPanel=new(){Visibility=Visibility.Collapsed,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(12),Padding=new Thickness(12),Background=new SolidColorBrush(Windows.UI.Color.FromArgb(255,40,40,40))};
 readonly Button nightButton=new(){Content=new FontIcon{Glyph="\uE706",FontFamily=new FontFamily("Segoe Fluent Icons"),FontSize=16},Padding=new Thickness(0),Width=28,Height=26,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(12)};
 readonly TextBlock cameraFov=new();
 bool renderingPreview;
 bool nightView;byte[]? mainPng,sidePng;
 void BuildPreviewWorkspace(Grid viewport)
 {
  viewport.SizeChanged+=(_,_)=>cameraPanel.MaxHeight=Math.Max(1,viewport.ActualHeight-24);
  var tests=EffectButtons();Grid.SetColumn(tests,1);viewport.Children.Add(tests);ToolTipService.SetToolTip(nightButton,UiLanguage.Text("Ночь"));Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(nightButton,UiLanguage.Text("Ночь"));nightButton.Click+=async(_,_)=>{if(!HasModel)return;nightView=!nightView;((FontIcon)nightButton.Content).Glyph=nightView?"\uE708":"\uE706";string label=UiLanguage.Text(nightView?"День":"Ночь");ToolTipService.SetToolTip(nightButton,label);Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(nightButton,label);await RefreshLighting();};
  previewWorkspace.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});previewWorkspace.RowDefinitions.Add(new(){Height=GridLength.Auto});
  var images=new Grid{Margin=new Thickness(24),ColumnSpacing=20};images.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});images.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});images.Children.Add(mainPreview);Grid.SetColumn(sidePreview,1);images.Children.Add(sidePreview);previewWorkspace.Children.Add(images);
  var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Spacing=8,Margin=new Thickness(12)};Grid.SetRow(actions,1);previewWorkspace.Children.Add(actions);
  var shot=new Button{Content="Shot"};actions.Children.Add(shot);shot.Click+=async(_,_)=>await RunCommand(SavePreviewCameras);
  var cameras=new Button{Content=UiLanguage.Text("Настроить камеры")};var menu=new MenuFlyout();for(int i=0;i<2;i++){int index=i;var item=new MenuFlyoutItem{Text=UiLanguage.Text(i==0?"Основная камера":"Боковая камера")};item.Click+=async(_,_)=>await EditPreviewCamera(index);menu.Items.Add(item);}cameras.Flyout=menu;actions.Children.Add(cameras);
  var close=new Button{Content=UiLanguage.Text("Закрыть превью")};close.Click+=(_,_)=>previewWorkspace.Visibility=Visibility.Collapsed;actions.Children.Add(close);Grid.SetColumnSpan(previewWorkspace,3);Grid.SetRowSpan(previewWorkspace,2);viewport.Children.Add(previewWorkspace);Grid.SetColumn(cameraPanel,2);viewport.Children.Add(cameraPanel);
 }
 async Task SavePreviewCameras()
 {
  string? previous=selectedLod;PreviewCamera? main=previewSettings.MainCamera,side=previewSettings.SideCamera;
  async Task<PreviewCamera> Camera(int height,float yaw,float pitch){await view.ExecuteScriptAsync("startPreviewCamera("+JsonSerializer.Serialize(new{width=256,height,fov=previewSettings.Fov,yaw,pitch})+")");try{return JsonSerializer.Deserialize<PreviewCamera>(await view.ExecuteScriptAsync("previewCameraPose()"))??throw new InvalidDataException("Invalid camera");}finally{await view.ExecuteScriptAsync("stopPreviewCamera()");}}
  try{SelectLod(lods.Keys.First());await Display();main??=await Camera(256,previewSettings.Yaw,previewSettings.Pitch);side??=await Camera(128,previewSettings.SideYaw,previewSettings.SidePitch);}
  finally{if(previous!=null)SelectLod(previous);await Display();}
  previewSettings=previewSettings with{Automatic=true,MainCamera=main,SideCamera=side};dirty=true;SetStatus("Камеры превью сохранены в проекте");
 }
 static void FormatEditorNumbers(DependencyObject element)
 {
  if(element is NumberBox number){AuoUI.FormatNumber(number);return;}
  if(element is Panel panel)foreach(var child in panel.Children)FormatEditorNumbers(child);else if(element is ContentControl content&&content.Content is DependencyObject child)FormatEditorNumbers(child);else if(element is Border border&&border.Child!=null)FormatEditorNumbers(border.Child);
 }
 string? lampTextureMedia;
 async Task LoadLampTextures()
 {
  string? media=MediaFolder();if(media==null||lampTextureMedia==media)return;
  foreach(var item in new[]{("forward","carlight_forward.dds"),("backward","carlight_backward.dds"),("water","particleeffect/water.dds")}){
   var image=await ReadImage(Path.Combine(media,item.Item2));using var stream=new InMemoryRandomAccessStream();var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream);encoder.SetPixelData(BitmapPixelFormat.Rgba8,BitmapAlphaMode.Straight,(uint)image.Width,(uint)image.Height,96,96,image.Pixels);await encoder.FlushAsync();stream.Seek(0);var bytes=new byte[(int)stream.Size];using var reader=new DataReader(stream);await reader.LoadAsync((uint)bytes.Length);reader.ReadBytes(bytes);await view.ExecuteScriptAsync("lampTexture("+JsonSerializer.Serialize(item.Item1)+","+JsonSerializer.Serialize("data:image/png;base64,"+Convert.ToBase64String(bytes))+")");
  }lampTextureMedia=media;
 }
 async Task RefreshLighting(){if(!ready)return;if(nightView||testLeft||testRight||testBeacon||AllLightPreview)await LoadLampTextures();var forward=Vector3.TransformNormal(Vector3.UnitZ,PivotFrame());var lights=VehicleLights.Read(VehicleTemplate()).Where(_=>nightView||AllLightPreview).Where(l=>JointFrameRequested?l.Rear:!JointLights||!l.Rear).Take(64).Select(l=>new{position=new[]{l.Position.X,l.Position.Y,l.Position.Z},rear=l.Rear,direction=new[]{forward.X*(l.Rear?-1:1),forward.Y*(l.Rear?-1:1),forward.Z*(l.Rear?-1:1)}});await view.ExecuteScriptAsync("lighting("+JsonSerializer.Serialize(new{night=nightView,lights=lights.Cast<object>().Concat(DiagnosticLights()),animate=testLeft||testRight||testBeacon})+")");}
 async Task ConfigurePreviews(){await view.ExecuteScriptAsync("stopWheels(true)");if(wheelRun!=null)wheelRun.IsChecked=false;EndMeshEditing();CloseAnimationTimeline();cabinAssemblyMenu.IsChecked=false;showCargo=false;gizmoVisible=false;transportPoint=null;await RefreshPreviewImages();previewWorkspace.Visibility=Visibility.Visible;}
 async Task RefreshPreviewImages()
 {
  string? previous=selectedLod;try{SelectLod(lods.Keys.First());await Display();mainPng=await RenderPreviewPng(1024,previewSettings.Yaw,previewSettings.Pitch,1024);sidePng=await RenderPreviewPng(512,previewSettings.SideYaw,previewSettings.SidePitch,1024);await SetPreviewImage(mainPreview,mainPng);await SetPreviewImage(sidePreview,sidePng);}finally{if(previous!=null)SelectLod(previous);await Display();}
 }
 static async Task SetPreviewImage(Image image,byte[] png){using var stream=new InMemoryRandomAccessStream();using(var writer=new DataWriter(stream.GetOutputStreamAt(0))){writer.WriteBytes(png);await writer.StoreAsync();}var source=new BitmapImage();await source.SetSourceAsync(stream);image.Source=source;}
 async Task EditPreviewCamera(int index)
 {
  if(cameraEditing||busy)return;cameraEditing=true;string? previous=selectedLod;var before=CaptureSnapshot();
  try{SelectLod(lods.Keys.First());await Display();previewWorkspace.Visibility=Visibility.Collapsed;gizmoActions.Visibility=gizmoNumbers.Visibility=Visibility.Collapsed;UpdateControls();if(effectBar!=null)effectBar.Visibility=Visibility.Collapsed;
   var controls=new StackPanel{Spacing=6,Width=210};var help=new StackPanel{Spacing=5};controls.Children.Add(new TextBlock{Text=UiLanguage.Text(index==0?"Основная камера":"Боковая камера"),FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});foreach(string text in new[]{"W / S — вперёд / назад","A / D — влево / вправо","Q / E — вниз / вверх","Z / X — крен камеры","R — сброс камеры","Мышь — поворот","Колесо — FOV"})help.Children.Add(new TextBlock{Text=UiLanguage.Text(text)});
   controls.Children.Add(cameraFov);controls.Children.Add(new Expander{Header=UiLanguage.Text("Управление"),Content=help,HorizontalAlignment=HorizontalAlignment.Stretch});var apply=new Button{Content=UiLanguage.Text("Применить")};var cancel=new Button{Content=UiLanguage.Text("Отмена")};var buttons=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6};buttons.Children.Add(apply);buttons.Children.Add(cancel);controls.Children.Add(buttons);var done=new TaskCompletionSource<bool>();apply.Click+=(_,_)=>done.TrySetResult(true);cancel.Click+=(_,_)=>done.TrySetResult(false);cameraPanel.Child=new ScrollViewer{Content=controls,HorizontalScrollMode=ScrollMode.Disabled,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};cameraPanel.Visibility=Visibility.Visible;
   await view.ExecuteScriptAsync("startPreviewCamera("+JsonSerializer.Serialize(new{width=256,height=index==0?256:128,fov=previewSettings.Fov,yaw=index==0?previewSettings.Yaw:previewSettings.SideYaw,pitch=index==0?previewSettings.Pitch:previewSettings.SidePitch,pose=index==0?previewSettings.MainCamera:previewSettings.SideCamera})+")");view.Focus(FocusState.Programmatic);
   if(await done.Task){string json=await view.ExecuteScriptAsync("previewCameraPose()");var pose=JsonSerializer.Deserialize<PreviewCamera>(json)??throw new InvalidDataException("Invalid camera");previewSettings=index==0?previewSettings with{MainCamera=pose}:previewSettings with{SideCamera=pose};dirty=true;CommitHistory(before);}
  }catch(Exception ex){SetStatus(ex.Message);}finally{await view.ExecuteScriptAsync("stopPreviewCamera()");cameraEditing=false;cameraPanel.Child=null;cameraPanel.Visibility=Visibility.Collapsed;view.Width=view.Height=double.NaN;view.HorizontalAlignment=HorizontalAlignment.Stretch;view.VerticalAlignment=VerticalAlignment.Stretch;view.Margin=new Thickness(0);if(effectBar!=null)effectBar.Visibility=Visibility.Visible;if(previous!=null)SelectLod(previous);await Display();UpdateControls();await ConfigurePreviews();}
 }
 void ReceivePreview(JsonElement message)
 {
  if(!message.TryGetProperty("preview",out var id)||id.GetString() is not string key||!pendingPreviews.TryGetValue(key,out var task))return;
  if(message.TryGetProperty("error",out var error))task.TrySetException(new InvalidDataException(error.GetString()));else if(message.TryGetProperty("png",out var png)&&png.GetString() is string value)task.TrySetResult(value);
 }
 async Task<byte[]> RenderPreviewPng(int height,float yaw,float pitch,int width=256)
 {
  string id=Guid.NewGuid().ToString("N");var task=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);pendingPreviews[id]=task;
  try{
   await view.ExecuteScriptAsync("requestPreview("+JsonSerializer.Serialize(id)+","+JsonSerializer.Serialize(new{width,height,fov=previewSettings.Fov,yaw,pitch,pose=height==width?previewSettings.MainCamera:previewSettings.SideCamera})+")");string url=await task.Task.WaitAsync(TimeSpan.FromSeconds(30));const string prefix="data:image/png;base64,";if(!url.StartsWith(prefix,StringComparison.Ordinal)||url.Length>8*1024*1024)throw new InvalidDataException("Invalid preview image");return Convert.FromBase64String(url[prefix.Length..]);
  }finally{pendingPreviews.Remove(id);}
 }
 async Task<byte[]> RenderPreview(int height,float yaw,float pitch)=>await PreviewDds(await RenderPreviewPng(height,yaw,pitch),height);
 static async Task<byte[]> PreviewDds(byte[] png,int height){using var stream=new InMemoryRandomAccessStream();using(var writer=new DataWriter(stream.GetOutputStreamAt(0))){writer.WriteBytes(png);await writer.StoreAsync();}var decoder=await BitmapDecoder.CreateAsync(stream);if(decoder.PixelWidth!=256||decoder.PixelHeight!=height)throw new InvalidDataException("Unexpected preview size");var data=await decoder.GetPixelDataAsync(BitmapPixelFormat.Rgba8,BitmapAlphaMode.Straight,new BitmapTransform(),ExifOrientationMode.IgnoreExifOrientation,ColorManagementMode.DoNotColorManage);return DdsImage.Encode(new(256,height,data.DetachPixelData()));}
 async Task<(byte[] Main,byte[] Side)> RenderVariantPreviews(MaterialVariant variant)
 {
  string savedView=JsonSerializer.Deserialize<string>(await view.ExecuteScriptAsync("JSON.stringify(savedCamera())"))??throw new InvalidDataException("Viewport camera missing");StoreVariant();int previous=activeVariant;string? previousLod=selectedLod;bool cargo=showCargo;bool visible=gizmoVisible;bool cabin=cabinAssemblyMenu.IsChecked;bool wasPreview=renderingPreview;renderingPreview=true;await view.ExecuteScriptAsync("stopAnimation()");try{cabinAssemblyMenu.IsChecked=false;ActivateVariant(variants.FindIndex(v=>v.Name==variant.Name));SelectLod(lods.Keys.First());showCargo=false;gizmoVisible=false;await Display();return(await RenderPreview(256,previewSettings.Yaw,previewSettings.Pitch),await RenderPreview(128,previewSettings.SideYaw,previewSettings.SidePitch));}
  finally{renderingPreview=wasPreview;ActivateVariant(previous);if(previousLod!=null)SelectLod(previousLod);showCargo=cargo;gizmoVisible=visible;cabinAssemblyMenu.IsChecked=cabin;await Display();await view.ExecuteScriptAsync("restoreCamera("+savedView+")");}
 }
}
