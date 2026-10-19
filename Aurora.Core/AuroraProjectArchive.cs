using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
namespace Aurora.Core;
public static class AuroraProjectArchive
{
 const int Limit=256*1024*1024;
 static readonly byte[] Magic="AURORA\0\x01"u8.ToArray();
 // Passwordless application format protection, not publisher authentication.
 static byte[] Key()=>SHA256.HashData(Encoding.UTF8.GetBytes("Aurora Editor / portable project / v1"));
 public static bool IsPortablePath(string path)=>path.Length is >0 and <240&&!path.Contains('\\')&&!path.StartsWith('/')&&!path.Contains(':')&&path.Split('/').All(p=>p.Length>0&&p is not "." and not ".."&&!p.EndsWith(' ')&&!p.EndsWith('.')&&!p.Any(c=>c<32||"<>|?*".Contains(c))&&!System.Text.RegularExpressions.Regex.IsMatch(p,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)",System.Text.RegularExpressions.RegexOptions.IgnoreCase));
 public static void Write(string path,IReadOnlyDictionary<string,byte[]> entries)
 {
  if(entries.Count is 0 or >2048||entries.Keys.Any(p=>!IsPortablePath(p))||entries.Sum(p=>(long)p.Value.Length)>Limit)throw new InvalidDataException("Project exceeds format limits");
  using var zipBytes=new MemoryStream();using(var zip=new ZipArchive(zipBytes,ZipArchiveMode.Create,true))foreach(var pair in entries){using var entry=zip.CreateEntry(pair.Key,CompressionLevel.Fastest).Open();entry.Write(pair.Value);}
  byte[] plain=zipBytes.ToArray();if(plain.Length>Limit)throw new InvalidDataException("Project too large");byte[] nonce=RandomNumberGenerator.GetBytes(12),tag=new byte[16],cipher=new byte[plain.Length];using var aes=new AesGcm(Key(),16);aes.Encrypt(nonce,plain,cipher,tag,Magic);
  string temporary=path+".writing-"+Guid.NewGuid().ToString("N");try{using(var file=File.Create(temporary)){file.Write(Magic);file.Write(nonce);file.Write(tag);file.Write(cipher);}File.Move(temporary,path,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
 public static Dictionary<string,byte[]> Read(string path)
 {
  long size=new FileInfo(path).Length;if(size<36||size>Limit+36)throw new InvalidDataException("Invalid Aurora project size");var data=File.ReadAllBytes(path);if(!data.AsSpan(0,8).SequenceEqual(Magic))throw new InvalidDataException("Unsupported Aurora project version");
  byte[] plain=new byte[data.Length-36];using var aes=new AesGcm(Key(),16);aes.Decrypt(data.AsSpan(8,12),data.AsSpan(36),data.AsSpan(20,16),plain,Magic);
  using var buffer=new MemoryStream(plain);using var zip=new ZipArchive(buffer,ZipArchiveMode.Read);if(zip.Entries.Count>2048||zip.Entries.Sum(e=>e.Length)>Limit)throw new InvalidDataException("Project archive exceeds limits");
  var result=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);foreach(var entry in zip.Entries){if(!IsPortablePath(entry.FullName)||entry.Length>Limit||result.ContainsKey(entry.FullName))throw new InvalidDataException("Invalid project entry");using var stream=entry.Open();using var value=new MemoryStream();var chunk=new byte[65536];int count;while((count=stream.Read(chunk))>0){if(value.Length+count>entry.Length)throw new InvalidDataException("Invalid project entry length");value.Write(chunk,0,count);}if(value.Length!=entry.Length)throw new InvalidDataException("Truncated project entry");result.Add(entry.FullName,value.ToArray());}return result;
 }
}
