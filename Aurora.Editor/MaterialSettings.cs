namespace Aurora.Editor;
record MaterialSettings(string? Base=null,string? Normal=null,string? Orm=null,float Metallic=.1f,float Roughness=.5f,float Ao=1,float NormalStrength=1,bool Native=true,string? MetalMap=null,string? RoughMap=null,string? AoMap=null,string? Emissive=null,float EmissiveStrength=1,float Ibl=1,int NormalY=-1,string? Specular=null);
