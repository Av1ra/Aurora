using Aurora.Core;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
namespace Aurora.Editor;

sealed record MaterialVariant(string Name,string NativePath,Dictionary<string,MaterialSettings> Settings);
static class MaterialDocuments
{
 public static MaterialSettings Remap(MaterialSettings s,Func<string?,string?> map)=>s with{Height=map(s.Height),Base=map(s.Base),Normal=map(s.Normal),Specular=map(s.Specular),PbrBase=map(s.PbrBase),PbrNormal=map(s.PbrNormal),Orm=map(s.Orm),MetalMap=map(s.MetalMap),RoughMap=map(s.RoughMap),AoMap=map(s.AoMap),Emissive=map(s.Emissive),Extra=s.Extra?.ToDictionary(p=>p.Key,p=>p.Key is "glass" or "glass_mask"&&p.Value!="none"&&!p.Value.StartsWith("aurora:",StringComparison.Ordinal)?map(p.Value)!:p.Value)};
 public static string? ResourceMaterial(string model){string folder=Path.GetDirectoryName(model)!;string stem=Path.GetFileNameWithoutExtension(model);string[] stems=[stem,Regex.Replace(stem,@"_vehicle$|_open$|\d+$","")];return stems.Select(n=>Path.Combine(folder,n+".mtl")).FirstOrDefault(File.Exists);}
 public static Dictionary<string,MaterialSettings> ResourceMaps(NativeModel model,string material){var native=Native(material);if(MaterialFiles.Companion(material) is string companion)native=Pbr(companion,native);if(model.Materials.All(native.ContainsKey))return model.Materials.ToDictionary(n=>n,n=>native[n]);if(native.Count!=model.Materials.Length)throw new InvalidDataException("Cargo material count differs from model");var values=native.Values.ToArray();return model.Materials.Select((n,i)=>(n,i)).ToDictionary(p=>p.n,p=>values[p.i]);}
 public static string PairedName(string native)=>MaterialFiles.PairedName(native);
 public static string? FindPbr(string native)=>MaterialFiles.Companion(native);
 public static Dictionary<string,MaterialSettings> Native(string file,NativeModel? model=null)
 {
  string text=File.ReadAllText(file);if(model!=null)text=NativeMaterialText.AlignNames(text,model.Materials);var result=new Dictionary<string,MaterialSettings>();string? name=null;
  foreach(string line in text.Split('\n')){
   var header=Regex.Match(line,@"^\s*\$SUBMATERIAL\s+(.+?)\s*$");if(header.Success){name=header.Groups[1].Value;if(!result.TryAdd(name,new()))throw new InvalidDataException("Duplicate material: "+name);continue;}
   var map=Regex.Match(line,@"^\s*\$TEXTURE(?:_MTL)?\s+([012])\s+(.+?)\s*$");if(name==null||!map.Success)continue;
   string path=MaterialFiles.Resolve(file,map.Groups[2].Value);var value=result[name];result[name]=map.Groups[1].Value switch{"0"=>value with{Base=path},"1"=>value with{Specular=path},_=>value with{Normal=path}};
  }
  if(result.Count==0)throw new InvalidDataException("No native submaterials in MTL");foreach(string material in result.Keys.ToArray())result[material]=result[material] with{NativeDiffuse=NativeMaterialText.Property(text,material,"DIFFUSECOLOR"),NativeAmbient=NativeMaterialText.Property(text,material,"AMBIENTCOLOR"),NativeSpecular=NativeMaterialText.Property(text,material,"SPECULARCOLOR"),NativePower=NativeMaterialText.Property(text,material,"SPECULARPOWER")?.FirstOrDefault()};return result;
 }
 public static Dictionary<string,MaterialSettings> Pbr(string file,IReadOnlyDictionary<string,MaterialSettings> current)
 {
  var result=current.ToDictionary(p=>p.Key,p=>p.Value);string? material=null;var defaults=new List<(string,string)>();var blocks=new Dictionary<string,List<(string,string)>>();
  foreach(string raw in File.ReadLines(file)){
   string line=raw.Trim();if(line.Length==0||line.StartsWith('#')||line.StartsWith("//",StringComparison.Ordinal))continue;
   var header=Regex.Match(line,@"^\{material\s+(.+?)\s*$");if(header.Success){material=header.Groups[1].Value.Trim('"');if(!blocks.TryAdd(material,[]))throw new InvalidDataException("Duplicate PBR material: "+material);continue;}
   if(line=="}"){material=null;continue;}
   var pair=Regex.Match(line,@"^\{\s*([\w]+)\s*=\s*(.*?)\s*\}$");if(!pair.Success)throw new InvalidDataException("Unsupported PBR line: "+line);
   (material==null?defaults:blocks[material]).Add((pair.Groups[1].Value,pair.Groups[2].Value.Trim('"')));
  }
  foreach(string name in result.Keys.ToArray()){
   var s=result[name] with{Native=false,Metallic=0,Roughness=.65f,Ao=1,NormalStrength=1,EmissiveStrength=1,Ibl=1};var extra=new Dictionary<string,string>();var values=defaults.Concat(blocks.GetValueOrDefault("default")??[]).Concat(blocks.GetValueOrDefault(name)??[]).ToArray();foreach(var (key,value) in values){
    float F(){float number=float.Parse(value,CultureInfo.InvariantCulture);if(!float.IsFinite(number))throw new InvalidDataException("Nonfinite PBR value");return number;}
    string? Map()=>value is "none" or "default"||value.StartsWith("aurora:",StringComparison.Ordinal)?null:MaterialFiles.Resolve(file,value);
    s=key switch{
     "base_color"=>s with{PbrBase=Map()},"normal"=>s with{PbrNormal=Map()},"orm"=>s with{Orm=Map()},"metallic"=>s with{MetalMap=Map()},"roughness"=>s with{RoughMap=Map()},"ao"=>s with{AoMap=Map()},"emissive"=>s with{Emissive=Map()},
     "metallic_factor"=>s with{Metallic=F()},"roughness_factor"=>s with{Roughness=F()},"ao_factor"=>s with{Ao=F()},"normal_strength"=>s with{NormalStrength=F()},"emissive_strength"=>s with{EmissiveStrength=F()},"ibl_strength"=>s with{Ibl=F()},"normal_y"=>s with{NormalY=value=="directx"?-1:1},"preserve_native_diffuse"=>s with{Native=bool.Parse(value)},_=>Preserve()};
    MaterialSettings Preserve(){extra[key]=key is "glass" or "glass_mask"&&value is not "none"&&!value.StartsWith("aurora:",StringComparison.Ordinal)?MaterialFiles.Resolve(file,value):value;return s;}
   }
   if(!values.Any(p=>p.Item1=="metallic_factor")&&values.Any(p=>p.Item1 is "metallic" or "orm"))s=s with{Metallic=1};
   if(!values.Any(p=>p.Item1=="roughness_factor")&&values.Any(p=>p.Item1 is "roughness" or "orm"))s=s with{Roughness=1};
   result[name]=s with{Extra=extra};
  }return result;
 }
 public static Dictionary<string,MaterialSettings> Obj(string obj,ObjImportResult imported,Dictionary<string,Vector4>? transforms=null,List<string>? warnings=null)
 {
  var result=new Dictionary<string,MaterialSettings>();
  foreach(string reference in imported.Libraries.SelectMany(reference=>{try{return new[]{MaterialFiles.Resolve(obj,reference)};}catch(FileNotFoundException){return Regex.Matches(reference,"\"[^\"]+\"|\\S+").Select(m=>MaterialFiles.Resolve(obj,m.Value.Trim('"'))).ToArray();}})){
   string library=reference;string? name=null;
   foreach(string raw in File.ReadLines(library)){
    string line=raw.Trim();if(line.StartsWith("newmtl ",StringComparison.Ordinal)){string source=line[7..].Trim();name=imported.MaterialNames.GetValueOrDefault(source);if(name!=null)result.TryAdd(name,new());continue;}
    if(name==null)continue;var scalar=Regex.Match(line,@"^(Kd|Ks|Ka|Ns)\s+(.+)$");if(scalar.Success){string key=scalar.Groups[1].Value;int count=key=="Ns"?1:3;var values=scalar.Groups[2].Value.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Take(count).Select(v=>float.Parse(v,CultureInfo.InvariantCulture)).ToArray();if(values.Length!=count||values.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Invalid OBJ material value: "+line);float[] color=[..values,1];var existing=result[name];result[name]=key switch{"Kd"=>existing with{NativeDiffuse=color},"Ks"=>existing with{NativeSpecular=color},"Ka"=>existing with{NativeAmbient=color},_=>existing with{NativePower=values[0]}};continue;}var pair=Regex.Match(line,@"^(map_Kd|map_Ks|map_Bump|map_bump|bump|norm|map_Pr|map_Pm|map_Ka|map_Ke)\s+(.+)$");if(!pair.Success)continue;
    var options=ObjTextureMap.Parse(pair.Groups[2].Value);var transform=new Vector4(options.Scale,options.Offset.X,options.Offset.Y);
    if(transforms!=null){if(transforms.TryGetValue(name,out var previous)&&previous!=transform)throw new InvalidDataException("Different texture transforms in material: "+name);transforms[name]=transform;}else if(transform!=new Vector4(1,1,0,0))throw new InvalidDataException("Texture transform requires UV conversion: "+name);
    foreach(string warning in options.Warnings)warnings?.Add(name+": "+warning);if(options.Clamp)warnings?.Add(name+": clamp");
    string path=MaterialFiles.Resolve(library,options.File);var s=result[name];result[name]=pair.Groups[1].Value switch{
     "map_Kd"=>s with{Base=path},"map_Ks"=>s with{Specular=path},"map_Pr"=>s with{RoughMap=path},"map_Pm"=>s with{MetalMap=path},"map_Ka"=>s with{AoMap=path},"map_Ke"=>s with{Emissive=path},"norm"=>s with{Normal=path},_=>s with{Height=path,HeightStrength=options.Bump}};
   }
  }return result;
 }
}
