using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 async Task<(MaterialVariant[] Variants,string[] Targets)?> SkinTargets(MaterialVariant[] chosen)
 {
  await LoadVehicleChoices();var panel=new StackPanel{Spacing=8,Width=490};var rows=new StackPanel{Spacing=8};panel.Children.Add(rows);var selections=new List<(ComboBox Material,ComboBox Object)>();var objects=vehicleCatalog!.Objects;string source=Path.GetDirectoryName(lods.Keys.First())!;string? initial=objects.FirstOrDefault(o=>Path.GetDirectoryName(o.Script)!.Equals(source,StringComparison.OrdinalIgnoreCase))?.Id;
  void Add(int material=0){var row=new Grid{ColumnSpacing=6};row.ColumnDefinitions.Add(new(){Width=new GridLength(70)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});var m=new ComboBox{ItemsSource=chosen.Select((v,i)=>(i+1).ToString()).ToArray(),SelectedIndex=material,HorizontalAlignment=HorizontalAlignment.Stretch};var target=new ComboBox{ItemsSource=objects.Select(o=>o.Name+" ["+o.Id+"]").ToArray(),IsEditable=true,HorizontalAlignment=HorizontalAlignment.Stretch,MinWidth=0,MaxDropDownHeight=240};if(initial!=null)target.SelectedIndex=Array.FindIndex(objects,o=>o.Id==initial);var remove=new Button{Content="×"};row.Children.Add(m);Grid.SetColumn(target,1);row.Children.Add(target);Grid.SetColumn(remove,2);row.Children.Add(remove);rows.Children.Add(row);selections.Add((m,target));remove.Click+=(_,_)=>{selections.Remove((m,target));rows.Children.Remove(row);};}
  for(int i=0;i<chosen.Length;i++)Add(i);var add=new Button{Content="+"};add.Click+=(_,_)=>Add();panel.Children.Add(add);var error=new InfoBar{Severity=InfoBarSeverity.Error,IsClosable=false};panel.Children.Add(error);string[] targets=[];MaterialVariant[] selected=[];
  var dialog=VehicleDialog("Цели окраски",panel);dialog.PrimaryButtonClick+=(_,e)=>{try{if(selections.Count==0)throw new InvalidDataException(UiLanguage.Text("Выберите материал."));targets=selections.Select(r=>r.Object.SelectedIndex>=0?objects[r.Object.SelectedIndex].Id:r.Object.Text.Trim()).ToArray();if(targets.Any(t=>!System.Text.RegularExpressions.Regex.IsMatch(t,@"^(?:[0-9]+/)?[A-Za-z0-9_.-]+$")))throw new InvalidDataException(UiLanguage.Text("Укажите объект игры или Workshop ID/объект"));selected=selections.Select(r=>chosen[r.Material.SelectedIndex]).ToArray();for(int i=0;i<targets.Length;i++){var target=objects.FirstOrDefault(o=>o.Id==targets[i]);if(target==null)continue;string file=Path.Combine(Path.GetDirectoryName(target.Script)!,"main.nmf");var targetModel=AssetModel(file);var names=MaterialDocuments.Native(selected[i].NativePath);if(targetModel.Materials.Any(n=>!names.ContainsKey(n)))throw new InvalidDataException(UiLanguage.Text("Материалы не совпадают с целевой моделью")+": "+targets[i]);}}catch(Exception ex){e.Cancel=true;error.Message=ex.Message;error.IsOpen=true;}};
  return await ShowDialog(dialog)==ContentDialogResult.Primary?(selected,targets):null;
 }
}
