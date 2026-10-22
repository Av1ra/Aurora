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
  string? previous=vehicleConfiguration,oldLod=selectedLod,oldSelection=objects.SelectedItem as string;var oldPoint=transportPoint;bool oldVisible=gizmoVisible;var parent=(Grid)view.Parent;
  meshPick=new(TaskCreationOptions.RunContinuationsAsynchronously);
  try{
   vehicleConfiguration=draft;transportPoint=null;meshPickJoint=joint;gizmoVisible=false;SelectLod(lods.Keys.First());await Display();meshPickValue=null;
   meshPickPanel=new(){Orientation=Orientation.Horizontal,Spacing=8,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(12)};
   var choose=new Button{Content=UiLanguage.Text("Выбрать"),IsEnabled=false};meshChooseButton=choose;var cancel=new Button{Content=UiLanguage.Text("Отмена")};meshPickPanel.Children.Add(choose);meshPickPanel.Children.Add(cancel);
   choose.Click+=(_,_)=>{if(meshPickValue!=null)meshPick?.TrySetResult(meshPickValue);};cancel.Click+=(_,_)=>meshPick?.TrySetResult(null);parent.Children.Add(meshPickPanel);return await meshPick.Task;
  }finally{if(meshPickPanel!=null)parent.Children.Remove(meshPickPanel);meshPickPanel=null;meshChooseButton=null;meshPick=null;meshPickJoint=false;vehicleConfiguration=previous;transportPoint=oldPoint;gizmoVisible=oldVisible;if(oldLod!=null)SelectLod(oldLod);await Display();if(oldSelection!=null&&objects.Items.Contains(oldSelection))objects.SelectedItem=oldSelection;}
 }
}
