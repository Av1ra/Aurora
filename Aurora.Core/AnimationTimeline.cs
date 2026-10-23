using System.Numerics;
using System.Text;
namespace Aurora.Core;

public sealed record AnimationPose(float[] Position,float[] Rotation,float[] Scale)
{
 public static AnimationPose Identity=>new([0,0,0],[0,0,0],[1,1,1]);
 public void Validate(){foreach(var a in new[]{Position,Rotation,Scale})if(a==null||a.Length!=3||a.Any(v=>!float.IsFinite(v)||MathF.Abs(v)>1e6f))throw new InvalidDataException("Invalid animation transform");if(Scale.Any(v=>v<=0))throw new InvalidDataException("Animation scale must be positive");}
 public Matrix4x4 Matrix(Vector3 pivot){Validate();var r=Rotation.Select(v=>v*MathF.PI/180).ToArray();return Matrix4x4.CreateTranslation(-pivot)*Matrix4x4.CreateScale(Scale[0],Scale[1],Scale[2])*Matrix4x4.CreateRotationX(r[0])*Matrix4x4.CreateRotationY(r[1])*Matrix4x4.CreateRotationZ(r[2])*Matrix4x4.CreateTranslation(pivot+new Vector3(Position[0],Position[1],Position[2]));}
}
public sealed record AnimationKey(int Frame,AnimationPose Pose);
public sealed record AnimationTrack(string Mesh,float[] Pivot,AnimationKey[] Keys,string Interpolation="Smooth",string? CargoParent=null,float[]? AxisRotation=null,string PivotSource="Custom",Guid? ObjectId=null)
{
 public void Validate(int length){if(ObjectId==Guid.Empty)throw new InvalidDataException("Empty object identity");if(PivotSource is not ("Custom" or "Object" or "Mechanism"))throw new InvalidDataException("Invalid pivot source");if(AxisRotation!=null&&(AxisRotation.Length!=3||AxisRotation.Any(v=>!float.IsFinite(v)||MathF.Abs(v)>1e6f)))throw new InvalidDataException("Invalid animation axis frame");if(string.IsNullOrWhiteSpace(Mesh)||Encoding.UTF8.GetByteCount(Mesh)>63||Mesh.Any(char.IsControl)||Pivot==null||Pivot.Length!=3||Pivot.Any(v=>!float.IsFinite(v)||MathF.Abs(v)>1e6f)||Keys==null||Keys.Length is <1 or >10001||Keys.Select(k=>k.Frame).Distinct().Count()!=Keys.Length||Keys.Any(k=>k.Frame<0||k.Frame>length)||Interpolation is not ("Smooth" or "Linear"))throw new InvalidDataException("Invalid animation track");foreach(var k in Keys)k.Pose.Validate();}
 public Matrix4x4 Matrix(float frame)=>Matrix(Sample(frame));
 public Matrix4x4 Matrix(AnimationPose pose){var axis=AxisRotation??[0,0,0];var basis=Matrix4x4.CreateRotationX(axis[0]*MathF.PI/180)*Matrix4x4.CreateRotationY(axis[1]*MathF.PI/180)*Matrix4x4.CreateRotationZ(axis[2]*MathF.PI/180);Matrix4x4.Invert(basis,out var inverse);var p=new Vector3(Pivot[0],Pivot[1],Pivot[2]);return Matrix4x4.CreateTranslation(-p)*inverse*(pose with{Position=[0,0,0]}).Matrix(Vector3.Zero)*basis*Matrix4x4.CreateTranslation(p+new Vector3(pose.Position[0],pose.Position[1],pose.Position[2]));}
 public AnimationPose Sample(float frame)=>Sampler()(frame);
 public Func<float,AnimationPose> Sampler(){var keys=Keys.OrderBy(k=>k.Frame).ToArray();return frame=>{
  if(!float.IsFinite(frame)||keys.Length==0)throw new InvalidDataException("Invalid animation time");if(frame<=keys[0].Frame)return keys[0].Pose;if(frame>=keys[^1].Frame)return keys[^1].Pose;int lo=1,hi=keys.Length-1;while(lo<hi){int mid=(lo+hi)/2;if(keys[mid].Frame<frame)lo=mid+1;else hi=mid;}int i=lo;var a=keys[i-1];var b=keys[i];float t=(frame-a.Frame)/(b.Frame-a.Frame);if(Interpolation=="Smooth")t=t*t*(3-2*t);
  float[] Mix(float[] x,float[] y)=>x.Select((v,n)=>v+(y[n]-v)*t).ToArray();return new(Mix(a.Pose.Position,b.Pose.Position),Mix(a.Pose.Rotation,b.Pose.Rotation),Mix(a.Pose.Scale,b.Pose.Scale));
 };}
}
public sealed record AnimationClip(string Name,int Fps,int LastFrame,AnimationTrack[] Tracks,string Action="Preview")
{
 public void Validate(){if(!AnimationBindings.Actions.Any(a=>a.Key==Action)||string.IsNullOrWhiteSpace(Name)||Name.Length>128||Fps is <1 or >240||LastFrame is <1 or >10000||Tracks==null||Tracks.Length is <1 or >64||Tracks.Select(t=>t.Mesh).Distinct().Count()!=Tracks.Length)throw new InvalidDataException("Invalid animation clip");foreach(var t in Tracks)t.Validate(LastFrame);if((long)(LastFrame+1)*Tracks.Length*64>128*1024*1024)throw new InvalidDataException("Animation is too large");}
 // NAF header and matrix order verified against the developer-linked Blender exporter.
 // Matching bones and an engine action binding are separate requirements.
 public byte[] Naf()
 {
  Validate();using var stream=new MemoryStream();using var w=new BinaryWriter(stream,Encoding.UTF8,true);w.Write("B3DAM\0"u8);w.Write("10"u8);w.Write(0);w.Write(Tracks.Length);w.Write(Fps);w.Write(1);w.Write(LastFrame+1);w.Write(0);
  foreach(var track in Tracks){byte[] name=new byte[64];Encoding.UTF8.GetBytes(track.Mesh).CopyTo(name,0);w.Write(name);var sample=track.Sampler();for(int frame=0;frame<=LastFrame;frame++){var m=track.Matrix(sample(frame));foreach(float v in new[]{m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44})w.Write(v);}}
  return stream.ToArray();
 }
}

// Validate remapping before geometry is committed.
public static class AnimationEditing
{
 public static AnimationTrack RebindPivot(AnimationTrack track,Vector3 pivot,int length,bool preserveWorld)
 {
  track.Validate(length);if(length is <1 or >10000||!float.IsFinite(pivot.X)||!float.IsFinite(pivot.Y)||!float.IsFinite(pivot.Z))throw new InvalidDataException("Invalid rebind target");
  var target=track with{Pivot=[pivot.X,pivot.Y,pivot.Z],PivotSource="Object"};if(!preserveWorld)return target;
  var sample=track.Sampler();var keys=new AnimationKey[length+1];var d=pivot-new Vector3(track.Pivot[0],track.Pivot[1],track.Pivot[2]);
  for(int frame=0;frame<=length;frame++){var pose=sample(frame);var correction=Vector3.TransformNormal(d,track.Matrix(pose))-d;keys[frame]=new(frame,pose with{Position=[pose.Position[0]+correction.X,pose.Position[1]+correction.Y,pose.Position[2]+correction.Z]});}
  return target with{Keys=keys,Interpolation="Linear"};
 }
 public static AnimationClip[] SplitMesh(AnimationClip[] clips,string previous,string[] pieces)
 {
  if(pieces.Length<2||pieces.Distinct().Count()!=pieces.Length)throw new InvalidDataException("Invalid animation mesh split");
  var result=clips.Select(clip=>clip with{Tracks=clip.Tracks.SelectMany(track=>track.Mesh==previous?pieces.Select((mesh,index)=>track with{Mesh=mesh,ObjectId=null,CargoParent=track.CargoParent==previous?(index==0?mesh:null):track.CargoParent}):[track with{CargoParent=track.CargoParent==previous?pieces[0]:track.CargoParent}]).ToArray()}).ToArray();
  foreach(var clip in result)clip.Validate();return result;
 }
}
