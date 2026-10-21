using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Aurora.Core;
public record ObjImportResult(NativeModel Model,Dictionary<string,string> MaterialNames,string[] Libraries,int MissingUvCorners=0);

public static class ObjImporter
{
 readonly record struct Corner(int Position,int Uv,int Normal);
 sealed record Face(Corner[] Corners,int Material,int Smoothing,int Id,Vector3 Normal);
 sealed class Part(string name){public string Name=name;public List<Face> Faces=[];}
 readonly record struct VertexKey(Corner Corner,int Smooth,int Face,int Material);
 sealed class Chunk(string name){public string Name=name;public List<Vector3> P=[],N=[];public List<Vector2> U=[];public List<ushort> I=[];public List<DrawGroup> Groups=[];public Dictionary<VertexKey,ushort> Vertices=[];}
 static float Number(string text){if(!float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out float n)||!float.IsFinite(n)||MathF.Abs(n)>1e7f)throw new InvalidDataException("Invalid OBJ coordinate");return n;}
 static Vector3 Position(string[] words){if(words.Length is not (4 or 5))throw new InvalidDataException("OBJ vertex needs XYZ and optional W");float w=words.Length==5?Number(words[4]):1;if(MathF.Abs(w)<1e-10f)throw new InvalidDataException("OBJ vertex W is zero");var p=new Vector3(Number(words[1]),Number(words[2]),Number(words[3]))/w;if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z)||MathF.Max(MathF.Abs(p.X),MathF.Max(MathF.Abs(p.Y),MathF.Abs(p.Z)))>1e7f)throw new InvalidDataException("OBJ vertex is outside supported coordinates");return p;}
 public static string Identifier(string value,string fallback="Object"){
  string clean=new(value.Select(c=>char.IsAsciiLetterOrDigit(c)||c is '_' or '-' or '.'?c:'_').ToArray());if(clean.Length==0)clean=fallback;
  if(clean!=value||clean.Length>63)clean=clean[..Math.Min(54,clean.Length)]+"_"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..8];return clean;
 }
 public static ObjImportResult Parse(string text)
 {
  if(text.Length>64*1024*1024)throw new InvalidDataException("OBJ exceeds 64 MiB");
  var positions=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var parts=new List<Part>();
  var materials=new Dictionary<string,string>(StringComparer.Ordinal);var materialIndices=new Dictionary<string,int>(StringComparer.Ordinal);var libraries=new List<string>();
  var current=new Part("Object");parts.Add(current);int material=-1,smoothing=0,faceId=0,triangles=0,lineNumber=0;
  int Material(string source){if(materialIndices.TryGetValue(source,out int found))return found;if(materials.Count>=512)throw new InvalidDataException("OBJ has too many materials");string id=Identifier(source,"Material");if(materials.Values.Contains(id))throw new InvalidDataException("OBJ material names collide");materialIndices[source]=materials.Count;materials[source]=id;return materials.Count-1;}
  int Index(string token,int count){if(!int.TryParse(token,NumberStyles.Integer,CultureInfo.InvariantCulture,out int value)||value==0)throw new InvalidDataException("Invalid OBJ index");long index=value>0?(long)value-1:(long)count+value;if(index<0||index>=count)throw new InvalidDataException("OBJ index is out of range");return (int)index;}
  using var reader=new StringReader(text.TrimStart('\ufeff'));string? line;
  while((line=reader.ReadLine())!=null){lineNumber++;int comment=line.IndexOf('#');if(comment>=0)line=line[..comment];line=line.Trim();if(line.Length==0)continue;
   try{
    var words=line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);string rest=line.Length>words[0].Length?line[words[0].Length..].Trim():"";
    switch(words[0]){
     case "v":positions.Add(Position(words));break;
     case "vt":if(words.Length<3)throw new InvalidDataException("OBJ texture coordinate needs UV");uv.Add(new(Number(words[1]),1-Number(words[2])));break;
     case "vn":if(words.Length!=4)throw new InvalidDataException("OBJ normal needs XYZ");var n=new Vector3(Number(words[1]),Number(words[2]),Number(words[3]));if(n.LengthSquared()<1e-12f)throw new InvalidDataException("OBJ normal is zero");normals.Add(Vector3.Normalize(n));break;
     case "o":case "g":if(current.Faces.Count==0)current.Name=rest;else{current=new(rest);parts.Add(current);}break;
     case "s":if(rest is "off" or "0")smoothing=0;else if(!int.TryParse(rest,out smoothing)||smoothing<1)throw new InvalidDataException("Invalid OBJ smoothing group");break;
     case "usemtl":if(rest.Length==0)throw new InvalidDataException("OBJ material name is empty");material=Material(rest);break;
     case "mtllib":if(rest.Length>0)libraries.Add(rest.Trim('"'));break;
     case "f":
      if(words.Length<4||words.Length>1025)throw new InvalidDataException("OBJ face must have 3–1024 corners");
      var corners=words.Skip(1).Select(word=>{var p=word.Split('/');if(p.Length>3||p[0].Length==0)throw new InvalidDataException("Invalid OBJ face corner");return new Corner(Index(p[0],positions.Count),p.Length>1&&p[1].Length>0?Index(p[1],uv.Count):-1,p.Length>2&&p[2].Length>0?Index(p[2],normals.Count):-1);}).ToArray();
      if(material<0)material=Material("Material");var polygon=Triangulate(corners,positions);triangles+=polygon.Triangles.Count;if(triangles>500000)throw new InvalidDataException("OBJ exceeds 500000 triangles");
      // Keep each polygon together for flat normals; smoothing is accumulated below.
      current.Faces.Add(new(corners,material,smoothing,faceId++,polygon.Normal));break;
     case "l":case "p":case "curv":case "surf":throw new InvalidDataException("Only polygonal OBJ geometry is supported");
    }
    if(positions.Count>1000000||normals.Count>1000000||uv.Count>1000000||parts.Count>2048)throw new InvalidDataException("OBJ exceeds geometry limits");
   }catch(InvalidDataException ex){throw new InvalidDataException("OBJ line "+lineNumber+": "+ex.Message,ex);}
  }
  if(triangles==0)throw new InvalidDataException("OBJ has no polygon faces");
  var chunks=new List<Chunk>();var names=new HashSet<string>(StringComparer.Ordinal);
  foreach(var part in parts.Where(p=>p.Faces.Count>0)){
   var smooth=new Dictionary<(int,int),Vector3>();foreach(var face in part.Faces.Where(f=>f.Smoothing>0))foreach(int v in face.Corners.Select(c=>c.Position).Distinct()){var key=(v,face.Smoothing);smooth[key]=smooth.GetValueOrDefault(key)+face.Normal;}
   string Name(){string stem=Identifier(part.Name);string name=stem;int i=2;while(!names.Add(name)){string suffix="_"+i++;name=stem[..Math.Min(stem.Length,63-suffix.Length)]+suffix;}return name;}
   var chunk=new Chunk(Name());chunks.Add(chunk);
   foreach(var group in part.Faces.GroupBy(f=>f.Material))foreach(var face in group){var polygon=Triangulate(face.Corners,positions);foreach(var triangle in polygon.Triangles){
    var keys=triangle.Select(i=>{var c=face.Corners[i];return new VertexKey(c,c.Normal>=0?0:face.Smoothing,c.Normal>=0||face.Smoothing>0?0:face.Id,face.Material);}).ToArray();
    if(chunk.P.Count+keys.Distinct().Count(k=>!chunk.Vertices.ContainsKey(k))>65536){chunk=new(Name());chunks.Add(chunk);}
    int first=chunk.I.Count;
    foreach(var key in keys){if(!chunk.Vertices.TryGetValue(key,out ushort index)){index=checked((ushort)chunk.P.Count);chunk.Vertices[key]=index;var c=key.Corner;var normal=c.Normal>=0?normals[c.Normal]:face.Smoothing>0?smooth[(c.Position,face.Smoothing)]:face.Normal;if(normal.LengthSquared()<1e-12f)throw new InvalidDataException("OBJ smoothing normals cancel to zero");chunk.P.Add(positions[c.Position]);chunk.N.Add(Vector3.Normalize(normal));chunk.U.Add(c.Uv>=0?uv[c.Uv]:Vector2.Zero);}chunk.I.Add(index);}
    if(chunk.Groups.Count>0&&chunk.Groups[^1].Material==group.Key)chunk.Groups[^1]=chunk.Groups[^1] with{Count=chunk.Groups[^1].Count+3};else chunk.Groups.Add(new(first,3,group.Key));
   }}
  }
  if(chunks.Count>2048)throw new InvalidDataException("OBJ needs too many NMF objects");
  return new(new NativeModel(Write(chunks,materials.Values.ToArray())),materials,libraries.ToArray(),parts.Sum(p=>p.Faces.Sum(f=>f.Corners.Count(c=>c.Uv<0))));
 }
 sealed record Polygon(Vector3 Normal,List<int[]> Triangles);
 static Polygon Triangulate(Corner[] corners,List<Vector3> source)
 {
  var p=corners.Select(c=>source[c.Position]).ToArray();Vector3 origin=p[0],normal=Vector3.Zero;
  for(int i=1;i<p.Length-1;i++)normal+=Vector3.Cross(p[i]-origin,p[i+1]-origin);
  if(!float.IsFinite(normal.LengthSquared())||normal.LengthSquared()<1e-14f)throw new InvalidDataException("Degenerate OBJ polygon");Vector3 unit=Vector3.Normalize(normal);
  float extent=p.Max(v=>(v-origin).Length());if(p.Any(v=>MathF.Abs(Vector3.Dot(v-origin,unit))>MathF.Max(1e-5f,extent*1e-4f)))throw new InvalidDataException("Non-planar OBJ polygon");
  int axis=MathF.Abs(unit.X)>MathF.Abs(unit.Y)?(MathF.Abs(unit.X)>MathF.Abs(unit.Z)?0:2):(MathF.Abs(unit.Y)>MathF.Abs(unit.Z)?1:2);
  var q=p.Select(v=>axis==0?new Vector2(v.Y-origin.Y,v.Z-origin.Z):axis==1?new Vector2(v.X-origin.X,v.Z-origin.Z):new Vector2(v.X-origin.X,v.Y-origin.Y)).ToArray();
  double Cross(Vector2 a,Vector2 b,Vector2 c)=>(double)(b.X-a.X)*(c.Y-a.Y)-(double)(b.Y-a.Y)*(c.X-a.X);
  double area=0;for(int i=0;i<q.Length;i++)area+=(double)q[i].X*q[(i+1)%q.Length].Y-(double)q[(i+1)%q.Length].X*q[i].Y;
  double sign=Math.Sign(area),epsilon=Math.Max(1e-12,Math.Abs(area)*1e-10);if(sign==0)throw new InvalidDataException("Degenerate OBJ polygon");
  for(int i=0;i<q.Length;i++)for(int j=i+1;j<q.Length;j++){
   if(j==i+1||(i==0&&j==q.Length-1))continue;
   Vector2 a=q[i],b=q[(i+1)%q.Length],c=q[j],d=q[(j+1)%q.Length];double x=Cross(a,b,c),y=Cross(a,b,d),z=Cross(c,d,a),w=Cross(c,d,b);
   if(x*y<=epsilon*epsilon&&z*w<=epsilon*epsilon&&Math.Max(Math.Min(a.X,b.X),Math.Min(c.X,d.X))<=Math.Min(Math.Max(a.X,b.X),Math.Max(c.X,d.X))+epsilon&&Math.Max(Math.Min(a.Y,b.Y),Math.Min(c.Y,d.Y))<=Math.Min(Math.Max(a.Y,b.Y),Math.Max(c.Y,d.Y))+epsilon)throw new InvalidDataException("Self-intersecting OBJ polygon");
  }
  var remaining=Enumerable.Range(0,q.Length).ToList();var triangles=new List<int[]>();
  while(remaining.Count>3){bool found=false;for(int i=0;i<remaining.Count;i++){int a=remaining[(i+remaining.Count-1)%remaining.Count],b=remaining[i],c=remaining[(i+1)%remaining.Count];if(sign*Cross(q[a],q[b],q[c])<=epsilon)continue;
    bool inside=remaining.Any(v=>v!=a&&v!=b&&v!=c&&sign*Cross(q[a],q[b],q[v])>=-epsilon&&sign*Cross(q[b],q[c],q[v])>=-epsilon&&sign*Cross(q[c],q[a],q[v])>=-epsilon);if(inside)continue;
    triangles.Add([a,b,c]);remaining.RemoveAt(i);found=true;break;}
   if(!found)throw new InvalidDataException("OBJ polygon cannot be triangulated");}
  if(sign*Cross(q[remaining[0]],q[remaining[1]],q[remaining[2]])<=epsilon)throw new InvalidDataException("Degenerate OBJ triangle");triangles.Add(remaining.ToArray());return new(normal,triangles);
 }
 static byte[] Write(List<Chunk> chunks,string[] materials)
 {
  using var stream=new MemoryStream();using var w=new BinaryWriter(stream);void Name(string name){var bytes=Encoding.ASCII.GetBytes(name);w.Write(bytes);w.Write(new byte[64-bytes.Length]);}void V(Vector3 v){w.Write(v.X);w.Write(v.Y);w.Write(v.Z);}void Patch(long at,int value){long end=stream.Position;stream.Position=at;w.Write(value);stream.Position=end;}
  w.Write("fromObj\0"u8);w.Write(materials.Length);w.Write(chunks.Count);w.Write(0);foreach(string name in materials)Name(name);
  foreach(var c in chunks){long start=stream.Position;w.Write(0);w.Write(0);Name(c.Name);w.Write((ushort)65535);w.Write((ushort)0);
   for(int m=0;m<2;m++)for(int i=0;i<16;i++)w.Write(i%5==0?1f:0f);
   V(new(c.P.Min(p=>p.X),c.P.Min(p=>p.Y),c.P.Min(p=>p.Z)));V(new(c.P.Max(p=>p.X),c.P.Max(p=>p.Y),c.P.Max(p=>p.Z)));w.Write(1);
   long lod=stream.Position;w.Write(0);w.Write(c.P.Count);w.Write(c.I.Count);w.Write(c.Groups.Count);w.Write(0);w.Write(0x108);w.Write(0);
   foreach(ushort i in c.I)w.Write(i);foreach(var p in c.P)V(p);foreach(var n in c.N)V(n);foreach(var u in c.U){w.Write(u.X);w.Write(u.Y);}foreach(var g in c.Groups){w.Write(g.First);w.Write(g.Count);w.Write((ushort)g.Material);w.Write((ushort)0);}
   Patch(start+4,checked((int)(stream.Position-start+4)));Patch(lod,checked((int)(stream.Position-lod+8)));if(stream.Length>128*1024*1024)throw new InvalidDataException("NMF exceeds 128 MiB");
  }
  Patch(16,checked((int)stream.Length));return stream.ToArray();
 }
}
