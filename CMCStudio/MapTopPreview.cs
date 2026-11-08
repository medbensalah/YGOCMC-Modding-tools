using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CMCStudio.Engine;
namespace CMCStudio;
public static class MapTopPreview
{
    // Same geometry, textures, materials and lighting as the Assets viewport.
    // Game placement uses world X/Z = tile coordinate * 10 + 5.
    public static byte[] Render(AlchemyModel model,int columns,int rows,bool hideSky=true)
    {
        int width=columns*32,height=rows*32;
        var view=new ModelViewport{Width=width,Height=height};view.ShowTop(model,columns,rows,hideSky);
        view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(view);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream=new MemoryStream();encoder.Save(stream);return stream.ToArray();
    }
}
