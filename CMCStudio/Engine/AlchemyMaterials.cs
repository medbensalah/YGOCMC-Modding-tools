using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SkiaSharp;
namespace CMCStudio.Engine
{
    public sealed partial class AlchemyModel
    {
        // Experimental opaque, single-material path. Preserve the native texture allocation.
        public byte[] ImportObjWithMaterials(string path, float? normalizedHeight = null)
        {
            string Read(string p,int limit) { if(new FileInfo(p).Length>limit)throw new InvalidDataException("Material input is too large.");return File.ReadAllText(p); }
            string Local(string parent,string name)
            {
                name=name.Trim().Trim('"');var dir=Path.GetFullPath(parent)+Path.DirectorySeparatorChar;
                var p=Path.GetFullPath(Path.Combine(dir,name));
                if(Path.IsPathRooted(name)||!p.StartsWith(dir,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Keep material files and images inside the OBJ folder (or a subfolder).");
                return p;
            }
            string[] Words(string line)=>line.Split('#')[0].Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
            var text=Read(path,256*1024*1024);var lines=text.Split('\n');
            var libraries=lines.Select(Words).Where(w=>w.Length>1&&w[0]=="mtllib").ToArray();
            var names=lines.Select(Words).Where(w=>w.Length>1&&w[0]=="usemtl").Select(w=>string.Join(" ",w.Skip(1))).Distinct().ToArray();
            if(libraries.Length!=1||names.Length!=1)throw new InvalidDataException("This experimental import needs one mtllib and one used material. Bake multiple materials into one texture first.");
            bool assigned=false;
            foreach(var line in lines){var w=Words(line);if(w.Length==0)continue;if(w[0]=="usemtl")assigned=true;if(w[0]=="f"&&!assigned)throw new InvalidDataException("Every face must use the material.");}
            var mtlPath=Local(Path.GetDirectoryName(Path.GetFullPath(path)),string.Join(" ",libraries[0].Skip(1)));
            var mtl=Read(mtlPath,1024*1024);bool active=false,found=false;float[] kd={1,1,1};string imagePath=null;
            float Number(string s){if(!float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out float f)||!float.IsFinite(f)||f<0||f>1)throw new InvalidDataException("Material colours and opacity must be between 0 and 1.");return f;}
            foreach(var line in mtl.Split('\n'))
            {
                var w=Words(line);if(w.Length==0)continue;
                if(w[0]=="newmtl"){active=string.Join(" ",w.Skip(1))==names[0];if(active&&found)throw new InvalidDataException("Duplicate material name.");found|=active;continue;}
                if(!active)continue;
                switch(w[0])
                {
                    case "Kd":if(w.Length!=4)throw new InvalidDataException("Kd needs three RGB values.");kd=w.Skip(1).Select(Number).ToArray();break;
                    case "map_Kd":if(w.Length<2||w[1].StartsWith("-"))throw new InvalidDataException("map_Kd options are unsupported; bake transformations into the image/UVs.");imagePath=Local(Path.GetDirectoryName(mtlPath),string.Join(" ",w.Skip(1)));break;
                    case "d":case "Tr":if(w.Length!=2||Number(w[1])!=(w[0]=="d"?1:0))throw new InvalidDataException("Only opaque materials are supported.");break;
                    case "Ka":case "Ks":case "Ns":case "Ni":case "illum":break; // Diffuse-only conversion; no specular/ambient shader emulation.
                    default:throw new InvalidDataException($"Unsupported MTL property: {w[0]}. This path supports opaque Kd and map_Kd only.");
                }
            }
            if(!found)throw new InvalidDataException("The used material is missing from the MTL.");
            var mesh=ParseObj(text);
            if(normalizedHeight is float height)
            {
                var min=mesh.Positions.Aggregate(System.Numerics.Vector3.Min);
                var max=mesh.Positions.Aggregate(System.Numerics.Vector3.Max);
                float span=max.Y-min.Y;
                // Horizontal cards intentionally have no Y extent. Keep their
                // authored dimensions and elevation, including the Y=1.5 offset.
                if(span>1e-8f)
                {
                    if(!float.IsFinite(height)||height<=0)
                        throw new InvalidDataException("The reference model has no usable size.");
                    var origin=new System.Numerics.Vector3((min.X+max.X)/2,min.Y,(min.Z+max.Z)/2);
                    float scale=height/span;
                    for(int i=0;i<mesh.Positions.Length;i++)mesh.Positions[i]=(mesh.Positions[i]-origin)*scale;
                }
            }
            if(imagePath!=null)
            {
                foreach(var line in lines){var w=Words(line);if(w.Length>0&&w[0]=="f"&&w.Skip(1).Any(c=>c.Split('/').Length<2||c.Split('/')[1]==""))throw new InvalidDataException("Textured faces need UV coordinates.");}
                if(mesh.Uvs.Any(v=>v.X<0||v.X>1||v.Y<0||v.Y>1))throw new InvalidDataException("Bake tiled UVs into the 0–1 range for this experimental importer.");
            }
            // Wavefront V runs bottom-to-top; Alchemy image rows run top-to-bottom.
            for(int i=0;i<mesh.Uvs.Length;i++)mesh.Uvs[i].Y=1-mesh.Uvs[i].Y;
            var output=Rebuild(mesh);var block=new Block(output,0);block.Read();var target=block.DiffuseTarget();
            using var bitmap=new SKBitmap(target.Width,target.Height);
            if(imagePath!=null)
            {
                if(new FileInfo(imagePath).Length>64*1024*1024)throw new InvalidDataException("Diffuse image exceeds 64 MB.");
                using var stream=File.OpenRead(imagePath);using var codec=SKCodec.Create(stream)??throw new InvalidDataException("Cannot decode diffuse image.");
                if(codec.Info.Width<=0||codec.Info.Height<=0||(long)codec.Info.Width*codec.Info.Height>16777216)throw new InvalidDataException("Diffuse image exceeds 16 million pixels.");
                using var decoded=SKBitmap.Decode(codec)??throw new InvalidDataException("Cannot decode diffuse image.");
                if(decoded.Pixels.Any(c=>c.Alpha!=255))throw new InvalidDataException("Diffuse images must be opaque for this importer.");
                using var resized=decoded.Resize(bitmap.Info,new SKSamplingOptions(SKCubicResampler.Mitchell))??throw new InvalidDataException("Cannot resize diffuse image.");
                bitmap.Pixels=resized.Pixels;
            }
            else bitmap.Erase(SKColors.White);
            var pixels=bitmap.Pixels.Select(c=>new SKColor((byte)Math.Round(c.Red*kd[0]),(byte)Math.Round(c.Green*kd[1]),(byte)Math.Round(c.Blue*kd[2]),255)).ToArray();
            if(target.Palette==null)
            {
                int channels=target.Pixels.Size/(target.Width*target.Height);
                for(int i=0;i<pixels.Length;i++){int at=target.Pixels.Offset+i*channels;output[at]=pixels[i].Red;output[at+1]=pixels[i].Green;output[at+2]=pixels[i].Blue;if(channels==4)output[at+3]=255;}
            }
            else
            {
            var unique=pixels.Distinct().ToArray();var boxes=new List<SKColor[]>{unique};
            int Range(SKColor[] a)=>Math.Max(a.Max(c=>c.Red)-a.Min(c=>c.Red),Math.Max(a.Max(c=>c.Green)-a.Min(c=>c.Green),a.Max(c=>c.Blue)-a.Min(c=>c.Blue)));
            while(boxes.Count<256)
            {
                var box=boxes.Where(b=>b.Length>1).OrderByDescending(Range).FirstOrDefault();if(box==null)break;boxes.Remove(box);
                int r=box.Max(c=>c.Red)-box.Min(c=>c.Red),g=box.Max(c=>c.Green)-box.Min(c=>c.Green),b=box.Max(c=>c.Blue)-box.Min(c=>c.Blue);
                var sorted=box.OrderBy(c=>r>=g&&r>=b?c.Red:g>=b?c.Green:c.Blue).ToArray();boxes.Add(sorted.Take(sorted.Length/2).ToArray());boxes.Add(sorted.Skip(sorted.Length/2).ToArray());
            }
            var palette=boxes.Select(b=>new SKColor((byte)Math.Round(b.Average(c=>(double)c.Red)),(byte)Math.Round(b.Average(c=>(double)c.Green)),(byte)Math.Round(b.Average(c=>(double)c.Blue)),255)).ToArray();
            var cache=new Dictionary<SKColor,byte>();
            foreach(var c in unique){int best=int.MaxValue;byte match=0;for(int i=0;i<palette.Length;i++){var a=palette[i];int d=(c.Red-a.Red)*(c.Red-a.Red)+(c.Green-a.Green)*(c.Green-a.Green)+(c.Blue-a.Blue)*(c.Blue-a.Blue);if(d<best){best=d;match=(byte)i;}}cache[c]=match;}
            for(int i=0;i<pixels.Length;i++)output[target.Pixels.Offset+i]=cache[pixels[i]];
            for(int i=0;i<256;i++){var c=palette[i%palette.Length];int p=target.Palette.Offset+i*4;output[p]=c.Red;output[p+1]=c.Green;output[p+2]=c.Blue;output[p+3]=255;}
            }
            block.UseDiffuse(target.TextureId);block.UseVertexDiffuse();
            ValidateReplacement(output);return output;
        }
        private static bool ValidateDiffuseReplacement(byte[] expected,byte[] replacement)
        {
            if(expected.Length!=replacement.Length)return false;
            try
            {
                // Derive all writable spans and references from the trusted rebuilt source, never from imported metadata.
                var b=new Block(expected,0);b.Read();var t=b.DiffuseTarget();b.UseDiffuse(t.TextureId);
                if(t.Palette!=null)for(int i=0;i<256;i++)if(replacement[t.Palette.Offset+i*4+3]!=255)return false;
                if(t.Palette==null&&t.Pixels.Size==t.Width*t.Height*4)for(int i=0;i<t.Width*t.Height;i++)if(replacement[t.Pixels.Offset+i*4+3]!=255)return false;
                replacement.AsSpan(t.Pixels.Offset,t.Pixels.Size).CopyTo(expected.AsSpan(t.Pixels.Offset));
                if(t.Palette!=null)replacement.AsSpan(t.Palette.Offset,t.Palette.Size).CopyTo(expected.AsSpan(t.Palette.Offset));
                if(expected.AsSpan().SequenceEqual(replacement))return true; // Existing experimental projects remain loadable.
                b.UseVertexDiffuse();
                return expected.AsSpan().SequenceEqual(replacement);
            }
            catch(InvalidDataException){return false;}
        }
        public byte[] DiffusePreviewPng()
        {
            if(Bytes.Length<48||BitConverter.ToUInt32(Bytes,40)!=0xFADA)return null;
            try
            {
                var b=new Block(Bytes,0);b.Read();var t=b.DiffuseTarget();
                if(b.Objects.Where(r=>r?.Type=="igTextureBindAttr").Any(r=>r.Get(4)!=t.TextureId))return null;
                using var image=new SKBitmap(t.Width,t.Height);
                for(int y=0;y<t.Height;y++)for(int x=0;x<t.Width;x++){int channels=t.Pixels.Size/(t.Width*t.Height);int p=t.Palette!=null?t.Palette.Offset+4*Bytes[t.Pixels.Offset+y*t.Width+x]:t.Pixels.Offset+(y*t.Width+x)*channels;image.SetPixel(x,y,new SKColor(Bytes[p],Bytes[p+1],Bytes[p+2],t.Palette!=null||channels==4?Bytes[p+3]:(byte)255));}
                using var png=image.Encode(SKEncodedImageFormat.Png,100);return png.ToArray();
            }
            catch(InvalidDataException){return null;}
        }
        private sealed partial class Block
        {
            public (int TextureId,int Width,int Height,Record Pixels,Record Palette) DiffuseTarget()
            {
                foreach(var scene in Objects.Where(r=>r?.Type=="igSceneInfo"))
                {
                    var root=Ref(scene.Get(5));if(root.Type!="igAttrSet")continue;
                    var rootAttrs=Ref(root.Get(8),"igAttrList");
                    if(rootAttrs.Get(2)==5 && Objects.Any(r=>r?.Type=="igAttrList" && !ReferenceEquals(r,rootAttrs) && r.Get(4)==rootAttrs.Get(4)))
                        throw new InvalidDataException("This resource contains the withdrawn shared-state shader rewrite. Start from the original ISO or your pre-fix textured.iso before importing materials again.");
                }
                var firstGeometry=Objects.FirstOrDefault(r=>r?.Type=="igGeometryAttr1_5")??throw new InvalidDataException("No material geometry.");
                var firstArray=Ref(firstGeometry.Get(4),"igVertexArray1_1");var firstChannels=Data(firstArray.Get(2),76);
                if(I(firstChannels.Offset+44)<0)throw new InvalidDataException("This geometry has no native UV channel for a diffuse texture.");
                if(Objects.Where(r=>r?.Type=="igTextureBindAttr"||r?.Type=="igTextureStateAttr").Any(r=>r.Get(5)!=0))throw new InvalidDataException("Multiple texture stages are unsupported.");
                var textures=Objects.Select((r,i)=>(r,i)).Where(x=>x.r?.Type=="igTextureAttr").ToArray();
                if(textures.Length==0)throw new InvalidDataException("No embedded texture allocation is available for material import.");
                var texture=textures[0];var im=Ref(texture.r.Get(12),"igImage");int w=im.Get(2),h=im.Get(3);
                int channels=im.Get(4);
                if(w<1||h<1||w>512||h>512||im.Get(5)!=1||im.Get(6)!=100||im.Get(12)!=w*h*channels||im.Get(19)!=w*channels||im.Get(20)!=0)throw new InvalidDataException("Unsupported native diffuse image layout.");
                Record palette=null;
                if(channels==1){
                    if(im.Get(11)!=65536||im.Get(18)!=8)throw new InvalidDataException("Unsupported indexed diffuse format.");
                    var clut=Ref(im.Get(17),"igClut");if(clut.Get(2)!=7||clut.Get(3)!=256||clut.Get(4)!=4||clut.Get(6)!=1024)throw new InvalidDataException("Unsupported embedded palette.");
                    palette=Data(clut.Get(5),1024);
                }else if(channels is not (3 or 4)||im.Get(17)>=0)throw new InvalidDataException("Unsupported direct-colour diffuse format.");
                var pixels=Data(im.Get(13),w*h*channels);
                if(ReferenceEquals(pixels,palette))throw new InvalidDataException("Image and palette allocations overlap.");
                // Keep all image allocations and scene edges; redirect existing bindings only.
                foreach(var va in Objects.Where(r=>r?.Type=="igVertexArray1_1")){var vertexChannels=Data(va.Get(2),76);for(int c=0;c<19;c++){int id=I(vertexChannels.Offset+c*4);if(id<0)continue;var memory=Ref(id,"memory");if(ReferenceEquals(memory,pixels)||ReferenceEquals(memory,palette))throw new InvalidDataException("Texture memory aliases geometry.");}}
                return(texture.i,w,h,pixels,palette);
            }
            public void UseVertexDiffuse()
            {
                // Only change the coordinate-source enum. Scene edges, shader nodes, list storage,
                // counts and ownership must remain intact; the prior graph bypass crashed in-game.
                foreach(var r in Objects.Where(r=>r?.Type=="igTextureCoordSourceAttr"))
                {
                    if(r.Get(5)!=0||r.Get(6)!=0||r.Get(4) is not (0 or 2))
                        throw new InvalidDataException("Unsupported texture-coordinate source/stage.");
                    BitConverter.GetBytes(0).CopyTo(_bytes,r.Offsets[4]);
                }
            }
            public void UseDiffuse(int textureId)
            {
                foreach(var r in Objects.Where(r=>r?.Type=="igTextureBindAttr"))BitConverter.GetBytes(textureId).CopyTo(_bytes,r.Offsets[4]);
                foreach(var r in Objects.Where(r=>r?.Type=="igTextureStateAttr"))_bytes[r.Offsets[4]]=1;
                // Neutral ambient/diffuse colour: Kd is baked into the texture; preserve lighting and other shader fields.
                foreach(var r in Objects.Where(r=>r?.Type=="igMaterialAttr"))foreach(int slot in new[]{5,6})for(int c=0;c<4;c++)BitConverter.GetBytes(1f).CopyTo(_bytes,r.Offsets[slot]+c*4);
            }
        }
    }
}
