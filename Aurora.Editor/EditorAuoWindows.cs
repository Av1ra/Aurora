using Aurora.Core;
using Aurora.Launcher;
using Aurora.Shared;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Numerics;
namespace Aurora.Editor;

sealed partial class EditorWindow
{
 readonly Dictionary<FrameworkElement,Window> toolWindows=[];
 readonly HashSet<Window> floatingWindows=[];
 bool offscreenWindowCheck;
 void PresentAuoWindow(Window window){if(offscreenWindowCheck){window.AppWindow.Move(new(-20000,-20000));window.AppWindow.Show(false);}else window.Activate();}
 sealed class AuoDialogHost{public required Window Window;public required Grid Root;public bool Closed;public TaskCompletionSource<ContentDialogResult>? Pending;}
 readonly Dictionary<ContentDialog,AuoDialogHost> dialogWindows=[];
 void ReleaseDialogWindows(){foreach(var host in dialogWindows.Values.ToArray())host.Window.Close();dialogWindows.Clear();}
 async Task<ContentDialogResult> ShowAuoDialog(ContentDialog dialog)
 {
  if(!dialogWindows.TryGetValue(dialog,out var state)){
   var source=(FrameworkElement)Content;var root=new Grid{Background=opaquePopup};var window=new Window{Title="Aurora Editor"};state=new(){Window=window,Root=root};dialogWindows[dialog]=state;floatingWindows.Add(window);
   double preferred=dialog.Content is ScrollViewer scroll&&double.IsFinite(scroll.Width)?scroll.Width:380;
   double width=Math.Min(Math.Max(320,preferred+40),Math.Max(320,source.ActualWidth>0?source.ActualWidth-32:800));if(dialog.Content is FrameworkElement content)content.Measure(new Windows.Foundation.Size(width-32,double.PositiveInfinity));
   double wanted=(dialog.Content as FrameworkElement)?.DesiredSize.Height??260,height=Math.Min(Math.Max(170,wanted+98),Math.Max(220,source.ActualHeight>0?source.ActualHeight-32:650));
   WindowChrome.ResizeLogical(window,(int)Math.Ceiling(width),(int)Math.Ceiling(height));WindowChrome.Attach(window,root,"Aurora Editor");
   void Resize(){double w=root.ActualWidth>0?root.ActualWidth:width,h=root.ActualHeight>0?root.ActualHeight:height;if(dialog.Content is ScrollViewer viewer){viewer.Width=Math.Max(0,w-24);viewer.MaxHeight=Math.Max(40,h-64);}dialog.MaxWidth=Math.Max(0,w);dialog.MaxHeight=Math.Max(0,h);dialog.Resources["ContentDialogMaxWidth"]=Math.Max(0,w);}
   root.SizeChanged+=(_,_)=>Resize();Resize();var captured=state;window.Closed+=(_,_)=>{captured.Closed=true;dialog.Hide();captured.Pending?.TrySetResult(ContentDialogResult.None);dialogWindows.Remove(dialog);floatingWindows.Remove(window);};
  }
  var completion=new TaskCompletionSource<ContentDialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);state.Pending=completion;
  async void Show(){try{if(dialog.XamlRoot==null)dialog.XamlRoot=state.Root.XamlRoot;if(dialog.Parent==null)state.Root.Children.Add(dialog);var result=await dialog.ShowAsync(ContentDialogPlacement.InPlace);if(!state.Closed)state.Window.AppWindow.Hide();completion.TrySetResult(result);}catch(Exception ex){completion.TrySetException(ex);}}
  if(state.Root.IsLoaded){PresentAuoWindow(state.Window);Show();}else{RoutedEventHandler? loaded=null;loaded=(_,_)=>{state.Root.Loaded-=loaded;Show();};state.Root.Loaded+=loaded;PresentAuoWindow(state.Window);}
  return await completion.Task;
 }
 void ShowAuoToolWindow(string title,FrameworkElement content,double width=320,double height=360,Action? closed=null)
 {
  if(toolWindows.TryGetValue(content,out var existing)){PresentAuoWindow(existing);return;}
  var window=new Window{Title=UiLanguage.Text(title)};var host=new Grid{Background=opaquePopup,Padding=new Thickness(6)};var scroll=new ScrollViewer{Content=content,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalContentAlignment=HorizontalAlignment.Stretch};host.Children.Add(scroll);content.HorizontalAlignment=HorizontalAlignment.Stretch;AuoUI.Compact(content);toolWindows[content]=window;floatingWindows.Add(window);
  WindowChrome.ResizeLogical(window,(int)Math.Ceiling(width),(int)Math.Ceiling(height));WindowChrome.Attach(window,host,UiLanguage.Text(title));window.Closed+=(_,_)=>{scroll.Content=null;toolWindows.Remove(content);floatingWindows.Remove(window);closed?.Invoke();};PresentAuoWindow(window);
 }
 async Task ConfigureObjectProperties()
 {
  if(!HasModel||objects.SelectedItem is not string name)return;var model=CurrentModel();var node=model.Nodes.First(n=>n.Name==name);var panel=new StackPanel{Spacing=4,Width=360};var rename=new TextBox{Text=name};SettingRow(panel,"Название",rename);var origin=LocalObjectPivot(model,node);var size=ModelOrientation.Bounds(RigidScene.Build(model.Materials,[RigidScene.Mesh(model,node)]));var material=node.Groups.FirstOrDefault() is DrawGroup g?model.Materials[g.Material]:"";
  SettingRow(panel,"Материал",new TextBlock{Text=material,TextWrapping=TextWrapping.Wrap});SettingRow(panel,"Габариты, м",new TextBlock{Text=$"{size.Max.X-size.Min.X:F2} × {size.Max.Y-size.Min.Y:F2} × {size.Max.Z-size.Min.Z:F2}"});SettingRow(panel,"Вершины",new TextBlock{Text=node.VertexCount.ToString()});
  foreach(var a in new[]{("X",origin.X),("Y",origin.Y),("Z",origin.Z)})SettingRow(panel,"Pivot "+a.Item1,new TextBlock{Text=a.Item2.ToString("F2")});
  var dialog=VehicleDialog("Свойства объекта",panel);string next=name;ValidateVehicleDialog(dialog,panel,()=>{next=rename.Text.Trim();foreach(var value in lods.Values)ModelEditing.Rename(value,name,next);});if(await ShowDialog(dialog)!=ContentDialogResult.Primary||next==name)return;
  await RenameObjectTo(name,next);
 }
 async Task ConfigureObjectTransform()
 {
  if(!meshEditing)await BeginMeshEditing();if(!meshEditing)return;
  var panel=new StackPanel{Spacing=4,Width=350};var mode=new ComboBox{ItemsSource=new[]{UiLanguage.Text("Положение"),UiLanguage.Text("Поворот"),UiLanguage.Text("Масштаб")},SelectedIndex=gizmoMode=="rotate"?1:gizmoMode=="scale"?2:0};panel.Children.Add(mode);
  var absolute=new NumberBox[3];var offset=new NumberBox[3];var columns=new Grid{ColumnSpacing=6};columns.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});columns.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});var left=new StackPanel{Spacing=3};var right=new StackPanel{Spacing=3};columns.Children.Add(left);Grid.SetColumn(right,1);columns.Children.Add(right);left.Children.Add(new TextBlock{Text=UiLanguage.Text("Значение"),FontSize=11});right.Children.Add(new TextBlock{Text=UiLanguage.Text("Смещение"),FontSize=11});
  Grid AxisRow(int axis,NumberBox box){var row=new Grid{ColumnSpacing=3};row.ColumnDefinitions.Add(new(){Width=new GridLength(14)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.Children.Add(new TextBlock{Text="XYZ"[axis].ToString(),FontSize=11,VerticalAlignment=VerticalAlignment.Center});Grid.SetColumn(box,1);row.Children.Add(box);return row;}
  for(int i=0;i<3;i++){absolute[i]=new NumberBox();offset[i]=new NumberBox{Value=0};AuoUI.Coordinate(absolute[i]);AuoUI.Coordinate(offset[i]);left.Children.Add(AxisRow(i,absolute[i]));right.Children.Add(AxisRow(i,offset[i]));}panel.Children.Add(columns);
  void Values(){gizmoMode=mode.SelectedIndex==1?"rotate":mode.SelectedIndex==2?"scale":"move";var value=GizmoValue();for(int i=0;i<3;i++){absolute[i].Value=i==0?value.X:i==1?value.Y:value.Z;offset[i].Value=0;}}
  Values();mode.SelectionChanged+=(_,_)=>Values();var dialog=VehicleDialog("Трансформация",panel);ValidateVehicleDialog(dialog,panel,()=>{if(absolute.Concat(offset).Any(n=>!double.IsFinite(n.Value)))throw new InvalidDataException(UiLanguage.Text("Проверьте значения"));if(gizmoMode=="scale"&&absolute.Zip(offset,(a,b)=>a.Value+b.Value).Any(v=>v<=0))throw new InvalidDataException(UiLanguage.Text("Масштаб должен быть больше нуля"));});if(await ShowDialog(dialog)!=ContentDialogResult.Primary)return;
  var result=absolute.Zip(offset,(a,b)=>(float)(a.Value+b.Value)).ToArray();if(gizmoMode=="scale"&&result.Any(v=>v<=0))throw new InvalidDataException(UiLanguage.Text("Масштаб должен быть больше нуля"));SetGizmoValue(new Vector3(result[0],result[1],result[2]));dirty=true;await RefreshGizmo();
 }
 async Task EditSelectedObjectPivot(){if(!meshEditing)await BeginMeshEditing();if(!meshEditing)return;meshLevel.SelectedIndex=0;if(!meshPivotEditing)await ToggleMeshPivot();}
 FrameworkElement? floatingLightContent;
 void ShowLightWindow()
 {
  if(floatingLightContent!=null&&toolWindows.TryGetValue(floatingLightContent,out var existing)){existing.Activate();return;}
  if(rightDock?.Content is not StackPanel flow)return;var original=flow.Children.ToArray();flow.Children.Clear();rightDock.Content=null;
  var grid=new Grid{ColumnSpacing=8};grid.ColumnDefinitions.Add(new(){Width=new GridLength(165)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});grid.Children.Add(lightPalette);var tools=new StackPanel{Spacing=4};tools.Children.Add(gizmoNumbers);tools.Children.Add(gizmoActions);Grid.SetColumn(tools,1);grid.Children.Add(tools);floatingLightContent=grid;rightDock.Visibility=Visibility.Collapsed;
  ShowAuoToolWindow("Световые эффекты",grid,410,440,()=>{grid.Children.Clear();tools.Children.Clear();foreach(var item in original)flow.Children.Add(item);rightDock.Content=flow;floatingLightContent=null;LayoutRightDock();});
 }
 void VerifyAuoUiContract()
 {
  if(animationPanel.Visibility!=Visibility.Collapsed)throw new InvalidDataException("Timeline must start hidden");
  var toolbar=menuButtons.Select(p=>p.Label).ToArray();int scripts=Array.IndexOf(toolbar,"Скрипты");if(scripts<0||scripts+1>=toolbar.Length||toolbar[scripts+1]!="Анимации")throw new InvalidDataException("Animations must follow Scripts");
  foreach(string key in new[]{"Меши…","Трансформация…","Pivot объекта…","Свойства объекта…","Переименовать объект…","Удалить объект","LOD…"})if(!editorCommands.ContainsKey(key)||!menuCommands.Any(c=>c.Label==key))throw new InvalidDataException("Missing object menu command: "+key);
  if(layerPanel.Children.OfType<Button>().Any())throw new InvalidDataException("Layer list must not contain action buttons");
  if(gizmoNumbers.Orientation!=Orientation.Vertical)throw new InvalidDataException("Inspector XYZ must use aligned rows");
  var text=new TextBox{Text="0,50"};text.ApplyTemplate();text.Measure(new Windows.Foundation.Size(120,80));if(text.DesiredSize.Height>24.1)throw new InvalidDataException("AuoUI field density was not applied: "+text.DesiredSize.Height);
  var style=Application.Current.Resources[typeof(Button)] as Style;bool ownTemplate=false;for(var current=style;current!=null;current=current.BasedOn)ownTemplate|=current.Setters.OfType<Setter>().Any(s=>s.Property==Control.TemplateProperty);if(!ownTemplate)throw new InvalidDataException("AuoUI button chrome missing");
 }
 // Only test-owned windows are displayed outside the desktop, without activation.
 public async Task<object> VerifyAuoUiWindows(string captureFolder)
 {
  offscreenWindowCheck=true;Directory.CreateDirectory(captureFolder);int opened=0;var sizes=new List<object>();
  try{
   var panel=new StackPanel{Spacing=4,Width=340};SettingRow(panel,"Название",new TextBox{Text="AuоUI"});SettingRow(panel,"Материал",new ComboBox{ItemsSource=new[]{"lambert1","glass"},SelectedIndex=0});var n=new NumberBox{Value=.12};AuoUI.Coordinate(n);SettingRow(panel,"X",n);var dialog=VehicleDialog("Параметры",panel);Exception? error=null;
   dialog.Opened+=async(_,_)=>{try{opened++;if(dialog.Content is not ScrollViewer content||content.ActualWidth<=0)throw new InvalidDataException("Separate window did not arrange dialog content");sizes.Add(new{content.ActualWidth,content.ActualHeight,content.ScrollableHeight});var bitmap=new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();await bitmap.RenderAsync((UIElement)dialog.Parent);var bytes=await bitmap.GetPixelsAsync();using var stream=new Windows.Storage.Streams.InMemoryRandomAccessStream();var encoder=await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,stream);var data=new byte[bytes.Length];using(var reader=Windows.Storage.Streams.DataReader.FromBuffer(bytes))reader.ReadBytes(data);encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,data);await encoder.FlushAsync();stream.Seek(0);var output=new byte[(int)stream.Size];using(var reader=new Windows.Storage.Streams.DataReader(stream)){await reader.LoadAsync((uint)output.Length);reader.ReadBytes(output);}await File.WriteAllBytesAsync(Path.Combine(captureFolder,"native-dialog-"+opened+".png"),output);}catch(Exception ex){error=ex;}finally{dialog.Hide();}};
   for(int i=0;i<2;i++){await ShowAuoDialog(dialog).WaitAsync(TimeSpan.FromSeconds(10));if(error!=null)throw error;}
   ReleaseDialogWindows();if(opened!=2||floatingWindows.Count!=0)throw new InvalidDataException("Dialog reopen or cleanup failed");
   var tall=new StackPanel{Width=340,Spacing=4};for(int i=0;i<40;i++)SettingRow(tall,"Параметр "+i,new TextBox{Text="0,00"});var longDialog=VehicleDialog("Параметры",tall);bool scrolled=false;longDialog.Opened+=(_,_)=>{scrolled=longDialog.Content is ScrollViewer scroll&&scroll.ScrollableHeight>0&&scroll.ViewportHeight>0;longDialog.Hide();};await ShowAuoDialog(longDialog).WaitAsync(TimeSpan.FromSeconds(10));ReleaseDialogWindows();if(!scrolled)throw new InvalidDataException("Long form did not expose automatic scrolling");
   DependencyObject? Find(DependencyObject parent,string name){if(parent is FrameworkElement f&&f.Name==name)return parent;for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var found=Find(VisualTreeHelper.GetChild(parent,i),name);if(found!=null)return found;}return null;}
   var validated=VehicleDialog("Проверка",new TextBox{Text="value"});int clicks=0;Action? invoke=null;validated.PrimaryButtonClick+=(_,e)=>{clicks++;if(clicks==1){e.Cancel=true;DispatcherQueue.TryEnqueue(()=>invoke!());}};validated.Opened+=(_,_)=>{var button=(Button)(Find(validated,"PrimaryButton")??throw new InvalidDataException("Primary action missing"));invoke=()=>{var peer=new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button);((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();};invoke();};var primary=await ShowAuoDialog(validated).WaitAsync(TimeSpan.FromSeconds(10));ReleaseDialogWindows();if(primary!=ContentDialogResult.Primary||clicks!=2)throw new InvalidDataException("Primary validation/cancellation contract broken");
   var tool=new StackPanel();tool.Children.Add(new Button{Content="Parameters"});ShowAuoToolWindow("Parameters",tool);ShowAuoToolWindow("Parameters",tool);if(toolWindows.Count!=1)throw new InvalidDataException("Tool window duplicated");toolWindows[tool].Close();if(toolWindows.Count!=0||floatingWindows.Count!=0)throw new InvalidDataException("Tool window did not release content");
   return new{Opened=opened,Reopened=true,Sizes=sizes,TallFormScrolling=scrolled,PrimaryValidationCancelledThenAccepted=true,ToolWindowReused=true,WindowActivated=false};
  }finally{foreach(var window in floatingWindows.ToArray())window.Close();offscreenWindowCheck=false;}
 }
}
