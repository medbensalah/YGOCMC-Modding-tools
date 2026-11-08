using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SkiaSharp;
namespace CMCStudio.Engine;
public sealed record MapTextureImage(string Key,int Width,int Height,byte[] Png);
public sealed partial class AlchemyModel
{
    private Block MapTextureBlock()
    {
        if(Bytes.Length<64||BitConverter.ToInt32(Bytes,0)!=16||BitConverter.ToUInt32(Bytes,56)!=0xfada)throw new InvalidDataException("Texture rebuilding requires a supported wrapped map resource.");
        var block=new Block(Bytes,16);block.Read();int game=BitConverter.ToInt32(Bytes,4)+4;
        if(game<block.End||game>Bytes.Length)throw new InvalidDataException("Invalid map section bounds.");
        return block;
    }
    public List<MapTextureImage> MapTextures()=>MapTextureBlock().Images().Select(t=>new MapTextureImage(t.id.ToString(),t.w,t.h,Encode(t.bitmap))).ToList();
    private static byte[] Encode(SKBitmap bitmap){using(bitmap){using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(SKEncodedImageFormat.Png,100);return data.ToArray();}}
    public byte[] ReplaceMapTextures(Dictionary<string,byte[]> enhanced,int scale)
    {
        if(scale is not (1 or 2 or 4))throw new InvalidDataException("ISO map texture scale must be 1, 2 or 4.");
        var block=MapTextureBlock();var changes=new Dictionary<int,(int w,int h,byte[] pixels)>();
        foreach(var t in block.Images())using(t.bitmap)
        {
            if(!enhanced.TryGetValue(t.id.ToString(),out var png))throw new InvalidDataException("An enhanced map texture is missing.");
            using var high=SKBitmap.Decode(png)??throw new InvalidDataException("Invalid enhanced PNG.");int w=Math.Max(t.w,Math.Min(1024,t.w*scale)),h=Math.Max(t.h,Math.Min(1024,t.h*scale));
            using var resized=high.Resize(new SKImageInfo(w,h),new SKSamplingOptions(SKCubicResampler.Mitchell));
            var pixels=block.TexturePixels(t.id,resized,t.bitmap);changes.Add(t.id,(w,h,pixels));
        }
        var result=block.RepackMap(changes);
        if(result.Length>16*1024*1024)throw new InvalidDataException("Enhanced map exceeds the experimental 16 MB resource budget. Choose 2x or export only.");
        return result;
    }
    private bool ValidateMapTextureReplacement(byte[] replacement)
    {
        try{
            if(replacement.Length>16*1024*1024)return false;var original=MapTextureBlock();var changed=new AlchemyModel(replacement).MapTextureBlock();var changes=new Dictionary<int,(int w,int h,byte[] pixels)>();
            var sourceImages=original.Images();var images=changed.Images();if(sourceImages.Count!=images.Count)return false;
            foreach(var a in sourceImages)using(a.bitmap)
            {
                var b=images.Single(x=>x.id==a.id);b.bitmap.Dispose();
                if(!new[]{1,2,4}.Any(scale=>b.w==Math.Max(a.w,Math.Min(1024,a.w*scale))&&b.h==Math.Max(a.h,Math.Min(1024,a.h*scale))))return false;
                var im=changed.Objects[a.id];var mem=changed.Objects[im.Get(13)];changes.Add(a.id,(b.w,b.h,replacement.AsSpan(mem.Offset,mem.Size).ToArray()));
            }
            return original.RepackMap(changes).AsSpan().SequenceEqual(replacement);
        }catch(InvalidDataException){return false;}
    }
    private sealed partial class Block
    {
        public List<(int id,int w,int h,SKBitmap bitmap)> Images()
        {
            var result=new List<(int,int,int,SKBitmap)>();
            for(int id=0;id<Objects.Length;id++)
            {
                var im=Objects[id];if(im?.Type!="igImage")continue;int w=im.Get(2),h=im.Get(3),channels=im.Get(4);
                if(w<1||h<1||w>1024||h>1024)throw new InvalidDataException("Unsupported map image dimensions.");
                var palette=im.Get(17)>=0?Ref(Ref(im.Get(17),"igClut").Get(5),"memory"):null;
                if(palette==null&&channels is not (3 or 4))throw new InvalidDataException("Map contains an unsupported single-channel texture.");
                if(palette!=null&&(channels!=1||palette.Size!=1024||im.Get(18)!=8))throw new InvalidDataException("Map contains an unsupported palette format.");
                if(im.Get(14)>=0&&Ref(im.Get(14)).Get(2)>0)throw new InvalidDataException("Mipmap rebuilding is not supported for this map.");
                var data=Ref(im.Get(13),"memory");if(data.Size!=w*h*channels)throw new InvalidDataException("Unsupported map texture allocation.");
                var bitmap=new SKBitmap(w,h);
                for(int y=0;y<h;y++)for(int x=0;x<w;x++){int p=data.Offset+(y*w+x)*channels;if(palette!=null)p=palette.Offset+_bytes[p]*4;bitmap.SetPixel(x,y,new SKColor(_bytes[p],_bytes[p+1],_bytes[p+2],palette!=null||channels==4?_bytes[p+3]:(byte)255));}
                result.Add((id,w,h,bitmap));
            }
            if(result.Count==0)throw new InvalidDataException("No editable map textures found.");return result;
        }
        public byte[] TexturePixels(int id,SKBitmap enhanced,SKBitmap original)
        {
            var im=Ref(id);int channels=im.Get(4),w=enhanced.Width,h=enhanced.Height;var output=new byte[w*h*channels];
            var palette=im.Get(17)>=0?Ref(Ref(im.Get(17)).Get(5),"memory"):null;var cache=new Dictionary<uint,byte>();
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                var c=enhanced.GetPixel(x,y);byte alpha=original.GetPixel(x*original.Width/w,y*original.Height/h).Alpha;int at=(y*w+x)*channels;
                if(palette!=null){uint key=(uint)((c.Red>>3)<<19|(c.Green>>3)<<14|(c.Blue>>3)<<9|alpha);if(!cache.TryGetValue(key,out byte index)){long best=long.MaxValue;index=0;for(int j=0;j<256;j++){int p=palette.Offset+4*j;long d=(c.Red-_bytes[p])*(c.Red-_bytes[p])+(c.Green-_bytes[p+1])*(c.Green-_bytes[p+1])+(c.Blue-_bytes[p+2])*(c.Blue-_bytes[p+2])+4*(alpha-_bytes[p+3])*(alpha-_bytes[p+3]);if(d<best){best=d;index=(byte)j;}}cache[key]=index;}output[at]=index;}
                else{output[at]=c.Red;output[at+1]=c.Green;output[at+2]=c.Blue;if(channels==4)output[at+3]=alpha;}
            }return output;
        }
        public byte[] RepackMap(Dictionary<int,(int w,int h,byte[] pixels)> changes)
        {
            var objectBytes=_bytes.AsSpan(_objectStart,_memoryStart-_objectStart).ToArray();var memories=new Dictionary<int,byte[]>();
            void Set(Record r,int slot,int v)=>BitConverter.GetBytes(v).CopyTo(objectBytes,r.Offsets[slot]-_objectStart);
            foreach(var c in changes)
            {
                var im=Ref(c.Key,"igImage");int memory=im.Get(13);var pixels=Ref(memory,"memory");
                if(c.Value.pixels.Length!=c.Value.w*c.Value.h*im.Get(4))throw new InvalidDataException("Invalid texture pixel count.");
                if(memories.TryGetValue(memory,out var prior)&&!prior.AsSpan().SequenceEqual(c.Value.pixels))throw new InvalidDataException("Aliased textures need incompatible pixel data.");
                memories[memory]=c.Value.pixels;Set(im,2,c.Value.w);Set(im,3,c.Value.h);Set(im,12,c.Value.pixels.Length);Set(im,19,c.Value.w*im.Get(4));
            }
            // Reject any geometry or non-image consumer of a resized image allocation.
            foreach(var va in Objects.Where(r=>r?.Type=="igVertexArray1_1")){var channel=Ref(va.Get(2),"memory");for(int i=0;i<19;i++)if(memories.ContainsKey(I(channel.Offset+4*i)))throw new InvalidDataException("Texture memory aliases geometry.");}
            using var dirs=new MemoryStream();foreach(var pair in _dirs.Select((r,i)=>(r,i))){var bytes=_bytes.AsSpan(pair.r.Offset,pair.r.Size).ToArray();if(memories.TryGetValue(pair.i,out var pixels))BitConverter.GetBytes(pixels.Length).CopyTo(bytes,pair.r.Offsets[7]-pair.r.Offset);dirs.Write(bytes);}
            using var mem=new MemoryStream();for(int i=0;i<Objects.Length;i++)if(Objects[i]?.Type=="memory"){var r=Objects[i];mem.Write(memories.TryGetValue(i,out var bytes)?bytes:_bytes.AsSpan(r.Offset,r.Size).ToArray());while(mem.Length%4!=0)mem.WriteByte(0);}
            using var block=new MemoryStream();block.Write(_bytes.AsSpan(_start,_dirStart-_start));block.Write(dirs.ToArray());using(var writer=new BinaryWriter(block,System.Text.Encoding.UTF8,true)){writer.Write(8+_dirs.Length*2);writer.Write(_dirs.Length);for(int i=0;i<_dirs.Length;i++)writer.Write((ushort)i);}
            block.Write(_bytes.AsSpan(_indexEnd,_objectStart-_indexEnd));block.Write(objectBytes);block.Write(mem.ToArray());var rebuilt=block.ToArray();BitConverter.GetBytes((int)dirs.Length).CopyTo(rebuilt,0);BitConverter.GetBytes(_dirs.Length).CopyTo(rebuilt,4);BitConverter.GetBytes((int)mem.Length).CopyTo(rebuilt,24);
            int game=BitConverter.ToInt32(_bytes,4)+4,newGame=(16+rebuilt.Length+2047)&~2047,delta=newGame-game;using var output=new MemoryStream();output.Write(_bytes.AsSpan(0,16));output.Write(rebuilt);while(output.Length<newGame)output.WriteByte(0);output.Write(_bytes.AsSpan(game));var result=output.ToArray();
            for(int p=4;p<16;p+=4)BitConverter.GetBytes(checked(BitConverter.ToInt32(_bytes,p)+delta)).CopyTo(result,p);
            return result;
        }
    }
}
