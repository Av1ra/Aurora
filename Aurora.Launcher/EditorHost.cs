using Aurora.Core;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.UI.Xaml;
namespace Aurora.Launcher;
static class EditorHost
{
 public static Window Create()
 {
  string folder=Path.Combine(AppContext.BaseDirectory,"Editor");
  string key=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Assets","publisher.pem"));
  var manifest=new ModCatalog(key).Verify(folder);
  if(manifest.Id!="aurora.editor")throw new InvalidDataException("Неверный пакет редактора.");
  string assembly=ModCatalog.SafePath(folder,"Aurora.Editor.Module.dll");
  var loaded=AssemblyLoadContext.Default.LoadFromAssemblyPath(assembly);
  return (Window)(loaded.GetType("Aurora.Editor.EditorFactory",true)!.GetMethod("Create",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,null)??throw new InvalidDataException("Редактор не открыл окно."));
 }
}
