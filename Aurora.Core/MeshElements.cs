using System.Numerics;
namespace Aurora.Core;
public sealed record MeshElement(int Id,int[] Triangles,int[] Vertices);
public static class MeshElements
{
 public static MeshElement[] Read(MeshNode node)
 {
  int[] parent=Enumerable.Range(0,node.VertexCount).ToArray();int Root(int a){while(parent[a]!=a){parent[a]=parent[parent[a]];a=parent[a];}return a;}void Join(int a,int b){a=Root(a);b=Root(b);if(a!=b)parent[b]=a;}
  Vector3 P(int v)=>new(node.Positions[v*3],node.Positions[v*3+1],node.Positions[v*3+2]);
  var edges=new Dictionary<(Vector3,Vector3),int>();
  int Compare(Vector3 a,Vector3 b){int c=a.X.CompareTo(b.X);if(c==0)c=a.Y.CompareTo(b.Y);return c==0?a.Z.CompareTo(b.Z):c;}
  for(int t=0;t<node.IndexCount;t+=3){int a=node.Indices[t],b=node.Indices[t+1],c=node.Indices[t+2];Join(a,b);Join(a,c);foreach(var pair in new[]{(a,b),(b,c),(c,a)}){var p=P(pair.Item1);var q=P(pair.Item2);var key=Compare(p,q)<=0?(p,q):(q,p);if(edges.TryGetValue(key,out int old))Join(a,old);else edges[key]=a;}}
  return Enumerable.Range(0,node.IndexCount/3).GroupBy(t=>Root(node.Indices[t*3])).Select((g,id)=>new MeshElement(id,g.ToArray(),g.SelectMany(t=>new[]{(int)node.Indices[t*3],node.Indices[t*3+1],node.Indices[t*3+2]}).Distinct().ToArray())).ToArray();
 }
 public static string NextName(NativeModel model){var used=model.Nodes.Select(n=>n.Name).ToHashSet();string name="mesh";for(int i=1;used.Contains(name);i++)name="mesh_"+i;return name;}
 public static (NativeModel Model,string Name,bool RemovedOriginal) Detach(NativeModel model,int index,IReadOnlySet<int> selected)
 {
  if(model.Animated)throw new InvalidDataException("Detach the rigid source model before skinning");if(index<0||index>=model.Nodes.Count)throw new InvalidDataException("Invalid mesh");var node=model.Nodes[index];var parts=Read(node);if(selected.Count==0||selected.Any(i=>i<0||i>=parts.Length))throw new InvalidDataException("Select mesh elements first");
  var triangles=parts.Where(p=>selected.Contains(p.Id)).SelectMany(p=>p.Triangles).ToHashSet();var remaining=Enumerable.Range(0,node.IndexCount/3).Where(t=>!triangles.Contains(t)).ToArray();var meshes=model.Nodes.Select(n=>RigidScene.Mesh(model,n)).ToList();string name=NextName(model);var piece=RigidScene.Triangles(meshes[index],triangles.Order(),name);var p=piece.Positions;var points=Enumerable.Range(0,p.Length/3).Select(i=>new Vector3(p[i*3],p[i*3+1],p[i*3+2])).ToArray();var frame=piece.Frame;frame.Translation=(points.Aggregate(Vector3.Min)+points.Aggregate(Vector3.Max))*.5f;piece=piece with{Frame=frame};if(remaining.Length>0)meshes[index]=RigidScene.Triangles(meshes[index],remaining,node.Name);else meshes.RemoveAt(index);meshes.Add(piece);return(RigidScene.Build(model.Materials,meshes),name,remaining.Length==0);
 }
}
