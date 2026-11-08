using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
namespace CMCStudio.Engine
{
    public sealed class MenuPortrait
    {
        public string Key { get; init; }
        public int ResourceId { get; init; }
        public long BlockOffset { get; init; }
        public int Frame { get; init; }
        public string Label { get; init; }
        public long PaletteOffset { get; init; }
        public long PixelOffset { get; init; }
        public bool SharedPalette { get; init; }
        public PortraitInfo Info { get; init; }
        internal Func<PortraitInfo> SmallPalette { get; init; }
        public byte[] Import(byte[] image)
        {
            using var stream=new MemoryStream(image);var payload=PortraitCodec.Import(Info,stream,SharedPalette,SharedPalette?null:SmallPalette?.Invoke());
            return SharedPalette?payload.AsSpan(512).ToArray():payload;
        }
        public void Validate(byte[] bytes)
        {
            if(Info.RgbaPixels!=null)throw new InvalidDataException("Use full-color mode for this converted menu image.");
            int expected=Info.Width*Info.Height+(SharedPalette?0:512);
            if(bytes==null || bytes.Length!=expected)throw new InvalidDataException("Menu portrait size mismatch.");
            if(!SharedPalette)for(int i=0;i<256;i++)if((bytes[2*i+1]&128)!=(Info.PaletteAndPixels[2*i+1]&128))throw new InvalidDataException("Menu portrait palette alpha flags changed.");
        }
        public byte[] Preview(byte[] edit=null)
        {
            if(edit==null)return PortraitCodec.ToPng(Info);
            Validate(edit);var bytes=(byte[])Info.PaletteAndPixels.Clone();edit.CopyTo(bytes,SharedPalette?512:0);return PortraitCodec.ToPng(Info,bytes);
        }
        public void AddPatches(PatchSet plan,byte[] bytes)
        {
            Validate(bytes);
            if(SharedPalette)plan.Add(PixelOffset,bytes);
            else {plan.Add(PaletteOffset,bytes.AsSpan(0,512).ToArray());plan.Add(PixelOffset,bytes.AsSpan(512).ToArray());}
        }
    }
    public static class MenuPortraits
    {
        public static int MonsterId(int portraitId)=>portraitId>=0&&portraitId<277?portraitId:portraitId>=512&&portraitId<789?portraitId-512:-1;
        public static MenuPortrait Resolve(string source,string key)
        {
            var parts=key.Split('/');
            if(parts.Length!=2||!int.TryParse(parts[0],NumberStyles.None,CultureInfo.InvariantCulture,out int id)||id<0||id>=277||!int.TryParse(parts[1],out int variant)||variant<0||variant>3||key!=$"{id}/{variant}")throw new InvalidDataException("Unknown menu portrait key.");
            int resource,block,frame,count;bool shared=variant<2;
            if(variant==0){resource=id<168?494:495;int local=id<168?id:id-168;block=local/56;frame=local%56;count=resource==495&&block==2?24:56;}
            else if(variant==1){resource=496;block=id/56;frame=id%56;count=block==5?24:56;}
            else if(variant==2){resource=1008;block=id;frame=0;count=1;}
            else {int bank=Math.Min(id/64,3);resource=1009+bank;block=id-bank*64;frame=0;count=1;}
            var asset=ModelArchives.Resolve(source,Catalog.Data.Assets.Single(a=>a.Type==7&&a.Id==resource));
            using var f=File.OpenRead(source);
            byte[] Read(long at,int size){if(at<asset.Iso || at+size>asset.Iso+asset.Size)throw new InvalidDataException("Menu portrait outside archive member.");var b=new byte[size];f.Position=at;f.ReadExactly(b);return b;}
            long pointer=asset.Iso+4L*block;long start=pointer+BitConverter.ToUInt32(Read(pointer,4));
            byte[] header=Read(start,4);int magic=BitConverter.ToUInt16(header);if((magic!=0x5f51&&magic!=0x5f52)||BitConverter.ToUInt16(header,2)!=count)throw new InvalidDataException("Unsupported menu portrait block layout.");
            byte[] descriptor=Read(start+4+16L*frame,16);int width=BitConverter.ToUInt16(descriptor,8),height=BitConverter.ToUInt16(descriptor,10);
            if(magic!=0x5f52&&(width!=(shared?64:112)||height!=(shared?72:128)))throw new InvalidDataException("Unexpected menu portrait dimensions.");
            long palette=start+8+16L*count,pixels=start+BitConverter.ToUInt32(descriptor);
            if(pixels<palette+(magic==0x5f52?0:512))throw new InvalidDataException("Menu portrait overlaps metadata.");
            var info=new PortraitInfo{Width=width,Height=height};
            if(magic==0x5f52){var raw=Read(start,checked((int)(asset.Iso+asset.Size-start)));info=FullColorPortraits.Read(raw,0,frame);}
            else{var payload=new byte[512+width*height];Read(palette,512).CopyTo(payload,0);Read(pixels,width*height).CopyTo(payload,512);info.PaletteAndPixels=payload;}
            return new MenuPortrait {Key=key,ResourceId=resource,BlockOffset=start,Frame=frame,Label=$"{(shared?"Small menu icon":"Named menu card")} — D_FACE {resource}, frame {block}/{frame}",PaletteOffset=palette,PixelOffset=pixels,SharedPalette=shared&&magic!=0x5f52,Info=info,SmallPalette=shared?null:()=>Resolve(source,$"{id}/0").Info};
        }
        public static List<MenuPortrait> ForMonster(string source,int id)
        {
            if(id<0||id>=277)throw new InvalidDataException("Linked menu portraits are available for monster portrait IDs 0–276 and 512–788.");
            return Enumerable.Range(0,4).Select(v=>Resolve(source,$"{id}/{v}")).ToList();
        }
    }
}
