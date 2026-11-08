using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CMCStudio.Engine;

namespace CMCStudio;
public static class CampaignThumbnail
{
    // Unused maps have no shipped thumbnail. Render their textured geometry at the
    // game's existing thumbnail dimensions, keeping its native upload budget.
    public static byte[] Render(AlchemyModel model)
    {
        byte[] result=null;Exception failure=null;
        var thread=new Thread(()=>
        {
            try
            {
                const int width=400,height=304;
                var view=new ModelViewport{Width=width,Height=height};view.Show(model,-1,-35,35,1.15);
                var background=new Border{Width=width,Height=height,Background=new SolidColorBrush(Color.FromRgb(23,30,40)),Child=view};
                background.Measure(new Size(width,height));background.Arrange(new Rect(0,0,width,height));background.UpdateLayout();
                var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(background);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream=new MemoryStream();encoder.Save(stream);result=stream.ToArray();
            }
            catch(Exception ex){failure=ex;}
            finally{System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(failure!=null)throw new InvalidDataException("Could not render the selected map thumbnail.",failure);
        return result;
    }
}
