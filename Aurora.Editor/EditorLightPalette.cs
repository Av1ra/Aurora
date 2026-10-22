using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Numerics;
using System.Globalization;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 readonly StackPanel lightPalette=new(){Spacing=6,Width=188,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(12),Visibility=Visibility.Collapsed};
 bool lightEditing,showAllLights;string? lightPaletteKey;
 bool AllLightPreview=>lightEditing&&gizmoVisible&&showAllLights;
 void RefreshLightPalette()
 {
  lightPalette.Visibility=lightEditing&&gizmoVisible&&!animationEditing?Visibility.Visible:Visibility.Collapsed;if(lightPalette.Visibility!=Visibility.Visible)return;
  string key=(vehicleConfiguration==null?"":string.Join("|",VehicleScript.Fields(vehicleConfiguration).Where(f=>f.Key.StartsWith("LIGHT_")||f.Key=="SIREN_POINT").Select(f=>f.Key+f.Occurrence)))+"/"+activeLamp+"/"+transportPoint?.Key+"/"+transportPoint?.Occurrence;
  if(lightPaletteKey==key)return;lightPaletteKey=key;lightPalette.Children.Clear();lightPalette.Children.Add(new TextBlock{Text=UiLanguage.Text("Световые эффекты"),FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
  void Add(string label,Func<Task> action){var b=new Button{Content=UiLanguage.Text(label),HorizontalAlignment=HorizontalAlignment.Stretch};b.Click+=async(_,_)=>await RunCommand(action);lightPalette.Children.Add(b);}
  var all=new CheckBox{Content=UiLanguage.Text("Показать все"),IsChecked=showAllLights};all.Click+=async(_,_)=>{showAllLights=all.IsChecked==true;await RefreshLighting();};lightPalette.Children.Add(all);
  string[] lamps=["Левая фара","Правая фара","Левый фонарь","Правый фонарь"];
  for(int i=0;i<4;i++){int slot=i;Add(lamps[i],async()=>{bool previous=JointFrameRequested;transportPoint=null;activeSmoke=activeCargo=-1;ReadLampSlots();if(lampSlots[slot]==null){var bounds=ModelOrientation.Bounds(lods.First().Value);lampSlots[slot]=new(slot>=2,new(slot%2==0?bounds.Max.X:bounds.Min.X,bounds.Min.Y+(bounds.Max.Y-bounds.Min.Y)*.4f,slot>=2?bounds.Min.Z:bounds.Max.Z));StoreLampSlots();dirty=true;}activeLamp=slot;gizmoEditing=true;gizmoMode="move";lightPaletteKey=null;if(previous!=JointFrameRequested)await Display();else await RefreshGizmo();});}
  foreach(var corner in new[]{(true,true,"Передний левый поворотник"),(true,false,"Передний правый поворотник"),(false,true,"Задний левый поворотник"),(false,false,"Задний правый поворотник")})Add(corner.Item3,async()=>{bool previous=JointFrameRequested;await SelectIndicator(corner.Item1,corner.Item2);lightPaletteKey=null;if(previous!=JointFrameRequested)await Display();});
  if(vehicleConfiguration!=null)foreach(var field in VehicleScript.Fields(vehicleConfiguration).Where(f=>f.Key=="SIREN_POINT")){int occurrence=field.Occurrence;Add(UiLanguage.Text("Маяк")+" "+(occurrence+1),async()=>{transportPoint=new("SIREN_POINT",occurrence,0);activeLamp=activeCargo=activeSmoke=-1;gizmoEditing=true;gizmoMode="move";lightPaletteKey=null;await RefreshGizmo();});}
  Add("Добавить маяк",async()=>{int occurrence=VehicleScript.Fields(vehicleConfiguration!).Count(f=>f.Key=="SIREN_POINT");var bounds=ModelOrientation.Bounds(lods.First().Value);var p=new Vector3((bounds.Min.X+bounds.Max.X)/2,bounds.Max.Y,(bounds.Min.Z+bounds.Max.Z)/2);vehicleConfiguration=VehicleScript.SetArguments(vehicleConfiguration!,"SIREN_POINT",occurrence,new[]{p.X,p.Y,p.Z}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)).ToArray());transportPoint=new("SIREN_POINT",occurrence,0);activeLamp=activeCargo=activeSmoke=-1;vehicleConfigurationEdited=dirty=true;gizmoEditing=true;lightPaletteKey=null;await RefreshGizmo();});
  Add("Зеркальная пара",MirrorActiveLight);
 }
 async Task MirrorActiveLight()
 {
  if(vehicleConfiguration==null)return;
  Vector3 Mirror(Vector3 p){if(!Matrix4x4.Invert(PivotFrame(),out var inverse))throw new InvalidDataException("Singular pivot frame");var local=Vector3.TransformNormal(p-mainPivot,inverse);local=new(-local.X,local.Y,local.Z);return mainPivot+Vector3.TransformNormal(local,PivotFrame());}
  if(activeLamp>=0&&lampSlots[activeLamp] is VehicleLight lamp){lampSlots[activeLamp^1]=lamp with{Position=Mirror(lamp.Position)};StoreLampSlots();}
  else if(transportPoint is {Key:"LIGHT_BLINKER" or "LIGHT_BLINKER_TRAILER" or "SIREN_POINT"} point){var source=TransportPointPosition();var mirrored=Mirror(source);var values=VehicleScript.Fields(vehicleConfiguration).Where(f=>f.Key==point.Key&&f.Occurrence!=point.Occurrence).ToArray();int other=-1;float best=float.PositiveInfinity;
   foreach(var value in values){var args=VehicleScript.Tokens(vehicleConfiguration).Where(t=>t.Key==point.Key&&t.Occurrence==value.Occurrence).Take(3).Select(t=>float.Parse(t.Value,CultureInfo.InvariantCulture)).ToArray();if(args.Length!=3)continue;var p=new Vector3(args[0],args[1],args[2]);if(VehicleLights.IsLeft(p,mainPivot,PivotFrame())==VehicleLights.IsLeft(source,mainPivot,PivotFrame()))continue;if(point.Key.StartsWith("LIGHT_BLINKER")){Matrix4x4.Invert(PivotFrame(),out var inv);if(Math.Sign(Vector3.TransformNormal(p-mainPivot,inv).Z)!=Math.Sign(Vector3.TransformNormal(source-mainPivot,inv).Z))continue;}float d=Vector3.DistanceSquared(p,mirrored);if(d<best){best=d;other=value.Occurrence;}}
   if(other<0)other=VehicleScript.Fields(vehicleConfiguration).Count(f=>f.Key==point.Key);vehicleConfiguration=VehicleScript.SetArguments(vehicleConfiguration,point.Key,other,new[]{mirrored.X,mirrored.Y,mirrored.Z}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)).ToArray());vehicleConfigurationEdited=true;
  }else return;
  dirty=true;lightPaletteKey=null;await RefreshGizmo();
 }
}
