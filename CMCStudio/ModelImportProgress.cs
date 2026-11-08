using System.Text.RegularExpressions;
using System.Windows;
namespace CMCStudio;
public sealed partial class MainWindow
{
    async Task<T> WithModelProgress<T>(Func<IProgress<string>,T> action,string initialMessage="Preparing model import...")
    {
        busyLabel.Text=initialMessage;
        busyProgress.Value=0;
        busyProgress.IsIndeterminate=true;
        busy.Visibility=Visibility.Visible;
        bool active=true;
        var progress=new Progress<string>(message=>{
            if(!active)return;
            busyLabel.Text=message;
            var count=Regex.Match(message,@"(\d+)/(\d+)");
            if(count.Success&&int.TryParse(count.Groups[1].Value,out int current)&&int.TryParse(count.Groups[2].Value,out int total)&&total>0)
            {
                busyProgress.IsIndeterminate=false;
                busyProgress.Value=Math.Clamp(100.0*(current-1)/total,0,100);
            }
            else busyProgress.IsIndeterminate=true;
        });
        try{return await Task.Run(()=>action(progress));}
        finally{active=false;busy.Visibility=Visibility.Collapsed;busyProgress.IsIndeterminate=true;}
    }
}
