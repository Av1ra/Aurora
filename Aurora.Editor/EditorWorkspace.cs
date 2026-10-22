using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;
namespace Aurora.Editor;
sealed record WorkspaceHistoryState(string Active,string? Origin,bool Dirty,Dictionary<string,Dictionary<string,byte[]>> Documents);
sealed partial class EditorWindow
{
 readonly Dictionary<string,Dictionary<string,byte[]>> workspaceDocuments=new(StringComparer.OrdinalIgnoreCase);
 WorkspaceHistoryState? workspaceHistoryState;
 WorkspaceHistoryState CaptureWorkspaceHistory(){if(workspaceHistoryState==null||workspaceHistoryState.Active!=workspaceActive||workspaceHistoryState.Origin!=originObjectId||workspaceHistoryState.Dirty!=workspaceDirty)workspaceHistoryState=new(workspaceActive,originObjectId,workspaceDirty,workspaceDocuments.ToDictionary(p=>p.Key,p=>new Dictionary<string,byte[]>(p.Value,StringComparer.OrdinalIgnoreCase),StringComparer.OrdinalIgnoreCase));return workspaceHistoryState;}
 void RestoreWorkspaceHistory(WorkspaceHistoryState state){workspaceDocuments.Clear();foreach(var p in state.Documents)workspaceDocuments[p.Key]=new(p.Value,StringComparer.OrdinalIgnoreCase);workspaceActive=state.Active;originObjectId=state.Origin;workspaceDirty=state.Dirty;workspaceHistoryState=state;}
 string workspaceActive="vehicle";string? originObjectId;bool workspaceDirty,workspaceTransition;
 void ResetWorkspace(){workspaceDocuments.Clear();workspaceActive="vehicle";workspaceDirty=false;workspaceHistoryState=null;}
 void StoreWorkspaceCurrent(bool leaving=false){if(!HasModel)return;workspaceDocuments[workspaceActive]=CaptureProjectEntries();if(leaving)workspaceDirty|=dirty;}
 EditorProject WorkspaceDocument(Dictionary<string,byte[]> entries)=>JsonSerializer.Deserialize<EditorProject>(entries["document.json"])??throw new InvalidDataException("Invalid vehicle document");
 string WorkspaceName(string name,string? script){if(script==null)return name;var f=VehicleScript.Fields(script);return f.FirstOrDefault(f=>f.Key=="NAME_STR")?.Value.Trim('"') is {Length:>0} label?label:name;}
 VehicleObject[] TransportObjects()=>((vehicleCatalog?.Objects)??[]).Concat(workspaceDocuments.Where(p=>p.Key!=workspaceActive).Select(p=>{var d=WorkspaceDocument(p.Value);return new VehicleObject("aurora:"+p.Key,WorkspaceName(p.Key,d.Vehicle)+" ["+p.Key+"]",d.Vehicle==null?"VEHICLETYPE_ROAD":VehicleScript.Fields(d.Vehicle).FirstOrDefault(f=>f.Key=="TYPE")?.Value??"","");})).Append(new("aurora:"+workspaceActive,WorkspaceName(workspaceActive,vehicleConfiguration)+" ["+workspaceActive+"]",VehicleScript.Fields(VehicleTemplate()).First(f=>f.Key=="TYPE").Value,"")).ToArray();
 async Task SwitchWorkspace(string name){if(name==workspaceActive)return;StoreWorkspaceCurrent(true);await RestoreProjectEntries(workspaceDocuments[name],name);workspaceActive=name;MarkHistorySaved();dirty=workspaceDirty;UpdateControls();}
 string UniqueWorkspaceName(string proposed){string name=ObjImporter.Identifier(System.Text.RegularExpressions.Regex.Replace(proposed,@"[^A-Za-z0-9_-]","_"),"vehicle");int index=1;string candidate=name;while(workspaceDocuments.ContainsKey(candidate))candidate=name[..Math.Min(name.Length,55)]+"_"+index++;return candidate;}
 async Task AddWorkspaceObject(bool project)
 {
  if(workspaceDocuments.Count>=64)throw new InvalidDataException(UiLanguage.Text("Не более 64 объектов транспорта"));
  string? path=project?await Pick(".aur"):await Pick(".obj",".nmf");if(path==null)return;StoreWorkspaceCurrent(true);string name=UniqueWorkspaceName(Path.GetFileNameWithoutExtension(path));long epoch=documentEpoch;workspaceTransition=true;
  try{if(project){var source=TransportWorkspace.Unpack(await Task.Run(()=>AuroraProjectArchive.Read(path)));if(source.Objects.Count>1)throw new InvalidDataException(UiLanguage.Text("Добавляйте отдельный объект, а не весь комплект"));await RestoreProjectEntries(ProjectData.RebaseObject(source.Objects[source.Active],source.Active,name),name);}else await OpenDocument(path);if(!project&&epoch==documentEpoch)return;workspaceActive=name;workspaceDocuments[name]=CaptureProjectEntries();workspaceDirty=dirty=true;UpdateControls();}finally{workspaceTransition=false;}
 }
 void RenameWorkspace(string previous,string next)
 {
  if(!TransportWorkspace.ValidName(next)||workspaceDocuments.Keys.Any(n=>n.Equals(next,StringComparison.OrdinalIgnoreCase)&&n!=previous))throw new InvalidDataException(UiLanguage.Text("Имя объекта должно быть уникальным и содержать латинские буквы, цифры, _ или -"));if(previous==next)return;StoreWorkspaceCurrent();var values=workspaceDocuments[previous];workspaceDocuments.Remove(previous);workspaceDocuments[next]=values;
  foreach(var pair in workspaceDocuments.ToArray())workspaceDocuments[pair.Key]=ProjectData.RebaseObject(pair.Value,previous,next);
  if(workspaceActive==previous)workspaceActive=next;if(vehicleConfiguration!=null)vehicleConfiguration=TransportPackage.MapObjectReferences(vehicleConfiguration,r=>r=="aurora:"+previous?"aurora:"+next:null);workspaceDirty=dirty=true;workspaceHistoryState=null;
 }
 async Task ConfigureWorkspace()
 {
  StoreWorkspaceCurrent();var panel=new StackPanel{Spacing=8,Width=470};panel.Children.Add(new TextBlock{Text=UiLanguage.Text("Здесь отдельные машины проекта, например локомотив и вагоны. Детали одной модели редактируются через меню «Объект»."),TextWrapping=TextWrapping.Wrap});var list=new ListView{MaxHeight=230,SelectionMode=ListViewSelectionMode.Single};panel.Children.Add(list);var name=new TextBox();SettingRow(panel,"Имя объекта",name);var actions=new StackPanel{Spacing=6};panel.Children.Add(actions);var error=new InfoBar{IsClosable=false,Severity=InfoBarSeverity.Error};panel.Children.Add(error);var dialog=VehicleDialog("Транспорт проекта…",panel);dialog.PrimaryButtonText="";dialog.CloseButtonText=UiLanguage.Text("Закрыть");string[] ids=[];string? target=null;int action=0;
  void Refresh(string? selected=null){ids=workspaceDocuments.Keys.ToArray();list.ItemsSource=ids;list.SelectedIndex=Array.IndexOf(ids,selected??workspaceActive);}
  Button Button(string label){var b=new Button{Content=UiLanguage.Text(label),HorizontalAlignment=HorizontalAlignment.Stretch};actions.Children.Add(b);return b;}Button open=Button("Открыть объект"),add=Button("Добавить OBJ/NMF…"),import=Button("Добавить проект…"),rename=Button("Переименовать"),remove=Button("Удалить");
  list.SelectionChanged+=(_,_)=>name.Text=list.SelectedIndex>=0?ids[list.SelectedIndex]:"";open.Click+=(_,_)=>{if(list.SelectedIndex<0)return;target=ids[list.SelectedIndex];action=1;dialog.Hide();};add.Click+=(_,_)=>{action=2;dialog.Hide();};import.Click+=(_,_)=>{action=3;dialog.Hide();};rename.Click+=(_,_)=>{try{if(list.SelectedIndex<0)return;RenameWorkspace(ids[list.SelectedIndex],name.Text.Trim());Refresh(name.Text.Trim());error.IsOpen=false;}catch(Exception ex){error.Message=ex.Message;error.IsOpen=true;}};
  remove.Click+=(_,_)=>{try{if(list.SelectedIndex<0||workspaceDocuments.Count<=1)return;string selected=ids[list.SelectedIndex];if(workspaceDocuments.Where(p=>p.Key!=selected).Any(p=>WorkspaceDocument(p.Value).Vehicle is string s&&TransportPackage.References(s).Contains("aurora:"+selected)))throw new InvalidDataException(UiLanguage.Text("Объект используется в составе"));target=selected;action=4;dialog.Hide();}catch(Exception ex){error.Message=ex.Message;error.IsOpen=true;}};Refresh();await ShowDialog(dialog);
  if(action==1&&target!=null)await SwitchWorkspace(target);else if(action is 2 or 3)await AddWorkspaceObject(action==3);else if(action==4&&target!=null){if(target==workspaceActive)await SwitchWorkspace(workspaceDocuments.Keys.First(n=>n!=target));workspaceDocuments.Remove(target);workspaceDirty=dirty=true;workspaceHistoryState=null;}UpdateControls();
 }
}
