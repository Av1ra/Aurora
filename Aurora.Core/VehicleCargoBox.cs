using System.Globalization;
using System.Numerics;
namespace Aurora.Core;
public sealed record CargoBox(Vector3 Min,Vector3 Max)
{
 public Vector3 Centre=>(Min+Max)/2;
 public Vector3 Size=>Vector3.Abs(Max-Min);
 public CargoBox MoveScale(Vector3 centre,Vector3 size){if(size.X<0||size.Y<0||size.Z<0)throw new InvalidDataException("Cargo box size must be nonnegative");var sign=Max-Min;var half=size/2*new Vector3(sign.X<0?-1:1,sign.Y<0?-1:1,sign.Z<0?-1:1);return new(centre-half,centre+half);}
}
public static class VehicleCargoBox
{
 public static CargoBox? Read(string script,string key,int occurrence=0){if(key is not ("CARGOVEHICLE_VISUALIZATION" or "CARGOVEHICLE_VISUALIZATION_JOINT"))throw new InvalidDataException("Invalid vehicle cargo frame");if(!VehicleScript.Fields(script).Any(f=>f.Key==key&&f.Occurrence==occurrence))return null;var tokens=VehicleScript.Tokens(script).Where(t=>t.Key==key&&t.Occurrence==occurrence).Select(t=>t.Value).ToArray();if(tokens.Length<8||tokens[0]!="min"||tokens[4] is not ("max" or "min"))throw new InvalidDataException("Unsupported vehicle cargo box");Vector3 Point(int start){var v=tokens.Skip(start).Take(3).Select(t=>float.Parse(t,CultureInfo.InvariantCulture)).ToArray();if(v.Any(x=>!float.IsFinite(x)))throw new InvalidDataException("Invalid cargo box coordinates");return new(v[0],v[1],v[2]);}return new(Point(1),Point(5));}
 public static string[] Warnings(string script)=>VehicleScript.Tokens(script).Where(t=>t.Key is "CARGOVEHICLE_VISUALIZATION" or "CARGOVEHICLE_VISUALIZATION_JOINT"&&t.Index==4&&t.Value!="max").Select(t=>t.Key+" #"+(t.Occurrence+1)+": min/min").ToArray();
 public static string Write(string script,string key,int occurrence,CargoBox? box){if(key is not ("CARGOVEHICLE_VISUALIZATION" or "CARGOVEHICLE_VISUALIZATION_JOINT"))throw new InvalidDataException("Invalid vehicle cargo frame");if(box==null)return VehicleScript.RemoveEntry(script,key,occurrence);if(Read(script,key,occurrence)==box)return script;var values=new[]{box.Min.X,box.Min.Y,box.Min.Z,box.Max.X,box.Max.Y,box.Max.Z};if(values.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Invalid cargo box coordinates");var args=new[]{"min"}.Concat(values.Take(3).Select(v=>v.ToString("G9",CultureInfo.InvariantCulture))).Concat(new[]{"max"}).Concat(values.Skip(3).Select(v=>v.ToString("G9",CultureInfo.InvariantCulture))).ToArray();return VehicleScript.SetArguments(script,key,occurrence,args);}
 public static CargoLayout Preview(CargoBox box,NativeModel model){var bounds=ModelOrientation.Bounds(model);var modelSize=bounds.Max-bounds.Min;float scale=Math.Min(box.Size.X/Math.Max(.01f,modelSize.X),box.Size.Z/Math.Max(.01f,modelSize.Z));scale=Math.Max(.001f,scale);var position=new Vector3(box.Centre.X,box.Centre.Y,box.Centre.Z)-new Vector3((bounds.Min.X+bounds.Max.X)/2,bounds.Min.Y,(bounds.Min.Z+bounds.Max.Z)/2)*scale;return new(Vector3.Zero,position,new(scale),position,new(scale));}
}
