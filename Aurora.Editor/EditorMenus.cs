using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace Aurora.Editor;

sealed partial class EditorWindow
{
 readonly ToggleMenuFlyoutItem wireframeMenu=new(){Text=UiLanguage.Text("Полигональная сетка")};
 readonly Dictionary<string,(Func<Task> Action,Func<bool> CanExecute)> editorCommands=[];
 readonly List<(MenuFlyoutItem Item,string Label,Func<bool> CanExecute)> menuCommands=[];
 readonly List<(MenuFlyoutSubItem Item,string Label)> menuGroups=[];
 readonly List<(Button Button,string Label)> menuButtons=[];
 readonly List<FlyoutBase> editorFlyouts=[];
 readonly List<FrameworkElement> detachedMenuContents=[];
 readonly TextBlock geometryInfo=new(){FontSize=11,Opacity=.7,TextWrapping=TextWrapping.Wrap,MinWidth=160};

 void CloseEditorFlyouts(){foreach(var flyout in editorFlyouts)flyout.Hide();}
 MenuFlyoutItem MenuCommand(string key,string? label=null)
 {
  var command=editorCommands[key];var item=new MenuFlyoutItem{Text=UiLanguage.Text(label??key)};
  menuCommands.Add((item,label??key,command.CanExecute));
  item.Click+=async(_,_)=>{CloseEditorFlyouts();await RunCommand(command.Action);};return item;
 }
 MenuFlyoutSubItem MenuGroup(string label){var item=new MenuFlyoutSubItem{Text=UiLanguage.Text(label)};menuGroups.Add((item,label));return item;}
 Button ToolbarMenu(string label,FlyoutBase flyout)
 {
  flyout.Placement=FlyoutPlacementMode.BottomEdgeAlignedLeft;
  flyout.ShouldConstrainToRootBounds=true;
  editorFlyouts.Add(flyout);var button=new Button{Content=UiLanguage.Text(label),Flyout=flyout,MinHeight=30,Padding=new Thickness(10,3,10,3)};
  menuButtons.Add((button,label));return button;
 }
 void BuildMenuLayout(Grid grid)
 {
  // Reuse the existing stateful selectors and command handlers.
  var toolbar=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6,Margin=new Thickness(12,6,12,6)};
  RegisterImportCommands();RegisterVehicleCommands();RegisterMaterialCommands();RegisterProjectCommands();RegisterObjectCommands();RegisterHistoryCommands();RegisterModelSettingsCommands();
  var file=new MenuFlyout();file.Items.Add(MenuCommand("Открыть NMF…"));
  file.Items.Add(MenuCommand("Импортировать OBJ…"));file.Items.Add(MenuCommand("Открыть проект…"));
  file.Items.Add(new MenuFlyoutSeparator());
  editorCommands["Сохранить NMF…"]=(SaveNmfMaterials,()=>HasModel&&variants.Count>0);
  file.Items.Add(MenuCommand("Сохранить NMF…"));file.Items.Add(MenuCommand("Сохранить проект…"));
  file.Items.Add(new MenuFlyoutSeparator());var undoItem=MenuCommand("Отменить");undoItem.KeyboardAcceleratorTextOverride="Ctrl+Z";file.Items.Add(undoItem);var redoItem=MenuCommand("Повторить");redoItem.KeyboardAcceleratorTextOverride="Ctrl+Y";file.Items.Add(redoItem);
  file.Items.Add(new MenuFlyoutSeparator());file.Items.Add(wireframeMenu);wireframeMenu.Click+=async(_,_)=>{if(ready)try{await view.ExecuteScriptAsync("wireframe("+(wireframeMenu.IsChecked?"true":"false")+")");}catch(Exception ex){SetStatus(ex.Message);}};
  var fileButton=ToolbarMenu("Меню",file);fileButton.Content=new FontIcon{Glyph="\uE700",FontSize=14};Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(fileButton,UiLanguage.Text("Меню"));toolbar.Children.Add(fileButton);
  var selection=new StackPanel{Spacing=12,MinWidth=260,MaxWidth=340};selection.Children.Add(lodChoice);selection.Children.Add(objects);detachedMenuContents.Add(selection);
  foreach(string key in new[]{"Переименовать объект…","Разделить несвязанные меши"}){var command=editorCommands[key];var button=new Button{Content=UiLanguage.Text(key),HorizontalAlignment=HorizontalAlignment.Stretch};commandButtons.Add((button,command.CanExecute));button.Click+=async(_,_)=>{CloseEditorFlyouts();await RunCommand(command.Action);};selection.Children.Add(button);}
  toolbar.Children.Add(ToolbarMenu("Выбрать объект",new Flyout{Content=selection}));
  var modifiers=new MenuFlyout();var glass=MenuGroup("Стекло");glass.Items.Add(MenuCommand("Сделать объект стеклом","Назначить объект"));glass.Items.Add(MenuCommand("Стекло через альфа-маску…","Альфа-канал…"));glass.Items.Add(new MenuFlyoutSeparator());glass.Items.Add(MenuCommand("Сбросить стекло"));modifiers.Items.Add(glass);
  modifiers.Items.Add(MenuCommand("Фары и фонари"));
  modifiers.Items.Add(MenuCommand("Починить ориентацию…"));toolbar.Children.Add(ToolbarMenu("Модификаторы",modifiers));
  var materialPanel=new StackPanel{Spacing=10,MinWidth=260,MaxWidth=360};materialPanel.Children.Add(variantChoice);materialPanel.Children.Add(materialChoice);detachedMenuContents.Add(materialPanel);
  foreach(string key in new[]{"Настроить PBR","Создать материал…","Создать вариант…","Назначить материал объекту"}){
   var command=editorCommands[key];var button=new Button{Content=UiLanguage.Text(key=="Настроить PBR"?"Настройки материала":key),HorizontalAlignment=HorizontalAlignment.Stretch};commandButtons.Add((button,command.CanExecute));button.Click+=async(_,_)=>{CloseEditorFlyouts();await RunCommand(command.Action);};materialPanel.Children.Add(button);
  }
  toolbar.Children.Add(ToolbarMenu("Материалы",new Flyout{Content=materialPanel}));
  editorCommands["Сборка и механизмы"]=(ConfigureTransport,()=>HasModel);editorCommands["Связи и состав"]=(ConfigureConsist,()=>HasModel);editorCommands["Звуковые слои"]=(ConfigureSoundLayers,()=>HasModel);var modelSettings=new MenuFlyout();foreach(string key in new[]{"Настройки модели","Сборка и механизмы","Связи и состав","Звуковые слои","Настройки превью…"})modelSettings.Items.Add(MenuCommand(key,key=="Настройки превью…"?"Превью":key));toolbar.Children.Add(ToolbarMenu("Скрипты",modelSettings));
  grid.Children.Add(new ScrollViewer{Content=toolbar,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalScrollMode=ScrollMode.Auto});

  var footer=new Grid{Margin=new Thickness(14,8,14,8),ColumnSpacing=16,RowSpacing=4};footer.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});footer.ColumnDefinitions.Add(new(){Width=GridLength.Auto});footer.RowDefinitions.Add(new(){Height=GridLength.Auto});footer.RowDefinitions.Add(new(){Height=GridLength.Auto});
  footer.Children.Add(geometryInfo);Grid.SetColumn(dirtyText,1);footer.Children.Add(dirtyText);Grid.SetRow(status,1);Grid.SetColumnSpan(status,2);footer.Children.Add(status);Grid.SetRow(footer,2);grid.Children.Add(footer);
  busyIndicator.Height=2;busyIndicator.VerticalAlignment=VerticalAlignment.Bottom;grid.Children.Add(busyIndicator);
  foreach(var shortcut in new[]{(Windows.System.VirtualKey.O,"Открыть NMF…"),(Windows.System.VirtualKey.S,"Сохранить проект…")}){
   var accelerator=new KeyboardAccelerator{Key=shortcut.Item1,Modifiers=Windows.System.VirtualKeyModifiers.Control};accelerator.Invoked+=async(_,e)=>{e.Handled=true;var command=editorCommands[shortcut.Item2];if(!busy&&command.CanExecute()){CloseEditorFlyouts();await RunCommand(command.Action);}};grid.KeyboardAccelerators.Add(accelerator);
  }
  RefreshMenuLanguage();
  foreach(var shortcut in new[]{(Windows.System.VirtualKey.Z,"Отменить"),(Windows.System.VirtualKey.Y,"Повторить")}){var accelerator=new KeyboardAccelerator{Key=shortcut.Item1,Modifiers=Windows.System.VirtualKeyModifiers.Control};accelerator.Invoked+=async(_,e)=>{if(EditingText()||view.FocusState!=FocusState.Unfocused)return;var command=editorCommands[shortcut.Item2];if(!busy&&command.CanExecute()){e.Handled=true;CloseEditorFlyouts();await RunCommand(command.Action);}};grid.KeyboardAccelerators.Add(accelerator);}
 }
 void RefreshMenuLanguage()
 {
  wireframeMenu.Text=UiLanguage.Text("Полигональная сетка");
  foreach(var entry in menuButtons){if(entry.Button.Content is string)entry.Button.Content=UiLanguage.Text(entry.Label);else Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(entry.Button,UiLanguage.Text(entry.Label));}
  foreach(var entry in menuCommands)entry.Item.Text=UiLanguage.Text(entry.Label);
  foreach(var entry in menuGroups)entry.Item.Text=UiLanguage.Text(entry.Label);
  foreach(var content in detachedMenuContents)UiLanguage.Apply(content);
 }
 void UpdateMenuState()
 {
  wireframeMenu.IsEnabled=HasModel&&!busy&&!cameraEditing;
  foreach(var command in menuCommands)command.Item.IsEnabled=!busy&&!cameraEditing&&command.CanExecute();
  if(selectedLod==null||!lods.TryGetValue(selectedLod,out var model)){geometryInfo.Text="";return;}
  var statistics=ModelStatistics.Of(model);
  geometryInfo.Text=UiLanguage.Text("Объекты")+": "+statistics.Objects+"\n"+UiLanguage.Text("Треугольники")+": "+statistics.Triangles+"\n"+UiLanguage.Text("Вершины")+": "+statistics.Vertices;
 }
 async Task ExportNmf()
 {
  if(vehicleConfigurationEdited)throw new InvalidDataException(UiLanguage.Text("Для сохранения игровых огней используйте экспорт мода."));
  if(mask!=null)throw new InvalidDataException(UiLanguage.Text("Для альфа-маски используйте экспорт мода."));
  var destination=await Folder();if(destination==null)return;
  processing=true;UpdateControls();string output=await Task.Run(()=>NmfExport.Write(lods,glassObjects,destination));SetStatus("NMF экспортирован",output);
  // NMF-only export does not save material settings or clear project changes.
 }
}
