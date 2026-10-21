using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Aurora.Editor;
sealed record ProjectModel(string Name,string Asset,string[] Glass);
sealed record ProjectVariant(string Name,string Native,Dictionary<string,MaterialSettings> Settings);
sealed record ProjectCargo(string Model,string Material,bool Show,float Fill,string Frame="RESOURCE_VISUALIZATION",string? Bone=null);
sealed record EditorProject(int Schema,ProjectModel[] Models,ProjectVariant[] Variants,int ActiveVariant,int SelectedLod,string? Mask,string? MaskMaterial,string? Vehicle,float[] Transform,float[]? Pivot=null,float[]? PivotRotation=null,float[][]? LampRotations=null,ProjectCargo? Cargo=null,PreviewSettings? Previews=null);
sealed partial class EditorWindow
{
 Task OpenDocument(string path)=>Path.GetExtension(path).ToLowerInvariant() switch{".aur"=>OpenProject(path),".obj"=>ImportObj(path),_=>Load(path)};
 void RegisterProjectCommands()
 {
  editorCommands["Сохранить проект…"]=(SaveProject,()=>HasModel);
  editorCommands["Открыть проект…"]=(async()=>{var path=await Pick(".aur");if(path!=null&&await ConfirmDiscard())await OpenProject(path);},()=>ready);
 }
 static MaterialSettings MapSettings(MaterialSettings s,Func<string?,string?> map)=>s with{Base=map(s.Base),Normal=map(s.Normal),Specular=map(s.Specular),PbrBase=map(s.PbrBase),PbrNormal=map(s.PbrNormal),Orm=map(s.Orm),MetalMap=map(s.MetalMap),RoughMap=map(s.RoughMap),AoMap=map(s.AoMap),Emissive=map(s.Emissive),Extra=s.Extra?.ToDictionary(p=>p.Key,p=>p.Key is "glass" or "glass_mask"&&p.Value!="none"&&!p.Value.StartsWith("aurora:",StringComparison.Ordinal)?map(p.Value)!:p.Value)};
 async Task SaveProject()
 {
  StoreVariant();var picker=new Windows.Storage.Pickers.FileSavePicker{SuggestedFileName="Aurora-model"};picker.FileTypeChoices.Add("Aurora project",new[]{".aur"});WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));var file=await picker.PickSaveFileAsync();if(file==null)return;
  processing=true;UpdateControls();var entries=new Dictionary<string,byte[]>();
  string? Asset(string? source){if(source==null)return null;byte[] bytes=File.ReadAllBytes(source);string name="assets/"+Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()+Path.GetExtension(source).ToLowerInvariant();entries.TryAdd(name,bytes);return name;}
  var modelNames=ModelFileNames.Allocate(lods.Keys);var models=lods.Select((p,i)=>{string asset="models/"+i+".nmf";entries[asset]=p.Value.Data;return new ProjectModel(modelNames[p.Key],asset,glassObjects[p.Key].ToArray());}).ToArray();
  var materials=variants.Select((v,i)=>{
   string asset="native/"+i+".mtl";string text=File.ReadAllText(v.NativePath);text=Regex.Replace(text,@"(?m)^\s*\$TEXTURE(?:_MTL)?\s+(\d+)\s+([^\r\n]+)",m=>"$TEXTURE_MTL "+m.Groups[1].Value+" ../"+Asset(MaterialFiles.Resolve(v.NativePath,m.Groups[2].Value)));entries[asset]=Encoding.UTF8.GetBytes(text);return new ProjectVariant(v.Name,asset,v.Settings.ToDictionary(p=>p.Key,p=>MapSettings(p.Value,Asset)));
  }).ToArray();
  ProjectCargo? cargo=null;if(cargoModel!=null&&cargoMtl!=null){string modelAsset="cargo/model.nmf",materialAsset="cargo/material.mtl";entries[modelAsset]=cargoModel.Data;string text=File.ReadAllText(cargoMtl);text=Regex.Replace(text,@"(?m)^\s*\$TEXTURE(?:_MTL)?\s+(\d+)\s+([^\r\n]+)",m=>"$TEXTURE_MTL "+m.Groups[1].Value+" ../"+Asset(MaterialFiles.Resolve(cargoMtl,m.Groups[2].Value)));entries[materialAsset]=Encoding.UTF8.GetBytes(text);cargo=new(modelAsset,materialAsset,showCargo,cargoFill,cargoFrame,cargoBone);}
  string? projectScript=vehicleConfiguration==null?null:VehicleAssets.Pack(VehicleSoundFiles.Map(vehicleConfiguration,r=>Asset(VehicleSoundFiles.Resolve(r,Path.GetDirectoryName(lods.Keys.First())!))!),Path.GetDirectoryName(lods.Keys.First())!,entries,MediaFolder());
  if(projectScript!=null)projectScript=VehicleScript.RewriteTokens(projectScript,t=>Regex.IsMatch(t.Key,@"^LOD\d*$")&&t.Index==0?ModelFileNames.Resolve(modelNames,t.Value,Path.GetDirectoryName(lods.Keys.First())!):null);
  var t=documentTransform;var project=new EditorProject(1,models,materials,activeVariant,lods.Keys.ToList().IndexOf(selectedLod!),Asset(mask),maskMaterial,projectScript,[t.M11,t.M12,t.M13,t.M14,t.M21,t.M22,t.M23,t.M24,t.M31,t.M32,t.M33,t.M34,t.M41,t.M42,t.M43,t.M44],[mainPivot.X,mainPivot.Y,mainPivot.Z],[mainRotation.X,mainRotation.Y,mainRotation.Z],lampRotations.Select(r=>new[]{r.X,r.Y,r.Z}).ToArray(),cargo,previewSettings);
  entries["document.json"]=JsonSerializer.SerializeToUtf8Bytes(project);await Task.Run(()=>AuroraProjectArchive.Write(file.Path,entries));dirty=false;MarkHistorySaved();SetStatus("Проект сохранён",file.Name);
 }
 async Task OpenProject(string path)
 {
  processing=true;UpdateControls();var entries=await Task.Run(()=>AuroraProjectArchive.Read(path));if(!entries.TryGetValue("document.json",out var document))throw new InvalidDataException("Project document missing");var p=JsonSerializer.Deserialize<EditorProject>(document)??throw new InvalidDataException("Invalid project document");
  if(p.Schema!=1||p.Models.Length is <1 or >64||p.Variants.Length is <1 or >64||p.ActiveVariant<0||p.ActiveVariant>=p.Variants.Length||p.SelectedLod<0||p.SelectedLod>=p.Models.Length||p.Transform.Length!=16||p.Transform.Any(x=>!float.IsFinite(x)))throw new InvalidDataException("Invalid project state");
  foreach(var value in new[]{p.Pivot,p.PivotRotation}.Concat(p.LampRotations??[]))if(value!=null&&(value.Length!=3||value.Any(n=>!float.IsFinite(n))))throw new InvalidDataException("Invalid project pivot");
  string prefix="project-"+Guid.NewGuid().ToString("N");var files=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  foreach(var entry in entries){string output=SessionFile(prefix+"/"+entry.Key);Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.WriteAllBytes(output,entry.Value);files.Add(entry.Key,output);}
  string? Asset(string? name)=>name==null?null:files.TryGetValue(name,out var file)?file:throw new InvalidDataException("Project asset missing");
  var parsed=p.Models.Select(m=>{if(Path.GetFileName(m.Name)!=m.Name||!AuroraProjectArchive.IsPortablePath(m.Name)||!m.Name.EndsWith(".nmf",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid project model name");return new NativeModel(File.ReadAllBytes(Asset(m.Asset)!));}).ToArray();
  var newVariants=p.Variants.Select(v=>new MaterialVariant(v.Name,Asset(v.Native)!,v.Settings.ToDictionary(x=>x.Key,x=>MapSettings(x.Value,Asset)))).ToArray();
  if(p.Models.Select(m=>m.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=p.Models.Length)throw new InvalidDataException("Duplicate project LOD");
  NativeModel? loadedCargo=p.Cargo==null?null:new NativeModel(File.ReadAllBytes(Asset(p.Cargo.Model)!));string? loadedCargoMaterial=p.Cargo==null?null:Asset(p.Cargo.Material);if(p.Cargo!=null&&p.Cargo.Frame is not ("RESOURCE_VISUALIZATION" or "RESOURCE_VISUALIZATION_JOINT" or "RESOURCE_VISUALIZATION_BONE" or "CARGOVEHICLE_VISUALIZATION" or "CARGOVEHICLE_VISUALIZATION_JOINT"))throw new InvalidDataException("Invalid cargo frame");if(p.Cargo!=null&&(!float.IsFinite(p.Cargo.Fill)||p.Cargo.Fill<0||p.Cargo.Fill>1))throw new InvalidDataException("Invalid cargo fill");
  NewHistoryDocument();lods.Clear();glassObjects.Clear();lodChoice.Items.Clear();settings.Clear();variants.Clear();variantChoice.Items.Clear();
  for(int i=0;i<p.Models.Length;i++){string key=SessionFile(prefix+"/loaded/"+p.Models[i].Name);lods[key]=parsed[i];glassObjects[key]=p.Models[i].Glass.ToHashSet();AddLodChoice(key);}
  variants.AddRange(newVariants);foreach(var v in variants)variantChoice.Items.Add((variantChoice.Items.Count+1).ToString());ActivateVariant(p.ActiveVariant);selectedLod=lods.Keys.ElementAt(p.SelectedLod);SelectLod(selectedLod);mask=Asset(p.Mask);maskMaterial=p.MaskMaterial;vehicleConfiguration=p.Vehicle==null?null:VehicleAssets.Map(VehicleSoundFiles.Map(p.Vehicle,r=>files.TryGetValue(r,out var soundPath)?soundPath:r),r=>files.TryGetValue(r,out var assetPath)?assetPath:r);vehicleConfigurationEdited=false;var t=p.Transform;documentTransform=new(t[0],t[1],t[2],t[3],t[4],t[5],t[6],t[7],t[8],t[9],t[10],t[11],t[12],t[13],t[14],t[15]);mainPivot=p.Pivot is {Length:3}?new(p.Pivot[0],p.Pivot[1],p.Pivot[2]):Vector3.Zero;mainRotation=p.PivotRotation is {Length:3}?new(p.PivotRotation[0],p.PivotRotation[1],p.PivotRotation[2]):Vector3.Zero;ReadLampSlots();if(p.LampRotations is {Length:4})for(int i=0;i<4;i++)lampRotations[i]=new(p.LampRotations[i][0],p.LampRotations[i][1],p.LampRotations[i][2]);cargoModel=loadedCargo;cargoModelPath=p.Cargo==null?null:Asset(p.Cargo.Model);cargoMtl=loadedCargoMaterial;showCargo=p.Cargo?.Show??false;cargoFill=p.Cargo?.Fill??1;cargoFrame=p.Cargo?.Frame??"RESOURCE_VISUALIZATION";cargoBone=p.Cargo?.Bone;previewSettings=p.Previews??new();transportPoint=null;activeSmoke=-1;activeCargo=-1;gizmoVisible=false;dirty=false;MarkHistorySaved();SetStatus("Проект открыт",Path.GetFileName(path));await Display();
 }
}
