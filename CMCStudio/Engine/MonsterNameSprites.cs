using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;
namespace CMCStudio.Engine
{
    public static class MonsterNameSprites
    {
        // D_SYS 0 embeds the same two Q71 name banks as standalone resources
        // 10 and 11: IDs 0-155 and 156-285 (the latter includes Symbols).
        // Index zero is transparent; retain every palette and sprite descriptor.
        public static void Apply(string source,IEnumerable<MonsterNameRow> rows)
        {
            var edits=rows.Where(r=>r.Changed).ToArray();
            if(edits.Length==0)return;
            using var face=SKTypeface.FromFamilyName("Times New Roman",SKFontStyle.Bold);
            foreach(int resource in new[]{0,10,11})
            {
                var asset=ModelArchives.Resolve(source,Catalog.Data.Assets.Single(a=>a.Type==3&&a.Id==resource));
                byte[] bytes=new byte[asset.Size];
                using(var input=File.OpenRead(source)){input.Position=asset.Iso;input.ReadExactly(bytes);}
                var patches=new List<(int Offset,byte[] Pixels)>();
                foreach(var row in edits)
                {
                    bool second=row.Id>=156;
                    if(resource==10&&second||resource==11&&!second)continue;
                    int block=resource==0?checked((int)BitConverter.ToUInt32(bytes,second?4:0)+(second?4:0)):0;
                    int frame=second?row.Id-156:row.Id;
                    int count=BitConverter.ToUInt16(bytes,block+2);
                    if(BitConverter.ToUInt16(bytes,block)!=0x5f71||count!=(second?130:156))throw new InvalidDataException("Unsupported monster-name sprite bank.");
                    int descriptor=block+4+16*frame;
                    int width=BitConverter.ToUInt16(bytes,descriptor+8),height=BitConverter.ToUInt16(bytes,descriptor+10);
                    int pixels=checked(block+(int)BitConverter.ToUInt32(bytes,descriptor));
                    int palette=block+8+16*count;
                    if(width!=192||height!=32||pixels<palette+32||pixels+width*height/2>bytes.Length||BitConverter.ToUInt32(bytes,palette-4)!=16)throw new InvalidDataException("Unsupported monster-name sprite layout.");
                    using var bitmap=new SKBitmap(width,height);
                    using var canvas=new SKCanvas(bitmap);canvas.Clear(SKColors.Transparent);
                    using var font=new SKFont(face,24);
                    using var fill=new SKPaint{Color=SKColors.White,IsAntialias=true};
                    using var outline=new SKPaint{Color=SKColors.Black,IsAntialias=true,Style=SKPaintStyle.Stroke,StrokeWidth=2};
                    float measured=font.MeasureText(row.Name,fill);
                    if(measured>width-6)font.Size*=(width-6)/measured;
                    float baseline=height/2f-(font.Metrics.Ascent+font.Metrics.Descent)/2;
                    canvas.DrawText(row.Name,width/2f,baseline,SKTextAlign.Center,font,outline);
                    canvas.DrawText(row.Name,width/2f,baseline,SKTextAlign.Center,font,fill);canvas.Flush();
                    var packed=new byte[width*height/2];
                    for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                    {
                        var color=bitmap.GetPixel(x,y);int index=0;
                        if(color.Alpha>=96)
                        {
                            int best=int.MaxValue;
                            for(int i=1;i<16;i++)
                            {
                                int entry=BitConverter.ToUInt16(bytes,palette+2*i);
                                int dr=color.Red-(entry&31)*255/31,dg=color.Green-((entry>>5)&31)*255/31,db=color.Blue-((entry>>10)&31)*255/31;
                                int distance=dr*dr+dg*dg+db*db;
                                if(distance<best){best=distance;index=i;}
                            }
                        }
                        int pixel=y*width+x;packed[pixel/2]|=(byte)(index<<((pixel&1)*4));
                    }
                    patches.Add((pixels,packed));
                }
                using var output=new FileStream(source,FileMode.Open,FileAccess.Write,FileShare.Read);
                foreach(var patch in patches){output.Position=asset.Iso+patch.Offset;output.Write(patch.Pixels);}
            }
        }
    }
}
