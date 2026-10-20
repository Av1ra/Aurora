using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Numerics;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 NativeModel? cargoModel;string? cargoModelPath,cargoMtl;
 bool showCargo;float cargoFill=1;
 int activeCargo=-1,cargoEndpoint=1;
 bool cargoUniform=true;
 CargoLayout? ActiveCargoLayout()=>activeCargo<0||vehicleConfiguration==null?null:VehicleScript.Cargo(vehicleConfiguration,activeCargo);
 Vector3 CargoPosition(){var c=ActiveCargoLayout()!;return cargoEndpoint==0?c.EmptyPosition:c.FullPosition;}
 Vector3 CargoScale(){var c=ActiveCargoLayout()!;return cargoEndpoint==0?c.EmptyScale:c.FullScale;}
 Vector3 CargoRotation()=>ActiveCargoLayout()!.Rotation*(180/MathF.PI);
 void SetCargoTool(Vector3 position,Vector3 rotation,Vector3 scale)
 {
  var c=ActiveCargoLayout()!;c=c with{Rotation=rotation*(MathF.PI/180)};c=cargoEndpoint==0?c with{EmptyPosition=position,EmptyScale=scale}:c with{FullPosition=position,FullScale=scale};vehicleConfiguration=VehicleScript.SetCargo(vehicleConfiguration!,c,activeCargo);vehicleConfigurationEdited=true;
 }
 async Task ConfigureCargo()
 {
  string original=VehicleTemplate();var fields=VehicleScript.Fields(original);string transport=fields.FirstOrDefault(f=>f.Key=="RESOURCE_TRANSPORT_TYPE")?.Value??"";string? only=fields.FirstOrDefault(f=>f.Key=="RESOURCE_ALLOW_ONLY")?.Value;var resources=VehicleCatalog.CargoResources(transport,only,UiLanguage.English);if(resources.Length==0){SetStatus("Для этого класса нет видимого груза");return;}
  string media=MediaFolder()??throw new DirectoryNotFoundException(UiLanguage.Text("Не найдена папка игры"));var choices=resources.Where(r=>File.Exists(Path.Combine(media,"resources",r.File))&&File.Exists(Path.Combine(media,"resources",Path.ChangeExtension(r.File,"mtl")))).ToArray();if(choices.Length==0)throw new InvalidDataException(UiLanguage.Text("Не найдены модели груза"));
  int count=fields.Count(f=>f.Key=="RESOURCE_VISUALIZATION");var panel=new StackPanel{Spacing=10,Width=340};var area=new ComboBox{Header=UiLanguage.Text("Грузовая область"),ItemsSource=Enumerable.Range(0,count).Select(i=>(i+1).ToString()).Append(UiLanguage.Text("Новая область")).ToArray(),SelectedIndex=activeCargo>=0&&activeCargo<count?activeCargo:0,HorizontalAlignment=HorizontalAlignment.Stretch};panel.Children.Add(area);
  var resource=new ComboBox{Header=UiLanguage.Text("Груз"),ItemsSource=choices.Select(r=>r.Name).ToArray(),SelectedIndex=Math.Max(0,Array.FindIndex(choices,r=>r.File==Path.GetFileName(cargoModelPath))),HorizontalAlignment=HorizontalAlignment.Stretch};panel.Children.Add(resource);
  var endpoint=new ComboBox{Header=UiLanguage.Text("Заполнение кузова"),ItemsSource=new[]{UiLanguage.Text("Пустой кузов"),UiLanguage.Text("Полный кузов")},SelectedIndex=cargoEndpoint,HorizontalAlignment=HorizontalAlignment.Stretch};panel.Children.Add(endpoint);var remove=new CheckBox{Content=UiLanguage.Text("Удалить область")};panel.Children.Add(remove);area.SelectionChanged+=(_,_)=>remove.IsEnabled=area.SelectedIndex<count;remove.IsEnabled=area.SelectedIndex<count;
  var dialog=VehicleDialog("Настроить груз…",panel);if(await ShowDialog(dialog)!=ContentDialogResult.Primary)return;int slot=area.SelectedIndex;if(slot<0||resource.SelectedIndex<0)return;
  if(remove.IsChecked==true&&slot<count){vehicleConfiguration=VehicleScript.SetCargo(original,null,slot);vehicleConfigurationEdited=true;activeCargo=-1;gizmoVisible=false;dirty=true;await Display();return;}
  string path=Path.Combine(media,"resources",choices[resource.SelectedIndex].File);var model=await Task.Run(()=>new NativeModel(File.ReadAllBytes(path)));var layout=VehicleScript.Cargo(original,slot);if(layout==null){var bounds=ModelOrientation.Bounds(lods[selectedLod!]);float height=Math.Max(.1f,bounds.Max.Y-bounds.Min.Y);layout=new(Vector3.Zero,new((bounds.Min.X+bounds.Max.X)/2,bounds.Min.Y+height*.5f,(bounds.Min.Z+bounds.Max.Z)/2),new(.1f),new((bounds.Min.X+bounds.Max.X)/2,bounds.Min.Y+height*.5f,(bounds.Min.Z+bounds.Max.Z)/2),Vector3.One);}
  vehicleConfiguration=VehicleScript.SetCargo(original,layout,slot);vehicleConfigurationEdited=true;cargoModel=model;cargoModelPath=path;cargoMtl=Path.ChangeExtension(path,"mtl");showCargo=true;activeCargo=slot;activeLamp=-1;cargoEndpoint=endpoint.SelectedIndex;cargoFill=cargoEndpoint;gizmoVisible=gizmoEditing=true;gizmoMode="move";dirty=true;await Display();
 }
 object BuildViewportScene(NativeModel model)
 {
  var materials=model.Materials.ToList();var entries=new List<object>();
  foreach(var n in model.Nodes)entries.Add(new{n.Name,n.Positions,n.Normals,n.Uv,Indices=n.Indices.Select(i=>(int)i).ToArray(),n.Groups,Cargo=false});
  if(showCargo&&cargoModel!=null&&vehicleConfiguration!=null){
   int first=materials.Count;materials.AddRange(cargoModel.Materials.Select(m=>"__cargo__"+m));int count=VehicleScript.Fields(vehicleConfiguration).Count(f=>f.Key=="RESOURCE_VISUALIZATION");
   for(int slot=0;slot<count;slot++){
    var cargo=VehicleScript.Cargo(vehicleConfiguration,slot)!;var scale=Vector3.Lerp(cargo.EmptyScale,cargo.FullScale,cargoFill);if(MathF.Min(scale.X,MathF.Min(scale.Y,scale.Z))<=1e-6f&&slot!=activeCargo)continue;
    var rotation=cargo.Rotation;var transform=Matrix4x4.CreateScale(scale)*Matrix4x4.CreateRotationX(rotation.X)*Matrix4x4.CreateRotationY(rotation.Y)*Matrix4x4.CreateRotationZ(rotation.Z)*Matrix4x4.CreateTranslation(Vector3.Lerp(cargo.EmptyPosition,cargo.FullPosition,cargoFill));if(!Matrix4x4.Invert(transform,out var inverse))inverse=Matrix4x4.Identity;var normalTransform=Matrix4x4.Transpose(inverse);
    foreach(var n in cargoModel.Nodes){var positions=new float[n.Positions.Length];var normals=new float[n.Normals.Length];for(int i=0;i<n.VertexCount;i++){var p=Vector3.Transform(new(n.Positions[i*3],n.Positions[i*3+1],n.Positions[i*3+2]),transform);var normal=Vector3.Normalize(Vector3.TransformNormal(new(n.Normals[i*3],n.Normals[i*3+1],n.Normals[i*3+2]),normalTransform));positions[i*3]=p.X;positions[i*3+1]=p.Y;positions[i*3+2]=p.Z;normals[i*3]=normal.X;normals[i*3+1]=normal.Y;normals[i*3+2]=normal.Z;}entries.Add(new{Name="Cargo_"+slot+"_"+n.Name,Positions=positions,Normals=normals,n.Uv,Indices=n.Indices.Select(i=>(int)i).ToArray(),Groups=n.Groups.Select(g=>g with{Material=g.Material+first}).ToArray(),Cargo=true,CargoSlot=slot,LocalPositions=n.Positions,LocalNormals=n.Normals});}
   }
  }return new{Materials=materials,Nodes=entries};
 }
 async Task LoadCargoTextures(){if(!showCargo||cargoMtl==null)return;foreach(var p in MaterialDocuments.Native(cargoMtl))await ApplyPreview("__cargo__"+p.Key,p.Value);}
}
