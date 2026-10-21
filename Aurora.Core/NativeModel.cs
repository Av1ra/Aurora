using System.Numerics;
using System.Text;
namespace Aurora.Core;
public record DrawGroup(int First,int Count,int Material);
public sealed record MeshNode(string Name,int Offset,int End,int VertexCount,int IndexCount,int IndexOffset,int AttributeOffset,int FaceOffset,int GroupOffset,int Stride,int FaceStride,DrawGroup[] Groups,float[] Positions,float[] Normals,float[] Uv,ushort[] Indices);
public sealed class NativeModel
{
 public byte[] Data{get;}
 public string[] Materials{get;}
 public List<MeshNode> Nodes{get;}=[];
 static void Require(bool condition,string text){if(!condition)throw new InvalidDataException(text);}
 public NativeModel(byte[] data)
 {
  Data=data;Require(data.Length>=20&&data.Length<=128*1024*1024,"NMF size invalid");
  Require(data.AsSpan(0,8).SequenceEqual("B3DMH\0"u8.ToArray().Concat("10"u8.ToArray()).ToArray())||data.AsSpan(0,8).SequenceEqual("fromObj\0"u8),"Unsupported NMF signature");
  int count=I(8),nodes=I(12);Require(I(16)==data.Length&&count is >0 and <=512&&nodes is >0 and <=2048,"Invalid NMF header");
  Materials=Enumerable.Range(0,count).Select(i=>Name(20+i*64)).ToArray();
  int offset=20+count*64;
  for(int n=0;n<nodes;n++)
  {
   Require(offset+260<=data.Length&&I(offset)==0,"Unsupported animated/external NMF node");
   int end=checked(offset+I(offset+4));
   bool Boundary(int at)=>n+1==nodes?at==data.Length:at>=0&&at+72<=data.Length&&I(at) is >=0 and <=3&&data[at+8]>=32&&data.AsSpan(at+8,64).Contains((byte)0);
   if(!Boundary(end))end-=4;Require(Boundary(end),"Unknown NMF boundary");
   Require(I(offset+228)==1,"Embedded multi-LOD node not supported; use separate LOD NMF files");
   int nv=I(offset+236),ni=I(offset+240),ng=I(offset+244),flags=I(offset+252);
   Require(I(offset+248)==0&&I(offset+256)==0&&nv is >0 and <=65536&&ni is >0 and <=3000000&&ni%3==0&&ng is >0 and <=512,"Unsupported skin or invalid geometry");
   int stride=flags is 0x40139 or 0x4013a or 0x13a?56:flags==0x4011a?44:flags==0x108?32:0,faceStride=(flags&0x40000)!=0?40:0;
   Require(stride!=0,"Unsupported NMF vertex stream: "+flags.ToString("x"));
   int ix=offset+260,attr=checked(ix+ni*2),face=checked(attr+nv*stride),groups=checked(face+ni/3*faceStride);
   Require(groups+ng*12==end,"Unsupported NMF trailing streams");
   var positions=new float[nv*3];var normals=new float[nv*3];var uv=new float[nv*2];
   var matrix=new Matrix4x4(F(offset+76),F(offset+80),F(offset+84),F(offset+88),F(offset+92),F(offset+96),F(offset+100),F(offset+104),F(offset+108),F(offset+112),F(offset+116),F(offset+120),F(offset+124),F(offset+128),F(offset+132),F(offset+136));
   Require(Matrix4x4.Invert(matrix,out var inverse),"Singular NMF transform");var normalMatrix=Matrix4x4.Transpose(inverse);
   // Native vertex streams are local; matrix preserves placement of wheels/parts.
   for(int v=0;v<nv;v++)
   {
    var p=Vector3.Transform(new(F(attr+v*12),F(attr+v*12+4),F(attr+v*12+8)),matrix);
    var normal=Vector3.TransformNormal(new(F(attr+nv*12+v*12),F(attr+nv*12+v*12+4),F(attr+nv*12+v*12+8)),normalMatrix);
    Require(float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z),"Nonfinite vertex");
    positions[v*3]=p.X;positions[v*3+1]=p.Y;positions[v*3+2]=p.Z;normals[v*3]=normal.X;normals[v*3+1]=normal.Y;normals[v*3+2]=normal.Z;
    int uvAt=attr+nv*(stride-8)+v*8;uv[v*2]=F(uvAt);uv[v*2+1]=F(uvAt+4);
   }
   var indices=new ushort[ni];for(int i=0;i<ni;i++){indices[i]=U(ix+i*2);Require(indices[i]<nv,"Invalid NMF vertex index");}
   var draw=new DrawGroup[ng];int consumed=0;
   for(int g=0;g<ng;g++){int at=groups+g*12,first=I(at),length=I(at+4),material=U(at+8);Require(first==consumed&&length>0&&length%3==0&&first+length<=ni&&material<count&&U(at+10)==0,"Invalid draw group");draw[g]=new(first,length,material);consumed+=length;}
   Require(consumed==ni,"Incomplete draw groups");
   Nodes.Add(new(Name(offset+8),offset,end,nv,ni,ix,attr,face,groups,stride,faceStride,draw,positions,normals,uv,indices));offset=end;
  }
  Require(offset==data.Length,"Trailing NMF data");
 }
 public int I(int at)=>BitConverter.ToInt32(Data,at);
 public ushort U(int at)=>BitConverter.ToUInt16(Data,at);
 public float F(int at)=>BitConverter.ToSingle(Data,at);
 string Name(int at){Require(at>=0&&at+64<=Data.Length,"Truncated NMF name");int end=Array.IndexOf(Data,(byte)0,at,64);Require(end>=at,"Unterminated NMF name");return Encoding.ASCII.GetString(Data,at,end-at);}

 public byte[] AssignGlass(ISet<string> nodeNames,string glassName="Aurora_Glass")
 {
  Require(!Materials.Contains(glassName),"Choose the original NMF, not a previously exported glass model");
  Require(Nodes.Any(x=>nodeNames.Contains(x.Name)),"Selected glass object is missing from this LOD");
  using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
  writer.Write(Data,0,20+Materials.Length*64);writer.Write(Encoding.ASCII.GetBytes(glassName).Concat(new byte[64-glassName.Length]).ToArray());
  foreach(var node in Nodes)
  {
   var block=Data.AsSpan(node.Offset,node.End-node.Offset).ToArray();
   if(nodeNames.Contains(node.Name))foreach(var g in Enumerable.Range(0,node.Groups.Length))BitConverter.GetBytes((ushort)Materials.Length).CopyTo(block,node.GroupOffset-node.Offset+g*12+8);
   writer.Write(block);
  }
  var result=stream.ToArray();BitConverter.GetBytes(Materials.Length+1).CopyTo(result,8);BitConverter.GetBytes(result.Length).CopyTo(result,16);
  _=new NativeModel(result);return result;
 }
}
