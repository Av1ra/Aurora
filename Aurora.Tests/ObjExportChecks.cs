using Aurora.Core;
using Aurora.Editor;
using System.Text.RegularExpressions;

static class ObjExportChecks
{
 public static async Task Run(Action<bool,string> check)
 {
  string root=Path.Combine(Path.GetTempPath(),"Aurora-obj-export-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  var model=ObjImporter.Parse("v 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl Paint\nf 1 2 3").Model;
  string diffuse=Path.Combine(root,"diffuse.dds"),specular=Path.Combine(root,"specular.dds"),normal=Path.Combine(root,"normal.dds");
  File.WriteAllBytes(diffuse,DdsImage.Encode(new(1,1,[180,80,20,255])));File.WriteAllBytes(specular,DdsImage.Encode(new(1,1,[20,180,80,255])));File.WriteAllBytes(normal,DdsImage.Encode(new(1,1,[128,128,255,255])));
  string mtl=Path.Combine(root,"material.mtl");File.WriteAllText(mtl,NativeMaterialText.Create(model.Materials,diffuse,diffuse,normal));
  string main=Path.Combine(root,"main.nmf"),lod=Path.Combine(root,"lod1.nmf");var models=new Dictionary<string,NativeModel>{{main,model},{lod,model}};var glass=models.Keys.ToDictionary(k=>k,_=>new HashSet<string>());
  await ModelExport.Export(models,glass,new(){{"Paint",new(Base:diffuse,Normal:normal,Specular:specular)}},mtl,null,null,root,p=>Task.FromResult(DdsImage.Decode(File.ReadAllBytes(p))));
  string output=Directory.GetDirectories(root,"Aurora-Model-*").Single();string text=File.ReadAllText(Path.Combine(output,"material.mtl"));
  string slot=Regex.Match(text,@"\$TEXTURE_MTL 1 ([^\r\n]+)").Groups[1].Value.Trim();
  check(File.ReadAllBytes(Path.Combine(output,slot)).SequenceEqual(File.ReadAllBytes(specular)),"complete OBJ export retains a distinct native specular texture");
  check(models.Keys.All(p=>File.ReadAllBytes(Path.Combine(output,Path.GetFileName(p))).SequenceEqual(model.Data))&&!File.Exists(main)&&!File.Exists(lod),"complete OBJ export serializes in-memory NMF/LOD without source writes");
  var box=File.ReadAllBytes(Path.Combine(output,"bbox.bin"));check(box.Length==24&&BitConverter.ToSingle(box,12)==1&&BitConverter.ToSingle(box,16)==1,"OBJ export calculates native bounds instead of reusing an unrelated hitbox");
 }
}
