using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml.Controls;
namespace Aurora.Editor;

sealed partial class EditorWindow
{
 string? importSession;
 readonly HashSet<string> importedFiles=[];
 string SessionFile(string name){importSession??=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aurora","Editor","Sessions",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(importSession);string file=Path.Combine(importSession,name);importedFiles.Add(file);return file;}
 void CleanImportSession(){foreach(string file in importedFiles)try{File.Delete(file);}catch{}if(importSession!=null)try{Directory.Delete(importSession);}catch{}}
 void RegisterImportCommands()
 {
  editorCommands["Импортировать OBJ…"]=(async()=>{var path=await Pick(".obj");if(path!=null&&await ConfirmDiscard())await ImportObj(path);},()=>ready);
  foreach(string channel in new[]{"Diffuse","Normal","Specular","AO","Roughness","Metallic"}){string selected=channel;editorCommands[channel]=(async()=>await ImportTexture(selected),()=>HasModel&&materialChoice.SelectedItem!=null);}
  Closed+=(_,_)=>CleanImportSession();
 }
 async Task ImportObj(string path,bool asLod=false)
 {
  processing=true;UpdateControls();var result=await Task.Run(()=>{if(new FileInfo(path).Length>64*1024*1024)throw new InvalidDataException("OBJ exceeds 64 MiB");return ObjImporter.Parse(File.ReadAllText(path));});
  string virtualNmf=Path.ChangeExtension(path,".nmf");
  if(asLod){
   if(lods.ContainsKey(virtualNmf))throw new InvalidDataException(UiLanguage.Text("Модель с таким именем уже загружена."));
   if(result.Model.Materials.Except(lods.Values.SelectMany(model=>model.Materials),StringComparer.Ordinal).Any())throw new InvalidDataException(UiLanguage.Text("Материалы OBJ для LOD должны совпадать с основной моделью."));
   lods[virtualNmf]=result.Model;glassObjects[virtualNmf]=[];AddLodChoice(virtualNmf);SelectLod(virtualNmf);
  }else{
   // Use private session assets; the source OBJ/MTL directories remain read-only.
   string diffuse=SessionFile("default-diffuse.dds"),specular=SessionFile("default-specular.dds"),normal=SessionFile("default-normal.dds");
   File.WriteAllBytes(diffuse,DdsImage.Encode(new(1,1,[180,180,180,255])));File.WriteAllBytes(specular,DdsImage.Encode(new(1,1,[255,255,255,255])));File.WriteAllBytes(normal,DdsImage.Encode(new(1,1,[128,128,255,255])));
   string generated=SessionFile("material-"+Guid.NewGuid().ToString("N")+".mtl");File.WriteAllText(generated,NativeMaterialText.Create(result.Model.Materials,diffuse,specular,normal));
   lods.Clear();glassObjects.Clear();settings.Clear();lodChoice.Items.Clear();mask=null;maskMaterial=null;
   lods[virtualNmf]=result.Model;glassObjects[virtualNmf]=[];AddLodChoice(virtualNmf);SelectLod(virtualNmf);mtl=generated;
   foreach(string name in result.Model.Materials)settings[name]=new(Base:diffuse,Native:false);
  }
  dirty=true;SetStatus("OBJ импортирован",null,"Назначьте текстуры через меню импорта.");await Display();
 }
 async Task ImportTexture(string channel)
 {
  if(materialChoice.SelectedItem is not string material)throw new InvalidDataException(UiLanguage.Text("Выберите материал"));
  var path=await Pick(".dds",".png",".jpg",".jpeg",".bmp",".tif",".tiff");if(path==null)return;
  processing=true;UpdateControls();var image=await ReadImage(path);string texture=path;
  if(!Path.GetExtension(path).Equals(".dds",StringComparison.OrdinalIgnoreCase)){texture=SessionFile("texture-"+Guid.NewGuid().ToString("N")+".dds");File.WriteAllBytes(texture,DdsImage.Encode(image));}
  var current=settings.GetValueOrDefault(material)??new();string preview;
  var next=channel switch{
   "Diffuse"=>current with{Base=texture,Native=false},"Normal"=>current with{Normal=texture},"Specular"=>current with{Specular=texture},
   "AO"=>current with{AoMap=texture},"Roughness"=>current with{RoughMap=texture},"Metallic"=>current with{MetalMap=texture},_=>throw new InvalidDataException("Unknown texture channel")};
  preview=channel switch{"Diffuse"=>"texture","Normal"=>"normalMap","Specular"=>"specularMap","AO"=>"aoMap","Roughness"=>"roughMap",_=>"metalMap"};
  await PreviewTexture(material,image,preview);settings[material]=next;dirty=true;SetStatus("Текстура",channel+": "+Path.GetFileName(path));
 }
}
