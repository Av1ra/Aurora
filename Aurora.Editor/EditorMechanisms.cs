using Aurora.Core;
using Aurora.Launcher;
using System.Text.Json;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Aurora.Editor;

sealed partial class EditorWindow
{
 readonly StackPanel mechanismControls=new(){Spacing=4,Visibility=Visibility.Collapsed,Width=220};
 readonly Slider mechanismAngle=new(){Minimum=-180,Maximum=180,StepFrequency=.1};
 readonly TextBlock mechanismAngleText=new();
 bool mechanismRendering;
 Dictionary<string,float[]> mechanismRotations=[];
 string MechanismIdentity=>transportPoint==null?"":transportPoint.Key+"/"+transportPoint.Occurrence;
 MechanismAxis? PreviewMechanism(){if(!gizmoVisible||transportPoint==null||!MechanismAxis.Supports(transportPoint.Key)||vehicleConfiguration==null)return null;var axis=MechanismAxis.Read(vehicleConfiguration,transportPoint.Key,transportPoint.Occurrence);if(mechanismRotations.TryGetValue(MechanismIdentity,out var r)){var m=Matrix4x4.CreateRotationX(r[0]*MathF.PI/180)*Matrix4x4.CreateRotationY(r[1]*MathF.PI/180)*Matrix4x4.CreateRotationZ(r[2]*MathF.PI/180);axis=axis with{End=axis.Start+Vector3.TransformNormal(Vector3.UnitX,m)*Vector3.Distance(axis.Start,axis.End)};}return axis;}
 Vector3 MechanismRotation(){if(mechanismRotations.TryGetValue(MechanismIdentity,out var r))return new(r[0],r[1],r[2]);var axis=PreviewMechanism();if(axis==null)return Vector3.Zero;var d=Vector3.Normalize(axis.End-axis.Start);return new(0,-MathF.Asin(Math.Clamp(d.Z,-1,1))*180/MathF.PI,MathF.Atan2(d.Y,d.X)*180/MathF.PI);}
 void SetMechanismRotation(Vector3 r){mechanismRotations[MechanismIdentity]=[r.X,r.Y,r.Z];if(transportPoint!.Key.StartsWith("MIXER",StringComparison.Ordinal)){var axis=PreviewMechanism()!;var args=VehicleScript.Tokens(vehicleConfiguration!).Where(t=>t.Key==transportPoint.Key&&t.Occurrence==transportPoint.Occurrence).Select(t=>t.Value).ToArray();for(int i=0;i<3;i++)args[4+i]=(i==0?axis.End.X:i==1?axis.End.Y:axis.End.Z).ToString("G9",System.Globalization.CultureInfo.InvariantCulture);vehicleConfiguration=VehicleScript.SetArguments(vehicleConfiguration!,transportPoint.Key,transportPoint.Occurrence,args);}dirty=true;}

 void BuildMechanismControls()
 {
  mechanismControls.Children.Add(mechanismAngleText);mechanismControls.Children.Add(mechanismAngle);var reset=new Button{Content=UiLanguage.Text("Сбросить угол")};reset.Click+=(_,_)=>mechanismAngle.Value=0;mechanismControls.Children.Add(reset);var animate=new Button{Content=UiLanguage.Text("Ключевые кадры")};animate.Click+=async(_,_)=>await RunCommand(OpenAnimationTimeline);mechanismControls.Children.Add(animate);gizmoActions.Children.Add(mechanismControls);
  mechanismAngle.ValueChanged+=async(_,_)=>{if(mechanismRendering||gizmoUpdating||!gizmoVisible||!MechanismAxis.Supports(transportPoint?.Key))return;mechanismRendering=true;try{await RefreshMechanismPose();RefreshMechanismControls();}catch(Exception ex){SetStatus(ex.Message);}finally{mechanismRendering=false;}};
 }
 async Task RefreshMechanismPose()
 {
  var axis=PreviewMechanism();await view.ExecuteScriptAsync("poseMechanism("+JsonSerializer.Serialize(axis==null?null:new{mesh=axis.Mesh,start=new[]{axis.Start.X,axis.Start.Y,axis.Start.Z},end=new[]{axis.End.X,axis.End.Y,axis.End.Z},angle=mechanismAngle.Value,cargo=showCargo})+")");
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
