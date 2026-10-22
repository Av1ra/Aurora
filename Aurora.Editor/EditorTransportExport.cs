using Aurora.Core;
using Aurora.Launcher;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 async Task<(string Item,string Name,bool Related)?> TransportExportSettings()
 {
  var panel=new StackPanel{Spacing=8,Width=420};var item=new TextBox{PlaceholderText="1234567890"};var name=new TextBox{Text=workspaceActive};var related=new CheckBox{Content=UiLanguage.Text("Включить связанные объекты"),IsChecked=true};SettingRow(panel,"Workshop ID",item);SettingRow(panel,"Имя папки машины",name);panel.Children.Add(related);var error=new InfoBar{Severity=InfoBarSeverity.Error,IsClosable=false};panel.Children.Add(error);var dialog=VehicleDialog("Комплект транспорта",panel);dialog.PrimaryButtonClick+=(_,e)=>{if(!ulong.TryParse(item.Text.Trim(),out ulong id)||id==0||!System.Text.RegularExpressions.Regex.IsMatch(name.Text.Trim(),@"^[A-Za-z0-9_-]{1,63}$")){e.Cancel=true;error.Message=UiLanguage.Text("Укажите Workshop ID и имя объекта латиницей");error.IsOpen=true;}};
  return await ShowDialog(dialog)==ContentDialogResult.Primary?(item.Text.Trim(),name.Text.Trim(),related.IsChecked==true):null;
 }
 async Task<string> ExportTransport(string root,string item,string name,bool related)
 {
  await LoadVehicleChoices();StoreWorkspaceCurrent();var objects=vehicleCatalog!.Objects.ToDictionary(o=>o.Id,o=>o.Script);var included=new List<string>();string prefix="workspace-export-"+Guid.NewGuid().ToString("N");string savedCamera=JsonSerializer.Deserialize<string>(await view.ExecuteScriptAsync("JSON.stringify(savedCamera())"))??throw new InvalidDataException("Viewport camera missing");
  try{
   foreach(var entry in workspaceDocuments.Where(p=>p.Key!=workspaceActive)){
    var data=ProjectData.Load(entry.Value,path=>SessionFile(prefix+"/"+entry.Key+"/"+path));if(data.Vehicle==null)throw new InvalidDataException(UiLanguage.Text("Настройте скрипт объекта")+": "+entry.Key);string output=await VariantExport.Write(data.Variants,data.Models,data.Glass,data.Mask,data.Document.MaskMaterial,data.Vehicle,Path.GetDirectoryName(root)!,ReadImage,(data.Document.Previews??new()).Automatic?variant=>RenderWorkspacePreviews(data,variant):null,allowWorkspaceReferences:true,animations:data.Document.Animations);string reference="aurora:"+entry.Key;objects[reference]=Path.Combine(output,"script.ini");included.Add(reference);
   }
   return await Task.Run(()=>TransportPackage.Write(root,originObjectId,item,name,objects,Path.GetDirectoryName(root)!,related,["aurora:"+workspaceActive],included));
  }finally{await Display();await view.ExecuteScriptAsync("restoreCamera("+savedCamera+")");}
 }
 async Task<(byte[] Main,byte[] Side)> RenderWorkspacePreviews(ProjectData data,MaterialVariant variant)
 {
  await view.ExecuteScriptAsync("stopAnimation()");
  var model=data.Models.Values.First();var materials=model.Materials.ToList();var nodes=model.Nodes.Select(n=>(object)new{n.Name,n.Positions,n.Normals,n.Uv,Indices=n.Indices.Select(i=>(int)i).ToArray(),n.Groups,Cargo=false}).ToList();AppendAssemblyParts(materials,nodes,data.Vehicle,Path.GetDirectoryName(data.Models.Keys.First())!,variant.Settings,independent:true);
  var previous=previewSettings;try{previewSettings=data.Document.Previews??new();await view.ExecuteScriptAsync("load("+JsonSerializer.Serialize(new{Materials=materials,Nodes=nodes})+")");foreach(var s in variant.Settings)await ApplyPreview(s.Key,s.Value);for(int i=0;i<model.Nodes.Count;i++)if(data.Glass[data.Models.Keys.First()].Contains(model.Nodes[i].Name))await view.ExecuteScriptAsync("glassObject("+i+",true)");await LoadPartTextures();await view.ExecuteScriptAsync("lighting({night:false,lights:[]})");return(await RenderPreview(256,previewSettings.Yaw,previewSettings.Pitch),await RenderPreview(128,previewSettings.SideYaw,previewSettings.SidePitch));}finally{previewSettings=previous;}
 }
}
