using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
namespace Aurora.Core;

public sealed record ObjTextureMap(string File,Vector2 Scale,Vector2 Offset,float Bump,bool Clamp,string[] Warnings)
{
 public static ObjTextureMap Parse(string text)
 {
  var tokens=Regex.Matches(text,"\"[^\"]*\"|\\S+").Select(m=>(Text:m.Value.Trim('"'),m.Index)).ToArray();
  var scale=Vector2.One;var offset=Vector2.Zero;float bump=1;bool clamp=false;var warnings=new List<string>();int i=0;
  bool Number(string v,out float n)=>float.TryParse(v,NumberStyles.Float,CultureInfo.InvariantCulture,out n)&&float.IsFinite(n);
  while(i<tokens.Length&&tokens[i].Text.StartsWith('-')){
   string option=tokens[i++].Text;
   if(option is "-s" or "-o" or "-t"){
    var values=new List<float>();while(i<tokens.Length&&values.Count<3&&Number(tokens[i].Text,out float n)){values.Add(n);i++;}
    if(values.Count==0)throw new InvalidDataException("Missing OBJ map option value: "+option);
    if(option=="-s")scale=new(values[0],values.Count>1?values[1]:1);
    else if(option=="-o")offset=new(values[0],values.Count>1?values[1]:0);
    else if(values.Any(v=>v!=0))warnings.Add("-t");
    if(values.Count>2&&values[2]!=(option=="-s"?1:0))warnings.Add(option+" W");
   }else if(option is "-bm" or "-clamp" or "-blendu" or "-blendv" or "-imfchan" or "-type" or "-texres" or "-boost" or "-cc"){
    if(i>=tokens.Length)throw new InvalidDataException("Missing OBJ map option value: "+option);string value=tokens[i++].Text;
    if(option=="-bm"){if(!Number(value,out bump))throw new InvalidDataException("Invalid bump multiplier");}
    else if(option=="-clamp"){if(value is not ("on" or "off"))throw new InvalidDataException("Invalid texture clamp");clamp=value=="on";}
    else if(option is not "-texres"&&!(option is "-blendu" or "-blendv"&&value=="on"))warnings.Add(option+" "+value);
   }else if(option=="-mm"){
    for(int n=0;n<2;n++){if(i>=tokens.Length||!Number(tokens[i++].Text,out _))throw new InvalidDataException("Invalid map range");}warnings.Add(option);
   }else throw new InvalidDataException("Unsupported OBJ map option: "+option);
  }
  if(i>=tokens.Length)throw new InvalidDataException("Missing OBJ map filename");
  string file=text[tokens[i].Index..].Trim().Trim('"');if(file.Length==0)throw new InvalidDataException("Missing OBJ map filename");
  return new(file,scale,offset,bump,clamp,warnings.ToArray());
 }
}
