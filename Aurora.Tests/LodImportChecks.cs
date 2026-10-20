using Aurora.Core;
using Aurora.Editor;
using System.Numerics;
static class LodImportChecks
{
 public static async Task Run(Action<bool,string> check,Action<Action,string> reject)
 {
  string root=Path.Combine(Path.GetTempPath(),"Aurora-lod-import-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{
   string triangle="v 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl Paint\nf 1 2 3\n";var main=ObjImporter.Parse(triangle).Model;string obj=Path.Combine(root,"low.obj");File.WriteAllText(obj,triangle);var imported=LodModelImport.Read(obj,main.Materials,Matrix4x4.CreateTranslation(2,0,0));
   check(imported.NativePath==Path.Combine(root,"low.nmf")&&!File.Exists(imported.NativePath)&&File.ReadAllText(obj)==triangle,"LOD OBJ conversion creates an in-memory native model without changing source files");check(imported.Model.Nodes[0].Positions[0]==2&&new NativeModel(imported.Model.Data).Nodes.Count==1,"converted LOD obeys the document pivot transform and parses as native NMF");
   string replacementObj=Path.Combine(root,"replacement.obj");File.WriteAllText(replacementObj,triangle.Replace("v 1 0 0","v 3 0 0"));var replacement=LodModelImport.Read(replacementObj,main.Materials,Matrix4x4.Identity);check(!imported.Model.Data.SequenceEqual(replacement.Model.Data),"replacing an OBJ LOD supplies the new native geometry");
   string nmf=Path.Combine(root,"existing.nmf");File.WriteAllBytes(nmf,main.Data);check(LodModelImport.Read(nmf,main.Materials,Matrix4x4.Identity).Model.Data.SequenceEqual(main.Data),"LOD picker still accepts an existing NMF without recooking it");
   string bad=Path.Combine(root,"bad.obj");File.WriteAllText(bad,triangle.Replace("Paint","Different"));reject(()=>LodModelImport.Read(bad,main.Materials,Matrix4x4.Identity),"LOD OBJ with incompatible material names");
   string script="$TYPE VEHICLETYPE_ROAD\n$LOD1 old.nmf\n$LODDISTANCE1 100\n$LOD2 low.nmf\n$LODDISTANCE2 200\n$LODJOINT1 joint.nmf\nend\n";var remaining=VehicleLods.Compact([new("LOD2",Path.GetFileName(replacement.NativePath),200)]);string rewritten=VehicleLods.Write(script,remaining);check(remaining.Single().Key=="LOD1"&&rewritten.Contains("$LOD1 replacement.nmf")&&!rewritten.Contains("$LOD2 ")&&!rewritten.Contains("old.nmf")&&rewritten.Contains("$LODJOINT1 joint.nmf"),"removing a LOD compacts surviving slots and preserves joint LOD data");
   string diffuse=Path.Combine(root,"map.dds");File.WriteAllBytes(diffuse,DdsImage.Encode(new(1,1,[128,128,128,255])));string material=Path.Combine(root,"material.mtl");File.WriteAllText(material,NativeMaterialText.Create(main.Materials,diffuse,diffuse,diffuse));string mainPath=Path.Combine(root,"main.nmf");var models=new Dictionary<string,NativeModel>{{mainPath,main},{replacement.NativePath,replacement.Model}};var glass=models.Keys.ToDictionary(k=>k,_=>new HashSet<string>());await ModelExport.Export(models,glass,new(),material,null,null,root,p=>Task.FromResult(DdsImage.Decode(File.ReadAllBytes(p))),rewritten,useSourceScript:false);string output=Directory.GetDirectories(root,"Aurora-Model-*").Single();check(new NativeModel(File.ReadAllBytes(Path.Combine(output,"replacement.nmf"))).Data.SequenceEqual(replacement.Model.Data)&&!File.Exists(Path.Combine(output,"old.nmf"))&&!File.Exists(Path.Combine(output,"low.nmf"))&&File.ReadAllText(Path.Combine(output,"script.ini")).Contains("$LODDISTANCE1 200"),"export writes replacement NMF and distance while excluding removed LODs");
  }finally{Directory.Delete(root,true);}
 }
}
