using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using SkiaSharp;
namespace CMCStudio.Engine
{
    public sealed class ModelMesh
    {
        public byte[] PreviewTexturePng {get;set;}
        public string Name { get; set; }
        public int PositionOffset { get; set; }
        public int NormalOffset { get; set; } = -1;
        public int UvOffset { get; set; } = -1;
        public Vector3[] Positions { get; set; }
        public Vector3[] Normals { get; set; }
        public Vector2[] Uvs { get; set; }
        public int[] Triangles { get; set; }
    }
    public sealed partial class AlchemyModel
    {
        public byte[] Bytes { get; }
        public List<ModelMesh> Meshes { get; } = new();
        public List<string> Notes { get; } = new();
        public int VertexCount => Meshes.Sum(x=>x.Positions.Length);
        public int TriangleCount => Meshes.Sum(x=>x.Triangles.Length/3);
        public static bool IsModelAsset(Asset asset)=>new[]{0,1,2,10,11}.Contains(asset.Type);
        private AlchemyModel(byte[] bytes) { Bytes=bytes; }
        public static byte[] ReadAsset(string source,Asset asset)
        {
            asset=ModelArchives.Resolve(source,asset);
            if(!IsModelAsset(asset) || asset.Flag || asset.Size<=0 || asset.Size>512*1024*1024) throw new InvalidDataException("Select an unflagged model, unit, character or map resource (up to 512 MB).");
            using var f=File.OpenRead(source);f.Position=asset.Iso;var bytes=new byte[asset.Size];f.ReadExactly(bytes);return bytes;
        }
        public static AlchemyModel Decode(byte[] bytes)
        {
            if(bytes==null || bytes.Length>512*1024*1024) throw new InvalidDataException("Invalid model size.");
            var model=new AlchemyModel(bytes);
            int blocks=0;
            for(int start=0;start+48<=bytes.Length;start+=4)
            {
                if(BitConverter.ToUInt32(bytes,start+40)!=0xFADA || (BitConverter.ToUInt32(bytes,start+44)&0xFFFF)!=6)continue;
                if(++blocks>64) throw new InvalidDataException("Too many embedded Alchemy blocks.");
                try
                {
                    var block=new Block(bytes,start);block.Read();
                    var previewTextures=block.PreviewTextures();
                    foreach(var item in block.Objects.Select((value,index)=>(value,index)).Where(x=>x.value?.Type=="igGeometryAttr1_5"))
                    {
                        try { var mesh=block.Mesh(item.index,$"block_{blocks}_mesh_{item.index}");mesh.PreviewTexturePng=previewTextures.GetValueOrDefault(item.index);model.Meshes.Add(mesh); }
                        catch(InvalidDataException e) { model.Notes.Add($"Mesh {item.index}: {e.Message}"); }
                    }
                    start=(block.End+3)/4*4-4;
                }
                catch(InvalidDataException e) { model.Notes.Add($"Block at 0x{start:X}: {e.Message}"); }
                if(model.VertexCount>6000000 || model.TriangleCount>2000000)throw new InvalidDataException("Model exceeds the preview geometry limit.");
            }
            if(model.Meshes.Count==0)throw new InvalidDataException("No supported Alchemy v6 triangle geometry found. "+string.Join("; ",model.Notes.Take(3)));
            return model;
        }
        private sealed class Field { public string Type; public int Slot,Size; }
        private sealed class Meta { public string Name; public List<Field> Fields=new(); }
        private sealed class Record
        {
            public string Type; public int Offset,Size;
            public Dictionary<int,int> Values=new();
            public Dictionary<int,int> Offsets=new();
            public int Get(int slot)=>Values.TryGetValue(slot,out int value)?value:throw new InvalidDataException($"Missing {Type} field {slot}.");
        }
        private sealed partial class Block
        {
            private readonly byte[] _bytes; private readonly int _start;
            private readonly List<Meta> _meta=new();
            public Record[] Objects; public int End;
            private Record[] _dirs; private int _dirStart,_objectStart,_memoryStart,_indexEnd;
            public Block(byte[] bytes,int start){_bytes=bytes;_start=start;}
            private void Range(int p,int size){if(p<0 || size<0 || p>(long)_bytes.Length-size)throw new InvalidDataException("Alchemy range is outside the resource.");}
            private int U(int p){Range(p,4);uint v=BitConverter.ToUInt32(_bytes,p);if(v>int.MaxValue)throw new InvalidDataException("Invalid Alchemy size/index.");return (int)v;}
            private int I(int p){Range(p,4);return BitConverter.ToInt32(_bytes,p);}
            private int H(int p){Range(p,2);return BitConverter.ToUInt16(_bytes,p);}
            private string Text(int p,int size){Range(p,size);int n=Array.IndexOf(_bytes,(byte)0,p,size);return Encoding.UTF8.GetString(_bytes,p,n<0?size:n-p);}
            private int Section(ref int p,int size){Range(p,size);int old=p;p=checked(p+size);return old;}
            private int Sized(ref int p){int size=U(p);if(size<4)throw new InvalidDataException("Empty Alchemy section.");return Section(ref p,size);}
            public void Read()
            {
                int[] h=Enumerable.Range(0,10).Select(i=>U(_start+4*i)).ToArray();
                if(h[1]>50000 || h[3]>4096 || h[9]>1024)throw new InvalidDataException("Invalid Alchemy table counts.");
                uint flags=BitConverter.ToUInt32(_bytes,_start+44);
                if((flags&0x20000000)==0)throw new InvalidDataException("Unshared Alchemy directories are unsupported.");
                int p=_start+48,mf=Section(ref p,h[8]),dyn=mf+checked(h[9]*12);
                if(dyn>p)throw new InvalidDataException("Invalid field metadata.");
                var types=new List<string>();
                for(int i=0;i<h[9];i++){int n=U(mf+i*12);if(n<=0 || n>p-dyn)throw new InvalidDataException("Invalid field name.");types.Add(Text(dyn,n));dyn+=n;}
                Sized(ref p);
                int metas=Section(ref p,h[2]);dyn=metas+checked(h[3]*24);
                if(dyn>p)throw new InvalidDataException("Invalid type metadata.");
                for(int i=0;i<h[3];i++)
                {
                    int n=U(metas+i*24),count=U(metas+i*24+12);
                    if(n<=0 || count>1024 || dyn+(long)n+count*6>p)throw new InvalidDataException("Invalid Alchemy type fields.");
                    var meta=new Meta { Name=Text(dyn,n) };dyn+=n;
                    for(int j=0;j<count;j++,dyn+=6)
                    { int type=H(dyn);if(type>=types.Count)throw new InvalidDataException("Invalid field type.");meta.Fields.Add(new Field{Type=types[type],Slot=H(dyn+2),Size=H(dyn+4)}); }
                    _meta.Add(meta);
                }
                if((flags&0x40000000)!=0)Sized(ref p);
                if((flags&0x10000000)!=0)Sized(ref p);
                _dirStart=p; int entriesEnd=checked(p+h[0]);Range(p,h[0]);var entries=new List<Record>();
                for(int i=0;i<h[1];i++)entries.Add(ReadRecord(ref p,entriesEnd));
                if(p!=entriesEnd)throw new InvalidDataException("Directory size mismatch.");
                int idx=Sized(ref p),countIdx=U(idx+4);
                if(countIdx>100000 || idx+8L+countIdx*2>p)throw new InvalidDataException("Invalid reference index.");
                var dirs=new Record[countIdx];Objects=new Record[countIdx];
                for(int i=0;i<countIdx;i++){int id=H(idx+8+2*i);if(id>=entries.Count)throw new InvalidDataException("Directory reference outside table.");dirs[i]=entries[id];}
                _indexEnd=p;_dirs=dirs;
                if((flags&0x80000000)!=0)Section(ref p,4);
                _objectStart=p;
                int objectsEnd=checked(p+h[4]);Range(p,h[4]);
                for(int i=0;i<countIdx;i++)if(dirs[i].Type=="igObjectDirEntry")
                {int type=dirs[i].Get(11);if(type<0 || type>=_meta.Count)throw new InvalidDataException("Invalid object type reference.");Objects[i]=ReadRecord(ref p,objectsEnd);if(Objects[i].Type!=_meta[type].Name)throw new InvalidDataException("Object directory/type mismatch.");}
                if(p!=objectsEnd)throw new InvalidDataException("Object section size mismatch.");
                _memoryStart=p; int memoryEnd=checked(p+h[6]);Range(p,h[6]);
                for(int i=0;i<countIdx;i++)if(dirs[i].Type=="igMemoryDirEntry")
                {int size=dirs[i].Get(7);if(size<0 || size>memoryEnd-p)throw new InvalidDataException("Invalid memory extent.");Objects[i]=new Record { Type="memory",Offset=p,Size=size };p=checked(p+((size+3)&~3));}
                if(p!=memoryEnd)throw new InvalidDataException("Memory section size mismatch.");End=p;
            }
            private Record ReadRecord(ref int p,int end)
            {
                if(end-p<8)throw new InvalidDataException("Truncated Alchemy record.");int type=U(p),size=U(p+4);
                if(type>=_meta.Count || size<8 || size>end-p)throw new InvalidDataException("Invalid record header.");
                var record=new Record{Type=_meta[type].Name,Offset=p,Size=size};int q=p+8,stop=p+size;
                foreach(var field in _meta[type].Fields)
                {
                    record.Offsets[field.Slot]=q;int n=field.Size;if(n<1 || n>stop-q)throw new InvalidDataException("Field outside record.");
                    if(n<=4)record.Values[field.Slot]=n==4?I(q):n==2?H(q):_bytes[q];
                    if(field.Type=="igStringMetaField") { if(n!=4)throw new InvalidDataException("Unsupported string field.");int len=U(q);if(len>stop-q-4)throw new InvalidDataException("String outside record.");n+=len; }
                    q=checked(q+((n+3)&~3));if(q>stop)throw new InvalidDataException("Field alignment outside record.");
                }
                p=stop;return record;
            }
            private Record Ref(int index,string type=null)
            {if(index<0 || index>=Objects.Length || Objects[index]==null || (type!=null && Objects[index].Type!=type))throw new InvalidDataException("Unresolved or unsupported geometry reference.");return Objects[index];}
            private Record Data(int index,int size){var r=Ref(index,"memory");if(r.Size!=size)throw new InvalidDataException("Geometry stream size does not match its count.");return r;}
            private Vector3[] Vec3(Record r,int count)
            {var values=new Vector3[count];for(int i=0;i<count;i++){int p=r.Offset+i*12;values[i]=new Vector3(F(p),F(p+4),F(p+8));}return values;}
            private float F(int p){Range(p,4);float v=BitConverter.ToSingle(_bytes,p);if(!float.IsFinite(v)||Math.Abs(v)>1000000)throw new InvalidDataException("Invalid geometry coordinate.");return v;}
            public ModelMesh Mesh(int index,string name)
            {
                var g=Ref(index,"igGeometryAttr1_5");var va=Ref(g.Get(4),"igVertexArray1_1");int count=va.Get(3);
                if(count<3 || count>6000000)throw new InvalidDataException("Unsupported vertex count.");
                var channels=Data(va.Get(2),76);int posId=I(channels.Offset),normalId=I(channels.Offset+4),uvId=I(channels.Offset+44);
                var positions=Data(posId,checked(count*12));
                var mesh=new ModelMesh { Name=name,PositionOffset=positions.Offset,Positions=Vec3(positions,count) };
                if(normalId>=0){var normals=Data(normalId,count*12);mesh.NormalOffset=normals.Offset;mesh.Normals=Vec3(normals,count);}
                if(uvId>=0){var uv=Data(uvId,count*8);mesh.UvOffset=uv.Offset;mesh.Uvs=Enumerable.Range(0,count).Select(i=>new Vector2(F(uv.Offset+i*8),F(uv.Offset+i*8+4))).ToArray();}
                int[] indices=Enumerable.Range(0,count).ToArray();
                if(g.Get(5)>=0)
                {
                    var ia=Ref(g.Get(5),"igIndexArray1_1");int n=ia.Get(3),bits=ia.Get(4);if(n<0 || n>6000000 || (bits!=16 && bits!=32))throw new InvalidDataException("Unsupported index array.");
                    var data=Data(ia.Get(2),checked(n*(bits/8)));indices=Enumerable.Range(0,n).Select(i=>bits==16?H(data.Offset+i*2):U(data.Offset+i*4)).ToArray();
                }
                if(indices.Any(i=>i<0 || i>=count))throw new InvalidDataException("Triangle index outside vertices.");
                int primitive=g.Get(6);var faces=new List<int>();
                if(primitive==3){if(indices.Length%3!=0)throw new InvalidDataException("Incomplete triangle list.");faces.AddRange(indices);}
                else if(primitive==4)
                {
                    int[] strips=new[]{indices.Length};
                    if(g.Get(13)>=0)
                    {
                        var pl=Ref(g.Get(13),"igPrimLengthArray1_1");int n=pl.Get(3),bits=pl.Get(4);if(n<1 || n>2000000 || (bits!=16 && bits!=32))throw new InvalidDataException("Unsupported strip table.");
                        var data=Data(pl.Get(2),checked(n*(bits/8)));strips=Enumerable.Range(0,n).Select(i=>bits==16?H(data.Offset+i*2):U(data.Offset+i*4)).ToArray();
                    }
                    if(strips.Any(n=>n<3) || strips.Sum(n=>(long)n)!=indices.Length)throw new InvalidDataException("Strip lengths do not cover the index stream.");
                    int offset=0;foreach(int length in strips){for(int i=2;i<length;i++){int a=indices[offset+i-2],b=indices[offset+i-1],c=indices[offset+i];if(a==b||b==c||a==c)continue;if((i&1)!=0)(a,b)=(b,a);faces.Add(a);faces.Add(b);faces.Add(c);}offset+=length;}
                }
                else throw new InvalidDataException("Only triangle lists and strips are supported.");
                if(faces.Count==0)throw new InvalidDataException("No triangles.");mesh.Triangles=faces.ToArray();return mesh;
            }
        }
        public string ExportObj() => ExportObj(null);
        private string ExportObj(string materialLibrary)
        {
            var s=new StringBuilder("# CMC model-space geometry. Geometry can be replaced using OBJ import.\n# Materials, skeleton and animation remain in the original resource.\n");
            if(materialLibrary!=null)s.AppendLine("mtllib "+materialLibrary);
            int meshIndex=0;int offset=1;string N(float n)=>n.ToString("R",CultureInfo.InvariantCulture);
            foreach(var mesh in Meshes)
            {
                s.AppendLine("o "+mesh.Name);
                if(materialLibrary!=null)s.AppendLine("usemtl material_"+meshIndex++);
                foreach(var v in mesh.Positions)s.AppendLine($"v {N(v.X)} {N(v.Y)} {N(v.Z)}");
                for(int i=0;i<mesh.Positions.Length;i++){var uv=mesh.Uvs?[i]??Vector2.Zero;s.AppendLine($"vt {N(uv.X)} {N(materialLibrary==null?uv.Y:1-uv.Y)}");}
                for(int i=0;i<mesh.Positions.Length;i++){var n=mesh.Normals?[i]??Vector3.Zero;s.AppendLine($"vn {N(n.X)} {N(n.Y)} {N(n.Z)}");}
                for(int i=0;i<mesh.Triangles.Length;i+=3){s.Append("f");for(int j=0;j<3;j++){int n=offset+mesh.Triangles[i+j];s.Append($" {n}/{n}/{n}");}s.AppendLine();}
                offset+=mesh.Positions.Length;
            }
            return s.ToString();
        }
        private byte[] ImportSameTopology(string text)
        {
            if(text==null || text.Length>64*1024*1024)throw new InvalidDataException("OBJ is too large.");
            var positions=new List<Vector3>();var normals=new List<Vector3>();var uvs=new List<Vector2>();var faces=new List<(int p,int uv,int n)>();
            float F(string s){if(!float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out float f)||!float.IsFinite(f)||Math.Abs(f)>1000000)throw new InvalidDataException("Invalid OBJ coordinate.");return f;}
            int Index(string s,int count){if(!int.TryParse(s,NumberStyles.Integer,CultureInfo.InvariantCulture,out int i)||i==0)throw new InvalidDataException("Invalid OBJ index.");i=i<0?count+i:i-1;if(i<0||i>=count)throw new InvalidDataException("OBJ index outside its stream.");return i;}
            using var reader=new StringReader(text);string line;
            while((line=reader.ReadLine())!=null)
            {
                line=line.Split('#')[0].Trim();if(line.Length==0)continue;var words=line.Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
                switch(words[0])
                {
                    case "v":if(words.Length!=4)throw new InvalidDataException("OBJ positions must have three coordinates.");positions.Add(new Vector3(F(words[1]),F(words[2]),F(words[3])));break;
                    case "vn":if(words.Length!=4)throw new InvalidDataException("Invalid OBJ normal.");normals.Add(new Vector3(F(words[1]),F(words[2]),F(words[3])));break;
                    case "vt":if(words.Length<3||words.Length>4)throw new InvalidDataException("Invalid OBJ UV.");uvs.Add(new Vector2(F(words[1]),F(words[2])));break;
                    case "f":
                        if(words.Length!=4)throw new InvalidDataException("Import requires the exported triangles; new polygons/topology are unsupported.");
                        for(int j=1;j<4;j++){var ids=words[j].Split('/');if(ids.Length>3)throw new InvalidDataException("Invalid OBJ face.");faces.Add((Index(ids[0],positions.Count),ids.Length>1&&ids[1]!=""?Index(ids[1],uvs.Count):-1,ids.Length>2&&ids[2]!=""?Index(ids[2],normals.Count):-1));}break;
                    case "o":case "g":case "s":case "usemtl":case "mtllib":break;
                    default:throw new InvalidDataException("Unsupported OBJ statement: "+words[0]);
                }
                if(positions.Count>6000000 || uvs.Count>6000000 || normals.Count>6000000 || faces.Count>6000000)throw new InvalidDataException("OBJ exceeds geometry limits.");
            }
            if(positions.Count!=VertexCount || faces.Count!=TriangleCount*3)throw new InvalidDataException("Keep the exported vertex count and triangle topology. Disable vertex merging/reordering in the 3D editor.");
            byte[] output=(byte[])Bytes.Clone();var writes=new Dictionary<int,byte>();
            void Write(int offset,float value){byte[] bytes=BitConverter.GetBytes(value);for(int i=0;i<4;i++){if(writes.TryGetValue(offset+i,out byte old)&&old!=bytes[i])throw new InvalidDataException("Shared vertex/UV/normal edits disagree.");writes[offset+i]=bytes[i];}}
            void V3(int offset,Vector3 value){Write(offset,value.X);Write(offset+4,value.Y);Write(offset+8,value.Z);}
            int vertex=0,face=0;
            foreach(var mesh in Meshes)
            {
                for(int i=0;i<mesh.Positions.Length;i++)V3(mesh.PositionOffset+i*12,positions[vertex+i]);
                foreach(int index in mesh.Triangles)
                {
                    var item=faces[face++];if(item.p!=vertex+index)throw new InvalidDataException("Triangle order/topology changed. Import supports edits to the exported vertex positions, normals and UVs only.");
                    if(item.n>=0 && mesh.NormalOffset>=0)V3(mesh.NormalOffset+index*12,normals[item.n]);
                    if(item.uv>=0 && mesh.UvOffset>=0){var uv=uvs[item.uv];Write(mesh.UvOffset+index*8,uv.X);Write(mesh.UvOffset+index*8+4,uv.Y);}
                }
                vertex+=mesh.Positions.Length;
            }
            foreach(var pair in writes)output[pair.Key]=pair.Value;
            ValidateSameTopology(output);return output;
        }
        private void ValidateSameTopology(byte[] replacement)
        {
            if(replacement==null || replacement.Length!=Bytes.Length)throw new InvalidDataException("Model resource size must stay unchanged.");
            var editable=new bool[Bytes.Length];
            foreach(var mesh in Meshes)
            {
                foreach(var span in new[]{(mesh.PositionOffset,mesh.Positions.Length*12),(mesh.NormalOffset,mesh.Positions.Length*12),(mesh.UvOffset,mesh.Positions.Length*8)})
                    if(span.Item1>=0)for(int i=0;i<span.Item2;i++)editable[span.Item1+i]=true;
            }
            for(int i=0;i<Bytes.Length;i++)if(!editable[i]&&Bytes[i]!=replacement[i])throw new InvalidDataException($"Model metadata/material/rig data changed at 0x{i:X}. Use the exported OBJ to edit supported geometry.");
            var decoded=Decode(replacement);
            if(decoded.VertexCount!=VertexCount || decoded.TriangleCount!=TriangleCount || decoded.Meshes.Count!=Meshes.Count)throw new InvalidDataException("Model structure changed.");
        }
        public byte[] Render(float yaw,float pitch,float zoom,bool wireframe,int selected=-1)
        {
            var meshes=Meshes.Where((_,i)=>selected<0||i==selected).ToArray();var all=meshes.SelectMany(x=>x.Positions).ToArray();
            Vector3 min=all.Aggregate(Vector3.Min),max=all.Aggregate(Vector3.Max),center=(min+max)/2;
            float radius=Math.Max((max-min).Length()/2,0.001f);
            var rotation=Matrix4x4.CreateRotationY(yaw*MathF.PI/180)*Matrix4x4.CreateRotationX(pitch*MathF.PI/180);
            using var bitmap=new SKBitmap(640,420);using var canvas=new SKCanvas(bitmap);canvas.Clear(new SKColor(27,30,38));
            var triangles=new List<(Vector3 a,Vector3 b,Vector3 c,int color)>();
            int colorIndex=0;
            foreach(var mesh in meshes)
            {
                var vertices=mesh.Positions.Select(v=>Vector3.Transform((v-center)/radius,rotation)).ToArray();
                for(int i=0,step=3*Math.Max(1,(TriangleCount+49999)/50000);i<mesh.Triangles.Length;i+=step)triangles.Add((vertices[mesh.Triangles[i]],vertices[mesh.Triangles[i+1]],vertices[mesh.Triangles[i+2]],colorIndex));
                colorIndex++;
            }
            using var paint=new SKPaint { IsAntialias=true,StrokeWidth=0.7f,Style=wireframe?SKPaintStyle.Stroke:SKPaintStyle.Fill };
            SKPoint Project(Vector3 v){float scale=170*Math.Clamp(zoom,0.25f,3f);return new SKPoint(320+v.X*scale,210-v.Y*scale);}
            var palette=new[]{new SKColor(100,174,232),new SKColor(223,166,97),new SKColor(131,203,150),new SKColor(191,147,227),new SKColor(228,137,152)};
            foreach(var triangle in triangles.OrderBy(t=>(t.a.Z+t.b.Z+t.c.Z)/3))
            {
                var n=Vector3.Cross(triangle.b-triangle.a,triangle.c-triangle.a);float shade=n.LengthSquared()<1e-12f?0.4f:0.3f+0.7f*Math.Abs(Vector3.Dot(Vector3.Normalize(n),Vector3.Normalize(new Vector3(-0.3f,0.5f,1))));var c=palette[triangle.color%palette.Length];
                paint.Color=new SKColor((byte)(c.Red*shade),(byte)(c.Green*shade),(byte)(c.Blue*shade));using var path=new SKPath();path.MoveTo(Project(triangle.a));path.LineTo(Project(triangle.b));path.LineTo(Project(triangle.c));path.Close();canvas.DrawPath(path,paint);
            }
            using var png=bitmap.Encode(SKEncodedImageFormat.Png,100);return png.ToArray();
        }
    }
}
