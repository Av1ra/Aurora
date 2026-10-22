using System.Numerics;
namespace Aurora.Core;
public sealed record WheelDiagnostic(string Mesh,float[] Pivot,float[] Axle,float[] SteeringAxis,bool Steering,int[]? Vertices=null,int Component=0);
public static class WheelDiagnostics
{
 public static bool IsWheel(string name)=>name is "tire_leftShape" or "tire_rightShape" or "tireShape"||name.StartsWith("tire",StringComparison.Ordinal)&&name.EndsWith("Shape",StringComparison.Ordinal)&&name.Length>9&&name[4..^5].All(char.IsAsciiDigit);
 public static WheelDiagnostic[] Read(NativeModel model)
 {
  var result=new List<WheelDiagnostic>();
  foreach(var node in model.Nodes.Where(n=>IsWheel(n.Name))){
   float F(int i)=>BitConverter.ToSingle(model.Data,node.Offset+76+i*4);
   var frame=new Matrix4x4(F(0),F(1),F(2),F(3),F(4),F(5),F(6),F(7),F(8),F(9),F(10),F(11),F(12),F(13),F(14),F(15));
   if(!Matrix4x4.Invert(frame,out var inverse))throw new InvalidDataException("Invalid wheel frame");
   Vector3 Point(int v)=>Vector3.Transform(new(node.Positions[v*3],node.Positions[v*3+1],node.Positions[v*3+2]),inverse);
   float[] Axis(Vector3 axis){var v=Vector3.Normalize(Vector3.TransformNormal(axis,frame));if(!float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z))throw new InvalidDataException("Invalid wheel axis");return [v.X,v.Y,v.Z];}
   int[] parent=Enumerable.Range(0,node.VertexCount).ToArray();int Root(int x){while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;}void Join(int a,int b){a=Root(a);b=Root(b);if(a!=b)parent[a]=b;}
   var welded=new Dictionary<(float,float,float),int>();for(int v=0;v<node.VertexCount;v++){var key=(node.Positions[v*3],node.Positions[v*3+1],node.Positions[v*3+2]);if(welded.TryGetValue(key,out int other))Join(v,other);else welded[key]=v;}
   for(int i=0;i<node.IndexCount;i+=3){Join(node.Indices[i],node.Indices[i+1]);Join(node.Indices[i],node.Indices[i+2]);}
   var pieces=Enumerable.Range(0,node.VertexCount).GroupBy(Root).Select(g=>g.ToArray()).ToArray();
   // Tyre, rim and hub can be disconnected but occupy the same local wheel box.
   var bounds=pieces.Select(p=>{var points=p.Select(Point).ToArray();return(Min:points.Aggregate(Vector3.Min),Max:points.Aggregate(Vector3.Max));}).ToArray();
   if(pieces.Length>2048)throw new InvalidDataException("Wheel contains too many isolated components");
   for(int a=0;a<pieces.Length;a++)for(int b=a+1;b<pieces.Length;b++)if(Vector3.Max(bounds[a].Min,bounds[b].Min)-new Vector3(.0001f) is var lo&&Vector3.Min(bounds[a].Max,bounds[b].Max) is var hi&&lo.X<=hi.X&&lo.Y<=hi.Y&&lo.Z<=hi.Z)Join(pieces[a][0],pieces[b][0]);
   int component=0;foreach(var group in Enumerable.Range(0,node.VertexCount).GroupBy(Root).OrderBy(g=>g.Select(Point).Min(p=>p.Z)).ThenBy(g=>g.Select(Point).Min(p=>p.X))){var vertices=group.ToArray();var points=vertices.Select(Point).ToArray();var centre=(points.Aggregate(Vector3.Min)+points.Aggregate(Vector3.Max))*.5f;var pivot=Vector3.Transform(centre,frame);result.Add(new(node.Name,[pivot.X,pivot.Y,pivot.Z],Axis(Vector3.UnitX),Axis(Vector3.UnitY),node.Name is "tire_leftShape" or "tire_rightShape",vertices,component++));}
  }
  return result.ToArray();
 }
}
