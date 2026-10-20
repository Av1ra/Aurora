using System.Security.Cryptography;
using System.Text.RegularExpressions;
namespace Aurora.Core;
public static class VehicleSoundFiles
{
 public static string Validate(string path){string text=File.ReadAllText(path);if(text.Length>2*1024*1024||!Regex.IsMatch(text,@"(?m)^\s*\$(TEXT|FLOAT)\s+\w+\s+[^\r\n]+"))throw new InvalidDataException("Invalid vehicle sound INI");return text;}
 public static string Map(string script,Func<string,string> map)=>Regex.Replace(script,@"(?m)^([\t ]*\$SOUND_PARAMS_WORKSHOP[\t ]+)([^\r\n]+)",m=>m.Groups[1].Value+map(m.Groups[2].Value.Trim().Trim('"')));
 public static string Resolve(string reference,string folder){string path=Path.IsPathRooted(reference)?reference:Path.Combine(folder,reference.Replace('/',Path.DirectorySeparatorChar));if(!File.Exists(path))throw new FileNotFoundException("Sound INI not found",path);return path;}
 public static string Export(string script,string folder,string destination)=>Map(script,reference=>{string path=Resolve(reference,folder);string text=Validate(path);string name="sound-"+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))[..16].ToLowerInvariant()+".ini";File.WriteAllText(Path.Combine(destination,name),text);return name;});
}
