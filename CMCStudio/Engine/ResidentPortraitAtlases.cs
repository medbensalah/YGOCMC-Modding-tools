using System.IO;
namespace CMCStudio.Engine;
internal static class ResidentPortraitAtlases
{
    public static byte[] Compact(byte[] bytes)
    {
        int header=BitConverter.ToInt32(bytes);using var output=new MemoryStream();output.Write(bytes,0,header);
        for(int p=0;p<header-4;p+=4)
        {
            int relative=BitConverter.ToInt32(bytes,p);if(relative==0)break;
            int next=BitConverter.ToInt32(bytes,p+4);
            long at=output.Length;output.Position=p;output.Write(BitConverter.GetBytes(checked((int)at-p)));output.Position=at;
            if(next==0)break;
            int start=p+relative,end=p+4+next;var block=bytes.AsSpan(start,end-start).ToArray();
            output.Write(BitConverter.ToUInt16(block)==0x5f52?Convert(block):block);
        }
        return output.ToArray();
    }
    static byte[] Convert(byte[] block)
    {
        int count=BitConverter.ToUInt16(block,2),header=8+16*count;
        var pixels=Enumerable.Range(0,count).Select(i=>FullColorPortraits.Pixels(FullColorPortraits.Read(block,0,i))).ToArray();
        ushort Color(byte[] b,int p)=>(ushort)((b[p]>>3)|((b[p+1]>>3)<<5)|((b[p+2]>>3)<<10));
        var counts=new Dictionary<ushort,int>();
        foreach(var frame in pixels)for(int p=0;p<frame.Length;p+=4)if(frame[p+3]>=64){ushort c=Color(frame,p);counts[c]=counts.GetValueOrDefault(c)+1;}
        var palette=ResidentPortraitCards.Palette(counts);var nearest=new Dictionary<ushort,byte>();
        var result=new byte[header+512+pixels.Sum(p=>p.Length/4)];block.AsSpan(0,header).CopyTo(result);
        result[0]=0x51;BitConverter.GetBytes(256).CopyTo(result,header-4);
        for(int i=0;i<palette.Length;i++)BitConverter.GetBytes(palette[i]).CopyTo(result,header+2*(i+1));
        int at=header+512;
        for(int i=0;i<count;i++)
        {
            int descriptor=4+16*i;BitConverter.GetBytes(at).CopyTo(result,descriptor);
            // Keep the native atlas X/Y coordinates. Only the low byte belongs
            // to the full-color/page extension; clearing all four bytes makes
            // every portrait sample the same atlas location.
            uint placement=BitConverter.ToUInt32(block,descriptor+4);
            if((placement&0x76)!=0)throw new InvalidDataException("Cannot compact a non-native, multipage portrait atlas.");
            BitConverter.GetBytes(placement&0xffffff00u).CopyTo(result,descriptor+4);
            var frame=pixels[i];
            for(int p=0;p<frame.Length;p+=4)
            {
                if(frame[p+3]<64){at++;continue;}
                ushort c=Color(frame,p);
                if(!nearest.TryGetValue(c,out byte index))
                {
                    int best=int.MaxValue;index=1;
                    for(int j=0;j<palette.Length;j++){int dr=(c&31)-(palette[j]&31),dg=((c>>5)&31)-((palette[j]>>5)&31),db=((c>>10)&31)-((palette[j]>>10)&31);int distance=3*dr*dr+6*dg*dg+2*db*db;if(distance<best){best=distance;index=(byte)(j+1);}}
                    nearest[c]=index;
                }
                result[at++]=index;
            }
        }
        return result;
    }
}
