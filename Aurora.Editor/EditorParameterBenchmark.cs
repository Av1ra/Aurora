using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 Action<ContentDialog>? dialogConstructionProbe;
 // Explicit offline benchmark entry point; never opens or operates a user window.
 public object BenchmarkTransportParameters(string media)
 {
  if(ready||lods.Count!=0)throw new InvalidOperationException("Benchmark requires a fresh hidden Editor");
  media=Path.GetFullPath(media);var timer=System.Diagnostics.Stopwatch.StartNew();vehicleCatalog=VehicleCatalog.Load(media,UiLanguage.English);double catalogMs=timer.Elapsed.TotalMilliseconds;
  vehicleChoices=vehicleCatalog.Values;string key=Path.Combine(media,"vehicles","auoui-benchmark","model.nmf");selectedLod=key;
  lods[key]=ObjImporter.Parse("o body\nv 0 0 0\nv 2 0 0\nv 0 2 0\nf 1 2 3\n").Model;glassObjects[key]=[];
  vehicleConfiguration="$TYPE VEHICLETYPE_ROAD\n$NAME_STR Police\n$COUNTRY 39030\n$COST_RUB 4300\n$AVAILABLE 1959 1971\n$RESOURCE_TRANSPORT_TYPE RESOURCE_TRANSPORT_PASSANGER\n$RESOURCE_CAPACITY 4\n$MOVEMENT_SPEED 90\n$MOVEMENT_POWER_KW 70\n$MOVEMENT_EMPTY_WEIGHT 1.5\n$SKILL_POLICE 1\n$END\n";
  catalogLoaded=(MediaFolder()??throw new DirectoryNotFoundException("Game media not found"),UiLanguage.English);
  var samples=new List<object>();int controls=0;double measureMs=0;
  void Count(DependencyObject item){if(item is Control)controls++;IEnumerable<DependencyObject> children=item switch{Panel panel=>panel.Children.Cast<DependencyObject>(),Border b when b.Child!=null=>[b.Child],ContentControl c when c.Content is DependencyObject d=>[d],_=>[]};foreach(var child in children)Count(child);}
  dialogConstructionProbe=dialog=>{if(dialog.Content is not FrameworkElement panel)throw new InvalidDataException("Parameter form has no content");controls=0;Count(panel);var watch=System.Diagnostics.Stopwatch.StartNew();panel.Measure(new Windows.Foundation.Size(520,600));panel.Arrange(new Windows.Foundation.Rect(0,0,520,panel.DesiredSize.Height));measureMs=watch.Elapsed.TotalMilliseconds;};
  try{for(int i=0;i<3;i++){timer.Restart();ConfigureModel().GetAwaiter().GetResult();if(controls<20)throw new InvalidDataException("Parameter form was not constructed");samples.Add(new{TotalMilliseconds=timer.Elapsed.TotalMilliseconds,MeasureMilliseconds=measureMs,Controls=controls});}return new{CatalogMilliseconds=catalogMs,Objects=vehicleCatalog.Objects.Length,Samples=samples,WindowActivated=false};}
  finally{dialogConstructionProbe=null;lods.Clear();glassObjects.Clear();selectedLod=null;vehicleConfiguration=null;catalogLoaded=null;}
 }
}
