using Aurora.Core;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace Aurora.Editor;
static class MaterialExport
{
 public static (string Native,StringBuilder Rules) Render(string sourceMtl,string mtl,IReadOnlyDictionary<string,MaterialSettings> settings,Func<string,string> CopyMap)
 {
  sourceMtl=NativeMaterialText.Normalize(sourceMtl);var materialNames=NativeMaterialText.Names(sourceMtl).ToHashSet();string outputMtl=sourceMtl;
   foreach(var pair in settings)foreach(var texture in new[]{(0,pair.Value.Base),(1,pair.Value.Specular),(2,pair.Value.Normal)})if(texture.Item2!=null)outputMtl=NativeMaterialText.SetTexture(outputMtl,pair.Key,texture.Item1,texture.Item2);
   foreach(var pair in settings){var s=pair.Value;foreach(var value in new[]{("DIFFUSECOLOR",s.NativeDiffuse),("AMBIENTCOLOR",s.NativeAmbient),("SPECULARCOLOR",s.NativeSpecular),("SPECULARPOWER",s.NativePower is float power?new[]{power}:null)})if(value.Item2!=null)outputMtl=NativeMaterialText.SetProperty(outputMtl,pair.Key,value.Item1,value.Item2);}
   // Retain every original MTL texture reference, including paint/light channels.
   outputMtl=VehicleAssets.MapMaterial(outputMtl,(r,local)=>CopyMap(MaterialFiles.ResolveNative(mtl,r,local)));
   var rules=new StringBuilder();string F(float value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
   foreach(var pair in settings)
   {
    if(!materialNames.Contains(pair.Key))throw new InvalidDataException("Unknown material setting");var s=pair.Value;
    if(pair.Key.IndexOfAny(['{','}','\r','\n'])>=0)throw new InvalidDataException("Invalid material identifier");
    rules.AppendLine("{material "+pair.Key).AppendLine(" {workflow = "+(s.Extra?.GetValueOrDefault("workflow")??"painter")+"}").AppendLine(" {preserve_native_diffuse = "+s.Native.ToString().ToLowerInvariant()+"}");
    if(s.Extra!=null)foreach(var extra in s.Extra.Where(p=>p.Key!="workflow")){
     if(!Regex.IsMatch(extra.Key,@"^\w+$")||extra.Value.IndexOfAny(['{','}','\r','\n'])>=0)throw new InvalidDataException("Invalid PBR property");
     string value=extra.Key is "glass" or "glass_mask"&&extra.Value!="none"&&!extra.Value.StartsWith("aurora:",StringComparison.Ordinal)?CopyMap(extra.Value):extra.Value;rules.AppendLine(" {"+extra.Key+" = "+value+"}");
    }
    foreach(var item in new[]{("base_color",s.PbrBase??s.Base),("normal",s.PbrNormal??s.Normal),("orm",s.Orm),("metallic",s.MetalMap),("roughness",s.RoughMap),("ao",s.AoMap),("emissive",s.Emissive)})if(item.Item2!=null)rules.AppendLine(" {"+item.Item1+" = "+CopyMap(item.Item2)+"}");
    rules.AppendLine(" {metallic_factor = "+F(s.Metallic)+"}").AppendLine(" {roughness_factor = "+F(s.Roughness)+"}").AppendLine(" {ao_factor = "+F(s.Ao)+"}").AppendLine(" {normal_strength = "+F(s.NormalStrength)+"}").AppendLine(" {normal_y = "+(s.NormalY<0?"directx":"opengl")+"}").AppendLine(" {emissive_strength = "+F(s.EmissiveStrength)+"}").AppendLine(" {ibl_strength = "+F(s.Ibl)+"}").AppendLine("}");
   }
  return(outputMtl,rules);
 }
}
