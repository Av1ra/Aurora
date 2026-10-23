using System.Text.Json;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 long displayCalls,displaySerializedBytes,selectionUpdates,imageCacheHits,imageCacheMisses;
 double displayMilliseconds;
 async Task ExportViewportDiagnostics()
 {
  var picker=new Windows.Storage.Pickers.FileSavePicker{SuggestedFileName="Aurora-diagnostics"};picker.FileTypeChoices.Add("JSON",new[]{".json"});WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));var file=await picker.PickSaveFileAsync();if(file==null)return;
  string gpu=ready?await view.ExecuteScriptAsync("JSON.stringify(viewportMetrics)"):"null";
  string? decoded=JsonSerializer.Deserialize<string>(gpu);
  using var json=JsonDocument.Parse(decoded??"null");
  await File.WriteAllTextAsync(file.Path,JsonSerializer.Serialize(new{Schema=1,DisplayCalls=displayCalls,SerializedSceneBytes=displaySerializedBytes,DisplayMilliseconds=displayMilliseconds,SelectionUpdates=selectionUpdates,ImageCacheHits=imageCacheHits,ImageCacheMisses=imageCacheMisses,Viewport=json.RootElement.Clone()},new JsonSerializerOptions{WriteIndented=true}));
 }
}
