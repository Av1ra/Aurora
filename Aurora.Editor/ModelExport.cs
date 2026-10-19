using Aurora.Core;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
namespace Aurora.Editor;
static class ModelExport
{
 public static async Task Export(Dictionary<string,NativeModel> lods,Dictionary<string,HashSet<string>> glass,Dictionary<string,MaterialSettings> settings,string mtl,string? mask,string? maskMaterial,string destination,Func<string,Task<RgbaImage>> image)
 {
  if(lods.Keys.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=lods.Count)throw new InvalidDataException("LOD filenames must be unique");
  if(mask!=null&&glass.Values.Any(x=>x.Count>0))throw new InvalidDataException("Выберите один способ стекла: объекты или альфа-маска.");
  if(glass.Values.Any(x=>x.Count>0)&&glass.Values.Any(x=>x.Count==0))throw new InvalidDataException("Отметьте стеклянные объекты во всех LOD.");
  string target=Path.Combine(destination,"Aurora-Model-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));if(Directory.Exists(target))throw new IOException("Output folder already exists");
  foreach(var file in lods.Keys.Append(mtl))if(target.Equals(Path.GetDirectoryName(file),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Choose a separate output folder");
  string staging=target+".building";Directory.CreateDirectory(staging);
  try
  {
   var sourceMtl=File.ReadAllText(mtl);var materialNames=Regex.Matches(sourceMtl,@"(?m)^\s*\$SUBMATERIAL\s+([^\r\n]+)").Select(x=>x.Groups[1].Value.Trim()).ToHashSet();
   foreach(var model in lods.Values)foreach(var material in model.Materials)if(!materialNames.Contains(material))throw new InvalidDataException("В MTL нет материала: "+material);
   string outputMtl=sourceMtl;var copyNames=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
   foreach(var pair in settings)foreach(var texture in new[]{(0,pair.Value.Base),(1,pair.Value.Specular),(2,pair.Value.Normal)})if(texture.Item2!=null)outputMtl=NativeMaterialText.SetTexture(outputMtl,pair.Key,texture.Item1,texture.Item2);
   string CopyMap(string path)
   {
    path=Path.GetFullPath(path);if(copyNames.TryGetValue(path,out string? prior))return prior;
    string name="map-"+copyNames.Count+"-"+Path.GetFileName(path);File.Copy(path,Path.Combine(staging,name),false);copyNames[path]=name;return name;
   }
   // Retain every original MTL texture reference, including paint/light channels.
   outputMtl=Regex.Replace(outputMtl,@"(?m)^\s*\$TEXTURE(?:_MTL)?\s+(\d+)\s+([^\r\n]+)",match=>{string file=MaterialFiles.Resolve(mtl,match.Groups[2].Value);return "$TEXTURE_MTL "+match.Groups[1].Value+" "+CopyMap(file);});
   var rules=new StringBuilder();string F(float value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
   foreach(var pair in settings)
   {
    if(!materialNames.Contains(pair.Key))throw new InvalidDataException("Unknown material setting");var s=pair.Value;
    if(pair.Key.IndexOfAny(['{','}','\r','\n'])>=0)throw new InvalidDataException("Invalid material identifier");
    rules.AppendLine("{material "+pair.Key).AppendLine(" {workflow = painter}").AppendLine(" {preserve_native_diffuse = "+s.Native.ToString().ToLowerInvariant()+"}");
    foreach(var item in new[]{("base_color",s.Base),("normal",s.Normal),("orm",s.Orm),("metallic",s.MetalMap),("roughness",s.RoughMap),("ao",s.AoMap),("emissive",s.Emissive)})if(item.Item2!=null)rules.AppendLine(" {"+item.Item1+" = "+CopyMap(item.Item2)+"}");
    rules.AppendLine(" {metallic_factor = "+F(s.Metallic)+"}").AppendLine(" {roughness_factor = "+F(s.Roughness)+"}").AppendLine(" {ao_factor = "+F(s.Ao)+"}").AppendLine(" {normal_strength = "+F(s.NormalStrength)+"}").AppendLine(" {normal_y = "+(s.NormalY<0?"directx":"opengl")+"}").AppendLine(" {emissive_strength = "+F(s.EmissiveStrength)+"}").AppendLine(" {ibl_strength = "+F(s.Ibl)+"}").AppendLine("}");
   }
   if(mask!=null)
   {
    if(maskMaterial==null)throw new InvalidDataException("Mask material missing");
    var rgba=await image(mask);if(!rgba.Pixels.Where((_,i)=>i%4==3).Any(a=>a==0)||!rgba.Pixels.Where((_,i)=>i%4==3).Any(a=>a>0))throw new InvalidDataException("В маске нужна альфа 0 вне окон и ненулевая альфа на окнах.");
    File.WriteAllBytes(Path.Combine(staging,"glass.dds"),DdsImage.Encode(rgba));
    // Imported OBJ and future pivot edits may exist only in memory.
    string maskSources=Path.Combine(staging,"mask-sources");Directory.CreateDirectory(maskSources);
    var sourceModels=lods.Select(pair=>{string file=Path.Combine(maskSources,Path.GetFileName(pair.Key));File.WriteAllBytes(file,pair.Value.Data);return file;}).ToArray();
    var request=new{Models=sourceModels,Material=maskMaterial,Mask=Path.Combine(staging,"glass.dds"),Output=staging};
    string requestFile=Path.Combine(staging,"mask-request.json");File.WriteAllText(requestFile,JsonSerializer.Serialize(request));
    string backend=Path.Combine(AppContext.BaseDirectory,"Editor","Backend","Aurora.ModelBackend.exe");if(!File.Exists(backend))backend=Path.Combine(AppContext.BaseDirectory,"Backend","Aurora.ModelBackend.exe");
    var start=new ProcessStartInfo(backend){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};start.ArgumentList.Add(requestFile);
    using var process=Process.Start(start)??throw new IOException("Backend failed to start");var error=process.StandardError.ReadToEndAsync();var result=process.StandardOutput.ReadToEndAsync();await process.WaitForExitAsync();if(process.ExitCode!=0)throw new InvalidDataException(await error);string glassName=(await result).Trim();
    foreach(string file in sourceModels)File.Delete(file);Directory.Delete(maskSources);
    outputMtl=AddMaterial(outputMtl,maskMaterial,glassName);
    // One material block per identifier: augment a configured block rather than shadow it.
    string body=" {glass_mask = glass.dds}"+Environment.NewLine;if(settings.ContainsKey(maskMaterial)){string marker="{material "+maskMaterial+Environment.NewLine;rules.Replace(marker,marker+body);}else rules.AppendLine("{material "+maskMaterial).Append(body).AppendLine(" {preserve_native_diffuse = true}\n}");
    rules.AppendLine("{material "+glassName+"\n {glass = glass.dds}\n {opacity = 1}\n {roughness_factor = 0.15}\n {metallic_factor = 0}\n}");
   }
   else
   {
    bool withGlass=glass.Values.Any(x=>x.Count>0);
    foreach(var pair in lods)File.WriteAllBytes(Path.Combine(staging,Path.GetFileName(pair.Key)),withGlass?pair.Value.AssignGlass(glass[pair.Key]):pair.Value.Data);
    if(withGlass)
    {
     outputMtl=AddMaterial(outputMtl,lods.First().Value.Materials[lods.First().Value.Nodes.First(n=>glass[lods.First().Key].Contains(n.Name)).Groups[0].Material],"Aurora_Glass");
     rules.AppendLine("{material Aurora_Glass\n {preserve_native_diffuse = false}\n {glass = aurora:glass_default}\n {opacity = 1}\n {roughness_factor = 0.15}\n {metallic_factor = 0}\n}");
    }
   }
   string mtlName=Path.GetFileName(mtl);File.WriteAllText(Path.Combine(staging,mtlName),outputMtl,new UTF8Encoding(false));File.WriteAllText(Path.Combine(staging,Path.ChangeExtension(mtlName,"pbr")),rules.ToString(),new UTF8Encoding(false));
   string sourceFolder=Path.GetDirectoryName(lods.Keys.First())!;
   string script=Path.Combine(sourceFolder,"script.ini");
   if(File.Exists(script))
   {
    string configuration=File.ReadAllText(script);
    configuration=Regex.Replace(configuration,@"(?m)^(\s*\$LOD\d+\s+)([^\r\n]+)",m=>{string file=Path.GetFileName(m.Groups[2].Value.Trim().Replace('/',Path.DirectorySeparatorChar));if(!File.Exists(Path.Combine(staging,file)))throw new InvalidDataException("Добавьте LOD, указанный в script.ini: "+file);return m.Groups[1].Value+file;});
    File.WriteAllText(Path.Combine(staging,"script.ini"),configuration,new UTF8Encoding(false));
   }
   foreach(var optional in new[]{"bbox.bin","preview.dds","preview_side.dds"})if(File.Exists(Path.Combine(sourceFolder,optional)))File.Copy(Path.Combine(sourceFolder,optional),Path.Combine(staging,optional));
   var primary=lods.First().Value;
   if(primary.Data.AsSpan(0,8).SequenceEqual("fromObj\0"u8)){
    var bounds=new float[6];for(int axis=0;axis<3;axis++){bounds[axis]=primary.Nodes.Min(n=>n.Positions.Where((_,i)=>i%3==axis).Min());bounds[axis+3]=primary.Nodes.Max(n=>n.Positions.Where((_,i)=>i%3==axis).Max());}
    using var bbox=new BinaryWriter(File.Create(Path.Combine(staging,"bbox.bin")));foreach(float value in bounds)bbox.Write(value);
   }
   File.WriteAllText(Path.Combine(staging,"aurora-model.json"),JsonSerializer.Serialize(new{Schema=1,Requires="aurora.graphics",Version="0.1.0",Models=lods.Keys.Select(Path.GetFileName),Material=mtlName,Glass=mask!=null?"mask":glass.Values.Any(x=>x.Count>0)?"objects":"none"},new JsonSerializerOptions{WriteIndented=true}));
   Directory.Move(staging,target);
  }
  catch{File.WriteAllText(Path.Combine(staging,"EXPORT-FAILED.txt"),"Export incomplete. Do not install this folder. Original files were not modified.");throw;}
 }
 static string AddMaterial(string text,string source,string name)
 {
  var blocks=Regex.Matches(text,@"(?ms)^\s*\$SUBMATERIAL\s+([^\r\n]+)[\r\n]+.*?(?=^\s*\$SUBMATERIAL|\z)");
  var block=blocks.Single(x=>x.Groups[1].Value.Trim()==source).Value;block=Regex.Replace(block,@"(?m)^\s*\$SUBMATERIAL\s+[^\r\n]+","$SUBMATERIAL "+name);block=block.Replace("$END","");int last=text.LastIndexOf("$END",StringComparison.Ordinal);if(last<0)throw new InvalidDataException("MTL $END missing");return text[..last]+text[(last+4)..]+"\n"+block+"\n$END\n";
 }
}
