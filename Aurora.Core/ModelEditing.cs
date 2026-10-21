using System.Text;
using System.Globalization;
namespace Aurora.Core;
public static class ModelEditing
{
 public static NativeModel NativeMaterialSlots(NativeModel model,string[] names,bool allowPartial=false)
 {
  var data=NativeMaterialData(model.Data,names,allowPartial);return ReferenceEquals(data,model.Data)?model:new(data);
 }
 public static byte[] NativeMaterialData(byte[] data,string[] names,bool allowPartial=false)
 {
  var materials=NativeModel.ReadMaterialNames(data);if(names.Length==0||names.Distinct().Count()!=names.Length)throw new InvalidDataException("Invalid native material names");if(materials.All(names.Contains)||names.All(materials.Contains))return data;
  if(names.Length<materials.Length){var model=new NativeModel(data);if(model.Nodes.SelectMany(n=>n.Groups).All(g=>names.Contains(materials[g.Material])))return data;if(allowPartial)return data;throw new InvalidDataException("Native material slots missing");}var mapped=materials.Select((name,i)=>names.Contains(name)?name:names[i]).ToArray();if(mapped.Distinct().Count()!=mapped.Length){if(allowPartial)return data;throw new InvalidDataException("Ambiguous native material slot correspondence");}var result=data.ToArray();for(int i=0;i<materials.Length;i++)Name(result,20+i*64,mapped[i]);return result;
 }
 public static NativeModel MaterialSlots(NativeModel model,string[] names){if(names.Length!=model.Materials.Length||names.Distinct().Count()!=names.Length)throw new InvalidDataException("Invalid material slot mapping");var data=model.Data.ToArray();for(int i=0;i<names.Length;i++)Name(data,20+i*64,names[i]);return new(data);}
 public static NativeModel TransformUv(NativeModel model,IReadOnlyDictionary<string,System.Numerics.Vector4> transforms)
 {
  var data=model.Data.ToArray();foreach(var node in model.Nodes){var edited=new Dictionary<int,System.Numerics.Vector4>();foreach(var group in node.Groups){var t=transforms.GetValueOrDefault(model.Materials[group.Material],new(1,1,0,0));if(!float.IsFinite(t.X)||!float.IsFinite(t.Y)||!float.IsFinite(t.Z)||!float.IsFinite(t.W))throw new InvalidDataException("Invalid UV transform");for(int i=group.First;i<group.First+group.Count;i++){int v=node.Indices[i];if(edited.TryGetValue(v,out var previous)){if(previous!=t)throw new InvalidDataException("Shared vertex has incompatible UV transforms");continue;}edited[v]=t;float u=node.Uv[v*2]*t.X+t.Z,w=1-((1-node.Uv[v*2+1])*t.Y+t.W);if(!float.IsFinite(u)||!float.IsFinite(w))throw new InvalidDataException("UV transform overflow");int at=node.AttributeOffset+node.VertexCount*(node.Stride-8)+v*8;BitConverter.GetBytes(u).CopyTo(data,at);BitConverter.GetBytes(w).CopyTo(data,at+4);}}}return new(data);
 }
 static void Name(byte[] data,int offset,string name){byte[] bytes=new UTF8Encoding(false,true).GetBytes(name);if(bytes.Length is <1 or >63||string.IsNullOrWhiteSpace(name)||name.Any(char.IsControl))throw new InvalidDataException("Invalid NMF name");int end=Array.IndexOf(data,(byte)0,offset,64);if(end>=offset&&NativeModel.DecodeName(data.AsSpan(offset,end-offset))==name)return;Array.Clear(data,offset,64);bytes.CopyTo(data,offset);}
 public static NativeModel Rename(NativeModel model,string oldName,string name){if(model.Nodes.Any(n=>n.Name==name&&n.Name!=oldName))throw new InvalidDataException("Object name already exists");var data=model.Data.ToArray();foreach(var node in model.Nodes.Where(n=>n.Name==oldName))Name(data,node.Offset+8,name);return new(data);}
 public static NativeModel AddMaterial(NativeModel model,string name){if(model.Materials.Contains(name)||model.Materials.Length>=512)throw new InvalidDataException("Invalid new material");int end=20+model.Materials.Length*64;var data=new byte[model.Data.Length+64];model.Data.AsSpan(0,end).CopyTo(data);Name(data,end,name);model.Data.AsSpan(end).CopyTo(data.AsSpan(end+64));BitConverter.GetBytes(model.Materials.Length+1).CopyTo(data,8);BitConverter.GetBytes(data.Length).CopyTo(data,16);return new(data);}
 public static NativeModel Assign(NativeModel model,string nodeName,string material){int index=Array.IndexOf(model.Materials,material);if(index<0)throw new InvalidDataException("Unknown material");var data=model.Data.ToArray();foreach(var node in model.Nodes.Where(n=>n.Name==nodeName))for(int g=0;g<node.Groups.Length;g++)BitConverter.GetBytes((ushort)index).CopyTo(data,node.GroupOffset+g*12+8);return new(data);}
 public static NativeModel AssignGroup(NativeModel model,string nodeName,int group,string material){int index=Array.IndexOf(model.Materials,material);var node=model.Nodes.Single(n=>n.Name==nodeName);if(index<0||group<0||group>=node.Groups.Length)throw new InvalidDataException("Invalid material group");var data=model.Data.ToArray();BitConverter.GetBytes((ushort)index).CopyTo(data,node.GroupOffset+group*12+8);return new(data);}
 public static NativeModel Split(NativeModel model,string nodeName)
 {
  var selected=model.Nodes.Single(n=>n.Name==nodeName);int[] parent=Enumerable.Range(0,selected.VertexCount).ToArray();int Root(int x){while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;}void Join(int a,int b){a=Root(a);b=Root(b);if(a!=b)parent[a]=b;}
  // Connectivity weld does not merge the stored UV/normal seams.
  var same=new Dictionary<(float,float,float),int>();for(int v=0;v<selected.VertexCount;v++){var key=(selected.Positions[v*3],selected.Positions[v*3+1],selected.Positions[v*3+2]);if(same.TryGetValue(key,out int prior))Join(v,prior);else same[key]=v;}
  for(int i=0;i<selected.IndexCount;i+=3){Join(selected.Indices[i],selected.Indices[i+1]);Join(selected.Indices[i],selected.Indices[i+2]);}
  float Centre(IEnumerable<int> triangles,int axis){var values=triangles.SelectMany(t=>Enumerable.Range(t*3,3)).Select(i=>selected.Positions[selected.Indices[i]*3+axis]).ToArray();return (values.Min()+values.Max())/2;}
  var pieces=Enumerable.Range(0,selected.IndexCount/3).GroupBy(t=>Root(selected.Indices[t*3])).OrderBy(g=>Centre(g,0)).ThenBy(g=>Centre(g,2)).ThenBy(g=>Centre(g,1)).Select(g=>g.ToArray()).ToArray();if(pieces.Length<2)throw new InvalidDataException("Объект уже связан.");
  var obj=new StringBuilder();int vertex=0;string F(float v)=>v.ToString("R",CultureInfo.InvariantCulture);
  foreach(var node in model.Nodes){int[][] parts=node==selected?pieces:[Enumerable.Range(0,node.IndexCount/3).ToArray()];for(int part=0;part<parts.Length;part++){
   string name=node==selected?node.Name[..Math.Min(52,node.Name.Length)]+"_"+(part+1):node.Name;if(node==selected&&model.Nodes.Any(n=>n!=selected&&n.Name==name))throw new InvalidDataException("Object name already exists");obj.AppendLine("o "+name);int last=-1;
   foreach(int tri in parts[part]){int material=node.Groups.Single(g=>tri*3>=g.First&&tri*3<g.First+g.Count).Material;if(material!=last){obj.AppendLine("usemtl "+model.Materials[material]);last=material;}
    for(int j=0;j<3;j++){int v=node.Indices[tri*3+j];obj.AppendLine("v "+F(node.Positions[v*3])+" "+F(node.Positions[v*3+1])+" "+F(node.Positions[v*3+2]));obj.AppendLine("vn "+F(node.Normals[v*3])+" "+F(node.Normals[v*3+1])+" "+F(node.Normals[v*3+2]));obj.AppendLine("vt "+F(node.Uv[v*2])+" "+F(1-node.Uv[v*2+1]));}
    obj.AppendLine("f "+string.Join(" ",Enumerable.Range(vertex+1,3).Select(v=>$"{v}/{v}/{v}")));vertex+=3;
   }
  }}
  var result=ObjImporter.Parse(obj.ToString()).Model;foreach(string unused in model.Materials.Except(result.Materials))result=AddMaterial(result,unused);return result;
 }
}
