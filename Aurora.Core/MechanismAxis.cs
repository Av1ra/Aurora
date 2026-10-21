using System.Globalization;
using System.Numerics;
namespace Aurora.Core;

// A diagnostic pose, never a baked model or an external animation.
public sealed record MechanismAxis(string Mesh,Vector3 Start,Vector3 End)
{
 public static bool Supports(string? key)=>key is "DUMPER_DECK_PIVOT" or "DUMPER_JOINTDECK_PIVOT" or "MIXER_DRUM" or "MIXER_JOINTDRUM";
 public static MechanismAxis Read(string script,string key,int occurrence)
 {
  if(!Supports(key))throw new InvalidDataException("Unsupported mechanism axis");
  var args=VehicleScript.Tokens(script).Where(t=>t.Key==key&&t.Occurrence==occurrence).Select(t=>t.Value).ToArray();bool mixer=key.StartsWith("MIXER",StringComparison.Ordinal);
  if(args.Length<(mixer?7:4))throw new InvalidDataException("Missing mechanism mesh or coordinates");
  Vector3 Point(int at){var v=new Vector3(float.Parse(args[at],CultureInfo.InvariantCulture),float.Parse(args[at+1],CultureInfo.InvariantCulture),float.Parse(args[at+2],CultureInfo.InvariantCulture));if(!float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z))throw new InvalidDataException("Invalid mechanism coordinates");return v;}
  var start=Point(1);var end=mixer?Point(4):start+Vector3.UnitX;if(!float.IsFinite(Vector3.DistanceSquared(start,end))||Vector3.DistanceSquared(start,end)<1e-10f)throw new InvalidDataException("Mechanism axis points must differ");return new(args[0],start,end);
 }
 public Matrix4x4 Rotation(float degrees){if(!float.IsFinite(degrees)||MathF.Abs(degrees)>360)throw new InvalidDataException("Invalid diagnostic angle");return Matrix4x4.CreateTranslation(-Start)*Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(End-Start),degrees*MathF.PI/180)*Matrix4x4.CreateTranslation(Start);}
 public MeshNode[] Pose(NativeModel model,float degrees)
 {
  if(model.Nodes.Count(n=>n.Name==Mesh)!=1)throw new InvalidDataException("Mechanism mesh missing or ambiguous: "+Mesh);
  var matrix=Rotation(degrees);return model.Nodes.Select(n=>{if(n.Name!=Mesh)return n;var positions=new float[n.Positions.Length];var normals=new float[n.Normals.Length];for(int i=0;i<n.VertexCount;i++){var p=Vector3.Transform(new(n.Positions[i*3],n.Positions[i*3+1],n.Positions[i*3+2]),matrix);var normal=Vector3.TransformNormal(new(n.Normals[i*3],n.Normals[i*3+1],n.Normals[i*3+2]),matrix);positions[i*3]=p.X;positions[i*3+1]=p.Y;positions[i*3+2]=p.Z;normals[i*3]=normal.X;normals[i*3+1]=normal.Y;normals[i*3+2]=normal.Z;}return n with{Positions=positions,Normals=normals};}).ToArray();
 }
}
