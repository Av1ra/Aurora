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
 async Task ConfigureCargo()
 {
  string original=VehicleTemplate();int count=VehicleScript.Fields(original).Count(f=>f.Key=="RESOURCE_VISUALIZATION");
  var choose=new ComboBox{Header=UiLanguage.Text("Грузовая область"),ItemsSource=Enumerable.Range(0,count).Select(i=>(i+1).ToString()).Append(UiLanguage.Text("Новая область")).ToArray(),SelectedIndex=0,MinWidth=280};
  var select=VehicleDialog("Настроить груз…",choose);if(await ShowDialog(select)!=ContentDialogResult.Primary)return;int occurrence=choose.SelectedIndex;
  var layout=VehicleScript.Cargo(original,occurrence)??new(Vector3.Zero,new(0,1,0),new(.1f),new(0,1,0),Vector3.One);
  var panel=new StackPanel{Spacing=10,Width=430};
  NumberBox[] VectorRow(string label,Vector3 vector){var row=new StackPanel{Spacing=4};row.Children.Add(new TextBlock{Text=UiLanguage.Text(label)});var line=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6};row.Children.Add(line);var values=new[]{vector.X,vector.Y,vector.Z};var boxes=values.Select((v,i)=>new NumberBox{Header="XYZ"[i].ToString(),Value=v,Width=130,SmallChange=.01,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact}).ToArray();foreach(var box in boxes)line.Children.Add(box);panel.Children.Add(row);return boxes;}
  var rotation=VectorRow("Поворот, радианы",layout.Rotation);var emptyPosition=VectorRow("Пустой кузов: положение",layout.EmptyPosition);var emptyScale=VectorRow("Пустой кузов: масштаб",layout.EmptyScale);var fullPosition=VectorRow("Полный кузов: положение",layout.FullPosition);var fullScale=VectorRow("Полный кузов: масштаб",layout.FullScale);
  string? media=MediaFolder();var resource=new ComboBox{Header=UiLanguage.Text("Модель груза"),HorizontalAlignment=HorizontalAlignment.Stretch};var paths=media==null?new List<string>():Directory.GetFiles(Path.Combine(media,"resources"),"*.nmf").Order().ToList();if(cargoModelPath!=null&&!paths.Contains(cargoModelPath))paths.Add(cargoModelPath);foreach(string path in paths)resource.Items.Add(Path.GetFileNameWithoutExtension(path));resource.SelectedIndex=cargoModelPath==null?paths.FindIndex(p=>Path.GetFileName(p)=="boards.nmf"):paths.IndexOf(cargoModelPath);panel.Children.Add(resource);
  var file=new Button{Content=UiLanguage.Text("Выбрать модель груза…")};file.Click+=async(_,_)=>{string? path=await Pick(".nmf");if(path!=null){if(!paths.Contains(path)){paths.Add(path);resource.Items.Add(Path.GetFileNameWithoutExtension(path));}resource.SelectedIndex=paths.IndexOf(path);}};panel.Children.Add(file);
  var enabled=new CheckBox{Content=UiLanguage.Text("Показывать груз"),IsChecked=true};var remove=new CheckBox{Content=UiLanguage.Text("Удалить область")};var fill=new Slider{Header=UiLanguage.Text("Заполнение, %"),Minimum=0,Maximum=100,Value=cargoFill*100,StepFrequency=1};panel.Children.Add(enabled);panel.Children.Add(fill);if(occurrence<count)panel.Children.Add(remove);
  string? updated=null;Vector3 V(NumberBox[] b){var v=new Vector3((float)b[0].Value,(float)b[1].Value,(float)b[2].Value);if(!float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z))throw new InvalidDataException("Invalid cargo vector");return v;}
  var dialog=VehicleDialog("Настроить груз…",panel);ValidateVehicleDialog(dialog,panel,()=>updated=VehicleScript.SetCargo(original,remove.IsChecked==true?null:new(V(rotation),V(emptyPosition),V(emptyScale),V(fullPosition),V(fullScale)),occurrence));
  if(await ShowDialog(dialog)!=ContentDialogResult.Primary||updated==null)return;
  string? modelFile=resource.SelectedIndex>=0?paths[resource.SelectedIndex]:null;NativeModel? model=modelFile==null?null:await Task.Run(()=>new NativeModel(File.ReadAllBytes(modelFile)));string? material=modelFile==null?null:Path.ChangeExtension(modelFile,"mtl");if(material!=null&&!File.Exists(material))throw new FileNotFoundException("Cargo MTL missing",material);
  vehicleConfiguration=updated;vehicleConfigurationEdited=true;cargoModel=model;cargoModelPath=modelFile;cargoMtl=material;showCargo=enabled.IsChecked==true&&model!=null;cargoFill=(float)(fill.Value/100);dirty=true;await Display();
 }
 object BuildViewportScene(NativeModel model)
 {
  var materials=model.Materials.ToList();var entries=new List<object>();
  foreach(var n in model.Nodes)entries.Add(new{n.Name,n.Positions,n.Normals,n.Uv,Indices=n.Indices.Select(i=>(int)i).ToArray(),n.Groups,Cargo=false});
  if(showCargo&&cargoModel!=null&&vehicleConfiguration!=null){
   int first=materials.Count;materials.AddRange(cargoModel.Materials.Select(m=>"__cargo__"+m));int count=VehicleScript.Fields(vehicleConfiguration).Count(f=>f.Key=="RESOURCE_VISUALIZATION");
   for(int slot=0;slot<count;slot++){
    var cargo=VehicleScript.Cargo(vehicleConfiguration,slot)!;var scale=Vector3.Lerp(cargo.EmptyScale,cargo.FullScale,cargoFill);if(MathF.Min(scale.X,MathF.Min(scale.Y,scale.Z))<=1e-6f)continue;
    var rotation=cargo.Rotation;var transform=Matrix4x4.CreateScale(scale)*Matrix4x4.CreateRotationX(rotation.X)*Matrix4x4.CreateRotationY(rotation.Y)*Matrix4x4.CreateRotationZ(rotation.Z)*Matrix4x4.CreateTranslation(Vector3.Lerp(cargo.EmptyPosition,cargo.FullPosition,cargoFill));if(!Matrix4x4.Invert(transform,out var inverse))continue;var normalTransform=Matrix4x4.Transpose(inverse);
    foreach(var n in cargoModel.Nodes){var positions=new float[n.Positions.Length];var normals=new float[n.Normals.Length];for(int i=0;i<n.VertexCount;i++){var p=Vector3.Transform(new(n.Positions[i*3],n.Positions[i*3+1],n.Positions[i*3+2]),transform);var normal=Vector3.Normalize(Vector3.TransformNormal(new(n.Normals[i*3],n.Normals[i*3+1],n.Normals[i*3+2]),normalTransform));positions[i*3]=p.X;positions[i*3+1]=p.Y;positions[i*3+2]=p.Z;normals[i*3]=normal.X;normals[i*3+1]=normal.Y;normals[i*3+2]=normal.Z;}entries.Add(new{Name="Cargo_"+slot+"_"+n.Name,Positions=positions,Normals=normals,n.Uv,Indices=n.Indices.Select(i=>(int)i).ToArray(),Groups=n.Groups.Select(g=>g with{Material=g.Material+first}).ToArray(),Cargo=true});}
   }
  }return new{Materials=materials,Nodes=entries};
 }
 async Task LoadCargoTextures(){if(!showCargo||cargoMtl==null)return;foreach(var p in MaterialDocuments.Native(cargoMtl))await ApplyPreview("__cargo__"+p.Key,p.Value);}
}
