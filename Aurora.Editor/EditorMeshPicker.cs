using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 bool meshPickJoint;
 TaskCompletionSource<string?>? meshPick;
 string? meshPickValue;
 StackPanel? meshPickPanel;Button? meshChooseButton;
 bool ReceiveMeshPick(int index)
 {
  if(meshPick==null)return false;var model=ViewportModel();meshPickValue=index>=0&&index<model.Nodes.Count?model.Nodes[index].Name:null;if(meshChooseButton!=null)meshChooseButton.IsEnabled=meshPickValue!=null;return true;
 }
 async Task<string?> PickMeshInViewport(string draft,bool joint)
 {
  string? previous=vehicleConfiguration,oldLod=selectedLod,oldSelection=objects.SelectedItem as string,confirmed=null;var oldContext=toolContext with{};bool oldVisible=gizmoVisible;var oldTimeline=animationPanel.Visibility;var oldNumbers=gizmoNumbers.Visibility;var oldEffects=effectBar?.Visibility;var parent=(Grid)view.Parent;
  meshPick=new(TaskCreationOptions.RunContinuationsAsynchronously);
  try{
   animationPanel.Visibility=gizmoNumbers.Visibility=Visibility.Collapsed;if(effectBar!=null)effectBar.Visibility=Visibility.Collapsed;processing=false;UpdateControls();
   vehicleConfiguration=draft;transportPoint=null;meshPickJoint=joint;gizmoVisible=false;SelectLod(lods.Keys.First());await Display();meshPickValue=null;
   meshPickPanel=new(){Orientation=Orientation.Horizontal,Spacing=8,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(12)};
   var choose=new Button{Content=UiLanguage.Text("Выбрать"),IsEnabled=false};meshChooseButton=choose;var cancel=new Button{Content=UiLanguage.Text("Отмена")};meshPickPanel.Children.Add(choose);meshPickPanel.Children.Add(cancel);
   choose.Click+=(_,_)=>{if(meshPickValue!=null)meshPick?.TrySetResult(meshPickValue);};cancel.Click+=(_,_)=>meshPick?.TrySetResult(null);Grid.SetColumnSpan(meshPickPanel,3);Grid.SetRow(meshPickPanel,1);parent.Children.Add(meshPickPanel);confirmed=await meshPick.Task;return confirmed;
  }finally{if(meshPickPanel!=null)parent.Children.Remove(meshPickPanel);meshPickPanel=null;meshChooseButton=null;meshPick=null;meshPickJoint=false;UpdateControls();animationPanel.Visibility=oldTimeline;gizmoNumbers.Visibility=oldNumbers;if(effectBar!=null&&oldEffects!=null)effectBar.Visibility=oldEffects.Value;vehicleConfiguration=previous;toolContext=oldContext;gizmoVisible=oldVisible;if(oldLod!=null)SelectLod(oldLod);await Display();string? restored=confirmed!=null&&!joint?confirmed:oldSelection;selectionSync=true;try{objects.SelectedItem=restored!=null&&objects.Items.Contains(restored)?restored:null;}finally{selectionSync=false;}await SyncViewportSelection();}
 }
}
