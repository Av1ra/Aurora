using Aurora.Core;
using Aurora.Editor;
using System.Text;
using System.Text.RegularExpressions;
static class EditorRevisionChecks
{
 public static async Task Run(Action<bool,string> check,Action<Action,string> reject)
 {
  string root=Path.Combine(Path.GetTempPath(),"Aurora-revision-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   string red=Path.Combine(root,"red.dds"),blue=Path.Combine(root,"blue.dds"),normal=Path.Combine(root,"normal.dds");File.WriteAllBytes(red,DdsImage.Encode(new(1,1,[220,50,20,255])));File.WriteAllBytes(blue,DdsImage.Encode(new(1,1,[20,50,220,255])));File.WriteAllBytes(normal,DdsImage.Encode(new(1,1,[128,128,255,255])));
   string native=Path.Combine(root,"material.mtl");File.WriteAllText(native,NativeMaterialText.Create(["Paint"],red,red,normal));var maps=MaterialDocuments.Native(native);
   check(maps["Paint"].Base==red&&maps["Paint"].Specular==red&&maps["Paint"].Normal==normal,"native MTL hydrates editable settings for all three slots");
   string pbr=Path.Combine(root,"material_pbr.mtl");File.WriteAllText(pbr,"{material Paint\n {base_color = blue.dds}\n {roughness_factor = 0.27}\n}\n");var authored=MaterialDocuments.Pbr(pbr,maps);
   check(authored["Paint"].Base==red&&authored["Paint"].PbrBase==blue&&authored["Paint"].Roughness==.27f,"native and PBR colours remain separate on import");
   check(MaterialDocuments.PairedName("material_12.mtl")=="material_pbr_12.mtl"&&MaterialDocuments.PairedName("material.mtl")=="material_pbr.mtl","numbered companion names match game material variants");
   string obj="mtllib paint.mtl\no Wheels\nv -3 0 0\nv -2 0 0\nv -3 1 0\nv 2 0 0\nv 3 0 0\nv 2 1 0\nusemtl Paint\nf 1 2 3\nf 4 5 6\n";File.WriteAllText(Path.Combine(root,"paint.mtl"),"newmtl Paint\nmap_Kd blue.dds\nmap_Ks red.dds\nmap_Bump normal.dds\n");var imported=ObjImporter.Parse(obj);var wave=MaterialDocuments.Obj(Path.Combine(root,"car.obj"),imported);
   check(wave["Paint"].Base==blue&&wave["Paint"].Specular==red&&wave["Paint"].Height==normal&&wave["Paint"].Normal==null,"Wavefront libraries hydrate maps by usemtl identifier");
   var model=imported.Model;var split=ModelEditing.Split(model,"Wheels");check(split.Nodes.Count==2&&split.Nodes.Sum(n=>n.IndexCount)==6&&split.Nodes[0].Positions.Where((_,i)=>i%3==0).Max()<0,"disconnected wheel meshes split in stable left-to-right order");
   var renamed=ModelEditing.Rename(split,split.Nodes[0].Name,"tire_leftShape");check(renamed.Nodes[0].Name=="tire_leftShape"&&renamed.Nodes[0].Positions.SequenceEqual(split.Nodes[0].Positions),"wheel rename preserves placement");
   var extra=ModelEditing.AddMaterial(renamed,"Glass");var assigned=ModelEditing.Assign(extra,"tire_leftShape","Glass");check(assigned.Nodes[0].Groups.All(g=>assigned.Materials[g.Material]=="Glass")&&assigned.Nodes[1].Groups.All(g=>assigned.Materials[g.Material]=="Paint"),"new submaterial assignment only affects selected object");
   reject(()=>ModelEditing.Rename(split,split.Nodes[0].Name,split.Nodes[1].Name),"duplicate object name");
   var entries=new Dictionary<string,byte[]>{{"document.json",Encoding.UTF8.GetBytes("{\"Schema\":1}")},{"models/main.nmf",model.Data},{"assets/colour.dds",File.ReadAllBytes(red)}};string archive=Path.Combine(root,"model.aur");AuroraProjectArchive.Write(archive,entries);var saved=File.ReadAllBytes(archive);var restored=AuroraProjectArchive.Read(archive);
   check(restored.Keys.Order().SequenceEqual(entries.Keys.Order())&&entries.All(p=>p.Value.SequenceEqual(restored[p.Key]))&&!Encoding.UTF8.GetString(saved).Contains("Schema"),"encrypted passwordless project retains exact portable entries");
   check(!AuroraProjectArchive.IsPortablePath("model:stream.nmf")&&!AuroraProjectArchive.IsPortablePath("assets/CON.dds")&&!AuroraProjectArchive.IsPortablePath("assets/trailing."),"project entries reject Windows streams and device aliases");
   saved[^1]^=1;File.WriteAllBytes(archive,saved);reject(()=>AuroraProjectArchive.Read(archive),"tampered project authentication");reject(()=>AuroraProjectArchive.Write(archive,new Dictionary<string,byte[]>{{"../escape",[]}}),"project traversal entry");
   string main=Path.Combine(root,"main.nmf");var lods=new Dictionary<string,NativeModel>{{main,model}};int captures=0;string output=await VariantExport.Write([new("material",native,maps),new("material_1",native,authored)],lods,new(){{main,[]}},null,null,null,root,p=>Task.FromResult(DdsImage.Decode(File.ReadAllBytes(p))),v=>{captures++;return Task.FromResult((DdsImage.Encode(new(256,256,new byte[256*256*4])),DdsImage.Encode(new(256,128,new byte[256*128*4]))));});
   check(captures==2&&new[]{"preview.dds","preview_side.dds","preview_1.dds","preview_side_1.dds"}.All(name=>File.Exists(Path.Combine(output,name)))&&DdsImage.Decode(File.ReadAllBytes(Path.Combine(output,"preview_side_1.dds"))).Height==128,"automatic per-variant previews export at native filenames and dimensions");
   check(new[]{"main.nmf","material.mtl","material_1.mtl","material_pbr.mtl","material_pbr_1.mtl"}.All(n=>File.Exists(Path.Combine(output,n)))&&!File.Exists(Path.Combine(output,"pbr.mtl")),"skin export has one model and individually paired numbered materials");
   string pbrText=File.ReadAllText(Path.Combine(output,"material_pbr_1.mtl"));string map=Regex.Match(pbrText,@"\{base_color = ([^}]+)\}").Groups[1].Value.Trim();check(File.ReadAllBytes(Path.Combine(output,map)).SequenceEqual(File.ReadAllBytes(blue))&&pbrText.Contains("0.27"),"second skin retains its own PBR maps/scalars without cross-variant collisions");
   var skinRoot=Path.Combine(root,"skins");Directory.CreateDirectory(skinRoot);var skinVariants=new[]{new MaterialVariant("one",native,new(wave)),new MaterialVariant("two",native,new(wave))};string skinFolder=await VariantExport.Write(skinVariants,new(){[Path.Combine(root,"main.nmf")]=imported.Model},new(){[Path.Combine(root,"main.nmf")]=[]},null,null,null,skinRoot,p=>Task.FromResult(DdsImage.Decode(File.ReadAllBytes(p))),skinTargets:["bus_ikr_260","bus_ikr_260"]);check(!Directory.EnumerateFiles(skinFolder,"*.nmf",SearchOption.AllDirectories).Any()&&!File.Exists(Path.Combine(skinFolder,"script.ini"))&&File.ReadAllText(Path.Combine(skinFolder,"workshopconfig.ini")).Contains("$TARGET_OBJECT_SKIN bus_ikr_260 material_1.mtl"),"standalone skin export contains indexed native and PBR materials without geometry or vehicle script");
  }finally{Directory.Delete(root,true);}
 }
}
