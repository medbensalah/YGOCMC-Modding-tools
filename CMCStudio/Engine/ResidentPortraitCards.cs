using System.IO;
using SkiaSharp;
namespace CMCStudio.Engine;

public static class ResidentPortraitCards
{
    public static byte[] Compact(byte[] bytes)
    {
        int header=checked((int)BitConverter.ToUInt32(bytes,0));
        if(header<8||header%4!=0||header>bytes.Length)throw new InvalidDataException("Invalid menu-card table.");
        using var output=new MemoryStream();output.Write(bytes,0,header);
        bool changed=false;
        for(int i=0;i<header/4-1;i++)
        {
            int pointer=i*4;uint relative=BitConverter.ToUInt32(bytes,pointer);
            if(relative==0)break;
            int start=checked(pointer+(int)relative);
            int nextPointer=pointer+4;
            uint next=BitConverter.ToUInt32(bytes,nextPointer);
            // The final nonzero entry is an end pointer, followed by zero.
            if(next==0){output.Position=pointer;output.Write(BitConverter.GetBytes(checked((uint)(output.Length-pointer))));break;}
            int end=checked(nextPointer+(int)next);
            if(start<header||end<start||end>bytes.Length)throw new InvalidDataException("Invalid menu-card span.");
            byte[] block=bytes.AsSpan(start,end-start).ToArray();
            if(block.Length>=4&&BitConverter.ToUInt16(block)==0x5f52)
            {
                if(BitConverter.ToUInt16(block,2)!=1)throw new InvalidDataException("Expected one frame per resident card.");
                var info=FullColorPortraits.Read(block,0);var pixels=FullColorPortraits.Pixels(info);
                if(info.Width!=112||info.Height!=128)throw new InvalidDataException("Resident cards must use native dimensions.");
                ushort Color(int p)=>(ushort)((pixels[p]>>3)|((pixels[p+1]>>3)<<5)|((pixels[p+2]>>3)<<10));
                var counts=new Dictionary<ushort,int>();
                for(int p=0;p<pixels.Length;p+=4)if(pixels[p+3]>=64){ushort c=Color(p);counts[c]=counts.GetValueOrDefault(c)+1;}
                var palette=Palette(counts);
                var converted=new byte[24+512+112*128];block.AsSpan(0,24).CopyTo(converted);
                converted[0]=0x51;BitConverter.GetBytes(536).CopyTo(converted,4);
                Array.Clear(converted,8,4);BitConverter.GetBytes(256).CopyTo(converted,20);
                for(int p=0;p<palette.Length;p++)BitConverter.GetBytes(palette[p]).CopyTo(converted,24+2*(p+1));
                var nearest=new Dictionary<ushort,byte>();
                int[] dither={-4,0,-3,1,2,-2,3,-1,1,-3,0,4,-1,3,-2,2};
                for(int p=0;p<pixels.Length;p+=4)
                {
                    if(pixels[p+3]<64)continue;
                    int x=(p/4)%112,y=(p/4)/112;
                    int bias=dither[(y&3)*4+(x&3)];
                    int r=Math.Clamp((pixels[p]+bias)>>3,0,31),g=Math.Clamp((pixels[p+1]+bias)>>3,0,31),b=Math.Clamp((pixels[p+2]+bias)>>3,0,31);
                    ushort c=(ushort)(r|(g<<5)|(b<<10));
                    if(!nearest.TryGetValue(c,out byte index))
                    {
                        int best=int.MaxValue;index=1;
                        for(int j=0;j<palette.Length;j++){int dr=(c&31)-(palette[j]&31),dg=((c>>5)&31)-((palette[j]>>5)&31),db=((c>>10)&31)-((palette[j]>>10)&31);int distance=3*dr*dr+6*dg*dg+2*db*db;if(distance<best){best=distance;index=(byte)(j+1);if(best==0)break;}}
                        nearest[c]=index;
                    }
                    converted[536+p/4]=index;
                }
                block=converted;changed=true;
            }
            long at=output.Length;output.Position=pointer;output.Write(BitConverter.GetBytes(checked((uint)(at-pointer))));output.Position=at;output.Write(block);
        }
        if(!changed)return bytes;
        var result=output.ToArray();Array.Resize(ref result,(result.Length+2047)&~2047);return result;
    }

    // Weighted median cut preserves uncommon but important colors that a
    // frequency-only palette would discard (outlines and bright highlights).
    internal static ushort[] Palette(Dictionary<ushort,int> counts)
    {
        if(counts.Count==0)return new ushort[]{0};
        var boxes=new List<List<(ushort color,int count)>>{counts.Select(kv=>(kv.Key,kv.Value)).ToList()};
        while(boxes.Count<255)
        {
            int chosen=-1;double score=0;int channel=0;
            for(int i=0;i<boxes.Count;i++)
            {
                var box=boxes[i];if(box.Count<2)continue;
                int weight=box.Sum(e=>e.count);
                for(int c=0;c<3;c++)
                {
                    int shift=c*5,low=box.Min(e=>(e.color>>shift)&31),high=box.Max(e=>(e.color>>shift)&31);
                    double value=(high-low)*(high-low)*(double)weight*(c==1?2:1);
                    if(value>score){score=value;chosen=i;channel=c;}
                }
            }
            if(chosen<0)break;
            var source=boxes[chosen].OrderBy(e=>(e.color>>(channel*5))&31).ThenBy(e=>e.color).ToList();
            int half=source.Sum(e=>e.count)/2,sum=0,split=1;
            while(split<source.Count-1&&sum+source[split-1].count<half){sum+=source[split-1].count;split++;}
            boxes[chosen]=source.GetRange(0,split);boxes.Add(source.GetRange(split,source.Count-split));
        }
        return boxes.Select(box=>
        {
            long weight=box.Sum(e=>(long)e.count);
            int r=(int)((box.Sum(e=>(long)(e.color&31)*e.count)+weight/2)/weight);
            int g=(int)((box.Sum(e=>(long)((e.color>>5)&31)*e.count)+weight/2)/weight);
            int b=(int)((box.Sum(e=>(long)((e.color>>10)&31)*e.count)+weight/2)/weight);
            return(ushort)(r|(g<<5)|(b<<10));
        }).ToArray();
    }
}

