using System.Text.Json;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 long displayCalls,displaySerializedBytes,selectionUpdates,imageCacheHits,imageCacheMisses;
 double displayMilliseconds;
 long openingStarted;
 readonly Dictionary<string,PanelOpeningMetric> panelOpenings=[];
 sealed record PanelOpeningMetric(int Count,double LastMilliseconds,double MaxMilliseconds);
 void RecordPanelOpening(string title,long started){if(started==0)return;double ms=System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;var old=panelOpenings.GetValueOrDefault(title);panelOpenings[title]=new((old?.Count??0)+1,ms,Math.Max(old?.MaxMilliseconds??0,ms));}
 async Task ExportViewportDiagnostics()
 {
  var picker=new Windows.Storage.Pickers.FileSavePicker{SuggestedFileName="Aurora-diagnostics"};picker.FileTypeChoices.Add("JSON",new[]{".json"});WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));openingStarted=0;var file=await picker.PickSaveFileAsync();if(file==null)return;
  string gpu=ready?await view.ExecuteScriptAsync("JSON.stringify(viewportMetrics)"):"null";
  string? decoded=JsonSerializer.Deserialize<string>(gpu);
  using var json=JsonDocument.Parse(decoded??"null");
  await File.WriteAllTextAsync(file.Path,JsonSerializer.Serialize(new{Schema=2,DisplayCalls=displayCalls,SerializedSceneBytes=displaySerializedBytes,DisplayMilliseconds=displayMilliseconds,SelectionUpdates=selectionUpdates,ImageCacheHits=imageCacheHits,ImageCacheMisses=imageCacheMisses,Catalog=catalogMetrics==null?null:new{catalogMetrics.CacheHit,catalogMetrics.SourceFiles,catalogMetrics.Milliseconds,catalogMetrics.ValidationMilliseconds,catalogMetrics.Persisted},CatalogError=catalogPreparationError,PanelOpenings=panelOpenings,Viewport=json.RootElement.Clone()},new JsonSerializerOptions{WriteIndented=true}));
 }
}
