using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;
namespace CMCStudio.Engine
{
    public sealed partial class AlchemyModel
    {
        private sealed partial class Block
        {
            public Dictionary<int,byte[]> PreviewTextures()
            {
                var result=new Dictionary<int,byte[]>();var cache=new Dictionary<int,byte[]>();
                int[] List(int id)
                {
                    if(id<0)return Array.Empty<int>();var r=Ref(id);int n=r.Get(2);if(n==0)return Array.Empty<int>();
                    var data=Ref(r.Get(4),"memory");if(n<0||n>data.Size/4)throw new InvalidDataException("Invalid preview attribute list.");
                    return Enumerable.Range(0,n).Select(i=>I(data.Offset+i*4)).ToArray();
                }
                byte[] Texture(int id)
                {
                    if(id<0)return null;if(cache.TryGetValue(id,out var png))return png;
                    var texture=Ref(id,"igTextureAttr");var im=Ref(texture.Get(12),"igImage");int w=im.Get(2),h=im.Get(3);if(w<1||h<1||w>4096||h>4096)return null;
                    var pixels=Ref(im.Get(13),"memory");Record palette=null;
                    if(im.Get(17)>=0){var clut=Ref(im.Get(17),"igClut");palette=Ref(clut.Get(5),"memory");}
                    int channels=palette==null?im.Get(4):1;
                    if((palette==null?channels!=3&&channels!=4:channels!=1)||pixels.Size<w*h*channels)return null;
                    using var bitmap=new SKBitmap(w,h);
                    for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                    {
                        int at=pixels.Offset+(y*w+x)*channels;
                        if(palette!=null){int index=_bytes[at];if(index*4+4>palette.Size)return null;at=palette.Offset+index*4;}
                        byte alpha=palette!=null||channels==4?_bytes[at+3]:(byte)255;
                        bitmap.SetPixel(x,y,new SKColor(_bytes[at],_bytes[at+1],_bytes[at+2],alpha));
                    }
                    using var image=SKImage.FromBitmap(bitmap);using var encoded=image.Encode(SKEncodedImageFormat.Png,100);return cache[id]=encoded.ToArray();
                }
                var visited=new HashSet<(int,int)>();
                void Walk(int id,int inherited,int depth)
                {
                    if(id<0||depth>128||!visited.Add((id,inherited)))return;var node=Ref(id);int texture=inherited;var geometry=new List<int>();
                    void Attributes(int list)
                    {
                        foreach(int a in List(list)){var attr=Ref(a);if(attr.Type=="igTextureBindAttr"&&attr.Get(5)==0)texture=attr.Get(4);else if(attr.Type=="igGeometryAttr1_5")geometry.Add(a);}
                    }
                    if(node.Values.TryGetValue(8,out int attrs)&&(node.Type=="igGeometry"||node.Type=="igAttrSet"))Attributes(attrs);
                    if(node.Type=="igSimpleShader")
                    {
                        var data=Ref(node.Get(11),"igShaderData");var lists=List(data.Get(3));if(lists.Length>0)Attributes(lists[0]);
                    }
                    foreach(int mesh in geometry){try{var png=Texture(texture);if(png!=null)result[mesh]=png;}catch(InvalidDataException){}}
                    if(node.Type=="igActor") {Walk(node.Get(11),texture,depth+1);return;}
                    if(node.Type=="igAppearance") {Walk(node.Get(3),texture,depth+1);return;}
                    if(node.Type=="igSkin"&&node.Values.TryGetValue(3,out int skinNode))Walk(skinNode,texture,depth+1);
                    if(node.Values.TryGetValue(7,out int children))foreach(int child in List(children))Walk(child,texture,depth+1);
                }
                foreach(var scene in Objects.Where(r=>r?.Type=="igSceneInfo"))try{Walk(scene.Get(5),-1,0);}catch(InvalidDataException){}
                return result;
            }
        }
    }
}
