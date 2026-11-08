using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
namespace CMCStudio.Engine
{
    public sealed partial class AlchemyModel
    {
        public void ExportObjWithTextures(string path)
        {
            path=Path.GetFullPath(path);
            // Fresh companion directory avoids overwriting unrelated material/texture files.
            string folder="cmc-materials-"+Guid.NewGuid().ToString("N");
            string directory=Path.Combine(Path.GetDirectoryName(path),folder);
            Directory.CreateDirectory(directory);
            var materials=new StringBuilder("# CMC diffuse materials; game shaders are not exported.\n");
            var images=new Dictionary<string,string>();var fallback=DiffusePreviewPng();
            for(int i=0;i<Meshes.Count;i++)
            {
                materials.AppendLine($"newmtl material_{i}\nKa 0 0 0\nKd 1 1 1\nKs 0 0 0\nd 1\nillum 1");
                var png=Meshes[i].PreviewTexturePng??fallback;
                if(png!=null)
                {
                    string hash=Convert.ToHexString(SHA256.HashData(png));
                    if(!images.TryGetValue(hash,out var name))
                    {
                        name=$"texture_{images.Count:D3}.png";images.Add(hash,name);
                        File.WriteAllBytes(Path.Combine(directory,name),png);
                    }
                    materials.AppendLine("map_Kd "+name);
                }
                materials.AppendLine();
            }
            File.WriteAllText(Path.Combine(directory,"materials.mtl"),materials.ToString(),new UTF8Encoding(false));
            // OBJ V is bottom-up; decoded game textures use top-down image coordinates.
            File.WriteAllText(path,ExportObj(folder+"/materials.mtl"),new UTF8Encoding(false));
        }
    }
}
