using System.Numerics;
using System.Text;
namespace Aurora.Core;

// Rigid models only: bake the common vehicle axes/pivot into the established
// 0x108 stream. Do not retain stale tangent or face-cache coordinates.
public static class ModelOrientation
{
 public static Vector3 BottomPivot(NativeModel model){var p=model.Nodes.SelectMany(n=>Enumerable.Range(0,n.VertexCount).Select(i=>new Vector3(n.Positions[i*3],n.Positions[i*3+1],n.Positions[i*3+2]))).ToArray();return new((p.Min(v=>v.X)+p.Max(v=>v.X))/2,p.Min(v=>v.Y),(p.Min(v=>v.Z)+p.Max(v=>v.Z))/2);}
 public static readonly Vector3[] Axes=[Vector3.UnitZ,-Vector3.UnitZ,Vector3.UnitX,-Vector3.UnitX,Vector3.UnitY,-Vector3.UnitY];
 public static Matrix4x4 Transform(NativeModel primary,int front,int up,bool centre,bool ground)
 {
  if(front<0||front>=Axes.Length||up<0||up>=Axes.Length||MathF.Abs(Vector3.Dot(Axes[front],Axes[up]))>.001f)throw new InvalidDataException("Перед и верх должны быть разными осями.");
  var f=Axes[front];var u=Axes[up];var r=Vector3.Cross(u,f);
  var matrix=new Matrix4x4(r.X,u.X,f.X,0,r.Y,u.Y,f.Y,0,r.Z,u.Z,f.Z,0,0,0,0,1);
  var points=primary.Nodes.SelectMany(n=>Enumerable.Range(0,n.VertexCount).Select(i=>Vector3.Transform(new Vector3(n.Positions[i*3],n.Positions[i*3+1],n.Positions[i*3+2]),matrix))).ToArray();
  if(centre){matrix.M41=-(points.Min(p=>p.X)+points.Max(p=>p.X))/2;matrix.M43=-(points.Min(p=>p.Z)+points.Max(p=>p.Z))/2;}
  if(ground)matrix.M42=-points.Min(p=>p.Y);
  return matrix;
 }
 public static NativeModel Apply(NativeModel model,Matrix4x4 transform)
 {
  using var stream=new MemoryStream();using var w=new BinaryWriter(stream);
  void Patch(long at,int value){long end=stream.Position;stream.Position=at;w.Write(value);stream.Position=end;}
  void V(Vector3 v){if(!float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z))throw new InvalidDataException("Nonfinite orientation result");w.Write(v.X);w.Write(v.Y);w.Write(v.Z);}
  // A freshly baked rigid model needs bounds rebuilt on full mod export.
  w.Write("fromObj\0"u8);w.Write(model.Materials.Length);w.Write(model.Nodes.Count);w.Write(0);w.Write(model.Data,20,model.Materials.Length*64);
  foreach(var node in model.Nodes){
   var p=Enumerable.Range(0,node.VertexCount).Select(i=>Vector3.Transform(new Vector3(node.Positions[i*3],node.Positions[i*3+1],node.Positions[i*3+2]),transform)).ToArray();
   long start=stream.Position;w.Write(0);w.Write(0);w.Write(model.Data,node.Offset+8,68);
   for(int m=0;m<2;m++)for(int i=0;i<16;i++)w.Write(i%5==0?1f:0f);
   V(new(p.Min(v=>v.X),p.Min(v=>v.Y),p.Min(v=>v.Z)));V(new(p.Max(v=>v.X),p.Max(v=>v.Y),p.Max(v=>v.Z)));w.Write(1);
   long lod=stream.Position;w.Write(0);w.Write(node.VertexCount);w.Write(node.IndexCount);w.Write(node.Groups.Length);w.Write(0);w.Write(0x108);w.Write(0);
   foreach(ushort i in node.Indices)w.Write(i);foreach(var v in p)V(v);
   for(int i=0;i<node.VertexCount;i++){var n=Vector3.TransformNormal(new Vector3(node.Normals[i*3],node.Normals[i*3+1],node.Normals[i*3+2]),transform);if(n.LengthSquared()<1e-16f)throw new InvalidDataException("Zero model normal");V(Vector3.Normalize(n));}
   foreach(float v in node.Uv)w.Write(v);
   foreach(var g in node.Groups){w.Write(g.First);w.Write(g.Count);w.Write((ushort)g.Material);w.Write((ushort)0);}
   Patch(start+4,checked((int)(stream.Position-start+4)));Patch(lod,checked((int)(stream.Position-lod+8)));
  }
  Patch(16,checked((int)stream.Length));return new NativeModel(stream.ToArray());
 }
}
