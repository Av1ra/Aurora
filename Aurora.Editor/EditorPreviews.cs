using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
namespace Aurora.Editor;
sealed record PreviewSettings(bool Automatic=true,float Fov=20,float Yaw=-45,float Pitch=30,float SideYaw=-90,float SidePitch=0);
sealed partial class EditorWindow
{
 PreviewSettings previewSettings=new();
 readonly Dictionary<string,TaskCompletionSource<string>> pendingPreviews=[];
 async Task ConfigurePreviews()
 {
  var panel=new StackPanel{Spacing=10,Width=340};var automatic=new CheckBox{Content=UiLanguage.Text("Создавать превью при сохранении NMF"),IsChecked=previewSettings.Automatic};panel.Children.Add(automatic);
  NumberBox Field(string label,double value,double min,double max){var box=new NumberBox{Header=UiLanguage.Text(label),Value=value,Minimum=min,Maximum=max,SmallChange=1,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Hidden};panel.Children.Add(box);return box;}
  var fov=Field("FOV, °",previewSettings.Fov,5,100);var yaw=Field("Основной ракурс: поворот",previewSettings.Yaw,-360,360);var pitch=Field("Основной ракурс: наклон",previewSettings.Pitch,-89,89);var sideYaw=Field("Боковой ракурс: поворот",previewSettings.SideYaw,-360,360);var sidePitch=Field("Боковой ракурс: наклон",previewSettings.SidePitch,-89,89);
  var dialog=VehicleDialog("Настройки превью…",panel);ValidateVehicleDialog(dialog,panel,()=>{if(new[]{fov,yaw,pitch,sideYaw,sidePitch}.Any(b=>!double.IsFinite(b.Value)))throw new InvalidDataException("Invalid preview settings");});if(await ShowDialog(dialog)!=ContentDialogResult.Primary)return;previewSettings=new(automatic.IsChecked==true,(float)fov.Value,(float)yaw.Value,(float)pitch.Value,(float)sideYaw.Value,(float)sidePitch.Value);dirty=true;
 }
 void ReceivePreview(JsonElement message)
 {
  if(!message.TryGetProperty("preview",out var id)||id.GetString() is not string key||!pendingPreviews.TryGetValue(key,out var task))return;
  if(message.TryGetProperty("error",out var error))task.TrySetException(new InvalidDataException(error.GetString()));else if(message.TryGetProperty("png",out var png)&&png.GetString() is string value)task.TrySetResult(value);
 }
 async Task<byte[]> RenderPreview(int height,float yaw,float pitch)
 {
  string id=Guid.NewGuid().ToString("N");var task=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);pendingPreviews[id]=task;
  try{
   await view.ExecuteScriptAsync("requestPreview("+JsonSerializer.Serialize(id)+","+JsonSerializer.Serialize(new{width=256,height,fov=previewSettings.Fov,yaw,pitch})+")");string url=await task.Task.WaitAsync(TimeSpan.FromSeconds(30));const string prefix="data:image/png;base64,";if(!url.StartsWith(prefix,StringComparison.Ordinal)||url.Length>2*1024*1024)throw new InvalidDataException("Invalid preview image");byte[] png=Convert.FromBase64String(url[prefix.Length..]);
   using var stream=new InMemoryRandomAccessStream();using(var writer=new DataWriter(stream.GetOutputStreamAt(0))){writer.WriteBytes(png);await writer.StoreAsync();}var decoder=await BitmapDecoder.CreateAsync(stream);if(decoder.PixelWidth!=256||decoder.PixelHeight!=height)throw new InvalidDataException("Unexpected preview size");var data=await decoder.GetPixelDataAsync(BitmapPixelFormat.Rgba8,BitmapAlphaMode.Straight,new BitmapTransform(),ExifOrientationMode.IgnoreExifOrientation,ColorManagementMode.DoNotColorManage);return DdsImage.Encode(new(256,height,data.DetachPixelData()));
  }finally{pendingPreviews.Remove(id);}
 }
 async Task<(byte[] Main,byte[] Side)> RenderVariantPreviews(MaterialVariant variant)
 {
  StoreVariant();int previous=activeVariant;string? previousLod=selectedLod;bool cargo=showCargo;bool visible=gizmoVisible;try{ActivateVariant(variants.FindIndex(v=>v.Name==variant.Name));SelectLod(lods.Keys.First());showCargo=false;gizmoVisible=false;await Display();return(await RenderPreview(256,previewSettings.Yaw,previewSettings.Pitch),await RenderPreview(128,previewSettings.SideYaw,previewSettings.SidePitch));}
  finally{ActivateVariant(previous);if(previousLod!=null)SelectLod(previousLod);showCargo=cargo;gizmoVisible=visible;await Display();}
 }
}
