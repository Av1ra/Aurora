using System.Numerics;
namespace Aurora.Core;
public sealed record ImportedLod(string NativePath,NativeModel Model);
public static class LodModelImport
{
 public static ImportedLod Read(string path,IEnumerable<string> materials,Matrix4x4 transform)
 {
  bool obj=Path.GetExtension(path).Equals(".obj",StringComparison.OrdinalIgnoreCase);if(new FileInfo(path).Length>(obj?64L:128L)*1024*1024)throw new InvalidDataException("LOD model exceeds supported size");
  var model=obj?ObjImporter.Parse(File.ReadAllText(path)).Model:new NativeModel(File.ReadAllBytes(path));
  if(obj&&model.Materials.Except(materials,StringComparer.Ordinal).Any())throw new InvalidDataException("Материалы OBJ для LOD должны совпадать с основной моделью.");
  return new(Path.ChangeExtension(path,".nmf"),transform==Matrix4x4.Identity?model:ModelOrientation.Apply(model,transform));
 }
}
