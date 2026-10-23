using System.Numerics;
namespace Aurora.Core;
public static class ObjectPivots
{
 public static NativeModel InitialiseSharedOrigins(NativeModel model)
 {
  if(model.Animated||!model.Nodes.All(n=>RigidScene.Frame(model,n)==Matrix4x4.Identity))return model;
  return SetFrames(model,Enumerable.Range(0,model.Nodes.Count).ToDictionary(i=>i,i=>Matrix4x4.CreateTranslation(Centre(model.Nodes[i]))));
 }
 public static Vector3 Centre(MeshNode node,IEnumerable<int>? vertices=null)
 {
  var points=(vertices??Enumerable.Range(0,node.VertexCount)).Select(v=>new Vector3(node.Positions[v*3],node.Positions[v*3+1],node.Positions[v*3+2])).ToArray();
  if(points.Length==0)throw new InvalidDataException("Select an object first");
  return (points.Aggregate(Vector3.Min)+points.Aggregate(Vector3.Max))*.5f;
 }
 // Rebase local coordinates while preserving the visible geometry and every other object's frame.
 public static NativeModel SetFrames(NativeModel model,IReadOnlyDictionary<int,Matrix4x4> frames)
 {
  if(model.Animated)throw new InvalidDataException("Edit the rigid source model before skinning");
  if(frames.Keys.Any(i=>i<0||i>=model.Nodes.Count))throw new InvalidDataException("Invalid object");
  return RigidScene.Build(model.Materials,model.Nodes.Select((node,i)=>{var mesh=RigidScene.Mesh(model,node);return frames.TryGetValue(i,out var frame)?mesh with{Frame=frame}:mesh;}));
 }
}
