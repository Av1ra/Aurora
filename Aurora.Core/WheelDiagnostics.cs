using System.Numerics;
namespace Aurora.Core;
public sealed record WheelDiagnostic(string Mesh,float[] Pivot,float[] Axle,float[] SteeringAxis,bool Steering);
public static class WheelDiagnostics
{
 public static WheelDiagnostic[] Read(NativeModel model)=>model.Nodes.Where(n=>n.Name is "tire_leftShape" or "tire_rightShape" or "tire2Shape" or "tireShape").Select(n=>{
  float F(int offset)=>BitConverter.ToSingle(model.Data,n.Offset+76+offset*4);
  var m=new Matrix4x4(F(0),F(1),F(2),F(3),F(4),F(5),F(6),F(7),F(8),F(9),F(10),F(11),F(12),F(13),F(14),F(15));
  float[] Axis(Vector3 v){v=Vector3.Normalize(Vector3.TransformNormal(v,m));if(!float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z))throw new InvalidDataException("Invalid wheel axis");return [v.X,v.Y,v.Z];}
  return new WheelDiagnostic(n.Name,[m.M41,m.M42,m.M43],Axis(Vector3.UnitX),Axis(Vector3.UnitY),n.Name is "tire_leftShape" or "tire_rightShape");
 }).ToArray();
}
