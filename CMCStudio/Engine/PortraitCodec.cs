using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;
namespace CMCStudio.Engine
{
    public sealed class PortraitInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int DisplayWidth { get; set; }
        public int DisplayHeight { get; set; }
        public byte[] PaletteAndPixels { get; set; }
        public byte[] RgbaPixels { get; set; }
    }
    public static class PortraitCodec
    {
        public static PortraitInfo Read(string source,Asset asset)
        {
            asset=ModelArchives.Resolve(source,asset);
            if(asset.Type!=7 || asset.Flag) throw new InvalidDataException("Only unflagged D_FACE resources with the decoded header are supported.");
            using var f=File.OpenRead(source); f.Position=asset.Iso;
            var header=new byte[24]; f.ReadExactly(header);
            if(BitConverter.ToUInt16(header)==0x5f52){f.Position=asset.Iso;var full=new byte[asset.Size];f.ReadExactly(full);return FullColorPortraits.Read(full,0);}
            if(!header.Take(12).SequenceEqual(Convert.FromHexString("515f01001802000000000000"))) throw new InvalidDataException("This resource uses an unresolved image format; raw export is available.");
            int width=BitConverter.ToUInt16(header,12),height=BitConverter.ToUInt16(header,14);
            if(width<=0 || height<=0 || width>2048 || height>2048 || 536L+width*height>asset.Size) throw new InvalidDataException("Invalid portrait dimensions or span.");
            var payload=new byte[512+width*height]; f.ReadExactly(payload);
            return new PortraitInfo{Width=width,Height=height,PaletteAndPixels=payload};
        }
        public static void ValidateReplacement(string source,Asset asset,byte[] bytes)
        {
            var info=Read(source,asset);
            if(info.RgbaPixels!=null)throw new InvalidDataException("Use full-color import for this converted portrait.");
            if(bytes.Length!=info.PaletteAndPixels.Length) throw new InvalidDataException("Portrait replacement must preserve the palette and pixel span.");
            for(int i=0;i<256;i++)
                if((bytes[2*i+1]&0x80)!=(info.PaletteAndPixels[2*i+1]&0x80)) throw new InvalidDataException("Palette bit 15 must be preserved; alpha semantics are unresolved.");
        }
        public static byte[] Import(string source,Asset asset,Stream imageStream)
        {
            var info=Read(source,asset);int monster=MenuPortraits.MonsterId(asset.Id);
            var palette=monster>=0?MenuPortraits.Resolve(source,$"{monster}/0").Info:null;
            var output=Import(info,imageStream,palette!=null,palette);ValidateReplacement(source,asset,output);return output;
        }
        public static byte[] Import(PortraitInfo info,Stream imageStream,bool fixedPalette,PortraitInfo paletteSource=null)
        {
            if(info.RgbaPixels!=null)throw new InvalidDataException("Use full-color import for this converted portrait.");
            if(paletteSource!=null&&(paletteSource.RgbaPixels!=null||paletteSource.PaletteAndPixels==null||paletteSource.PaletteAndPixels.Length<512))throw new InvalidDataException("The small portrait has no indexed palette. Use full-color import or load the original ISO.");
            using var decoded=SKBitmap.Decode(imageStream) ?? throw new InvalidDataException("Unsupported or invalid imported image.");
            using var image=decoded.Resize(new SKImageInfo(info.Width,info.Height),new SKSamplingOptions(SKCubicResampler.Mitchell)) ?? throw new InvalidDataException("Image resize failed.");
            // Dimensions and byte count remain fixed. Large imports are downsampled.
            var histogram=new Dictionary<ushort,int>();
            ushort Pack(SKColor c) => (ushort)((c.Red*31/255)|((c.Green*31/255)<<5)|((c.Blue*31/255)<<10));
            for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++)
            {
                ushort color=Pack(image.GetPixel(x,y)); histogram[color]=histogram.TryGetValue(color,out int n)?n+1:1;
            }
            var palette=fixedPalette||paletteSource!=null?Enumerable.Range(0,256).Select(i=>(ushort)(BitConverter.ToUInt16((paletteSource??info).PaletteAndPixels,2*i)&0x7fff)).ToArray():histogram.OrderByDescending(x=>x.Value).ThenBy(x=>x.Key).Take(256).Select(x=>x.Key).ToArray();
            var output=(byte[])info.PaletteAndPixels.Clone();
            for(int i=0;i<palette.Length;i++)
            {
                ushort color=(ushort)(palette[i]|(BitConverter.ToUInt16(output,2*i)&0x8000));
                output[2*i]=(byte)color; output[2*i+1]=(byte)(color>>8);
            }
            var cache=new Dictionary<ushort,byte>();
            for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++)
            {
                ushort color=Pack(image.GetPixel(x,y));
                if(!cache.TryGetValue(color,out byte index))
                {
                    int best=int.MaxValue; index=0;
                    for(int i=0;i<palette.Length;i++)
                    {
                        int dr=(color&31)-(palette[i]&31),dg=((color>>5)&31)-((palette[i]>>5)&31),db=((color>>10)&31)-((palette[i]>>10)&31);
                        int distance=dr*dr+dg*dg+db*db;
                        if(distance<best){best=distance;index=(byte)i;}
                    }
                    cache[color]=index;
                }
                output[512+y*image.Width+x]=index;
            }
            return output;
        }
        public static byte[] ToPng(PortraitInfo info,byte[] replacement=null)
        {
            if(info.RgbaPixels!=null)
            {
                if(replacement!=null)throw new InvalidDataException("Indexed replacement on a full-color portrait.");
                using var rgba=new SKBitmap(info.Width,info.Height);var pixels=info.RgbaPixels;
                for(int y=0;y<info.Height;y++)for(int x=0;x<info.Width;x++){int p=(y*info.Width+x)*4;rgba.SetPixel(x,y,new SKColor(pixels[p],pixels[p+1],pixels[p+2],(byte)Math.Min(255,(pixels[p+3]*255+64)/128)));}
                using var encoded=rgba.Encode(SKEncodedImageFormat.Png,100);return encoded.ToArray();
            }
            byte[] bytes=replacement??info.PaletteAndPixels;
            if(bytes.Length!=512+info.Width*info.Height) throw new InvalidDataException("Invalid preview data.");
            using var image=new SKBitmap(info.Width,info.Height);
            for(int y=0;y<info.Height;y++) for(int x=0;x<info.Width;x++)
            {
                int index=bytes[512+y*info.Width+x]; ushort p=BitConverter.ToUInt16(bytes,2*index);
                image.SetPixel(x,y,new SKColor((byte)((p&31)*255/31),(byte)(((p>>5)&31)*255/31),(byte)(((p>>10)&31)*255/31),255));
            }
            using var output=image.Encode(SKEncodedImageFormat.Png,100); return output.ToArray();
        }
    }
}
