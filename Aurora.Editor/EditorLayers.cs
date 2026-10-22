using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 readonly ListView meshGroups=new(){SelectionMode=ListViewSelectionMode.Extended,MaxHeight=240};
 readonly StackPanel layerPanel=new(){Spacing=6,Width=180};
 Border? layerBorder;bool meshEditing;
 void BuildLayers(Grid viewport)
 {
  layerPanel.Children.Add(new TextBlock{Text=UiLanguage.Text("Объекты"),FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
  layerPanel.Children.Add(objects);objects.MaxHeight=500;objects.SelectionChanged+=(_,_)=>RefreshMeshGroups();
  meshGroups.Visibility=Visibility.Collapsed;layerPanel.Children.Add(meshGroups);
  var menu=new MenuFlyout{MenuFlyoutPresenterStyle=opaqueMenuStyle};void Add(string text,Func<Task> action){var item=new MenuFlyoutItem{Text=UiLanguage.Text(text)};item.Click+=async(_,_)=>await RunCommand(action);menu.Items.Add(item);}
  Add("Редактировать",async()=>{meshEditing=!meshEditing;RefreshMeshGroups();await Task.CompletedTask;});
  Add("Отделить",DetachMeshSelection);Add("Переименовать…",RenameObject);Add("Удалить",DeleteMeshSelection);
  objects.ContextFlyout=menu;meshGroups.ContextFlyout=menu;
  objects.RightTapped+=(_,e)=>{if(e.OriginalSource is DependencyObject node){while(node!=null&&node is not ListViewItem)node=VisualTreeHelper.GetParent(node);if(node is ListViewItem item&&item.Content is string name&&!objects.SelectedItems.Contains(name))objects.SelectedItem=name;}};
  var scroll=new ScrollViewer{Content=layerPanel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};viewport.SizeChanged+=(_,_)=>scroll.MaxHeight=Math.Max(80,viewport.ActualHeight-160);
  layerBorder=new Border{Child=scroll,Padding=new Thickness(8),Margin=new Thickness(8),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Background=opaquePopup,Visibility=Visibility.Collapsed};viewport.Children.Add(layerBorder);
 }
 void RefreshMeshGroups()
 {
  meshGroups.Visibility=meshEditing?Visibility.Visible:Visibility.Collapsed;meshGroups.Items.Clear();if(!meshEditing||!HasModel||objects.SelectedIndex<0)return;
  var model=CurrentModel();var node=model.Nodes[objects.SelectedIndex];for(int i=0;i<node.Groups.Length;i++)meshGroups.Items.Add((i+1)+". "+model.Materials[node.Groups[i].Material]);
 }
 async Task ConfigureMeshLayers(){if(layerBorder!=null)layerBorder.Visibility=Visibility.Visible;meshEditing=true;RefreshMeshGroups();await Task.CompletedTask;}
 async Task ConfigureLodsWindow()
 {
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
  animationClips=animationClips.Select(c=>c with{Tracks=c.Tracks.Where(t=>!names.Contains(t.Mesh)).ToArray()}).Where(c=>c.Tracks.Length>0).ToArray();CloseAnimationTimeline();
  if(vehicleConfiguration!=null){foreach(var field in VehicleScript.Fields(vehicleConfiguration).Reverse()){var parameter=TransportParameters.All.FirstOrDefault(p=>p.Key==field.Key);if(parameter==null)continue;var tokens=VehicleScript.Tokens(vehicleConfiguration).Where(t=>t.Key==field.Key&&t.Occurrence==field.Occurrence).ToArray();if(parameter.Arguments.Where((a,i)=>a.Mesh&&i<tokens.Length&&names.Contains(tokens[i].Value)).Any())vehicleConfiguration=VehicleScript.RemoveEntry(vehicleConfiguration,field.Key,field.Occurrence);}vehicleConfigurationEdited=true;}
  dirty=true;await Display();
 }
 async Task DetachMeshSelection()
 {
  if(!HasModel||objects.SelectedIndex<0)return;var model=CurrentModel();if(model.Animated)throw new InvalidDataException("Edit the original project before skinning");
  if(!meshEditing||meshGroups.SelectedItems.Count==0){await SplitObject();return;}
  int index=objects.SelectedIndex;var node=model.Nodes[index];var chosen=meshGroups.SelectedItems.Select(x=>meshGroups.Items.IndexOf(x)).ToHashSet();
  var triangles=Enumerable.Range(0,node.IndexCount/3).Where(t=>chosen.Any(g=>t*3>=node.Groups[g].First&&t*3<node.Groups[g].First+node.Groups[g].Count)).ToHashSet();
  if(triangles.Count==node.IndexCount/3)throw new InvalidDataException(UiLanguage.Text("Выберите часть меша для отделения"));
  string name=node.Name[..Math.Min(45,node.Name.Length)]+"_detached";int suffix=1;while(model.Nodes.Any(n=>n.Name==name))name=node.Name[..Math.Min(40,node.Name.Length)]+"_detached_"+suffix++;
  var meshes=model.Nodes.Select(n=>RigidScene.Mesh(model,n)).ToList();var original=meshes[index];meshes[index]=RigidScene.Triangles(original,Enumerable.Range(0,node.IndexCount/3).Where(t=>!triangles.Contains(t)),node.Name);meshes.Add(RigidScene.Triangles(original,triangles,name));
  lods[selectedLod!]=RigidScene.Build(model.Materials,meshes);if(glassObjects[selectedLod!].Contains(node.Name))glassObjects[selectedLod!].Add(name);
  animationClips=AnimationEditing.SplitMesh(animationClips,node.Name,[node.Name,name]);dirty=true;await Display();objects.SelectedItem=name;
 }
}
