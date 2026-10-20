using System.Globalization;
using System.Numerics;
namespace Aurora.Core;
public sealed record SmokePoint(string Effect,Vector3 Position);
public static class VehicleSmoke
{
 public static SmokePoint[] Read(string script)=>VehicleScript.Fields(script).Where(f=>f.Key=="PARTICLE_MOVEMENT").Select(f=>{var v=f.Value.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);if(v.Length!=4)throw new InvalidDataException("Unsupported movement smoke point");var xyz=v.Skip(1).Select(t=>float.Parse(t,CultureInfo.InvariantCulture)).ToArray();if(xyz.Any(n=>!float.IsFinite(n)))throw new InvalidDataException("Invalid smoke coordinates");return new SmokePoint(v[0],new(xyz[0],xyz[1],xyz[2]));}).ToArray();
 public static string Set(string script,int index,SmokePoint? point){if(point==null)return VehicleScript.Set(script,"PARTICLE_MOVEMENT",index,null);if(point.Effect.Any(char.IsWhiteSpace)||point.Effect.Length==0||new[]{point.Position.X,point.Position.Y,point.Position.Z}.Any(n=>!float.IsFinite(n)))throw new InvalidDataException("Invalid smoke point");return VehicleScript.Set(script,"PARTICLE_MOVEMENT",index,point.Effect+" "+string.Join(" ",new[]{point.Position.X,point.Position.Y,point.Position.Z}.Select(n=>n.ToString("G9",CultureInfo.InvariantCulture))));}
}
