using System.IO;
using SkiaSharp;

namespace CMCStudio.Engine;

public static class ImageImportSizing
{
    // Preserve proportions and native canvas dimensions. Smaller inputs remain
    // at their original size; unused canvas space is transparent.
    public static SKBitmap Fit(SKBitmap input,int width,int height)
    {
        double scale=Math.Min(1,Math.Min((double)width/input.Width,(double)height/input.Height));
        int w=Math.Max(1,(int)Math.Round(input.Width*scale)),h=Math.Max(1,(int)Math.Round(input.Height*scale));
        using var resized=input.Resize(new SKImageInfo(w,h),new SKSamplingOptions(SKCubicResampler.Mitchell))
            ??throw new InvalidDataException("Cannot resize imported image.");
        var result=new SKBitmap(width,height);result.Erase(SKColors.Transparent);
        using var canvas=new SKCanvas(result);canvas.DrawBitmap(resized,(width-w)/2,(height-h)/2);
        return result;
    }

    public static byte[] CapMonster(byte[] image)
    {
        using var input=SKBitmap.Decode(image)??throw new InvalidDataException("Invalid monster image.");
        if(input.Width<=512&&input.Height<=512)return image;
        using var resized=Fit(input,512,512);
        using var png=resized.Encode(SKEncodedImageFormat.Png,100);return png.ToArray();
    }
}
