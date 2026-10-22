using Aurora.Core;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 string? viewportDocument;
 readonly Dictionary<string,(DateTime Stamp,long Size,RgbaImage Image)> decodedImages=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<(string Material,string Channel),RgbaImage> sentImages=[];
 async Task<RgbaImage> ReadImage(string file)
 {
  file=Path.GetFullPath(file);var info=new FileInfo(file);
  if(decodedImages.TryGetValue(file,out var prior)&&prior.Stamp==info.LastWriteTimeUtc&&prior.Size==info.Length)return prior.Image;
  var image=await DecodeImage(file);
  while(decodedImages.Count>0&&decodedImages.Values.Sum(v=>(long)v.Image.Pixels.Length)+image.Pixels.LongLength>96L*1024*1024)decodedImages.Remove(decodedImages.Keys.First());
  if(image.Pixels.LongLength<=96L*1024*1024)decodedImages[file]=(info.LastWriteTimeUtc,info.Length,image);
  return image;
 }
}
