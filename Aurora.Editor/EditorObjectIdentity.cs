using Aurora.Core;
namespace Aurora.Editor;
sealed partial class EditorWindow
{
 Dictionary<string,Dictionary<string,Guid>> objectIdentities=[];HashSet<Guid> explicitPivots=[];
 Dictionary<string,Guid> ObjectIds(string lod)
 {
  if(!objectIdentities.TryGetValue(lod,out var ids))objectIdentities[lod]=ids=[];
  var names=lods[lod].Nodes.Select(n=>n.Name).ToHashSet();
  foreach(var name in ids.Keys.Where(n=>!names.Contains(n)).ToArray())ids.Remove(name);
  foreach(var name in names)if(!ids.ContainsKey(name))ids[name]=Guid.NewGuid();
  return ids;
 }
 System.Numerics.Vector3 LocalObjectPivot(NativeModel model,MeshNode node)=>explicitPivots.Contains(ObjectIds(selectedLod!)[node.Name])?RigidScene.Frame(model,node).Translation:ObjectPivots.Local(model,node);
 AnimationTrack NewAnimationTrack(string mesh){var model=CurrentModel();var p=LocalObjectPivot(model,model.Nodes.First(n=>n.Name==mesh));return ObjectPivots.Track(model,mesh) with{Pivot=[p.X,p.Y,p.Z],ObjectId=ObjectIds(selectedLod!)[mesh]};}
 NativeModel ObjectFramesChanged(NativeModel after,IEnumerable<int> indices){var ids=ObjectIds(selectedLod!);var targets=indices.Distinct().ToDictionary(i=>after.Nodes[i].Name,i=>RigidScene.Frame(after,after.Nodes[i]));foreach(string name in targets.Keys)explicitPivots.Add(ids[name]);animationClips=animationClips.Select(c=>c with{Tracks=c.Tracks.Select(t=>t.PivotSource=="Object"&&targets.TryGetValue(t.Mesh,out var frame)?t with{Pivot=[frame.M41,frame.M42,frame.M43],AxisRotation=ObjectPivots.Axes(frame)}:t).ToArray()}).ToArray();return after;}
 void BindAnimationIdentities()
 {
  if(lods.Count==0)return;var ids=ObjectIds(lods.Keys.First());
  animationClips=animationClips.Select(c=>c with{Tracks=c.Tracks.Select(t=>{
   if(t.ObjectId is Guid id){var name=ids.FirstOrDefault(p=>p.Value==id).Key;return name==null?t:t with{Mesh=name};}
   return ids.TryGetValue(t.Mesh,out var existing)?t with{ObjectId=existing}:t;
  }).ToArray()}).ToArray();
 }
}
