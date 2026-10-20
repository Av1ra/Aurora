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
 readonly StackPanel cargoPalette=new(){Spacing=5,Width=180,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(12),Visibility=Visibility.Collapsed};
 string? cargoPaletteKey;
 CargoResource[] CargoChoices(){if(vehicleConfiguration==null)return [];var f=VehicleScript.Fields(vehicleConfiguration);string transport=f.FirstOrDefault(x=>x.Key=="RESOURCE_TRANSPORT_TYPE")?.Value??"";string? only=f.FirstOrDefault(x=>x.Key=="RESOURCE_ALLOW_ONLY")?.Value;string? media=MediaFolder();return media==null?[]:VehicleCatalog.CargoResources(transport,only,UiLanguage.English).Where(r=>File.Exists(Path.Combine(media,"resources",r.File))&&MaterialDocuments.ResourceMaterial(Path.Combine(media,"resources",r.File))!=null).ToArray();}
 async Task LoadCargoResource(CargoResource resource){string path=Path.Combine(MediaFolder()!,"resources",resource.File);var model=await Task.Run(()=>new NativeModel(File.ReadAllBytes(path)));string material=MaterialDocuments.ResourceMaterial(path)??throw new FileNotFoundException("Cargo material missing");_=MaterialDocuments.ResourceMaps(model,material);cargoModel=model;cargoModelPath=path;cargoMtl=material;showCargo=true;}
 async Task ConfigureCargo(){var choices=CargoChoices();if(choices.Length==0)throw new InvalidDataException(UiLanguage.Text("Для этого класса нет видимого груза"));int count=VehicleScript.Fields(vehicleConfiguration!).Count(f=>f.Key=="RESOURCE_VISUALIZATION");await OpenCargoSlot(activeCargo>=0&&activeCargo<count?activeCargo:0);}
 async Task OpenCargoSlot(int slot)
 {
  var choices=CargoChoices();if(choices.Length==0)return;var selected=choices.FirstOrDefault(c=>c.File==Path.GetFileName(cargoModelPath))??choices[0];await LoadCargoResource(selected);var layout=VehicleScript.Cargo(vehicleConfiguration!,slot);
  if(layout==null){var vehicle=ModelOrientation.Bounds(lods.First().Value);var cargo=ModelOrientation.Bounds(cargoModel!);var size=vehicle.Max-vehicle.Min;var cargoSize=cargo.Max-cargo.Min;var scale=new Vector3(Math.Max(.01f,size.X*.8f/Math.Max(.01f,cargoSize.X)),Math.Max(.01f,size.Y*.3f/Math.Max(.01f,cargoSize.Y)),Math.Max(.01f,size.Z*.4f/Math.Max(.01f,cargoSize.Z)));var p=new Vector3((vehicle.Min.X+vehicle.Max.X)/2,vehicle.Min.Y+size.Y*.5f,(vehicle.Min.Z+vehicle.Max.Z)/2);layout=new(Vector3.Zero,p,scale*.1f,p,scale);vehicleConfiguration=VehicleScript.SetCargo(vehicleConfiguration!,layout,slot);vehicleConfigurationEdited=true;}
  activeCargo=slot;activeSmoke=-1;activeLamp=-1;cargoEndpoint=1;cargoFill=1;gizmoVisible=gizmoEditing=true;gizmoMode="move";dirty=true;await Display();SetStatus("Настройка груза");
 }
 void RefreshCargoPalette()
 {
  cargoPalette.Visibility=gizmoVisible&&activeCargo>=0?Visibility.Visible:Visibility.Collapsed;if(cargoPalette.Visibility!=Visibility.Visible)return;var choices=CargoChoices();int count=VehicleScript.Fields(vehicleConfiguration!).Count(f=>f.Key=="RESOURCE_VISUALIZATION");string key=activeCargo+"|"+count+"|"+cargoModelPath+"|"+UiLanguage.English+"|"+string.Join(";",choices.Select(c=>c.File));if(cargoPaletteKey==key)return;cargoPaletteKey=key;cargoPalette.Children.Clear();
  var area=new ComboBox{ItemsSource=Enumerable.Range(0,count).Select(i=>UiLanguage.Text("Область")+" "+(i+1)).ToArray(),SelectedIndex=activeCargo,HorizontalAlignment=HorizontalAlignment.Stretch};area.SelectionChanged+=async(_,_)=>{if(!busy&&area.SelectedIndex>=0&&area.SelectedIndex!=activeCargo)await RunCommand(()=>OpenCargoSlot(area.SelectedIndex));};cargoPalette.Children.Add(area);
  var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=5};var add=new Button{Content="+"};add.Click+=async(_,_)=>await RunCommand(()=>OpenCargoSlot(count));var remove=new Button{Content=UiLanguage.Text("Удалить")};remove.Click+=async(_,_)=>await RunCommand(async()=>{vehicleConfiguration=VehicleScript.SetCargo(vehicleConfiguration!,null,activeCargo);vehicleConfigurationEdited=true;activeCargo=count>1?Math.Min(activeCargo,count-2):-1;if(activeCargo<0){showCargo=false;gizmoVisible=false;}dirty=true;await Display();});actions.Children.Add(add);actions.Children.Add(remove);cargoPalette.Children.Add(actions);
  var buttons=new StackPanel{Spacing=4};foreach(var choice in choices){var button=new Button{Content=choice.Name,HorizontalAlignment=HorizontalAlignment.Stretch,IsEnabled=choice.File!=Path.GetFileName(cargoModelPath)};button.Click+=async(_,_)=>await RunCommand(async()=>{await LoadCargoResource(choice);dirty=true;await Display();});buttons.Children.Add(button);}cargoPalette.Children.Add(new ScrollViewer{Content=buttons,MaxHeight=250,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
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
 async Task LoadCargoTextures(){if(!showCargo||cargoMtl==null)return;foreach(var p in MaterialDocuments.ResourceMaps(cargoModel!,cargoMtl))await ApplyPreview("__cargo__"+p.Key,p.Value);}
}
