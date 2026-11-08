using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using CMCStudio.Engine;
namespace CMCStudio;
public sealed record MapUpscaleResult(byte[] IsoBytes,int TextureCount,byte[] Before,byte[] After);
public sealed record MapUpscaleOptions(bool Export,bool Embed,bool DetailEnhancement,int Scale);
public static class MapAiUpscaler
{
    public static MapUpscaleResult Run(AlchemyModel model,string objPath,int isoScale,IProgress<string> progress,CancellationToken token)
        =>RunCore(model,objPath,new MapUpscaleOptions(true,isoScale>0,false,4),isoScale,progress,token);
    public static MapUpscaleResult Run(AlchemyModel model,string objPath,MapUpscaleOptions options,IProgress<string> progress,CancellationToken token)
    {
        if(options.Scale is not (1 or 2))throw new ArgumentException("Choose original resolution or 2x upscale.");
        if(options.Export&&string.IsNullOrWhiteSpace(objPath))throw new ArgumentException("Choose an export filename.");
        return RunCore(model,objPath,options,options.Embed?options.Scale:0,progress,token);
    }
    private static MapUpscaleResult RunCore(AlchemyModel model,string objPath,MapUpscaleOptions options,int isoScale,IProgress<string> progress,CancellationToken token)
    {
        string root=Path.Combine(AppContext.BaseDirectory,"tools","realesrgan"),exe=Path.Combine(root,"realesrgan-ncnn-vulkan.exe");
        if(!File.Exists(exe))throw new FileNotFoundException("The bundled Real-ESRGAN engine is missing.",exe);
        var textures=model.MapTextures();var unique=textures.GroupBy(t=>Convert.ToHexString(SHA256.HashData(t.Png))).ToArray();
        string temp=Path.Combine(Path.GetTempPath(),"CMC-upscale-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try
        {
            var enhanced=new Dictionary<string,byte[]>();byte[] before=null,after=null;
            for(int i=0;i<unique.Length;i++)
            {
                token.ThrowIfCancellationRequested();var group=unique[i];var source=group.First();progress.Report($"AI texture {i+1}/{unique.Length} - {source.Width} x {source.Height}");
                using var original=SKBitmap.Decode(source.Png);const int pad=16;
                using var padded=new SKBitmap(new SKImageInfo(original.Width+2*pad,original.Height+2*pad,SKColorType.Rgba8888,SKAlphaType.Opaque));
                // Repeat padding lets the network see neighbouring repetitions at tile edges.
                for(int y=0;y<padded.Height;y++)for(int x=0;x<padded.Width;x++){var c=original.GetPixel((x-pad+original.Width*pad)%original.Width,(y-pad+original.Height*pad)%original.Height);padded.SetPixel(x,y,new SKColor(c.Red,c.Green,c.Blue,255));}
                string input=Path.Combine(temp,"input.png"),output=Path.Combine(temp,"output.png");using(var im=SKImage.FromBitmap(padded))using(var png=im.Encode(SKEncodedImageFormat.Png,100))File.WriteAllBytes(input,png.ToArray());
                var start=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,WorkingDirectory=root};
                foreach(var a in new[]{"-i",input,"-o",output,"-n","realesrgan-x4plus","-s","4","-t","64","-j","1:1:1","-m",Path.Combine(root,"models")})start.ArgumentList.Add(a);
                using var process=Process.Start(start)??throw new IOException("Cannot start the AI engine.");using var cancel=token.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}});
                var errors=process.StandardError.ReadToEndAsync();var stdout=process.StandardOutput.ReadToEndAsync();
                while(!process.WaitForExit(200)){}Task.WaitAll(errors,stdout);token.ThrowIfCancellationRequested();
                if(process.ExitCode!=0||!File.Exists(output))throw new IOException("Real-ESRGAN failed. A working Vulkan graphics driver is required. "+errors.Result[^Math.Min(errors.Result.Length,1200)..]);
                using var high=SKBitmap.Decode(output)??throw new InvalidDataException("The AI engine returned an invalid image.");
                if(high.Width!=padded.Width*4||high.Height!=padded.Height*4)throw new InvalidDataException("Unexpected AI output dimensions.");
                using var cropped=new SKBitmap(new SKImageInfo(original.Width*4,original.Height*4,SKColorType.Rgba8888,SKAlphaType.Unpremul));
                for(int y=0;y<cropped.Height;y++)for(int x=0;x<cropped.Width;x++){var c=high.GetPixel(x+pad*4,y+pad*4);cropped.SetPixel(x,y,new SKColor(c.Red,c.Green,c.Blue,original.GetPixel(x/4,y/4).Alpha));}
                using var scaled=options.Scale==4?cropped.Copy():cropped.Resize(new SKImageInfo(original.Width*options.Scale,original.Height*options.Scale),new SKSamplingOptions(SKCubicResampler.Mitchell));
                if(options.DetailEnhancement)EnhanceDetail(scaled,token);
                using var image=SKImage.FromBitmap(scaled);using var data=image.Encode(SKEncodedImageFormat.Png,100);var bytes=data.ToArray();foreach(var t in group)enhanced[t.Key]=bytes;
                if(before==null){before=source.Png;after=bytes;}
            }
            token.ThrowIfCancellationRequested();progress.Report("Validating enhanced map textures...");
            byte[] iso=isoScale==0?null:model.ReplaceMapTextures(enhanced,isoScale);if(iso!=null)model.ValidateReplacement(iso);
            if(options.Export)
            {
            // Export a separately decoded preview so temporary high-res images never alter the source model.
            var preview=AlchemyModel.Decode(model.Bytes);var byHash=textures.ToDictionary(t=>t.Key,t=>Convert.ToHexString(SHA256.HashData(t.Png)));var replacements=textures.GroupBy(t=>byHash[t.Key]).ToDictionary(g=>g.Key,g=>enhanced[g.First().Key]);
            foreach(var mesh in preview.Meshes)if(mesh.PreviewTexturePng!=null&&replacements.TryGetValue(Convert.ToHexString(SHA256.HashData(mesh.PreviewTexturePng)),out var png))mesh.PreviewTexturePng=png;
            token.ThrowIfCancellationRequested();preview.ExportObjWithTextures(objPath);
            string all=Path.Combine(Path.GetDirectoryName(objPath)!,Path.GetFileNameWithoutExtension(objPath)+"-all-textures-"+Guid.NewGuid().ToString("N")[..8]);Directory.CreateDirectory(all);
            foreach(var t in textures)File.WriteAllBytes(Path.Combine(all,$"image_{t.Key}_{t.Width*options.Scale}x{t.Height*options.Scale}.png"),enhanced[t.Key]);
            File.WriteAllText(Path.Combine(all,"manifest.json"),JsonSerializer.Serialize(new{Engine="Real-ESRGAN realesrgan-x4plus",Scale=options.Scale,DetailEnhancement=options.DetailEnhancement,IsoScale=isoScale,Textures=textures.Select(t=>new{t.Key,t.Width,t.Height})},new JsonSerializerOptions{WriteIndented=true}));
            }
            return new MapUpscaleResult(iso,textures.Count,before,after);
        }
        finally{if(Directory.Exists(temp))Directory.Delete(temp,true);}
    }
    internal static void EnhanceDetail(SKBitmap bitmap,CancellationToken token)
    {
        // Mild unsharp mask adds local contrast after neural reconstruction.
        using var source=bitmap.Copy();
        for(int y=0;y<bitmap.Height;y++)
        {
            token.ThrowIfCancellationRequested();
            for(int x=0;x<bitmap.Width;x++)
            {
                float r=0,g=0,b=0;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    var sample=source.GetPixel((x+dx+source.Width)%source.Width,(y+dy+source.Height)%source.Height);int weight=(dx==0?2:1)*(dy==0?2:1);r+=sample.Red*weight;g+=sample.Green*weight;b+=sample.Blue*weight;
                }
                var c=source.GetPixel(x,y);byte Adjust(byte value,float blurred)=>(byte)Math.Clamp((int)Math.Round(value+.35f*(value-blurred/16)),0,255);
                bitmap.SetPixel(x,y,new SKColor(Adjust(c.Red,r),Adjust(c.Green,g),Adjust(c.Blue,b),c.Alpha));
            }
        }
    }
}
