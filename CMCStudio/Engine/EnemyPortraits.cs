using System.IO;
using SkiaSharp;
namespace CMCStudio.Engine;

public static class EnemyPortraits
{
    static readonly (int Start,int End)[] Ranges={(16,24),(34,42),(50,58),(24,34),(42,50),(68,76),(58,68),(76,86),(96,104),(112,122),(104,112),(86,96),(122,130),(130,138),(138,148),(188,198),(156,168),(168,176),(176,188),(148,156)};
    public static Asset[] CharacterAssets(int script)
    {
        var range=Ranges[DialogueOwner(script)-1];
        return Catalog.Data.Assets.Where(a=>a.Type==2&&a.Id>=range.Start&&a.Id<range.End).ToArray();
    }
    public static byte[] CharacterBlock(ModSession session,int id)=>session.EnemyPortraitEdits.TryGetValue($"char/{id}",out var b)?b:Read(session.Source,ModelArchives.Resolve(session.Source,Catalog.Data.Assets.Single(a=>a.Type==2&&a.Id==id)));
    public static int Frames(byte[] block)=>Count(block);
    public static int DialogueOwner(int script)=>script switch {21=>13,22=>18,23=>19,24=>16,25=>17,_=>script};
    public static int LoadingResource(int script)=>32+CampaignVisuals.CharacterId(DialogueOwner(script));
    static Asset Asset(int id)=>Catalog.Data.Assets.Single(a=>a.Type==3&&a.Id==id);
    static byte[] Read(string source,Asset asset){using var f=File.OpenRead(source);f.Position=asset.Iso;var b=new byte[asset.Size];f.ReadExactly(b);return b;}
    public static string Key(int script,bool loading)=>$"{(loading?DialogueOwner(script):script)}/{(loading?"loading":"campaign")}";
    public static byte[] SourceBlock(ModSession session,int script,bool loading)
    {
        if(script<1||script>25)throw new InvalidDataException("Unknown opponent.");
        if(loading)return Read(session.Source,ModelArchives.Resolve(session.Source,Asset(LoadingResource(script))));
        int index=script-1;
        // Relocated campaign banks are already arranged in the loaded ISO's slot order.
        if(ModelArchives.Resolve(session.Source,Asset(88)).Iso!=Asset(88).Iso)
        {
            using var f=File.OpenRead(session.Source);var ids=new byte[100];f.Position=0x601389;f.ReadExactly(ids);
            index=Enumerable.Range(0,25).Single(i=>BitConverter.ToUInt16(ids,4*i)==script);
            return CampaignVisuals.Split(Read(session.Source,ModelArchives.Resolve(session.Source,Asset(88+index/5))))[1+index%5];
        }
        return CampaignVisuals.Split(Read(session.Source,Asset(88+index/5)))[1+index%5];
    }
    public static byte[] Block(ModSession session,int script,bool loading)=>session.EnemyPortraitEdits.TryGetValue(Key(script,loading),out var b)?b:SourceBlock(session,script,loading);
    static int Count(byte[] b)
    {
        if(b.Length<24||BitConverter.ToUInt16(b)!=0x5f51)throw new InvalidDataException("Unsupported enemy portrait format.");
        int n=BitConverter.ToUInt16(b,2);
        if(n<1||n>3||b.Length<8+16*n+512||BitConverter.ToUInt32(b,4+16*n)!=256)throw new InvalidDataException("Unsupported enemy portrait palette.");
        for(int i=0;i<n;i++){int p=4+16*i,w=BitConverter.ToUInt16(b,p+8),h=BitConverter.ToUInt16(b,p+10);long at=BitConverter.ToUInt32(b,p);if(w<1||h<1||at<8+16*n+512||at+(long)w*h>b.Length)throw new InvalidDataException("Enemy portrait pixels exceed the resource.");}
        return n;
    }
    public static void Validate(ModSession session,string key,byte[] bytes)
    {
        var p=key.Split('/');
        if(p.Length==2&&p[0]=="ui"&&int.TryParse(p[1],out int ui)&&new[]{24,26,27,28,29,32}.Contains(ui))
        {
            var native=Read(session.Source,ModelArchives.Resolve(session.Source,Asset(ui)));int frames=Count(native);Count(bytes);
            if(bytes.Length!=native.Length||!bytes.AsSpan(0,8+16*frames).SequenceEqual(native.AsSpan(0,8+16*frames)))throw new InvalidDataException("UI image dimensions or layout changed.");
            return;
        }
        if(p.Length==2&&p[0]=="char"&&int.TryParse(p[1],out int resource)&&key==$"char/{resource}")
        {
            if(!Enumerable.Range(1,25).SelectMany(CharacterAssets).Any(a=>a.Id==resource))throw new InvalidDataException("Unknown dialogue portrait.");
            var a=Catalog.Data.Assets.Single(a=>a.Type==2&&a.Id==resource);var native=Read(session.Source,ModelArchives.Resolve(session.Source,a));int frames=Count(native);Count(bytes);
            if(bytes.Length!=native.Length||!bytes.AsSpan(0,8+16*frames).SequenceEqual(native.AsSpan(0,8+16*frames)))throw new InvalidDataException("Dialogue portrait allocation changed.");
            return;
        }
        if(p.Length!=2||!int.TryParse(p[0],out int id)||p[1] is not ("loading" or "campaign")||key!=Key(id,p[1]=="loading"))throw new InvalidDataException("Unknown enemy portrait key.");
        var original=SourceBlock(session,id,p[1]=="loading");int n=Count(original);Count(bytes);
        if(bytes.Length!=original.Length||!bytes.AsSpan(0,8+16*n).SequenceEqual(original.AsSpan(0,8+16*n)))throw new InvalidDataException("Enemy portrait metadata or allocation changed.");
    }
    public static (int Width,int Height) Size(byte[] block,bool strip=false)
    {
        int n=Count(block),w=BitConverter.ToUInt16(block,12),h=BitConverter.ToUInt16(block,14);
        if(strip)return(Enumerable.Range(0,n).Sum(i=>(int)BitConverter.ToUInt16(block,12+16*i)),Enumerable.Range(0,n).Max(i=>(int)BitConverter.ToUInt16(block,14+16*i)));
        if(n>1)w+=BitConverter.ToUInt16(block,28);return(w,h);
    }
    public static byte[] Preview(byte[] block,bool loading,bool strip=false)
    {
        int n=Count(block),palette=8+16*n;var size=Size(block,strip);
        using var bitmap=new SKBitmap(size.Width,size.Height);bitmap.Erase(SKColors.Transparent);
        int left=0;
        for(int i=0;i<(strip?n:Math.Min(n,2));i++)
        {
            int p=4+16*i,w=BitConverter.ToUInt16(block,p+8),h=BitConverter.ToUInt16(block,p+10),at=BitConverter.ToInt32(block,p);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                int ix=block[at+y*w+x],colorIndex=!loading&&ix<2?2:ix;ushort color=BitConverter.ToUInt16(block,palette+2*colorIndex);
                bitmap.SetPixel(left+x,y,new SKColor((byte)((color&31)*255/31),(byte)(((color>>5)&31)*255/31),(byte)(((color>>10)&31)*255/31),(byte)(ix==0?0:!loading&&ix==1?128:255)));
            }
            left+=w;
        }
        using var png=bitmap.Encode(SKEncodedImageFormat.Png,100);return png.ToArray();
    }
    public static byte[] Import(byte[] original,byte[] image,bool strip=false)
    {
        int n=Count(original),paletteAt=8+16*n;var size=Size(original,strip);
        using var input=SKBitmap.Decode(image)??throw new InvalidDataException("Unsupported image.");
        using var scaled=ImageImportSizing.Fit(input,size.Width,size.Height);
        ushort Pack(SKColor c)=>(ushort)((c.Red*31/255)|((c.Green*31/255)<<5)|((c.Blue*31/255)<<10));
        var histogram=new Dictionary<ushort,int>();
        foreach(var c in scaled.Pixels)if(c.Alpha>=128){ushort q=Pack(c);histogram[q]=histogram.GetValueOrDefault(q)+1;}
        // Native loader reserves index zero for transparency; campaign flags 6 also
        // use index one for a half-alpha copy of index two. Keep all three reserved.
        var colors=histogram.OrderByDescending(p=>p.Value).ThenBy(p=>p.Key).Take(253).Select(p=>p.Key).ToArray();
        var result=(byte[])original.Clone();for(int i=0;i<colors.Length;i++)BitConverter.GetBytes(colors[i]).CopyTo(result,paletteAt+2*(i+3));
        var cache=new Dictionary<ushort,byte>();
        byte Pixel(SKColor c)
        {
            if(c.Alpha<128)return 0;ushort q=Pack(c);if(cache.TryGetValue(q,out byte ix))return ix;
            int best=int.MaxValue;ix=3;for(int i=0;i<colors.Length;i++){int a=(q&31)-(colors[i]&31),b=((q>>5)&31)-((colors[i]>>5)&31),d=((q>>10)&31)-((colors[i]>>10)&31),distance=a*a+b*b+d*d;if(distance<best){best=distance;ix=(byte)(i+3);}}
            return cache[q]=ix;
        }
        int left=0;
        for(int i=0;i<n;i++)
        {
            int p=4+16*i,w=BitConverter.ToUInt16(result,p+8),h=BitConverter.ToUInt16(result,p+10),at=BitConverter.ToInt32(result,p);
            // Third frames (Tea/Shadi) duplicate the whole assembled portrait.
            using var full=i==2&&!strip?ImageImportSizing.Fit(scaled,w,h):null;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)result[at+y*w+x]=Pixel(full!=null?full.GetPixel(x,y):scaled.GetPixel(left+x,y));
            left+=w;
        }
        return result;
    }
}

