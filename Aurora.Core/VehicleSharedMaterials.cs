using System.Security.Cryptography;
namespace Aurora.Core;

public static class VehicleSharedMaterials
{
 static bool Shared(VehicleToken t)=>t.Index==0&&t.Key is "JOINT" or "LODJOINT1" or "LODJOINT2" or "ALPHA" or "CRANE_STATIC" or "CRANE_DYNAMIC" or "CRANE_DYNAMIC2" or "PROPELER_STATIC" or "PROPELER_DYNAMIC";
 // Only the material table changes; opaque nodes, transforms and animation bytes stay intact.
 public static string Export(string script,string folder,string[] names)=>VehicleScript.RewriteTokens(script,t=>{
  if(!Shared(t))return null;string path=VehicleAssets.Resolve(t.Value,folder);var bytes=File.ReadAllBytes(path);var mapped=ModelEditing.NativeMaterialData(bytes,names);if(ReferenceEquals(bytes,mapped))return null;string name="asset-"+Convert.ToHexString(SHA256.HashData(mapped))[..20].ToLowerInvariant()+".nmf",output=Path.Combine(folder,name);if(File.Exists(output)&&!File.ReadAllBytes(output).SequenceEqual(mapped))throw new InvalidDataException("Shared material dependency collision");File.WriteAllBytes(output,mapped);return name;
 });
}
