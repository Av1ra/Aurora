using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 Task<CatalogLoadResult>? catalogPreparation;
 (string Media,bool English)? catalogRequest,catalogLoaded;
 CatalogLoadResult? catalogMetrics;string? catalogPreparationError;
 readonly TextBlock catalogPreparationText=new(){FontSize=11,Opacity=.7,Visibility=Visibility.Collapsed,VerticalAlignment=VerticalAlignment.Center};
 async Task LoadVehicleChoices(bool refresh=false)
 {
  string media=MediaFolder()??throw new DirectoryNotFoundException(UiLanguage.Text("Не найдена папка игры"));var key=(media,UiLanguage.English);
  if(!refresh&&vehicleCatalog!=null&&catalogLoaded==key)return;
  if(refresh||catalogPreparation==null||catalogPreparation.IsFaulted||catalogRequest!=key){
   catalogRequest=key;string cache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aurora","cache","catalog");
   catalogPreparation=Task.Run(()=>VehicleCatalogCache.Load(media,key.English,cache,refresh));
  }
  var task=catalogPreparation;var result=await task;
  if(!ReferenceEquals(task,catalogPreparation)||catalogRequest!=key||UiLanguage.English!=key.English){await LoadVehicleChoices();return;}
  vehicleCatalog=result.Catalog;vehicleChoices=vehicleCatalog.Values;catalogLoaded=key;catalogMetrics=result;catalogPreparationError=null;
 }
 async Task PrepareEditorData()
 {
  if(MediaFolder()==null)return;
  catalogPreparationText.Text=UiLanguage.Text("Подготовка справочников…");catalogPreparationText.Visibility=Visibility.Visible;
  try{await LoadVehicleChoices();}catch(Exception ex){catalogPreparationError=ex.GetType().Name;}
  finally{catalogPreparationText.Visibility=Visibility.Collapsed;}
 }
 void StartEditorPreparation(){_=PrepareEditorData();}
}
