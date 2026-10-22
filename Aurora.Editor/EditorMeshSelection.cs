using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Numerics;
using System.Text.Json;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 readonly ComboBox selectionRegion=new(){ItemsSource=new[]{UiLanguage.Text("Касание рамки"),UiLanguage.Text("Полностью внутри")},SelectedIndex=0,MinWidth=155};
 readonly ComboBox meshLevel=new(){ItemsSource=new[]{UiLanguage.Text("Объект"),UiLanguage.Text("Элемент")},SelectedIndex=1,HorizontalAlignment=HorizontalAlignment.Stretch};
 readonly List<(int Node,int Part)> meshSelected=[];
 readonly Dictionary<int,MeshElement[]> meshElements=[];
 NativeModel? meshBase;int meshEditNode=-1,meshRevision;bool selectionSync;
 Vector3 meshPivot;AnimationPose meshPose=AnimationPose.Identity;
 Button? meshDetachButton;
 MenuFlyout? viewportObjectMenu;
 ScrollViewer? rightDock;Grid? editorViewport;
 MeshElement[] Elements(int index){if(!meshElements.TryGetValue(index,out var parts))meshElements[index]=parts=MeshElements.Read(CurrentModel().Nodes[index]);return parts;}
 void ResetMeshBasis(){if(!HasModel)return;meshBase=CurrentModel();meshPose=AnimationPose.Identity;meshRevision++;var vertices=MeshEditItems().SelectMany(item=>item.vertices.Select(v=>{var n=meshBase.Nodes[item.node];return new Vector3(n.Positions[v*3],n.Positions[v*3+1],n.Positions[v*3+2]);})).ToArray();meshPivot=vertices.Length>0?(vertices.Aggregate(Vector3.Min)+vertices.Aggregate(Vector3.Max))*.5f:Vector3.Zero;}
 (int node,int[] vertices)[] MeshEditItems()=>meshSelected.GroupBy(i=>i.Node).Select(g=>(node:g.Key,vertices:g.Any(i=>i.Part<0)?Enumerable.Range(0,(meshBase??CurrentModel()).Nodes[g.Key].VertexCount).ToArray():g.SelectMany(i=>Elements(g.Key)[i.Part].Vertices).Distinct().ToArray())).ToArray();
 async Task SyncViewportSelection()
 {
  if(!ready||!HasModel)return;
  if(!meshEditing){meshSelected.Clear();foreach(var item in objects.SelectedItems){int index=objects.Items.IndexOf(item);if(index>=0)meshSelected.Add((index,-1));}}
  await view.ExecuteScriptAsync("selectionOptions("+JsonSerializer.Serialize(new{enabled=!cameraEditing,contained=selectionRegion.SelectedIndex==1,elements=meshEditing&&meshLevel.SelectedIndex==1,editNode=meshEditNode,parts=meshEditing&&meshEditNode>=0?Elements(meshEditNode):[],items=meshSelected.Select(i=>new{node=i.Node,part=i.Part}).ToArray()})+")");
 }
 async Task BeginMeshEditing()
 {
  if(!HasModel)return;if(CurrentModel().Animated)throw new InvalidDataException("Edit the rigid source model before skinning");await view.ExecuteScriptAsync("stopWheels(true)");if(wheelRun!=null)wheelRun.IsChecked=false;CloseAnimationTimeline();lightEditing=false;showAllLights=false;toolStart=CaptureSnapshot();meshEditing=true;meshEditNode=Math.Max(0,objects.SelectedIndex);meshElements.Clear();meshSelected.Clear();meshLevel.SelectedIndex=1;meshBase=CurrentModel();ResetMeshBasis();transportPoint=null;activeLamp=activeCargo=activeSmoke=-1;gizmoMode="move";gizmoVisible=gizmoEditing=true;await SyncViewportSelection();await RefreshGizmo();
 }
 void EndMeshEditing(){meshEditing=false;meshEditNode=-1;meshSelected.Clear();meshElements.Clear();meshBase=null;meshPose=AnimationPose.Identity;meshRevision++;}
 async Task ReceiveViewportSelection(JsonElement message)
 {
  if(!message.TryGetProperty("selection",out var values)||!HasModel||busy&&meshPick==null)return;var selected=values.EnumerateArray().Select(v=>(Node:v.GetProperty("node").GetInt32(),Part:v.GetProperty("part").GetInt32())).Where(v=>v.Node>=0&&v.Node<(meshPick!=null?ViewportModel():CurrentModel()).Nodes.Count).ToArray();
  if(meshPick!=null){ReceiveMeshPick(selected.FirstOrDefault((-1,-1)).Item1);return;}
  if(meshEditing){meshSelected.Clear();meshSelected.AddRange(selected.Where(v=>meshLevel.SelectedIndex==0?v.Part==-1:v.Node==meshEditNode&&v.Part>=0&&v.Part<Elements(v.Node).Length));if(meshLevel.SelectedIndex==0&&meshSelected.Count>0){meshEditNode=meshSelected[0].Node;selectionSync=true;objects.SelectedItems.Clear();foreach(var item in meshSelected)objects.SelectedItems.Add(objects.Items[item.Node]);selectionSync=false;}ResetMeshBasis();await SyncViewportSelection();await RefreshGizmo();}
  else{selectionSync=true;objects.SelectedItems.Clear();foreach(var item in selected.Where(v=>v.Part==-1))objects.SelectedItems.Add(objects.Items[item.Node]);selectionSync=false;await SyncViewportSelection();}
 }
 async Task ShowViewportContext(JsonElement message)
 {
  if(!message.TryGetProperty("meshContext",out var point)||busy||!HasModel||viewportObjectMenu==null)return;await Task.CompletedTask;viewportObjectMenu.ShowAt(view,new FlyoutShowOptions{Position=new Windows.Foundation.Point(point.GetProperty("x").GetDouble(),point.GetProperty("y").GetDouble())});
 }
 void SetMeshPose(Vector3 value){if(gizmoMode=="scale"&&cargoUniform){var old=meshPose.Scale;int axis=value.X!=old[0]?0:value.Y!=old[1]?1:2;float next=axis==0?value.X:axis==1?value.Y:value.Z;value=new Vector3(old[0],old[1],old[2])*(next/Math.Max(.0001f,old[axis]));}meshPose=gizmoMode switch{"move"=>meshPose with{Position=[value.X-meshPivot.X,value.Y-meshPivot.Y,value.Z-meshPivot.Z]},"rotate"=>meshPose with{Rotation=[value.X,value.Y,value.Z]},_=>meshPose with{Scale=[Math.Max(.0001f,value.X),Math.Max(.0001f,value.Y),Math.Max(.0001f,value.Z)]}};ApplyMeshPose();}
 void ApplyMeshPose()
 {
  if(meshBase==null||meshSelected.Count==0)return;meshPose.Validate();var r=meshPose.Rotation;var delta=Matrix4x4.CreateTranslation(-meshPivot)*Matrix4x4.CreateScale(meshPose.Scale[0],meshPose.Scale[1],meshPose.Scale[2])*Matrix4x4.CreateRotationX(r[0]*MathF.PI/180)*Matrix4x4.CreateRotationY(r[1]*MathF.PI/180)*Matrix4x4.CreateRotationZ(r[2]*MathF.PI/180)*Matrix4x4.CreateTranslation(meshPivot+new Vector3(meshPose.Position[0],meshPose.Position[1],meshPose.Position[2]));Matrix4x4.Invert(delta,out var inverse);var normal=Matrix4x4.Transpose(inverse);var meshes=meshBase.Nodes.Select(n=>RigidScene.Mesh(meshBase,n)).ToArray();
  foreach(var item in MeshEditItems()){var mesh=meshes[item.node];var p=mesh.Positions.ToArray();var n=mesh.Normals.ToArray();foreach(int v in item.vertices){var point=Vector3.Transform(new(p[v*3],p[v*3+1],p[v*3+2]),delta);var norm=Vector3.Normalize(Vector3.TransformNormal(new(n[v*3],n[v*3+1],n[v*3+2]),normal));for(int a=0;a<3;a++){p[v*3+a]=new[]{point.X,point.Y,point.Z}[a];n[v*3+a]=new[]{norm.X,norm.Y,norm.Z}[a];}}meshes[item.node]=mesh with{Positions=p,Normals=n,Frame=meshSelected.Any(s=>s.Node==item.node&&s.Part<0)?mesh.Frame*delta:mesh.Frame};}
  lods[selectedLod!]=RigidScene.Build(meshBase.Materials,meshes);dirty=true;
 }
 async Task RefreshMeshPreview(){if(!ready)return;await view.ExecuteScriptAsync("meshPreview("+(meshEditing?JsonSerializer.Serialize(new{revision=meshRevision,pivot=new[]{meshPivot.X,meshPivot.Y,meshPivot.Z},items=MeshEditItems().Select(i=>new{node=i.node,vertices=i.vertices}).ToArray()}):"null")+")");}
 public void VerifyViewportLayout()
 {
  if(rightDock?.Content is not StackPanel flow||!ReferenceEquals(lightPalette.Parent,flow)||!ReferenceEquals(gizmoActions.Parent,flow)||rightDock.VerticalScrollBarVisibility!=ScrollBarVisibility.Auto)throw new InvalidDataException("Viewport controls must share one automatic-scroll flow");
  string? oldScript=vehicleConfiguration;bool oldLight=lightEditing,oldVisible=gizmoVisible;try{vehicleConfiguration="$TYPE VEHICLETYPE_ROAD\n$SIREN_POINT -1 2 0\n$SIREN_POINT 1 2 0\n$END\n";lightEditing=gizmoVisible=true;RefreshLightPalette();gizmoActions.Visibility=Visibility.Visible;rightDock.Visibility=Visibility.Visible;foreach(double height in new[]{180d,320d,600d}){rightDock.MaxHeight=height;rightDock.Measure(new Windows.Foundation.Size(280,height));rightDock.Arrange(new Windows.Foundation.Rect(0,0,280,rightDock.DesiredSize.Height));if(rightDock.DesiredSize.Height>height+.1||rightDock.DesiredSize.Width>280.1)throw new InvalidDataException("Viewport control dock exceeds available size");}}finally{vehicleConfiguration=oldScript;lightEditing=oldLight;gizmoVisible=oldVisible;rightDock.Visibility=Visibility.Collapsed;lightPalette.Visibility=gizmoActions.Visibility=Visibility.Collapsed;lightPaletteKey=null;}
 }
 void LayoutRightDock(){if(rightDock==null||editorViewport==null)return;double bottom=animationEditing?animationPanel.ActualHeight+24:12;rightDock.MaxHeight=Math.Max(1,editorViewport.ActualHeight-bottom-12);rightDock.Margin=new Thickness(12,0,12,bottom);rightDock.Visibility=gizmoVisible?Visibility.Visible:Visibility.Collapsed;}
}
