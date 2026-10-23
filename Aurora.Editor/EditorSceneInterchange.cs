using Aurora.Core;
using Microsoft.UI.Xaml.Controls;
using System.Numerics;
using System.Text.Json;
namespace Aurora.Editor;
sealed record SceneEditorMetadata(string? Script,float[] Pivot,float[] Rotation);
sealed partial class EditorWindow
{
 InterchangeScene? interchange;
 string SceneBridge=>Path.Combine(AppContext.BaseDirectory,"Editor","Aurora.SceneBridge.exe");
 void RegisterSceneCommands()
 {
  editorCommands["Импортировать FBX…"]=(async()=>{var path=await Pick(".fbx");if(path!=null&&await ConfirmDiscard())await ImportFbx(path);},()=>ready);
  foreach(string format in new[]{"OBJ","FBX"}){string kind=format;editorCommands["Экспортировать "+kind+"…"]=(async()=>await ExportScene(kind),()=>HasModel);}
 }
 async Task ImportFbx(string path)
 {
  processing=true;UpdateControls();var scene=await SceneInterchange.ImportFbx(SceneBridge,path);string obj=SessionFile("fbx-"+Guid.NewGuid().ToString("N")+".obj");
  // Reuse material import and private-session handling; no source files are changed.
  SceneInterchange.ExportObj(obj,scene);importedFiles.Add(obj+".aur-scene.json");importedFiles.Add(Path.ChangeExtension(obj,".mtl"));await ImportObj(obj);
 }
 void RestoreInterchange(InterchangeScene scene)
 {
  interchange=scene;if(string.IsNullOrWhiteSpace(scene.Metadata))return;using var json=JsonDocument.Parse(scene.Metadata);if(!json.RootElement.TryGetProperty("Pivot",out _))return;var metadata=JsonSerializer.Deserialize<SceneEditorMetadata>(scene.Metadata);if(metadata==null)return;
  if(metadata.Script!=null){vehicleConfiguration=metadata.Script;vehicleConfigurationEdited=true;}
  if(metadata.Pivot.Length==3)mainPivot=new(metadata.Pivot[0],metadata.Pivot[1],metadata.Pivot[2]);if(metadata.Rotation.Length==3)mainRotation=new(metadata.Rotation[0],metadata.Rotation[1],metadata.Rotation[2]);
  var root=scene.Nodes.FirstOrDefault(n=>n.Name=="Aurora_MainPivot");if(root!=null){mainPivot=root.Matrix.Translation;if(Matrix4x4.Decompose(root.Matrix,out _,out var q,out _)){var r=Matrix4x4.CreateFromQuaternion(q);float y=MathF.Asin(Math.Clamp(-r.M13,-1,1));mainRotation=new Vector3(MathF.Atan2(r.M23,r.M33),y,MathF.Atan2(r.M12,r.M11))*(180/MathF.PI);}}
  if(vehicleConfiguration!=null)foreach(var node in scene.Nodes.Where(n=>!n.IsMesh&&n.Name.StartsWith("Aurora_Point_"))){var parts=node.Name[13..].Split("__");if(parts.Length!=3||!int.TryParse(parts[1],out int occurrence)||!int.TryParse(parts[2],out int start))continue;var field=VehicleScript.Fields(vehicleConfiguration).FirstOrDefault(f=>f.Key==parts[0]&&f.Occurrence==occurrence);if(field==null)continue;var args=VehicleScript.Tokens(vehicleConfiguration).Where(t=>t.Key==field.Key&&t.Occurrence==occurrence).Select(t=>t.Value).ToArray();if(start<0||args.Length<start+3)continue;var p=node.Matrix.Translation;for(int a=0;a<3;a++)args[start+a]=new[]{p.X,p.Y,p.Z}[a].ToString("R",System.Globalization.CultureInfo.InvariantCulture);vehicleConfiguration=VehicleScript.SetArguments(vehicleConfiguration,field.Key,occurrence,args);}
  ReadLampSlots();
 }
 InterchangeScene InterchangeForExport()
 {
  var model=CurrentModel();var nodes=model.Nodes.Select(n=>SceneInterchange.Object(model,n)).ToList();var helperMap=new Dictionary<int,int>();
  if(interchange!=null){for(int i=0;i<interchange.Nodes.Length;i++)if(!interchange.Nodes[i].IsMesh&&!interchange.Nodes[i].Name.StartsWith("Aurora_")){helperMap[i]=nodes.Count;nodes.Add(interchange.Nodes[i] with{Parent=-1});}
   int Find(int i){if(i<0)return -1;if(helperMap.TryGetValue(i,out int mapped))return mapped;return nodes.FindIndex(n=>n.Name==interchange.Nodes[i].Name);}
   for(int i=0;i<interchange.Nodes.Length;i++){var old=interchange.Nodes[i];int index=Find(i);if(index>=0)nodes[index]=nodes[index] with{Parent=Find(old.Parent)};}}
  void Helper(string name,Vector3 position,Matrix4x4? frame=null)=>nodes.Add(new(name,-1,SceneInterchange.Values(frame??Matrix4x4.CreateTranslation(position)),[],[],[],[],[]));
  Helper("Aurora_MainPivot",mainPivot,PivotFrame()*Matrix4x4.CreateTranslation(mainPivot));
  if(vehicleConfiguration!=null)foreach(var field in VehicleScript.Fields(vehicleConfiguration)){int start=field.Key switch{"LIGHT_FORWARD" or "LIGHT_BACKWARD" or "LIGHT_BLINKER" or "LIGHT_BLINKER_TRAILER" or "SIREN_POINT"=>0,"PARTICLE_MOVEMENT"=>1,_=>-1};if(start<0)continue;var args=VehicleScript.Tokens(vehicleConfiguration).Where(t=>t.Key==field.Key&&t.Occurrence==field.Occurrence).Skip(start).Take(3).Select(t=>float.Parse(t.Value,System.Globalization.CultureInfo.InvariantCulture)).ToArray();if(args.Length==3)Helper("Aurora_Point_"+field.Key+"__"+field.Occurrence+"__"+start,new(args[0],args[1],args[2]));}
  var materials=model.Materials.Select(m=>new SceneMaterial(m,settings.GetValueOrDefault(m)?.Base??"")).ToArray();return new(JsonSerializer.Serialize(new SceneEditorMetadata(vehicleConfiguration,[mainPivot.X,mainPivot.Y,mainPivot.Z],[mainRotation.X,mainRotation.Y,mainRotation.Z])),materials,nodes.ToArray());
 }
 async Task ExportScene(string format)
 {
  var picker=new Windows.Storage.Pickers.FileSavePicker{SuggestedFileName="model"};picker.FileTypeChoices.Add(format,new[]{"."+format.ToLowerInvariant()});WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));openingStarted=0;var file=await picker.PickSaveFileAsync();if(file==null)return;
  var scene=InterchangeForExport();if(format=="FBX")await SceneInterchange.ExportFbx(SceneBridge,file.Path,scene);else SceneInterchange.ExportObj(file.Path,scene);SetStatus("Модель экспортирована",file.Name);
 }
}
