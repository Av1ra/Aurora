using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.RegularExpressions;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 readonly List<MaterialVariant> variants=[];
 readonly ComboBox variantChoice=new(){Header=UiLanguage.Text("Вариант"),HorizontalAlignment=HorizontalAlignment.Stretch};
 int activeVariant=-1;
 void StoreVariant(){if(activeVariant>=0&&activeVariant<variants.Count&&mtl!=null)variants[activeVariant]=variants[activeVariant] with{NativePath=mtl,Settings=new(settings)};}
 void RegisterMaterialCommands()
 {
  editorCommands["Создать материал…"]=(CreateMaterial,()=>HasModel);
  editorCommands["Создать вариант…"]=(CreateVariant,()=>HasModel&&mtl!=null);
  editorCommands["Назначить материал объекту"]=(AssignMaterial,()=>HasModel&&objects.SelectedItem!=null&&materialChoice.SelectedItem!=null);
  variantChoice.SelectionChanged+=async(_,_)=>{if(!busy&&variantChoice.SelectedIndex>=0&&variantChoice.SelectedIndex!=activeVariant)await RunCommand(async()=>{StoreVariant();ActivateVariant(variantChoice.SelectedIndex);await Display();});};
 }
 void ActivateVariant(int index)
 {
  activeVariant=index;var variant=variants[index];mtl=variant.NativePath;settings.Clear();foreach(var p in variant.Settings)settings[p.Key]=p.Value;variantChoice.SelectedIndex=index;
 }
 async Task LoadMaterialVariants(string model)
 {
  string folder=Path.GetDirectoryName(model)!;var files=Directory.GetFiles(folder,"*.mtl").Where(f=>Regex.IsMatch(Path.GetFileName(f),@"^material(?:_\d+)?\.mtl$",RegexOptions.IgnoreCase)).OrderBy(f=>Path.GetFileName(f).Length).ThenBy(f=>f).ToList();
  string direct=Path.ChangeExtension(model,"mtl");if(files.Count==0&&File.Exists(direct))files.Add(direct);
  variants.Clear();variantChoice.Items.Clear();activeVariant=-1;
  foreach(string file in files){var maps=MaterialDocuments.Native(file);string? paired=MaterialDocuments.FindPbr(file);if(paired!=null)maps=MaterialDocuments.Pbr(paired,maps);await NormalizeMaterialMaps(maps);variants.Add(new(Path.GetFileNameWithoutExtension(file),file,maps));variantChoice.Items.Add(variants.Count.ToString());}
  if(variants.Count>0)ActivateVariant(0);else CreateDefaultVariant(lods.Values.SelectMany(m=>m.Materials).Distinct());
 }
 void CreateDefaultVariant(IEnumerable<string> names)
 {
  string diffuse=SessionFile("default-diffuse.dds"),normal=SessionFile("default-normal.dds"),specular=SessionFile("default-specular.dds");File.WriteAllBytes(diffuse,DdsImage.Encode(new(1,1,[180,180,180,255])));File.WriteAllBytes(normal,DdsImage.Encode(new(1,1,[128,128,255,255])));File.WriteAllBytes(specular,DdsImage.Encode(new(1,1,[255,255,255,255])));
  string file=SessionFile("material-"+Guid.NewGuid().ToString("N")+".mtl");File.WriteAllText(file,NativeMaterialText.Create(names,diffuse,specular,normal));variants.Clear();variantChoice.Items.Clear();variants.Add(new("material",file,MaterialDocuments.Native(file)));variantChoice.Items.Add("1");ActivateVariant(0);
 }
 async Task NormalizeMaterialMaps(Dictionary<string,MaterialSettings> maps)
 {
  var converted=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  async Task<string?> Map(string? path){if(path==null||Path.GetExtension(path).Equals(".dds",StringComparison.OrdinalIgnoreCase))return path;if(converted.TryGetValue(path,out var found))return found;string output=SessionFile("texture-"+Guid.NewGuid().ToString("N")+".dds");File.WriteAllBytes(output,DdsImage.Encode(await ReadImage(path)));converted[path]=output;return output;}
  foreach(string key in maps.Keys.ToArray()){var s=maps[key];maps[key]=s with{Base=await Map(s.Base),Normal=await Map(s.Normal),Specular=await Map(s.Specular),Orm=await Map(s.Orm),MetalMap=await Map(s.MetalMap),RoughMap=await Map(s.RoughMap),AoMap=await Map(s.AoMap),Emissive=await Map(s.Emissive),PbrBase=await Map(s.PbrBase),PbrNormal=await Map(s.PbrNormal)};}
 }
 async Task LoadNativeMaterial()
 {
  string? path=await Pick(".mtl");if(path==null)return;var maps=MaterialDocuments.Native(path);await NormalizeMaterialMaps(maps);
  foreach(string name in lods.Values.SelectMany(m=>m.Materials).Distinct())if(!maps.ContainsKey(name))throw new InvalidDataException("В MTL нет материала: "+name);
  mtl=path;settings.Clear();foreach(var p in maps)settings[p.Key]=p.Value;StoreVariant();dirty=true;SetStatus("Материал загружен");
 }
 async Task LoadPbrMaterial()
 {
  string? path=await Pick(".mtl",".pbr");if(path==null)return;var maps=MaterialDocuments.Pbr(path,settings);await NormalizeMaterialMaps(maps);settings.Clear();foreach(var p in maps)settings[p.Key]=p.Value;StoreVariant();dirty=true;SetStatus("Материал загружен");
 }
 async Task<string?> MaterialName(string title)
 {
  var box=new TextBox{Header=UiLanguage.Text("Имя"),MaxLength=63};var dialog=VehicleDialog(title,box);
  if(await ShowDialog(dialog)!=ContentDialogResult.Primary)return null;string name=box.Text.Trim();if(name.Length==0||ObjImporter.Identifier(name)!=name)throw new InvalidDataException(UiLanguage.Text("Используйте латиницу, цифры и знак подчёркивания."));return name;
 }
 async Task CreateMaterial()
 {
  string? name=await MaterialName("Создать материал…");if(name==null)return;
  if(lods.Values.Any(m=>m.Materials.Contains(name)))throw new InvalidDataException("Material already exists");
  var changed=lods.ToDictionary(p=>p.Key,p=>ModelEditing.AddMaterial(p.Value,name));
  string blank=SessionFile("neutral.dds");if(!File.Exists(blank))File.WriteAllBytes(blank,DdsImage.Encode(new(1,1,[180,180,180,255])));
  string normal=SessionFile("neutral-normal.dds");if(!File.Exists(normal))File.WriteAllBytes(normal,DdsImage.Encode(new(1,1,[128,128,255,255])));
  StoreVariant();if(variants.Count==0){variants.Add(new("material","",new()));variantChoice.Items.Add("1");activeVariant=0;}
  var updatedVariants=new List<MaterialVariant>();
  for(int i=0;i<variants.Count;i++){
   var variant=variants[i];string text=File.Exists(variant.NativePath)?File.ReadAllText(variant.NativePath):"$END\n";
   int end=text.LastIndexOf("$END",StringComparison.Ordinal);if(end<0)throw new InvalidDataException("MTL $END missing");
   string added=NativeMaterialText.Create([name],blank,blank,normal);string output=SessionFile("material-"+Guid.NewGuid().ToString("N")+".mtl");
   // Resolve pre-existing references before relocating a native file into a session.
   if(File.Exists(variant.NativePath))text=Regex.Replace(text,@"(?m)^\s*\$TEXTURE(?:_MTL)?\s+(\d+)\s+([^\r\n]+)",m=>"$TEXTURE_MTL "+m.Groups[1].Value+" "+MaterialFiles.Resolve(variant.NativePath,m.Groups[2].Value));
   end=text.LastIndexOf("$END",StringComparison.Ordinal);File.WriteAllText(output,text[..end]+added+text[(end+4)..]);var maps=new Dictionary<string,MaterialSettings>(variant.Settings){{name,new(Base:blank,Specular:blank,Normal:normal)}};updatedVariants.Add(variant with{NativePath=output,Settings=maps});
  }
  foreach(var pair in changed)lods[pair.Key]=pair.Value;variants.Clear();variants.AddRange(updatedVariants);
  ActivateVariant(activeVariant);dirty=true;await Display();materialChoice.SelectedItem=name;
 }
 async Task CreateVariant()
 {
  if(variants.Count>=64)throw new InvalidDataException("Maximum 64 material variants");
  StoreVariant();var source=variants[activeVariant];variants.Add(new("material_"+variants.Count,source.NativePath,new(settings)));variantChoice.Items.Add(variants.Count.ToString());ActivateVariant(variants.Count-1);dirty=true;await Display();
 }
 async Task AssignMaterial()
 {
  if(objects.SelectedItem is not string name||materialChoice.SelectedItem is not string material)return;
  var selected=CurrentModel().Nodes.Single(n=>n.Name==name);int group=-1;bool all=true;
  if(selected.Groups.Length>1){var panel=new StackPanel{Spacing=10};var regions=new ComboBox{ItemsSource=new[]{UiLanguage.Text("Весь объект")}.Concat(selected.Groups.Select((g,i)=>(i+1)+": "+CurrentModel().Materials[g.Material])).ToArray(),SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};SettingRow(panel,"Часть объекта",regions);var scope=new ComboBox{ItemsSource=new[]{UiLanguage.Text("Текущая модель"),UiLanguage.Text("Все LOD")},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};SettingRow(panel,"Применить к",scope);if(await ShowDialog(VehicleDialog("Назначить материал объекту",panel))!=ContentDialogResult.Primary)return;group=regions.SelectedIndex-1;all=scope.SelectedIndex==1;}
  var result=lods.ToDictionary(p=>p.Key,p=>{if((!all&&p.Key!=selectedLod)||!p.Value.Nodes.Any(n=>n.Name==name))return p.Value;if(group<0)return ModelEditing.Assign(p.Value,name,material);var node=p.Value.Nodes.Single(n=>n.Name==name);if(p.Key==selectedLod)return ModelEditing.AssignGroup(p.Value,name,group,material);string source=CurrentModel().Materials[selected.Groups[group].Material];var matches=node.Groups.Select((g,i)=>(g,i)).Where(g=>p.Value.Materials[g.g.Material]==source).ToArray();if(matches.Length!=1)throw new InvalidDataException(UiLanguage.Text("Неоднозначное соответствие материалов между LOD"));return ModelEditing.AssignGroup(p.Value,name,matches[0].i,material);});
  foreach(var p in result)lods[p.Key]=p.Value;dirty=true;await Display();
 }
 async Task SaveNmfMaterials()
 {
  StoreVariant();var panel=new StackPanel{Spacing=10,MinWidth=280};var mode=new ComboBox{Header=UiLanguage.Text("Материалы"),ItemsSource=new[]{UiLanguage.Text("Один материал"),UiLanguage.Text("Несколько материалов")},SelectedIndex=variants.Count>1?1:0,HorizontalAlignment=HorizontalAlignment.Stretch};panel.Children.Add(mode);
  var checks=variants.Select((v,i)=>new CheckBox{Content=(i+1).ToString(),IsChecked=i==activeVariant}).ToArray();foreach(var box in checks)panel.Children.Add(box);
  var dialog=VehicleDialog("Сохранить NMF…",panel);ValidateVehicleDialog(dialog,panel,()=>{int count=checks.Count(c=>c.IsChecked==true);if(count==0||(mode.SelectedIndex==0&&count!=1))throw new InvalidDataException(UiLanguage.Text("Выберите материал."));});
  if(await ShowDialog(dialog)!=ContentDialogResult.Primary)return;string? folder=await Folder();if(folder==null)return;
  processing=true;UpdateControls();var selected=variants.Where((_,i)=>checks[i].IsChecked==true).ToArray();string output=await VariantExport.Write(selected,lods,glassObjects,mask,maskMaterial,vehicleConfiguration,folder,ReadImage,previewSettings.Automatic?RenderVariantPreviews:null);SetStatus("Готово",output);
  // A game export is not the editable document. Only Save project clears dirty.
 }
}
