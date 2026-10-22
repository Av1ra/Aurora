using System.Numerics;
using System.Text;
namespace Aurora.Core;
public record DrawGroup(int First,int Count,int Material);
public sealed record MeshNode(string Name,int Offset,int End,int VertexCount,int IndexCount,int IndexOffset,int AttributeOffset,int FaceOffset,int GroupOffset,int Stride,int FaceStride,DrawGroup[] Groups,float[] Positions,float[] Normals,float[] Uv,ushort[] Indices,int[]? GroupOffsets=null,int SkinBytes=0)
{public int DrawOffset(int index)=>GroupOffsets?[index]??GroupOffset+index*12;}
public sealed class NativeModel
{
 static readonly UTF8Encoding nameEncoding=new(false,true);
 public static string DecodeName(ReadOnlySpan<byte> bytes){try{return nameEncoding.GetString(bytes);}catch(DecoderFallbackException){return Encoding.Latin1.GetString(bytes);}}
 public static string[] ReadMaterialNames(byte[] data)
 {
  Require(data.Length>=20&&data.Length<=128*1024*1024,"NMF size invalid");Require(data.AsSpan(0,8).SequenceEqual("B3DMH\0"u8.ToArray().Concat("10"u8.ToArray()).ToArray())||data.AsSpan(0,8).SequenceEqual("fromObj\0"u8),"Unsupported NMF signature");int count=BitConverter.ToInt32(data,8);Require(count is >0 and <=512&&BitConverter.ToInt32(data,16)==data.Length&&20+count*64<=data.Length,"Invalid NMF material table");return Enumerable.Range(0,count).Select(i=>{int at=20+i*64,end=Array.IndexOf(data,(byte)0,at,64);Require(end>=at,"Unterminated NMF material");return DecodeName(data.AsSpan(at,end-at));}).ToArray();
 }
 public byte[] Data{get;}
 public string[] Materials{get;}
 public bool Animated{get;private set;}
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
   Require(offset+76<=data.Length&&I(offset) is >=0 and <=2,"Unsupported external NMF node");int nodeType=I(offset);
   int end=checked(offset+I(offset+4));
   bool Boundary(int at)=>n+1==nodes?at==data.Length:at>=0&&at+72<=data.Length&&I(at) is >=0 and <=3&&data[at+8]>=32&&data.AsSpan(at+8,64).Contains((byte)0);
   if(!Boundary(end))end-=4;Require(Boundary(end),"Unknown NMF boundary");
   if(nodeType!=0){Require(end-offset==292,"Unsupported bone/helper NMF layout");_=Name(offset+8);for(int i=0;i<54;i++)Require(float.IsFinite(F(offset+76+i*4)),"Invalid bone matrix");Animated=true;offset=end;continue;}Require(offset+260<=end,"Truncated NMF mesh");
   Require(I(offset+228)==1,"Embedded multi-LOD node not supported; use separate LOD NMF files");
   int nv=I(offset+236),ni=I(offset+240),ng=I(offset+244),flags=I(offset+252);
   Require(I(offset+248)==0&&I(offset+256)==0&&nv is >0 and <=65536&&ni is >0 and <=3000000&&ni%3==0&&ng is >0 and <=512,"Unsupported skin or invalid geometry");
   int geometryFlags=flags&~0x30000,skinBytes=(flags&0x30000)==0x30000?12:0;Require((flags&0x30000)==0||skinBytes==12,"Unsupported partial skin stream");int baseStride=geometryFlags is 0x40139 or 0x4013a or 0x13a or 0x139?56:geometryFlags is 0x4011a or 0x119?44:geometryFlags is 0x108 or 0x109?32:0;int stride=baseStride+skinBytes,faceStride=(flags&0x40000)!=0?40:0;Animated|=skinBytes>0;
   Require(baseStride!=0,"Unsupported NMF vertex stream: "+flags.ToString("x"));
   int ix=offset+260,attr=checked(ix+ni*2),face=checked(attr+nv*stride),groups=checked(face+ni/3*faceStride);
   Require(groups>=attr&&groups+ng*12<=end,"Truncated NMF draw groups");
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
    int uvAt=attr+nv*(baseStride-8)+v*8;uv[v*2]=F(uvAt);uv[v*2+1]=F(uvAt+4);
   }
   var indices=new ushort[ni];for(int i=0;i<ni;i++){indices[i]=U(ix+i*2);Require(indices[i]<nv,"Invalid NMF vertex index");}
   var draw=new DrawGroup[ng];var groupOffsets=new int[ng];int consumed=0,groupAt=groups;
   for(int g=0;g<ng;g++){int at=groupAt;Require(at+12<=end,"Truncated draw group");int first=I(at),length=I(at+4),material=U(at+8),joints=U(at+10);Require(first==consumed&&length>0&&length%3==0&&first+length<=ni&&material<count&&joints<=256&&at+12+joints*2<=end&&(skinBytes>0||joints==0),"Invalid draw group");for(int j=0;j<joints;j++)Require(U(at+12+j*2)<nodes,"Invalid skin bone reference");if(skinBytes>0){Require(joints>0,"Skinned draw group has no bones");for(int i=first;i<first+length;i++)for(int j=0;j<4;j++){int vertex=indices[i];ushort weight=U(attr+nv*baseStride+vertex*8+j*2);if(weight!=0)Require(data[attr+nv*(baseStride+8)+vertex*4+j]<joints,"Invalid local bone index");}}draw[g]=new(first,length,material);groupOffsets[g]=at;consumed+=length;groupAt+=12+joints*2;}
   Require(groupAt==end,"Unsupported NMF trailing streams");
   Require(consumed==ni,"Incomplete draw groups");
   Nodes.Add(new(Name(offset+8),offset,end,nv,ni,ix,attr,face,groups,stride,faceStride,draw,positions,normals,uv,indices,groupOffsets,skinBytes));offset=end;
  }
  Require(offset==data.Length&&Nodes.Count>0,"Trailing data or no NMF meshes");
 }
 public int I(int at)=>BitConverter.ToInt32(Data,at);
 public ushort U(int at)=>BitConverter.ToUInt16(Data,at);
 public float F(int at)=>BitConverter.ToSingle(Data,at);
 string Name(int at){Require(at>=0&&at+64<=Data.Length,"Truncated NMF name");int end=Array.IndexOf(Data,(byte)0,at,64);Require(end>=at,"Unterminated NMF name");return DecodeName(Data.AsSpan(at,end-at));}

 public byte[] AssignGlass(ISet<string> nodeNames,string glassName="Aurora_Glass")
 {
  Require(!Materials.Contains(glassName),"Choose the original NMF, not a previously exported glass model");
  Require(Nodes.Any(x=>nodeNames.Contains(x.Name)),"Selected glass object is missing from this LOD");
  using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
  byte[] glassBytes=new UTF8Encoding(false,true).GetBytes(glassName);Require(glassBytes.Length is >0 and <=63&&!glassName.Any(char.IsControl),"Invalid glass material name");writer.Write(Data,0,20+Materials.Length*64);writer.Write(glassBytes.Concat(new byte[64-glassBytes.Length]).ToArray());
  int payloadStart=20+Materials.Length*64;var payload=Data.AsSpan(payloadStart).ToArray();foreach(var node in Nodes)if(nodeNames.Contains(node.Name))for(int g=0;g<node.Groups.Length;g++)BitConverter.GetBytes((ushort)Materials.Length).CopyTo(payload,node.DrawOffset(g)-payloadStart+8);writer.Write(payload);
  var result=stream.ToArray();BitConverter.GetBytes(Materials.Length+1).CopyTo(result,8);BitConverter.GetBytes(result.Length).CopyTo(result,16);
  _=new NativeModel(result);return result;
 }
}
