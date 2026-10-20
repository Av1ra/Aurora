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
sealed record PreviewCamera(float Yaw,float Pitch,float Roll,float Fov,float X,float Y,float Z,float Distance);
sealed record PreviewSettings(bool Automatic=true,float Fov=20,float Yaw=-45,float Pitch=30,float SideYaw=-90,float SidePitch=0,PreviewCamera? MainCamera=null,PreviewCamera? SideCamera=null);
sealed partial class EditorWindow
{
 PreviewSettings previewSettings=new();
 readonly Dictionary<string,TaskCompletionSource<string>> pendingPreviews=[];
 readonly Grid previewWorkspace=new(){Visibility=Visibility.Collapsed,Background=new SolidColorBrush(Windows.UI.Color.FromArgb(255,32,32,32))};
 readonly Image mainPreview=new(){Stretch=Stretch.Uniform},sidePreview=new(){Stretch=Stretch.Uniform};
 readonly Border cameraPanel=new(){Visibility=Visibility.Collapsed,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(12),Padding=new Thickness(12),Background=new SolidColorBrush(Windows.UI.Color.FromArgb(255,40,40,40))};
 readonly Button nightButton=new(){Content="☾",Width=34,Height=32,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(12)};
 readonly TextBlock cameraFov=new();
 bool nightView;byte[]? mainPng,sidePng;bool cameraEditing;double cameraAspect=1;Action? resizeCamera;
 void BuildPreviewWorkspace(Grid viewport)
 {
  resizeCamera=()=>{if(!cameraEditing)return;double width=Math.Max(1,Math.Min(512,Math.Min(viewport.ActualWidth-280,(viewport.ActualHeight-32)*cameraAspect)));view.Width=width;view.Height=width/cameraAspect;cameraPanel.MaxHeight=Math.Max(1,viewport.ActualHeight-24);};viewport.SizeChanged+=(_,_)=>resizeCamera();
  viewport.Children.Add(nightButton);ToolTipService.SetToolTip(nightButton,UiLanguage.Text("Ночь"));nightButton.Click+=async(_,_)=>{if(!HasModel)return;nightView=!nightView;nightButton.Content=nightView?"☀":"☾";ToolTipService.SetToolTip(nightButton,UiLanguage.Text(nightView?"День":"Ночь"));await RefreshLighting();};
  previewWorkspace.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});previewWorkspace.RowDefinitions.Add(new(){Height=GridLength.Auto});
  var images=new Grid{Margin=new Thickness(24),ColumnSpacing=20};images.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});images.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});images.Children.Add(mainPreview);Grid.SetColumn(sidePreview,1);images.Children.Add(sidePreview);previewWorkspace.Children.Add(images);
  var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Spacing=8,Margin=new Thickness(12)};Grid.SetRow(actions,1);previewWorkspace.Children.Add(actions);
  var shot=new Button{Content="Shot"};actions.Children.Add(shot);shot.Click+=async(_,_)=>await RunCommand(async()=>{string? folder=await Folder();if(folder==null)return;await RefreshPreviewImages();string output=Path.Combine(folder,"previews-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]);Directory.CreateDirectory(output);await File.WriteAllBytesAsync(Path.Combine(output,"preview.png"),mainPng!);await File.WriteAllBytesAsync(Path.Combine(output,"preview_side.png"),sidePng!);await File.WriteAllBytesAsync(Path.Combine(output,"preview.dds"),await PreviewDds(mainPng!,256));await File.WriteAllBytesAsync(Path.Combine(output,"preview_side.dds"),await PreviewDds(sidePng!,128));SetStatus("Снимки сохранены",output);});
  var cameras=new Button{Content=UiLanguage.Text("Настроить камеры")};var menu=new MenuFlyout();for(int i=0;i<2;i++){int index=i;var item=new MenuFlyoutItem{Text=UiLanguage.Text(i==0?"Основная камера":"Боковая камера")};item.Click+=async(_,_)=>await EditPreviewCamera(index);menu.Items.Add(item);}cameras.Flyout=menu;actions.Children.Add(cameras);
  var close=new Button{Content=UiLanguage.Text("Закрыть превью")};close.Click+=(_,_)=>previewWorkspace.Visibility=Visibility.Collapsed;actions.Children.Add(close);viewport.Children.Add(previewWorkspace);viewport.Children.Add(cameraPanel);
 }
 static void FormatEditorNumbers(DependencyObject element)
 {
  if(element is NumberBox number){number.SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Hidden;number.NumberFormatter=new Windows.Globalization.NumberFormatting.DecimalFormatter{FractionDigits=1,IntegerDigits=1,IsGrouped=false,NumberRounder=new Windows.Globalization.NumberFormatting.IncrementNumberRounder{Increment=.1,RoundingAlgorithm=Windows.Globalization.NumberFormatting.RoundingAlgorithm.RoundHalfUp}};return;}
  if(element is Panel panel)foreach(var child in panel.Children)FormatEditorNumbers(child);else if(element is ContentControl content&&content.Content is DependencyObject child)FormatEditorNumbers(child);else if(element is Border border&&border.Child!=null)FormatEditorNumbers(border.Child);
 }
 async Task RefreshLighting(){if(!ready)return;var forward=Vector3.TransformNormal(Vector3.UnitZ,PivotFrame());var lights=VehicleLights.Read(VehicleTemplate()).Take(16).Select(l=>new{position=new[]{l.Position.X,l.Position.Y,l.Position.Z},rear=l.Rear,direction=new[]{forward.X*(l.Rear?-1:1),forward.Y*(l.Rear?-1:1),forward.Z*(l.Rear?-1:1)}});await view.ExecuteScriptAsync("lighting("+JsonSerializer.Serialize(new{night=nightView,lights})+")");}
 async Task ConfigurePreviews(){await RefreshPreviewImages();previewWorkspace.Visibility=Visibility.Visible;}
 async Task RefreshPreviewImages()
 {
  string? previous=selectedLod;try{SelectLod(lods.Keys.First());await Display();mainPng=await RenderPreviewPng(256,previewSettings.Yaw,previewSettings.Pitch);sidePng=await RenderPreviewPng(128,previewSettings.SideYaw,previewSettings.SidePitch);await SetPreviewImage(mainPreview,mainPng);await SetPreviewImage(sidePreview,sidePng);}finally{if(previous!=null)SelectLod(previous);await Display();}
 }
 static async Task SetPreviewImage(Image image,byte[] png){using var stream=new InMemoryRandomAccessStream();using(var writer=new DataWriter(stream.GetOutputStreamAt(0))){writer.WriteBytes(png);await writer.StoreAsync();}var source=new BitmapImage();await source.SetSourceAsync(stream);image.Source=source;}
 async Task EditPreviewCamera(int index)
 {
  if(cameraEditing||busy)return;cameraEditing=true;string? previous=selectedLod;var before=CaptureSnapshot();
  try{SelectLod(lods.Keys.First());await Display();previewWorkspace.Visibility=Visibility.Collapsed;gizmoActions.Visibility=gizmoNumbers.Visibility=Visibility.Collapsed;UpdateControls();cameraAspect=index==0?1:2;resizeCamera?.Invoke();view.HorizontalAlignment=HorizontalAlignment.Left;view.VerticalAlignment=VerticalAlignment.Center;view.Margin=new Thickness(12);nightButton.Visibility=Visibility.Collapsed;
   var controls=new StackPanel{Spacing=6,Width=220};controls.Children.Add(new TextBlock{Text=UiLanguage.Text(index==0?"Основная камера":"Боковая камера"),FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});foreach(string text in new[]{"W / S — вперёд / назад","A / D — влево / вправо","Q / E — вниз / вверх","Z / X — крен камеры","R — сброс камеры","Мышь — поворот","Колесо — FOV"})controls.Children.Add(new TextBlock{Text=UiLanguage.Text(text)});
   controls.Children.Add(cameraFov);var apply=new Button{Content=UiLanguage.Text("Применить")};var cancel=new Button{Content=UiLanguage.Text("Отмена")};controls.Children.Add(apply);controls.Children.Add(cancel);var done=new TaskCompletionSource<bool>();apply.Click+=(_,_)=>done.TrySetResult(true);cancel.Click+=(_,_)=>done.TrySetResult(false);cameraPanel.Child=new ScrollViewer{Content=controls,HorizontalScrollMode=ScrollMode.Disabled,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};cameraPanel.Visibility=Visibility.Visible;
   await view.ExecuteScriptAsync("startPreviewCamera("+JsonSerializer.Serialize(new{width=256,height=index==0?256:128,fov=previewSettings.Fov,yaw=index==0?previewSettings.Yaw:previewSettings.SideYaw,pitch=index==0?previewSettings.Pitch:previewSettings.SidePitch,pose=index==0?previewSettings.MainCamera:previewSettings.SideCamera})+")");view.Focus(FocusState.Programmatic);
   if(await done.Task){string json=await view.ExecuteScriptAsync("previewCameraPose()");var pose=JsonSerializer.Deserialize<PreviewCamera>(json)??throw new InvalidDataException("Invalid camera");previewSettings=index==0?previewSettings with{MainCamera=pose}:previewSettings with{SideCamera=pose};dirty=true;CommitHistory(before);}
  }catch(Exception ex){SetStatus(ex.Message);}finally{await view.ExecuteScriptAsync("stopPreviewCamera()");cameraEditing=false;cameraPanel.Child=null;cameraPanel.Visibility=Visibility.Collapsed;view.Width=view.Height=double.NaN;view.HorizontalAlignment=HorizontalAlignment.Stretch;view.VerticalAlignment=VerticalAlignment.Stretch;view.Margin=new Thickness(0);nightButton.Visibility=Visibility.Visible;if(previous!=null)SelectLod(previous);await Display();UpdateControls();await ConfigurePreviews();}
 }
 void ReceivePreview(JsonElement message)
 {
  if(!message.TryGetProperty("preview",out var id)||id.GetString() is not string key||!pendingPreviews.TryGetValue(key,out var task))return;
  if(message.TryGetProperty("error",out var error))task.TrySetException(new InvalidDataException(error.GetString()));else if(message.TryGetProperty("png",out var png)&&png.GetString() is string value)task.TrySetResult(value);
 }
 async Task<byte[]> RenderPreviewPng(int height,float yaw,float pitch)
 {
  string id=Guid.NewGuid().ToString("N");var task=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);pendingPreviews[id]=task;
  try{
   await view.ExecuteScriptAsync("requestPreview("+JsonSerializer.Serialize(id)+","+JsonSerializer.Serialize(new{width=256,height,fov=previewSettings.Fov,yaw,pitch,pose=height==256?previewSettings.MainCamera:previewSettings.SideCamera})+")");string url=await task.Task.WaitAsync(TimeSpan.FromSeconds(30));const string prefix="data:image/png;base64,";if(!url.StartsWith(prefix,StringComparison.Ordinal)||url.Length>2*1024*1024)throw new InvalidDataException("Invalid preview image");return Convert.FromBase64String(url[prefix.Length..]);
  }finally{pendingPreviews.Remove(id);}
 }
 async Task<byte[]> RenderPreview(int height,float yaw,float pitch)=>await PreviewDds(await RenderPreviewPng(height,yaw,pitch),height);
 static async Task<byte[]> PreviewDds(byte[] png,int height){using var stream=new InMemoryRandomAccessStream();using(var writer=new DataWriter(stream.GetOutputStreamAt(0))){writer.WriteBytes(png);await writer.StoreAsync();}var decoder=await BitmapDecoder.CreateAsync(stream);if(decoder.PixelWidth!=256||decoder.PixelHeight!=height)throw new InvalidDataException("Unexpected preview size");var data=await decoder.GetPixelDataAsync(BitmapPixelFormat.Rgba8,BitmapAlphaMode.Straight,new BitmapTransform(),ExifOrientationMode.IgnoreExifOrientation,ColorManagementMode.DoNotColorManage);return DdsImage.Encode(new(256,height,data.DetachPixelData()));}
 async Task<(byte[] Main,byte[] Side)> RenderVariantPreviews(MaterialVariant variant)
 {
  StoreVariant();int previous=activeVariant;string? previousLod=selectedLod;bool cargo=showCargo;bool visible=gizmoVisible;try{ActivateVariant(variants.FindIndex(v=>v.Name==variant.Name));SelectLod(lods.Keys.First());showCargo=false;gizmoVisible=false;await Display();return(await RenderPreview(256,previewSettings.Yaw,previewSettings.Pitch),await RenderPreview(128,previewSettings.SideYaw,previewSettings.SidePitch));}
  finally{ActivateVariant(previous);if(previousLod!=null)SelectLod(previousLod);showCargo=cargo;gizmoVisible=visible;await Display();}
 }
}
