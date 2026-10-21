using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 async Task<(string Item,string Name,bool Related)?> TransportExportSettings()
 {
  var panel=new StackPanel{Spacing=8,Width=420};var item=new TextBox{PlaceholderText="1234567890"};var name=new TextBox{Text="vehicle"};var related=new CheckBox{Content=UiLanguage.Text("Включить связанные объекты"),IsChecked=true};SettingRow(panel,"Workshop ID",item);SettingRow(panel,"Имя папки машины",name);panel.Children.Add(related);var error=new InfoBar{Severity=InfoBarSeverity.Error,IsClosable=false};panel.Children.Add(error);var dialog=VehicleDialog("Комплект транспорта",panel);dialog.PrimaryButtonClick+=(_,e)=>{if(!ulong.TryParse(item.Text.Trim(),out ulong id)||id==0||!System.Text.RegularExpressions.Regex.IsMatch(name.Text.Trim(),@"^[A-Za-z0-9_-]{1,63}$")){e.Cancel=true;error.Message=UiLanguage.Text("Укажите Workshop ID и имя объекта латиницей");error.IsOpen=true;}};
  return await ShowDialog(dialog)==ContentDialogResult.Primary?(item.Text.Trim(),name.Text.Trim(),related.IsChecked==true):null;
 }
 async Task<string> ExportTransport(string root,string item,string name,bool related)
 {
  await LoadVehicleChoices();string source=Path.GetDirectoryName(lods.Keys.First())!;string? nativeId=vehicleCatalog!.Objects.FirstOrDefault(o=>Path.GetDirectoryName(o.Script)!.Equals(source,StringComparison.OrdinalIgnoreCase))?.Id;var objects=vehicleCatalog.Objects.ToDictionary(o=>o.Id,o=>o.Script);return await Task.Run(()=>TransportPackage.Write(root,nativeId,item,name,objects,Path.GetDirectoryName(root)!,related));
 }
}
