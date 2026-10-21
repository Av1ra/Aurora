using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml.Controls;
namespace Aurora.Editor;

sealed partial class EditorWindow
{
 string? importSession;
 readonly HashSet<string> importedFiles=[];
 string SessionFile(string name){importSession??=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aurora","Editor","Sessions",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(importSession);string file=Path.Combine(importSession,name);importedFiles.Add(file);return file;}
 void CleanImportSession(){foreach(string file in importedFiles)try{File.Delete(file);}catch{}if(importSession==null)return;var folders=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(string file in importedFiles)for(string? folder=Path.GetDirectoryName(file);folder!=null&&folder.StartsWith(importSession+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);folder=Path.GetDirectoryName(folder))folders.Add(folder);foreach(string folder in folders.OrderByDescending(f=>f.Length))try{Directory.Delete(folder);}catch{}try{Directory.Delete(importSession);}catch{}}
 void RegisterImportCommands()
 {
  editorCommands["Импортировать OBJ…"]=(async()=>{var path=await Pick(".obj");if(path!=null&&await ConfirmDiscard())await ImportObj(path);},()=>ready);
  foreach(string channel in new[]{"Diffuse","Normal","Specular","AO","Roughness","Metallic"}){string selected=channel;editorCommands[channel]=(async()=>await ImportTexture(selected),()=>HasModel&&materialChoice.SelectedItem!=null);}
  Closed+=(_,_)=>CleanImportSession();
 }
 async Task ImportObj(string path,bool asLod=false)
 {
  processing=true;UpdateControls();var result=await Task.Run(()=>ObjImporter.Read(path));
  var dimensions=new Microsoft.UI.Xaml.Controls.TextBlock();var unit=new ComboBox{ItemsSource=new[]{UiLanguage.Text("Метры"),UiLanguage.Text("Сантиметры"),UiLanguage.Text("Миллиметры")},SelectedIndex=0};double[] scales=[1,.01,.001];var positions=result.Model.Nodes.SelectMany(n=>Enumerable.Range(0,n.VertexCount).Select(i=>new System.Numerics.Vector3(n.Positions[i*3],n.Positions[i*3+1],n.Positions[i*3+2]))).ToArray();var size=positions.Aggregate(System.Numerics.Vector3.Max)-positions.Aggregate(System.Numerics.Vector3.Min);void Dimensions()=>dimensions.Text=string.Join(" × ",new[]{size.X,size.Y,size.Z}.Select(v=>(v*scales[unit.SelectedIndex]).ToString("0.0",System.Globalization.CultureInfo.CurrentCulture)))+" m";unit.SelectionChanged+=(_,_)=>Dimensions();Dimensions();var importPanel=new StackPanel{Spacing=8};SettingRow(importPanel,"Единицы OBJ",unit);importPanel.Children.Add(dimensions);var importDialog=VehicleDialog("Импортировать OBJ…",importPanel);if(await ShowDialog(importDialog)!=ContentDialogResult.Primary)return;float scale=(float)scales[unit.SelectedIndex];if(scale!=1)result=result with{Model=ModelOrientation.Apply(result.Model,System.Numerics.Matrix4x4.CreateScale(scale))};
  var warnings=new List<string>();var transforms=new Dictionary<string,System.Numerics.Vector4>();var importedMaps=MaterialDocuments.Obj(path,result,transforms,warnings);result=result with{Model=ModelEditing.TransformUv(result.Model,transforms)};
  if(result.MissingUvCorners>0)warnings.Add(UiLanguage.Text("Нет UV у части граней")+": "+result.MissingUvCorners);
  string virtualNmf=Path.ChangeExtension(path,".nmf");
  if(asLod){
   if(lods.ContainsKey(virtualNmf))throw new InvalidDataException(UiLanguage.Text("Модель с таким именем уже загружена."));
   if(result.Model.Materials.Except(lods.Values.SelectMany(model=>model.Materials),StringComparer.Ordinal).Any())throw new InvalidDataException(UiLanguage.Text("Материалы OBJ для LOD должны совпадать с основной моделью."));
   lods[virtualNmf]=documentTransform==System.Numerics.Matrix4x4.Identity?result.Model:ModelOrientation.Apply(result.Model,documentTransform);glassObjects[virtualNmf]=[];AddLodChoice(virtualNmf);SelectLod(virtualNmf);
  }else{
   await NormalizeMaterialMaps(importedMaps);
   // Use private session assets; the source OBJ/MTL directories remain read-only.
   string diffuse=SessionFile("default-diffuse.dds"),specular=SessionFile("default-specular.dds"),normal=SessionFile("default-normal.dds");
   File.WriteAllBytes(diffuse,DdsImage.Encode(new(1,1,[180,180,180,255])));File.WriteAllBytes(specular,DdsImage.Encode(new(1,1,[255,255,255,255])));File.WriteAllBytes(normal,DdsImage.Encode(new(1,1,[128,128,255,255])));
   string generated=SessionFile("material-"+Guid.NewGuid().ToString("N")+".mtl");File.WriteAllText(generated,NativeMaterialText.Create(result.Model.Materials,diffuse,specular,normal));
   lods.Clear();glassObjects.Clear();settings.Clear();lodChoice.Items.Clear();mask=null;maskMaterial=null;ResetVehicle(Path.Combine(Path.GetDirectoryName(path)!,"script.ini"));
   lods[virtualNmf]=result.Model;glassObjects[virtualNmf]=[];AddLodChoice(virtualNmf);SelectLod(virtualNmf);mtl=generated;
   foreach(string name in result.Model.Materials){var value=importedMaps.GetValueOrDefault(name)??new();settings[name]=value with{Base=value.Base??diffuse,Specular=value.Specular??specular,Normal=value.Normal??normal};}
   string? paired=MaterialDocuments.FindPbr(Path.ChangeExtension(path,"mtl"));if(paired!=null){var maps=MaterialDocuments.Pbr(paired,settings);await NormalizeMaterialMaps(maps);settings.Clear();foreach(var p in maps)settings[p.Key]=p.Value;}
   variants.Clear();variantChoice.Items.Clear();variants.Add(new("material",generated,new(settings)));variantChoice.Items.Add("1");activeVariant=0;variantChoice.SelectedIndex=0;
  }
  dirty=true;SetStatus("OBJ импортирован");await Display();if(warnings.Count>0)SetStatus("Проверьте импорт OBJ",string.Join("; ",warnings));
 }
 async Task ImportTexture(string channel)
 {
  if(materialChoice.SelectedItem is not string material)throw new InvalidDataException(UiLanguage.Text("Выберите материал"));
  var path=await Pick(".dds",".png",".jpg",".jpeg",".bmp",".tif",".tiff");if(path==null)return;
  processing=true;UpdateControls();var image=await ReadImage(path);string texture=path;
  if(!Path.GetExtension(path).Equals(".dds",StringComparison.OrdinalIgnoreCase)){texture=SessionFile("texture-"+Guid.NewGuid().ToString("N")+".dds");File.WriteAllBytes(texture,DdsImage.Encode(image,mipmaps:true,normalMap:channel=="Normal",srgb:channel=="Diffuse"));}
  var current=settings.GetValueOrDefault(material)??new();string preview;
  var next=channel switch{
   "Diffuse"=>current with{Base=texture,Native=false},"Normal"=>current with{Normal=texture},"Specular"=>current with{Specular=texture},
   "AO"=>current with{AoMap=texture},"Roughness"=>current with{RoughMap=texture},"Metallic"=>current with{MetalMap=texture},_=>throw new InvalidDataException("Unknown texture channel")};
  preview=channel switch{"Diffuse"=>"texture","Normal"=>"normalMap","Specular"=>"specularMap","AO"=>"aoMap","Roughness"=>"roughMap",_=>"metalMap"};
  await PreviewTexture(material,image,preview);settings[material]=next;dirty=true;SetStatus("Текстура",channel+": "+Path.GetFileName(path));
 }
}
