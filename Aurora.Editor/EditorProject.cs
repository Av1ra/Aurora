using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 Task OpenDocument(string path)=>Path.GetExtension(path).ToLowerInvariant() switch{".aur"=>OpenProject(path),".obj"=>ImportObj(path),_=>Load(path)};
 void RegisterProjectCommands()
 {
  editorCommands["Сохранить проект…"]=(SaveProject,()=>HasModel);
  editorCommands["Открыть проект…"]=(async()=>{var path=await Pick(".aur");if(path!=null&&await ConfirmDiscard())await OpenProject(path);},()=>ready);
 }
 readonly Dictionary<string,(DateTime Stamp,long Size,byte[] Data)> projectAssetCache=new(StringComparer.OrdinalIgnoreCase);
 byte[] ProjectAssetBytes(string path){var file=new FileInfo(path);if(!file.Exists)throw new FileNotFoundException("Project asset missing",path);if(projectAssetCache.TryGetValue(path,out var cached)&&cached.Stamp==file.LastWriteTimeUtc&&cached.Size==file.Length)return cached.Data;var bytes=File.ReadAllBytes(path);while(projectAssetCache.Count>0&&(projectAssetCache.Count>=128||projectAssetCache.Values.Sum(c=>(long)c.Data.Length)+bytes.Length>192L*1024*1024))projectAssetCache.Remove(projectAssetCache.Keys.First());if(bytes.Length<=192L*1024*1024)projectAssetCache[path]=(file.LastWriteTimeUtc,file.Length,bytes);return bytes;}
 static MaterialSettings MapSettings(MaterialSettings s,Func<string?,string?> map)=>MaterialDocuments.Remap(s,map);
 async Task SaveProject()
 {
  StoreVariant();var picker=new Windows.Storage.Pickers.FileSavePicker{SuggestedFileName="Aurora-model"};picker.FileTypeChoices.Add("Aurora project",new[]{".aur"});WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));var file=await picker.PickSaveFileAsync();if(file==null)return;
  processing=true;UpdateControls();workspaceDocuments[workspaceActive]=CaptureProjectEntries();var archive=new TransportWorkspace(workspaceActive,new(workspaceDocuments)).Pack();await Task.Run(()=>AuroraProjectArchive.Write(file.Path,archive));workspaceDirty=false;dirty=false;MarkHistorySaved();SetStatus("Проект сохранён",file.Name);
 }
 Dictionary<string,byte[]> CaptureProjectEntries()
 {
  StoreVariant();var entries=new Dictionary<string,byte[]>();
  string? Asset(string? source){if(source==null)return null;byte[] bytes=ProjectAssetBytes(source);string name="assets/"+Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()+Path.GetExtension(source).ToLowerInvariant();entries.TryAdd(name,bytes);return name;}
  var modelNames=ModelFileNames.Allocate(lods.Keys);var models=lods.Select((p,i)=>{string asset="models/"+i+".nmf";entries[asset]=p.Value.Data;return new ProjectModel(modelNames[p.Key],asset,glassObjects[p.Key].ToArray());}).ToArray();
  var materials=variants.Select((v,i)=>{
   string asset="native/"+i+".mtl";string text=File.ReadAllText(v.NativePath);text=Regex.Replace(text,@"(?m)^\s*\$TEXTURE(?:_MTL)?\s+(\d+)\s+([^\r\n]+)",m=>"$TEXTURE_MTL "+m.Groups[1].Value+" ../"+Asset(MaterialFiles.Resolve(v.NativePath,m.Groups[2].Value)));entries[asset]=Encoding.UTF8.GetBytes(text);return new ProjectVariant(v.Name,asset,v.Settings.ToDictionary(p=>p.Key,p=>MapSettings(p.Value,Asset)));
  }).ToArray();
  ProjectCargo? cargo=null;if(cargoModel!=null&&cargoMtl!=null){string modelAsset="cargo/model.nmf";entries[modelAsset]=cargoModel.Data;string materialScript=VehicleAssets.Pack("$HORSE_MATERIAL \""+cargoMtl+"\"\n$END\n",Path.GetDirectoryName(cargoMtl)!,entries,MediaFolder());string materialAsset=VehicleScript.Tokens(materialScript).First(t=>t.Key=="HORSE_MATERIAL"&&t.Index==0).Value;cargo=new(modelAsset,materialAsset,showCargo,cargoFill,cargoFrame,cargoBone);}
  string? projectScript=vehicleConfiguration==null?null:VehicleAssets.Pack(VehicleSoundFiles.Map(vehicleConfiguration,r=>Asset(VehicleSoundFiles.Resolve(r,Path.GetDirectoryName(lods.Keys.First())!))!),Path.GetDirectoryName(lods.Keys.First())!,entries,MediaFolder());
  if(projectScript!=null)projectScript=VehicleScript.RewriteTokens(projectScript,t=>Regex.IsMatch(t.Key,@"^LOD\d*$")&&t.Index==0?ModelFileNames.ResolveForProject(modelNames,t.Value,Path.GetDirectoryName(lods.Keys.First())!,path=>Asset(path)!):null);
  var t=documentTransform;var project=new EditorProject(1,models,materials,activeVariant,lods.Keys.ToList().IndexOf(selectedLod!),Asset(mask),maskMaterial,projectScript,[t.M11,t.M12,t.M13,t.M14,t.M21,t.M22,t.M23,t.M24,t.M31,t.M32,t.M33,t.M34,t.M41,t.M42,t.M43,t.M44],[mainPivot.X,mainPivot.Y,mainPivot.Z],[mainRotation.X,mainRotation.Y,mainRotation.Z],lampRotations.Select(r=>new[]{r.X,r.Y,r.Z}).ToArray(),cargo,previewSettings,originObjectId);
  entries["document.json"]=JsonSerializer.SerializeToUtf8Bytes(project);return entries;
 }

 async Task OpenProject(string path)
 {
  processing=true;UpdateControls();var data=await Task.Run(()=>TransportWorkspace.Unpack(AuroraProjectArchive.Read(path)));await RestoreProjectEntries(data.Objects[data.Active],Path.GetFileName(path));workspaceDocuments.Clear();foreach(var item in data.Objects)workspaceDocuments[item.Key]=item.Value;workspaceActive=data.Active;workspaceDirty=false;workspaceHistoryState=null;dirty=false;MarkHistorySaved();
 }
 async Task RestoreProjectEntries(Dictionary<string,byte[]> entries,string label)
 {
  processing=true;UpdateControls();string prefix="project-"+Guid.NewGuid().ToString("N");var data=ProjectData.Load(entries,name=>SessionFile(prefix+"/"+name));var p=data.Document;
  NewHistoryDocument();lods.Clear();glassObjects.Clear();lodChoice.Items.Clear();settings.Clear();variants.Clear();variantChoice.Items.Clear();
  foreach(var pair in data.Models){lods[pair.Key]=pair.Value;glassObjects[pair.Key]=data.Glass[pair.Key];AddLodChoice(pair.Key);}variants.AddRange(data.Variants);foreach(var v in variants)variantChoice.Items.Add((variantChoice.Items.Count+1).ToString());ActivateVariant(p.ActiveVariant);selectedLod=lods.Keys.ElementAt(p.SelectedLod);SelectLod(selectedLod);mask=data.Mask;maskMaterial=p.MaskMaterial;vehicleConfiguration=data.Vehicle;vehicleConfigurationEdited=false;originObjectId=p.NativeObject;var t=p.Transform;documentTransform=new(t[0],t[1],t[2],t[3],t[4],t[5],t[6],t[7],t[8],t[9],t[10],t[11],t[12],t[13],t[14],t[15]);mainPivot=p.Pivot is {Length:3}?new(p.Pivot[0],p.Pivot[1],p.Pivot[2]):Vector3.Zero;mainRotation=p.PivotRotation is {Length:3}?new(p.PivotRotation[0],p.PivotRotation[1],p.PivotRotation[2]):Vector3.Zero;ReadLampSlots();if(p.LampRotations is {Length:4})for(int i=0;i<4;i++)lampRotations[i]=new(p.LampRotations[i][0],p.LampRotations[i][1],p.LampRotations[i][2]);cargoModel=data.Cargo;cargoModelPath=data.CargoPath;cargoMtl=data.CargoMaterial;showCargo=p.Cargo?.Show??false;cargoFill=p.Cargo?.Fill??1;cargoFrame=p.Cargo?.Frame??"RESOURCE_VISUALIZATION";cargoBone=p.Cargo?.Bone;previewSettings=p.Previews??new();transportPoint=null;activeSmoke=-1;activeCargo=-1;gizmoVisible=false;dirty=false;MarkHistorySaved();SetStatus("Проект открыт",label);await Display();
 }
}
