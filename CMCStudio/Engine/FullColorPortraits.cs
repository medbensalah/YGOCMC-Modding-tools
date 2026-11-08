using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;
namespace CMCStudio.Engine
{
    public static class FullColorPortraits
    {
        public static PortraitInfo Read(byte[] bytes,int start,int frame=0)
        {
            int magic=BitConverter.ToUInt16(bytes,start),count=BitConverter.ToUInt16(bytes,start+2);
            if((magic!=0x5f51&&magic!=0x5f52)||frame<0||frame>=count)throw new InvalidDataException("Unsupported portrait block.");
            int d=start+4+16*frame,w=BitConverter.ToUInt16(bytes,d+8),h=BitConverter.ToUInt16(bytes,d+10),p=checked(start+(int)BitConverter.ToUInt32(bytes,d));
            if(w<1||h<1||w>2048||h>2048)throw new InvalidDataException("Invalid portrait dimensions.");
            int n=checked(w*h);var info=new PortraitInfo{Width=w,Height=h};
            info.DisplayWidth=w;info.DisplayHeight=h;
            if(magic==0x5f52){if((bytes[d+4]&1)!=0){info.DisplayWidth=checked((int)BitConverter.ToSingle(bytes,p-8)+BitConverter.ToInt16(bytes,d+12));info.DisplayHeight=checked((int)BitConverter.ToSingle(bytes,p-4)+BitConverter.ToInt16(bytes,d+14));}info.RgbaPixels=bytes.AsSpan(p,checked(n*4)).ToArray();return info;}
            int palette=start+8+16*count;info.PaletteAndPixels=new byte[512+n];bytes.AsSpan(palette,512).CopyTo(info.PaletteAndPixels);bytes.AsSpan(p,n).CopyTo(info.PaletteAndPixels.AsSpan(512));return info;
        }
        public static byte[] Pixels(PortraitInfo info)
        {
            if(info.RgbaPixels!=null)return (byte[])info.RgbaPixels.Clone();
            var rgba=new byte[info.Width*info.Height*4];var b=info.PaletteAndPixels;
            for(int i=0;i<info.Width*info.Height;i++){int index=b[512+i];ushort color=BitConverter.ToUInt16(b,index*2);rgba[i*4]=(byte)((color&31)<<3);rgba[i*4+1]=(byte)(((color>>5)&31)<<3);rgba[i*4+2]=(byte)(((color>>10)&31)<<3);rgba[i*4+3]=(byte)(index==0?0:128);}
            return rgba;
        }
        public static byte[] ImportPixels(byte[] image,int w,int h)
        {
            using var decoded=SKBitmap.Decode(image)??throw new InvalidDataException("Invalid portrait image.");
            using var resized=decoded.Width==w&&decoded.Height==h?decoded.Copy():decoded.Resize(new SKImageInfo(w,h),new SKSamplingOptions(SKCubicResampler.Mitchell));
            if(resized==null)throw new InvalidDataException("Portrait resize failed.");var b=new byte[w*h*4];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){var c=resized.GetPixel(x,y);int p=(y*w+x)*4;b[p]=c.Red;b[p+1]=c.Green;b[p+2]=c.Blue;b[p+3]=(byte)((c.Alpha*128+127)/255);}
            return b;
        }
        public static (Asset asset,int block,int frame) Locate(string source,string key)
        {
            if(key.StartsWith("detail/",StringComparison.Ordinal)&&int.TryParse(key.Substring(7),out int id)&&key==$"detail/{id}")
            {
                var asset=Catalog.Data.Assets.SingleOrDefault(a=>a.Type==7&&a.Id==id)??throw new InvalidDataException("Unknown portrait.");
                if(MenuPortraits.MonsterId(id)<0)throw new InvalidDataException("Full-color import supports ordinary monster portraits.");
                return(ModelArchives.Resolve(source,asset),0,0);
            }
            if(key.StartsWith("menu/",StringComparison.Ordinal))
            {
                var target=MenuPortraits.Resolve(source,key.Substring(5));var asset=ModelArchives.Resolve(source,Catalog.Data.Assets.Single(a=>a.Type==7&&a.Id==target.ResourceId));return(asset,checked((int)(target.BlockOffset-asset.Iso)),target.Frame);
            }
            throw new InvalidDataException("Unknown full-color portrait key.");
        }
        public static (int width,int height) ImageSize(byte[] image)
        {
            using var stream=new MemoryStream(image);using var codec=SKCodec.Create(stream)??throw new InvalidDataException("Invalid portrait image.");
            int w=codec.Info.Width,h=codec.Info.Height;
            if(w<1||h<1||w>1024||h>1024)throw new InvalidDataException("Original-size portraits must be 1–1024 pixels per dimension. Each image must fit a 1 MiB RGBA page, such as 512 × 512; each shared set supports four pages.");
            return(w,h);
        }
        // Collection/shop load shared banks, not just the selected monster. Keep
        // their resident images at the game's native dimensions even when detail
        // portraits retain the imported resolution (including older projects).
        public static (int width,int height) OutputSize(string key,byte[] image,PortraitInfo info,bool originalSize)
        {
            if(key.StartsWith("menu/",StringComparison.Ordinal))
            {
                int variant=int.Parse(key.Substring(key.LastIndexOf('/')+1),System.Globalization.CultureInfo.InvariantCulture);
                return variant<2?(64,72):(112,128);
            }
            return DetailSize(originalSize?ImageSize(image):(info.Width,info.Height));
        }
        // Experimental battle budget: retain full color at increased resolution
        // without the 1 MiB pixel allocation made by a 512-square import.
        // Keep the imported PNG unchanged in the project.
        public static (int width,int height) DetailSize((int width,int height) size)
        {
            int longest=Math.Max(size.width,size.height);
            if(longest<=256)return size;
            return (Math.Max(1,(int)Math.Round(size.width*256.0/longest)),Math.Max(1,(int)Math.Round(size.height*256.0/longest)));
        }
        private static byte[] ResizePixels(PortraitInfo info,int w,int h)
        {
            return info.Width==w&&info.Height==h?Pixels(info):ImportPixels(PortraitCodec.ToPng(info),w,h);
        }
        public static Dictionary<string,byte[]> Build(string source,Dictionary<string,byte[]> edits,ISet<string> originalSizes=null)
        {
            var locations=edits.Select(e=>(location:Locate(source,e.Key),image:e.Value,native:originalSizes?.Contains(e.Key)==true)).ToList();var result=new Dictionary<string,byte[]>();
            using var f=File.OpenRead(source);
            foreach(var group in locations.GroupBy(e=>e.location.asset.Id))
            {
                var asset=group.First().location.asset;byte[] bytes=new byte[asset.Size];f.Position=asset.Iso;f.ReadExactly(bytes);
                foreach(var block in group.GroupBy(e=>e.location.block).OrderByDescending(g=>g.Key))
                {
                    int start=block.Key,count=BitConverter.ToUInt16(bytes,start+2),headerSize=8+16*count,oldEnd=start+headerSize;
                    var frames=new List<byte[]>();var replacements=block.ToDictionary(e=>e.location.frame);var dimensions=new List<(int w,int h,int logicalW,int logicalH)>();
                    for(int i=0;i<count;i++)
                    {
                        var info=Read(bytes,start,i);int p=start+checked((int)BitConverter.ToUInt32(bytes,start+4+16*i));oldEnd=Math.Max(oldEnd,p+info.Width*info.Height*(info.RgbaPixels==null?1:4));
                        int w=info.Width,h=info.Height;
                        bool replaced=replacements.TryGetValue(i,out var edit);
                        // Normalize all frames in a rebuilt menu bank, including
                        // oversized frames inherited from an already modded ISO.
                        bool menu=asset.Id is 494 or 495 or 496 or 1008 or 1009 or 1010 or 1011 or 1012;
                        if(menu)(w,h)=asset.Id<=496?(64,72):(112,128);
                        else if(replaced&&edit.native)(w,h)=ImageSize(edit.image);
                        if(!menu)(w,h)=DetailSize((w,h));
                        dimensions.Add((w,h,info.DisplayWidth,info.DisplayHeight));
                        frames.Add(replaced?ImportPixels(edit.image,w,h):ResizePixels(info,w,h));
                    }
                    // Eight bytes before each pixel span preserve logical geometry independently of its resolution.
                    int length=checked(headerSize+32+frames.Sum(b=>b.Length)+8*count);var rebuilt=new byte[length];bytes.AsSpan(start,headerSize).CopyTo(rebuilt);rebuilt[0]=0x52;BitConverter.GetBytes(0).CopyTo(rebuilt,headerSize-4);
                    if(asset.Id is 494 or 495 or 496)PackMenu(rebuilt,count);
                    else Pack(rebuilt,dimensions);
                    int at=headerSize+32;
                    for(int i=0;i<count;i++)
                    {
                        int d=4+16*i;var size=dimensions[i];
                        BitConverter.GetBytes((float)(size.logicalW-BitConverter.ToInt16(rebuilt,d+12))).CopyTo(rebuilt,at);
                        BitConverter.GetBytes((float)(size.logicalH-BitConverter.ToInt16(rebuilt,d+14))).CopyTo(rebuilt,at+4);at+=8;
                        BitConverter.GetBytes(at).CopyTo(rebuilt,d);
                        BitConverter.GetBytes((ushort)size.w).CopyTo(rebuilt,d+8);BitConverter.GetBytes((ushort)size.h).CopyTo(rebuilt,d+10);
                        frames[i].CopyTo(rebuilt,at);at+=frames[i].Length;
                    }
                    int delta=length-(oldEnd-start);var output=new byte[checked(bytes.Length+delta)];bytes.AsSpan(0,start).CopyTo(output);rebuilt.CopyTo(output,start);bytes.AsSpan(oldEnd).CopyTo(output.AsSpan(start+length));
                    if(start!=0)
                    {
                        int table=checked((int)BitConverter.ToUInt32(bytes));if(table%4!=0||table>start)throw new InvalidDataException("Unsupported portrait container.");
                        for(int i=0;i<table;i+=4){uint value=BitConverter.ToUInt32(bytes,i);if(value==0)continue;long target=(long)value+i;if(target>=oldEnd)BitConverter.GetBytes(checked((uint)(value+delta))).CopyTo(output,i);else if(target>start)throw new InvalidDataException("Portrait container pointer overlaps image data.");}
                    }
                    bytes=output;
                }
                if(asset.Id>=1008&&asset.Id<=1012)bytes=ResidentPortraitCards.Compact(bytes);
                if(asset.Id is 494 or 495 or 496)bytes=ResidentPortraitAtlases.Compact(bytes);
                Array.Resize(ref bytes,(bytes.Length+2047)&~2047);result[ModSession.ModelKey(asset)]=bytes;
            }
            return result;
        }
        private static void PackMenu(byte[] header,int count)
        {
            // Shop enlarged portraits use the native thumbnail atlas layout.
            // Generic packing can choose 64x1024 strips across four pages;
            // those are incompatible with the shop's fixed atlas coordinates.
            int columns=count==56?8:count==24?4:throw new InvalidDataException("Unknown menu atlas layout.");
            int top=count==56?8:0;
            for(int i=0;i<count;i++)
                BitConverter.GetBytes((uint)(((i%columns*64)<<20)|((top+i/columns*72)<<8)|0x81)).CopyTo(header,8+16*i);
            int table=8+16*count;
            BitConverter.GetBytes(columns*64).CopyTo(header,table);
            BitConverter.GetBytes(512).CopyTo(header,table+4);
        }
        private static void Pack(byte[] header,List<(int w,int h,int logicalW,int logicalH)> sizes)
        {
            // The game has four image slots per sprite set. Keep each GPU upload at
            // most 1 MiB, rather than uploading a 2–4 MiB shared shop atlas.
            int bestArea=int.MaxValue;Dictionary<int,(int x,int y,int page)> best=null;
            List<(int w,int h)> bestPages=null;
            int Rounded(int n){int p=1;while(p<n)p*=2;return p;}
            foreach(int width in new[]{64,128,256,512,1024})
            {
                int maxHeight=Math.Min(1024,262144/width);
                var pages=new List<List<(int y,int h,int used)>>();var positions=new Dictionary<int,(int x,int y,int page)>();bool fits=true;
                foreach(int i in Enumerable.Range(0,sizes.Count).OrderByDescending(i=>sizes[i].h).ThenByDescending(i=>sizes[i].w))
                {
                    var s=sizes[i];if(s.w>width||s.h>maxHeight){fits=false;break;}
                    bool placed=false;
                    for(int page=0;page<4;page++)
                    {
                        if(page==pages.Count)pages.Add(new List<(int y,int h,int used)>());
                        var shelves=pages[page];int shelf=shelves.FindIndex(row=>row.h>=s.h&&row.used+s.w<=width);
                        if(shelf<0){int height=shelves.Sum(row=>row.h);if(height+s.h>maxHeight)continue;shelf=shelves.Count;shelves.Add((height,s.h,0));}
                        var row=shelves[shelf];positions[i]=(row.used,row.y,page);shelves[shelf]=(row.y,row.h,row.used+s.w);placed=true;break;
                    }
                    if(!placed){fits=false;break;}
                }
                if(!fits)continue;
                var dimensions=pages.Select(rows=>(w:Rounded(rows.Max(row=>row.used)),h:Rounded(rows.Sum(row=>row.h)))).ToList();
                int area=dimensions.Sum(p=>p.w*p.h);
                if(area<bestArea){bestArea=area;best=positions;bestPages=dimensions;}
            }
            if(best==null)throw new InvalidDataException("These images exceed four texture pages of 1 MiB each. Each image must fit a page (for example 512 × 512); use a smaller image.");
            foreach(var p in best)BitConverter.GetBytes((uint)((p.Value.x<<20)|(p.Value.y<<8)|0x81|((bestPages.Count-1)<<4)|(p.Value.page<<1))).CopyTo(header,8+16*p.Key);
            int table=8+16*sizes.Count;
            for(int i=0;i<bestPages.Count;i++){BitConverter.GetBytes(bestPages[i].w).CopyTo(header,table+8*i);BitConverter.GetBytes(bestPages[i].h).CopyTo(header,table+8*i+4);}
        }
    }
}

