using Aurora.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Numerics;
using System.Text.Json;
namespace Aurora.Editor;
sealed record EditorSnapshot(Dictionary<string,NativeModel> Models,Dictionary<string,HashSet<string>> Glass,MaterialVariant[] Variants,int Variant,string? Selected,string? Object,string? Material,string? Mtl,string? Mask,string? MaskMaterial,string? Vehicle,Matrix4x4 Transform,Vector3 Pivot,Vector3 Rotation,VehicleLight?[] Lamps,Vector3[] LampRotations,VehicleLight[] ExtraLights,int ActiveLamp,bool Visible,bool Editing,string Mode,string Settings,NativeModel? Cargo,string? CargoPath,string? CargoMaterial,bool ShowCargo,float Fill,PreviewSettings Previews,int CargoSlot,int CargoEnd,bool UniformScale,int SmokeSlot,TransportPoint? Point,string CargoFrame,string? Bone,WorkspaceHistoryState Workspace,string Animations,string MechanismRotations);
sealed partial class EditorWindow
{
 readonly EditHistory<EditorSnapshot> history=new(SameSnapshot,s=>s.Models.Values.Sum(m=>(long)m.Data.Length),changeSize:HistoryChangeSize);
 static long HistoryChangeSize(EditorSnapshot a,EditorSnapshot b){long geometry=a.Models.Values.Where(m=>!b.Models.Values.Contains(m,ReferenceEqualityComparer.Instance)).Sum(m=>(long)m.Data.Length)+b.Models.Values.Where(m=>!a.Models.Values.Contains(m,ReferenceEqualityComparer.Instance)).Sum(m=>(long)m.Data.Length);long workspace=0;if(!ReferenceEquals(a.Workspace,b.Workspace)){var first=a.Workspace.Documents.Values.SelectMany(d=>d.Values).Distinct<byte[]>(ReferenceEqualityComparer.Instance).ToHashSet<byte[]>(ReferenceEqualityComparer.Instance);var second=b.Workspace.Documents.Values.SelectMany(d=>d.Values).Distinct<byte[]>(ReferenceEqualityComparer.Instance).ToHashSet<byte[]>(ReferenceEqualityComparer.Instance);workspace=first.Where(v=>!second.Contains(v)).Sum(v=>(long)v.Length)+second.Where(v=>!first.Contains(v)).Sum(v=>(long)v.Length);}return checked(geometry+workspace+(long)(a.Settings.Length+b.Settings.Length)*2+4096);}
 long documentEpoch,historyTraversal;
 EditorSnapshot? dragBefore,savedSnapshot;
 static bool SameSnapshot(EditorSnapshot a,EditorSnapshot b)=>ReferenceEquals(a.Workspace,b.Workspace)&&a.Models.Count==b.Models.Count&&a.Models.All(p=>b.Models.TryGetValue(p.Key,out var model)&&ReferenceEquals(p.Value,model))&&a.Glass.Count==b.Glass.Count&&a.Glass.All(p=>b.Glass.TryGetValue(p.Key,out var set)&&p.Value.SetEquals(set))&&a.Settings==b.Settings&&a.Mask==b.Mask&&a.MaskMaterial==b.MaskMaterial&&a.Vehicle==b.Vehicle&&a.CargoFrame==b.CargoFrame&&a.Bone==b.Bone&&a.Transform==b.Transform&&a.Pivot==b.Pivot&&a.Rotation==b.Rotation&&a.Lamps.SequenceEqual(b.Lamps)&&a.LampRotations.SequenceEqual(b.LampRotations)&&ReferenceEquals(a.Cargo,b.Cargo)&&a.CargoPath==b.CargoPath&&a.ShowCargo==b.ShowCargo&&a.Fill==b.Fill&&a.Previews==b.Previews&&a.Animations==b.Animations&&a.MechanismRotations==b.MechanismRotations;
 EditorSnapshot CaptureSnapshot()
 {
  StoreVariant();var materialVariants=variants.Select(v=>v with{Settings=new(v.Settings)}).ToArray();return new(new(lods),glassObjects.ToDictionary(p=>p.Key,p=>new HashSet<string>(p.Value)),materialVariants,activeVariant,selectedLod,objects.SelectedItem as string,materialChoice.SelectedItem as string,mtl,mask,maskMaterial,vehicleConfiguration,documentTransform,mainPivot,mainRotation,lampSlots.ToArray(),lampRotations.ToArray(),extraLights.ToArray(),activeLamp,gizmoVisible,gizmoEditing,gizmoMode,JsonSerializer.Serialize(materialVariants),cargoModel,cargoModelPath,cargoMtl,showCargo,cargoFill,previewSettings,activeCargo,cargoEndpoint,cargoUniform,activeSmoke,transportPoint,cargoFrame,cargoBone,CaptureWorkspaceHistory(),JsonSerializer.Serialize(animationClips),JsonSerializer.Serialize(mechanismRotations));
 }
 void NewHistoryDocument(){documentEpoch++;history.Clear();dragBefore=savedSnapshot=null;}
 void MarkHistorySaved()=>savedSnapshot=HasModel?CaptureSnapshot():null;
 void CommitHistory(EditorSnapshot before){var after=CaptureSnapshot();history.Commit(before,after);dirty=workspaceDirty||savedSnapshot==null||!SameSnapshot(after,savedSnapshot);UpdateControls();}
 void RegisterHistoryCommands()
 {
  editorCommands["Отменить"]=(async()=>await TraverseHistory(false),()=>HasModel&&history.CanUndo);
  editorCommands["Повторить"]=(async()=>await TraverseHistory(true),()=>HasModel&&history.CanRedo);
 }
 async Task TraverseHistory(bool redo)
 {
  historyTraversal++;dragBefore=null;var state=redo?history.Redo():history.Undo();await RestoreSnapshot(state);SetStatus(redo?"Действие повторено":"Действие отменено");
 }
 async Task RestoreSnapshot(EditorSnapshot state)
 {
  string? priorClip=animationEditing?ActiveClip?.Name:null,priorMesh=animationEditing?ActiveTrack?.Mesh:null;double priorFrame=animationFrame.Value;
  lods.Clear();glassObjects.Clear();lodChoice.Items.Clear();foreach(var p in state.Models){lods[p.Key]=p.Value;glassObjects[p.Key]=new(state.Glass[p.Key]);AddLodChoice(p.Key);}
  variants.Clear();variantChoice.Items.Clear();variants.AddRange(state.Variants);foreach(var v in variants)variantChoice.Items.Add((variantChoice.Items.Count+1).ToString());settings.Clear();activeVariant=-1;if(state.Variant>=0)ActivateVariant(state.Variant);else mtl=state.Mtl;
  selectedLod=state.Selected;if(selectedLod!=null)SelectLod(selectedLod);mask=state.Mask;maskMaterial=state.MaskMaterial;vehicleConfiguration=state.Vehicle;vehicleConfigurationEdited=vehicleConfiguration!=null;documentTransform=state.Transform;mainPivot=state.Pivot;mainRotation=state.Rotation;state.Lamps.CopyTo(lampSlots,0);state.LampRotations.CopyTo(lampRotations,0);extraLights.Clear();extraLights.AddRange(state.ExtraLights);activeLamp=state.ActiveLamp;gizmoVisible=state.Visible;gizmoEditing=state.Editing;gizmoMode=state.Mode;
  cargoModel=state.Cargo;cargoModelPath=state.CargoPath;cargoMtl=state.CargoMaterial;showCargo=state.ShowCargo;cargoFill=state.Fill;previewSettings=state.Previews;activeCargo=state.CargoSlot;cargoEndpoint=state.CargoEnd;cargoUniform=state.UniformScale;activeSmoke=state.SmokeSlot;transportPoint=state.Point;cargoFrame=state.CargoFrame;cargoBone=state.Bone;await view.ExecuteScriptAsync("gizmoDrag=null;drag=null;");
  animationClips=JsonSerializer.Deserialize<AnimationClip[]>(state.Animations)??[];mechanismRotations=JsonSerializer.Deserialize<Dictionary<string,float[]>>(state.MechanismRotations)??[];CloseAnimationTimeline();RestoreWorkspaceHistory(state.Workspace);await Display();objects.SelectedItem=state.Object;materialChoice.SelectedItem=state.Material;dirty=workspaceDirty||savedSnapshot==null||!SameSnapshot(state,savedSnapshot);if(priorClip!=null){animationIndex=Array.FindIndex(animationClips,c=>c.Name==priorClip);if(animationIndex>=0){animationTrack=Math.Max(0,Array.FindIndex(ActiveClip!.Tracks,t=>t.Mesh==priorMesh));animationEditing=true;animationPanel.Visibility=Visibility.Visible;if(effectBar!=null)effectBar.Visibility=Visibility.Collapsed;animationUpdating=true;animationFrame.Value=Math.Clamp(priorFrame,0,ActiveClip.LastFrame);animationUpdating=false;animationPose=ActiveTrack!.Sample((float)animationFrame.Value);gizmoVisible=gizmoEditing=true;RefreshAnimationTracks();await RefreshGizmo();}}
 }
 async Task RunHistoryCommand(Func<Task> action)
 {
  if(busy||cameraEditing)return;var before=HasModel?CaptureSnapshot():null;long epoch=documentEpoch,traversal=historyTraversal;busy=true;processing=false;UpdateControls();try{await action();}catch(Exception ex){editorNotice.Message=ex.Message;editorNotice.Severity=InfoBarSeverity.Error;editorNotice.IsOpen=true;SetStatus(ex.Message);}finally{if(before!=null&&epoch==documentEpoch&&traversal==historyTraversal&&HasModel)history.Commit(before,CaptureSnapshot());busy=false;processing=false;UpdateControls();}
 }
 bool EditingText()=>FocusManager.GetFocusedElement(((FrameworkElement)Content).XamlRoot) is TextBox or RichEditBox;
 async Task ReceiveHistory(JsonElement message){if(busy||!message.TryGetProperty("history",out var command))return;string? value=command.GetString();if(value=="undo"&&history.CanUndo)await RunCommand(()=>TraverseHistory(false));else if(value=="redo"&&history.CanRedo)await RunCommand(()=>TraverseHistory(true));}
}
