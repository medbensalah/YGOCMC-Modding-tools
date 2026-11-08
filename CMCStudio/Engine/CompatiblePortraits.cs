using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace CMCStudio.Engine;

public static class CompatiblePortraits
{
    // Retain staged PNGs, but export through the game's original indexed loader.
    public static void AddPatches(string source,Dictionary<string,byte[]> images,PatchSet plan)
    {
        foreach(var edit in images)
        {
            if(edit.Key.StartsWith("menu/",StringComparison.Ordinal))
            {
                var target=MenuPortraits.Resolve(source,edit.Key.Substring(5));
                if(target.Info.RgbaPixels!=null)throw new InvalidDataException("Safe portrait export needs the original og.iso as the project source. Reopen the project against og.iso.");
                target.AddPatches(plan,target.Import(edit.Value));
            }
            else
            {
                var target=FullColorPortraits.Locate(source,edit.Key).asset;
                var info=PortraitCodec.Read(source,target);
                if(info.RgbaPixels!=null)throw new InvalidDataException("Safe portrait export needs the original og.iso as the project source. Reopen the project against og.iso.");
                using var image=new MemoryStream(edit.Value);
                plan.Add(target.Iso+24,PortraitCodec.Import(source,target,image));
            }
        }
    }
}
