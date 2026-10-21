using Aurora.Core;
using System.Text;
using System.Text.RegularExpressions;
namespace Aurora.Editor;
static class VariantExport
{
 public static async Task<string> Write(IReadOnlyList<MaterialVariant> variants,Dictionary<string,NativeModel> lods,Dictionary<string,HashSet<string>> glass,string? mask,string? maskMaterial,string? script,string destination,Func<string,Task<RgbaImage>> image,Func<MaterialVariant,Task<(byte[] Main,byte[] Side)>>? previews=null,IReadOnlyList<string>? skinTargets=null)
 {
  if(variants.Count==0)throw new InvalidDataException("Выберите материал.");
  if(skinTargets!=null&&(skinTargets.Count!=variants.Count||skinTargets.Any(t=>!Regex.IsMatch(t,@"^(?:[0-9]+/)?[A-Za-z0-9_.-]+$"))))throw new InvalidDataException("Invalid vehicle skin target");
  string final=Path.Combine(destination,"Aurora-Model-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]);string stage=final+".building";Directory.CreateDirectory(stage);
  string work=Path.Combine(stage,"variant-work");Directory.CreateDirectory(work);
  try{
   for(int i=0;i<variants.Count;i++){
    var variant=variants[i];string folder=Path.Combine(work,i.ToString());Directory.CreateDirectory(folder);string nativeName=i==0?"material.mtl":"material_"+i+".mtl";
    string native=Path.Combine(folder,nativeName);string text=File.ReadAllText(variant.NativePath);
    text=Regex.Replace(text,@"(?m)^\s*\$TEXTURE(?:_MTL)?\s+(\d+)\s+([^\r\n]+)",m=>"$TEXTURE_MTL "+m.Groups[1].Value+" "+MaterialFiles.Resolve(variant.NativePath,m.Groups[2].Value));File.WriteAllText(native,text);
    await ModelExport.Export(lods,glass,variant.Settings,native,mask,maskMaterial,folder,image,script,useSourceScript:false,materialsOnly:skinTargets!=null);
    string output=Directory.GetDirectories(folder,"Aurora-Model-*").Single();var maps=Directory.GetFiles(output,"map-*").ToDictionary(p=>Path.GetFileName(p)!,p=>"v"+i+"-"+Path.GetFileName(p));
    foreach(string path in Directory.GetFiles(output)){
     string name=Path.GetFileName(path);if(name=="aurora-model.json"||(previews!=null&&name is "preview.dds" or "preview_side.dds"))continue;
     if(name.EndsWith(".mtl",StringComparison.OrdinalIgnoreCase)||name.EndsWith(".pbr",StringComparison.OrdinalIgnoreCase)){
      string content=File.ReadAllText(path);foreach(var pair in maps.OrderByDescending(p=>p.Key!.Length))content=content.Replace(pair.Key!,pair.Value,StringComparison.Ordinal);
      string target=Path.Combine(stage,name.EndsWith(".pbr",StringComparison.OrdinalIgnoreCase)?MaterialDocuments.PairedName(nativeName):name);File.WriteAllText(target,content,new UTF8Encoding(false));
     }else{
      string target=Path.Combine(stage,maps.GetValueOrDefault(name)??name);
      if(File.Exists(target)){if(!File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(path)))throw new InvalidDataException("Variant geometry differs: "+name);}else File.Copy(path,target);
     }
    }
    if(previews!=null){var pair=await previews(variant);string suffix=i==0?"":"_"+i;File.WriteAllBytes(Path.Combine(stage,"preview"+suffix+".dds"),pair.Main);File.WriteAllBytes(Path.Combine(stage,"preview_side"+suffix+".dds"),pair.Side);}
   }
   if(skinTargets!=null)File.WriteAllText(Path.Combine(stage,"workshopconfig.ini"),string.Join(Environment.NewLine,skinTargets.Select((t,i)=>"$TARGET_OBJECT_SKIN "+t+" "+(i==0?"material.mtl":"material_"+i+".mtl")))+Environment.NewLine,new UTF8Encoding(false));
   // work is a freshly created child of this export's unique staging directory.
   Directory.Delete(work,true);Directory.Move(stage,final);return final;
  }catch{File.WriteAllText(Path.Combine(stage,"EXPORT-FAILED.txt"),"Incomplete export. Original files are untouched.");throw;}
 }
}
