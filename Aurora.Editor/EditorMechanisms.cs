using Aurora.Core;
using Aurora.Launcher;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Aurora.Editor;

sealed partial class EditorWindow
{
 readonly StackPanel mechanismControls=new(){Spacing=4,Visibility=Visibility.Collapsed,Width=220};
 readonly Slider mechanismAngle=new(){Minimum=-180,Maximum=180,StepFrequency=.1};
 readonly TextBlock mechanismAngleText=new();
 bool mechanismRendering;
 MechanismAxis? PreviewMechanism()=>gizmoVisible&&transportPoint!=null&&MechanismAxis.Supports(transportPoint.Key)&&vehicleConfiguration!=null?MechanismAxis.Read(vehicleConfiguration,transportPoint.Key,transportPoint.Occurrence):null;
 void BuildMechanismControls()
 {
  mechanismControls.Children.Add(mechanismAngleText);mechanismControls.Children.Add(mechanismAngle);var reset=new Button{Content=UiLanguage.Text("Сбросить угол")};reset.Click+=(_,_)=>mechanismAngle.Value=0;mechanismControls.Children.Add(reset);gizmoActions.Children.Add(mechanismControls);
  mechanismAngle.ValueChanged+=async(_,_)=>{if(mechanismRendering||gizmoUpdating||busy||!gizmoVisible||!MechanismAxis.Supports(transportPoint?.Key))return;mechanismRendering=true;try{double drawn;do{drawn=mechanismAngle.Value;await RunCommand(async()=>{string? camera=JsonSerializer.Deserialize<string>(await view.ExecuteScriptAsync("JSON.stringify(savedCamera())"));await Display();if(camera!=null)await view.ExecuteScriptAsync("restoreCamera("+camera+")");});}while(drawn!=mechanismAngle.Value);}catch(Exception ex){SetStatus(ex.Message);}finally{mechanismRendering=false;}};
 }
 IEnumerable<MeshNode> MechanismPreviewNodes(NativeModel model)
 {
  try{var axis=PreviewMechanism();return axis==null||mechanismAngle.Value==0?model.Nodes:axis.Pose(model,(float)mechanismAngle.Value);}catch(Exception ex){SetStatus(ex.Message);return model.Nodes;}
 }
 void RefreshMechanismControls()
 {
  bool show=gizmoVisible&&MechanismAxis.Supports(transportPoint?.Key);mechanismControls.Visibility=show?Visibility.Visible:Visibility.Collapsed;mechanismAngleText.Text=UiLanguage.Text("Проверка оси")+": "+mechanismAngle.Value.ToString("F1")+"°";
 }
 float[][]? MechanismAxisPoints()
 {
  try{var axis=PreviewMechanism();return axis==null?null:[[axis.Start.X,axis.Start.Y,axis.Start.Z],[axis.End.X,axis.End.Y,axis.End.Z]];}catch{return null;}
 }
}
