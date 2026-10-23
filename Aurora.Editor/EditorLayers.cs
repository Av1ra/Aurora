using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 readonly StackPanel layerPanel=new(){Spacing=6,Width=180};
 Border? layerBorder;
 void BuildLayers(Grid viewport)
 {
  layerPanel.Children.Add(new TextBlock{Text=UiLanguage.Text("Объекты"),FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
  layerPanel.Children.Add(objects);objects.MaxHeight=500;objects.SelectionChanged+=(_,_)=>RefreshMeshGroups();
  var menu=new MenuFlyout{MenuFlyoutPresenterStyle=opaqueMenuStyle};void Add(string text,Func<Task> action){var item=new MenuFlyoutItem{Text=UiLanguage.Text(text)};item.Click+=async(_,_)=>await RunCommand(action);menu.Items.Add(item);}
  Add("Редактировать",async()=>{if(meshEditing){EndMeshEditing();gizmoVisible=false;await Display();}else await BeginMeshEditing();});
  Add("Отделить",DetachMeshSelection);Add("Переименовать…",RenameObject);Add("Удалить",DeleteMeshSelection);
  menu.Opening+=(_,_)=>{menu.Items[1].IsEnabled=meshEditing&&meshLevel.SelectedIndex==1&&meshSelected.Count>0;menu.Items[2].IsEnabled=menu.Items[3].IsEnabled=!meshEditing||meshLevel.SelectedIndex==0;};
  objects.ContextFlyout=menu;viewportObjectMenu=menu;
  objects.RightTapped+=(_,e)=>{if(e.OriginalSource is DependencyObject node){while(node!=null&&node is not ListViewItem)node=VisualTreeHelper.GetParent(node);if(node is ListViewItem item&&item.Content is string name&&!objects.SelectedItems.Contains(name))objects.SelectedItem=name;}};
  var scroll=new ScrollViewer{Content=layerPanel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};viewport.SizeChanged+=(_,_)=>scroll.MaxHeight=Math.Max(80,viewport.ActualHeight-160);
  layerBorder=new Border{Child=scroll,Padding=new Thickness(8),Margin=new Thickness(8),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Background=opaquePopup,Visibility=Visibility.Collapsed};Grid.SetColumn(layerBorder,0);viewport.Children.Add(layerBorder);
 }
 async void RefreshMeshGroups(){if(processing||selectionSync||!ready)return;try{if(animationEditing){await SelectAnimationObject(objects.SelectedItem as string);await SyncViewportSelection();return;}if(meshEditing&&objects.SelectedIndex>=0&&(meshLevel.SelectedIndex==0||objects.SelectedIndex!=meshEditNode)){meshEditNode=objects.SelectedIndex;meshSelected.Clear();if(meshLevel.SelectedIndex==0)foreach(var name in objects.SelectedItems.OfType<string>()){int at=CurrentModel().Nodes.FindIndex(n=>n.Name==name);if(at>=0)meshSelected.Add((at,-1));}ResetMeshBasis();await RefreshGizmo();}await SyncViewportSelection();}catch(Exception ex){SetStatus(ex.Message);}}
 Task ConfigureMeshLayers()=>BeginMeshEditing();
 async Task ConfigureLodsWindow()
 {
  EndMeshEditing();gizmoVisible=false;
  string script=VehicleTemplate();var drafts=CreateLodDrafts(script);var rows=new StackPanel{Spacing=8};var panel=new StackPanel{Spacing=10,Width=520};panel.Children.Add(lodChoice);panel.Children.Add(rows);var error=new InfoBar{Severity=InfoBarSeverity.Error};
  foreach(var draft in drafts)AddLodRow(rows,drafts,draft,error);
  var add=new Button{Content="+",HorizontalAlignment=HorizontalAlignment.Left};add.Click+=(_,_)=>{int next=1;while(drafts.Any(d=>d.Key=="LOD"+next))next++;var draft=new ModelLodDraft{Key="LOD"+next};drafts.Add(draft);AddLodRow(rows,drafts,draft,error);};panel.Children.Add(add);panel.Children.Add(error);
  var dialog=VehicleDialog("LOD",panel);string? updated=null;dialog.PrimaryButtonClick+=(_,e)=>{try{updated=ValidateLodDrafts(script,drafts);}catch(Exception ex){e.Cancel=true;error.Message=ex.Message;error.IsOpen=true;}};
  try{if(await ShowDialog(dialog)!=ContentDialogResult.Primary||updated==null)return;ApplyLodDrafts(drafts);vehicleConfiguration=updated;vehicleConfigurationEdited=true;dirty=true;await Display();}finally{panel.Children.Remove(lodChoice);}
 }
 async Task DeleteMeshSelection()
 {
  if(!HasModel)return;var names=objects.SelectedItems.OfType<string>().ToHashSet();if(names.Count==0)return;var model=CurrentModel();if(model.Animated)throw new InvalidDataException("Edit the original project before skinning");
  var remaining=model.Nodes.Where(n=>!names.Contains(n.Name)).ToArray();if(remaining.Length==0)throw new InvalidDataException(UiLanguage.Text("В модели должен остаться хотя бы один объект"));
  if(await ShowDialog(VehicleDialog("Удалить",new TextBlock{Text=string.Join("\n",names),TextWrapping=TextWrapping.Wrap}))!=ContentDialogResult.Primary)return;
  lods[selectedLod!]=RigidScene.Build(model.Materials,remaining.Select(n=>RigidScene.Mesh(model,n)));foreach(string name in names)glassObjects[selectedLod!].Remove(name);
  if(selectedLod==lods.Keys.First())animationClips=animationClips.Select(c=>c with{Tracks=c.Tracks.Where(t=>!names.Contains(t.Mesh)).ToArray()}).Where(c=>c.Tracks.Length>0).ToArray();CloseAnimationTimeline();
  if(selectedLod==lods.Keys.First()&&vehicleConfiguration!=null){foreach(var field in VehicleScript.Fields(vehicleConfiguration).Reverse()){var parameter=TransportParameters.All.FirstOrDefault(p=>p.Key==field.Key);if(parameter==null)continue;var tokens=VehicleScript.Tokens(vehicleConfiguration).Where(t=>t.Key==field.Key&&t.Occurrence==field.Occurrence).ToArray();if(parameter.Arguments.Where((a,i)=>a.Mesh&&i<tokens.Length&&names.Contains(tokens[i].Value)).Any())vehicleConfiguration=VehicleScript.RemoveEntry(vehicleConfiguration,field.Key,field.Occurrence);}vehicleConfigurationEdited=true;}
  dirty=true;await Display();
 }
 async Task DetachMeshSelection()
 {
  if(!meshEditing||meshLevel.SelectedIndex!=1||meshEditNode<0||meshSelected.Count==0)throw new InvalidDataException(UiLanguage.Text("Включите уровень «Элемент» и выберите часть меша"));
  var model=CurrentModel();string original=model.Nodes[meshEditNode].Name;var result=MeshElements.Detach(model,meshEditNode,meshSelected.Where(i=>i.Node==meshEditNode).Select(i=>i.Part).ToHashSet());lods[selectedLod!]=result.Model;
  if(glassObjects[selectedLod!].Contains(original))glassObjects[selectedLod!].Add(result.Name);
  if(result.RemovedOriginal){glassObjects[selectedLod!].Remove(original);if(selectedLod==lods.Keys.First())RenameAnimationMesh(original,result.Name);if(selectedLod==lods.Keys.First()&&vehicleConfiguration!=null){vehicleConfiguration=VehicleScript.RenamePart(vehicleConfiguration,original,result.Name);vehicleConfigurationEdited=true;}}
  else if(selectedLod==lods.Keys.First())animationClips=AnimationEditing.SplitMesh(animationClips,original,[original,result.Name]);
  EndMeshEditing();gizmoVisible=false;dirty=true;await Display();objects.SelectedItem=result.Name;await SyncViewportSelection();
 }
}
