using System.Globalization;
using System.Numerics;
namespace Aurora.Core;
public sealed record VehiclePart(string Id,string Model,string? Material,Matrix4x4 Transform);
public static class VehicleParts
{
 public static bool HasCabinAssembly(string? script)=>script!=null&&VehicleScript.Fields(script).Any(f=>f.Key=="TYPE"&&f.Value=="VEHICLETYPE_CABIN")&&VehicleScript.Tokens(script).Any(t=>t.Key=="CRANE_STATIC"&&t.Index==0)&&VehicleScript.Tokens(script).Any(t=>t.Key=="CRANE_DYNAMIC"&&t.Index==0);
 public static VehiclePart[] Read(string script,bool cabinAssembly=false)
 {
  var tokens=VehicleScript.Tokens(script);string[] Args(string key,int occurrence)=>tokens.Where(t=>t.Key==key&&t.Occurrence==occurrence).Select(t=>t.Value).ToArray();
  float N(string value){float v=float.Parse(value,CultureInfo.InvariantCulture);if(!float.IsFinite(v))throw new InvalidDataException("Invalid attachment value");return v;}
  Vector3 V(string[] a,int i)=>new(N(a[i]),N(a[i+1]),N(a[i+2]));var result=new List<VehiclePart>();
  var staticProp=Args("PROPELER_STATIC",0);if(staticProp.Length>0)foreach(var point in VehicleScript.Fields(script).Where(f=>f.Key=="PROPELER_POINT")){var a=Args(point.Key,point.Occurrence);if(a.Length<3)throw new InvalidDataException("Missing propeller coordinates");result.Add(new("propeller_"+point.Occurrence,staticProp[0],null,Matrix4x4.CreateTranslation(V(a,0))));}
  foreach(var field in VehicleScript.Fields(script).Where(f=>f.Key=="PROPELER_HELICOPTER")){var a=Args(field.Key,field.Occurrence);var p=Args("PROPELER_HELICOPTER_POINT_DIR",field.Occurrence);if(a.Length<5||p.Length<6)throw new InvalidDataException("Missing helicopter rotor model or coordinates");var r=V(p,3);var matrix=Matrix4x4.CreateScale(N(a[0]))*Matrix4x4.CreateRotationX(r.X)*Matrix4x4.CreateRotationY(r.Y)*Matrix4x4.CreateRotationZ(r.Z)*Matrix4x4.CreateTranslation(V(p,0));result.Add(new("rotor_"+field.Occurrence,a[1],a[4],matrix));}
  bool cabin=VehicleScript.Fields(script).Any(f=>f.Key=="TYPE"&&f.Value=="VEHICLETYPE_CABIN");
  // Cabin MAIN is a purchase model. Working bucket/trolley use the wire origin instead.
  if(!cabin||cabinAssembly)foreach(var field in VehicleScript.Fields(script).Where(f=>f.Key=="CRANE_STATIC")){var a=Args(field.Key,field.Occurrence);if(a.Length>0)result.Add(new("static_"+field.Occurrence,a[0],null,a.Length>=4?Matrix4x4.CreateTranslation(V(a,1)):Matrix4x4.Identity));}
  if(cabin&&cabinAssembly)foreach(var field in VehicleScript.Fields(script).Where(f=>f.Key=="CRANE_DYNAMIC")){var a=Args(field.Key,field.Occurrence);if(a.Length<4)throw new InvalidDataException("Missing cabin trolley model or coordinates");result.Add(new("trolley_"+field.Occurrence,a[0],null,Matrix4x4.CreateTranslation(V(a,1))));}
  return result.ToArray();
 }
}
