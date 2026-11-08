using System;
using System.IO;
namespace CMCStudio;

internal static class PortraitPreferences
{
    private static readonly string FilePath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CMCStudio","portrait-resolution.txt");
    private static bool keepImportedResolution=Load();
    public static bool KeepImportedResolution
    {
        get=>keepImportedResolution;
        set
        {
            keepImportedResolution=value;
            try{Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);File.WriteAllText(FilePath,value.ToString());}
            catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
    private static bool Load()
    {
        try{if(File.Exists(FilePath)&&bool.TryParse(File.ReadAllText(FilePath),out bool value))return value;}
        catch(IOException){}catch(UnauthorizedAccessException){}
        return true;
    }
}
