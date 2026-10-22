using System.Text;
namespace Aurora.Core;

// Rigid per-mesh bones. Format fields follow the developer-linked Blender exporter.
public static class AnimatedNmf
{
 public static string Bone(string mesh)=>"aurora_"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(mesh))).Substring(0,24).ToLowerInvariant();
 public static byte[] Write(NativeModel model,AnimationClip clip)
 {
  if(model.Animated)throw new InvalidDataException("Export animation from the original editable project mesh");clip.Validate();foreach(var t in clip.Tracks)if(model.Nodes.Count(n=>n.Name==t.Mesh)!=1)throw new InvalidDataException("Animated mesh missing: "+t.Mesh);
  using var stream=new MemoryStream();using var w=new BinaryWriter(stream,Encoding.UTF8,true);
  void Name(string value){byte[] b=Encoding.UTF8.GetBytes(value);if(b.Length>63)throw new InvalidDataException("Animated name too long");w.Write(b);w.Write(new byte[64-b.Length]);}
  void Matrix(){for(int i=0;i<16;i++)w.Write(i%5==0?1f:0f);}
  void Patch(long at,int value){long end=stream.Position;stream.Position=at;w.Write(value);stream.Position=end;}
  void Bounds(float[] positions){for(int i=0;i<3;i++)w.Write(positions.Where((_,n)=>n%3==i).Min());for(int i=0;i<3;i++)w.Write(positions.Where((_,n)=>n%3==i).Max());}
  w.Write("B3DMH\010"u8);w.Write(model.Materials.Length);w.Write(model.Nodes.Count+clip.Tracks.Length);w.Write(0);foreach(string m in model.Materials)Name(m);
  for(int i=0;i<clip.Tracks.Length;i++){var mesh=model.Nodes.Single(n=>n.Name==clip.Tracks[i].Mesh);w.Write(1);w.Write(292);Name(Bone(clip.Tracks[i].Mesh));w.Write((short)-1);w.Write((short)0);Matrix();Matrix();Bounds(mesh.Positions);Matrix();}
  foreach(var n in model.Nodes){int bone=Array.FindIndex(clip.Tracks,t=>t.Mesh==n.Name);if(bone<0){var block=model.Data.AsSpan(n.Offset,n.End-n.Offset).ToArray();BitConverter.GetBytes((short)-1).CopyTo(block,72);BitConverter.GetBytes((short)0).CopyTo(block,74);Array.Copy(block,76,block,140,64);w.Write(block);continue;}
   long start=stream.Position;w.Write(0);w.Write(0);Name(n.Name);w.Write((short)-1);w.Write((short)0);Matrix();Matrix();Bounds(n.Positions);w.Write(1);long lod=stream.Position;w.Write(0);w.Write(n.VertexCount);w.Write(n.IndexCount);w.Write(n.Groups.Length);w.Write(0);w.Write(0x30109);w.Write(0);
   foreach(ushort index in n.Indices)w.Write(index);foreach(float v in n.Positions)w.Write(v);foreach(float v in n.Normals)w.Write(v);foreach(float v in n.Uv)w.Write(v);
   for(int i=0;i<n.VertexCount;i++){w.Write((ushort)32767);w.Write((ushort)0);w.Write((ushort)0);w.Write((ushort)0);}for(int i=0;i<n.VertexCount;i++)w.Write(0);
   foreach(var group in n.Groups){w.Write(group.First);w.Write(group.Count);w.Write((ushort)group.Material);w.Write((ushort)1);w.Write((ushort)bone);}
   Patch(lod,checked((int)(stream.Position-lod)));Patch(start+4,checked((int)(stream.Position-start)));
  }
  Patch(16,checked((int)stream.Length));if(stream.Length>128*1024*1024)throw new InvalidDataException("Animated NMF exceeds size limit");return stream.ToArray();
 }
 public static byte[] Naf(AnimationClip clip)=> (clip with{Tracks=clip.Tracks.Select((t,i)=>t with{Mesh=Bone(t.Mesh)}).ToArray()}).Naf();
}
