using System.Buffers.Binary;
namespace Aurora.Core;
public record RgbaImage(int Width,int Height,byte[] Pixels);
public static class DdsImage
{
 public static byte[] Encode(RgbaImage image)
 {
  if(image.Width<1||image.Height<1||image.Width>8192||image.Height>8192||image.Pixels.Length!=image.Width*image.Height*4)throw new InvalidDataException("Invalid RGBA image");
  var result=new byte[128+image.Pixels.Length];"DDS "u8.CopyTo(result);void W(int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(at,4),value);
  W(4,124);W(8,0x100f);W(12,(uint)image.Height);W(16,(uint)image.Width);W(20,(uint)image.Width*4);W(76,32);W(80,0x41);W(88,32);W(92,0xff);W(96,0xff00);W(100,0xff0000);W(104,0xff000000);W(108,0x1000);image.Pixels.CopyTo(result,128);return result;
 }
 public static RgbaImage Decode(byte[] data)
 {
  if(data.Length<128||!data.AsSpan(0,4).SequenceEqual("DDS "u8))throw new InvalidDataException("Invalid DDS");
  int I(int at)=>BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at,4));
  int width=I(16),height=I(12);if(width is <1 or >8192||height is <1 or >8192)throw new InvalidDataException("DDS dimensions unsupported");
  var pixels=new byte[width*height*4];int four=I(84);
  if(four==0)
  {
   if(I(88)!=32||data.Length<128+pixels.Length)throw new InvalidDataException("Only RGBA8 and BC1/BC3 DDS supported");
   uint[] masks=[(uint)I(92),(uint)I(96),(uint)I(100),(uint)I(104)];
   for(int i=0;i<width*height;i++){uint value=BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(128+i*4,4));for(int c=0;c<4;c++){uint mask=masks[c];pixels[i*4+c]=mask==0?(byte)(c==3?255:0):(byte)(((value&mask)>>System.Numerics.BitOperations.TrailingZeroCount(mask))*255/(mask>>System.Numerics.BitOperations.TrailingZeroCount(mask)));}}
  }
  else
  {
   bool bc3=four==0x35545844,bc1=four==0x31545844;
   if(four==0x32495441||four==0x55354342)return DecodeBc5(data,width,height,128);
   if(four==0x30315844&&data.Length>=148&&I(128)==83)return DecodeBc5(data,width,height,148);
   if(!bc1&&!bc3)throw new InvalidDataException("Unsupported DDS compression (use RGBA8/BC3 for glass)");
   int size=bc3?16:8,offset=128;int[] Colour(int v)=>[(v>>11&31)*255/31,(v>>5&63)*255/63,(v&31)*255/31,255];
   for(int y=0;y<height;y+=4)for(int x=0;x<width;x+=4)
   {
    if(offset+size>data.Length)throw new InvalidDataException("Truncated DDS block");int colourAt=offset+(bc3?8:0);
    int c0=BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(colourAt,2)),c1=BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(colourAt+2,2));
    var palette=new[]{Colour(c0),Colour(c1),new int[4],new int[4]};
    for(int c=0;c<3;c++){palette[2][c]=(2*palette[0][c]+palette[1][c])/3;palette[3][c]=(palette[0][c]+2*palette[1][c])/3;}palette[2][3]=palette[3][3]=255;
    if(bc1&&c0<=c1){for(int c=0;c<3;c++)palette[2][c]=(palette[0][c]+palette[1][c])/2;palette[3]=[0,0,0,0];}
    var alpha=new int[8];ulong alphaBits=0;if(bc3){alpha[0]=data[offset];alpha[1]=data[offset+1];for(int a=2;a<8;a++)alpha[a]=alpha[0]>alpha[1]?((8-a)*alpha[0]+(a-1)*alpha[1])/7:a<6?((6-a)*alpha[0]+(a-1)*alpha[1])/5:a==6?0:255;for(int b=0;b<6;b++)alphaBits|=(ulong)data[offset+2+b]<<(8*b);}
    uint codes=BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(colourAt+4,4));
    for(int p=0;p<16;p++){int px=x+p%4,py=y+p/4;if(px>=width||py>=height)continue;int target=(py*width+px)*4,code=(int)((codes>>(p*2))&3);for(int c=0;c<4;c++)pixels[target+c]=(byte)palette[code][c];if(bc3)pixels[target+3]=(byte)alpha[(alphaBits>>(p*3))&7];}
    offset+=size;
   }
  }
  return new(width,height,pixels);
 }
 static RgbaImage DecodeBc5(byte[] data,int width,int height,int at)
 {
  var pixels=new byte[width*height*4];
  byte[] Block(int offset){var p=new int[8];p[0]=data[offset];p[1]=data[offset+1];for(int i=2;i<8;i++)p[i]=p[0]>p[1]?((8-i)*p[0]+(i-1)*p[1])/7:i<6?((6-i)*p[0]+(i-1)*p[1])/5:i==6?0:255;ulong codes=0;for(int b=0;b<6;b++)codes|=(ulong)data[offset+2+b]<<(8*b);return Enumerable.Range(0,16).Select(i=>(byte)p[(codes>>(3*i))&7]).ToArray();}
  for(int y=0;y<height;y+=4)for(int x=0;x<width;x+=4){if(at+16>data.Length)throw new InvalidDataException("Truncated BC5");var r=Block(at);var g=Block(at+8);for(int p=0;p<16;p++){int px=x+p%4,py=y+p/4;if(px>=width||py>=height)continue;int target=(py*width+px)*4;float nx=r[p]/127.5f-1,ny=g[p]/127.5f-1;pixels[target]=r[p];pixels[target+1]=g[p];pixels[target+2]=(byte)Math.Clamp((MathF.Sqrt(Math.Max(0,1-nx*nx-ny*ny))+1)*127.5f,0,255);pixels[target+3]=255;}at+=16;}
  return new(width,height,pixels);
 }
}
