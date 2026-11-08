using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
namespace CMCStudio.Engine
{
    public sealed partial class AlchemyModel
    {
        // Keep the byte-preserving path for edits to exported geometry.
        public byte[] ImportObj(string text)
        {
            try { return ImportSameTopology(text); }
            catch(InvalidDataException) { return Rebuild(ParseObj(text)); }
        }
        private static ModelMesh ParseObj(string text)
        {
            if(text==null || text.Length>256*1024*1024)throw new InvalidDataException("OBJ exceeds the 256 MB input limit.");
            var p=new List<Vector3>();var uv=new List<Vector2>();var n=new List<Vector3>();
            var vertices=new List<Vector3>();var tex=new List<Vector2>();var normals=new List<Vector3>();
            float F(string s){if(!float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out var f)||!float.IsFinite(f)||Math.Abs(f)>1000000)throw new InvalidDataException("OBJ coordinates must be finite and within ±1,000,000.");return f;}
            int Id(string s,int count){if(!int.TryParse(s,out int i)||i==0)throw new InvalidDataException("Invalid OBJ index.");i=i<0?count+i:i-1;if(i<0||i>=count)throw new InvalidDataException("OBJ index outside its stream.");return i;}
            using var reader=new StringReader(text);string line;int lineNumber=0;
            while((line=reader.ReadLine())!=null)
            {
                lineNumber++;var w=line.Split('#')[0].Split((char[])null,StringSplitOptions.RemoveEmptyEntries);if(w.Length==0)continue;
                switch(w[0])
                {
                    case "v":
                        if(w.Length<4)throw new InvalidDataException($"Invalid vertex on line {lineNumber}.");
                        p.Add(new Vector3(F(w[1]),F(w[2]),F(w[3])));break;
                    case "vt":if(w.Length<3)throw new InvalidDataException("Invalid UV.");uv.Add(new Vector2(F(w[1]),F(w[2])));break;
                    case "vn":if(w.Length<4)throw new InvalidDataException("Invalid normal.");n.Add(new Vector3(F(w[1]),F(w[2]),F(w[3])));break;
                    case "f":
                        if(w.Length<4)throw new InvalidDataException("Faces need at least three corners.");
                        var corners=w.Skip(1).Select(s=>{var a=s.Split('/');if(a.Length>3)throw new InvalidDataException("Invalid face corner.");return (p:Id(a[0],p.Count),t:a.Length>1&&a[1]!=""?Id(a[1],uv.Count):-1,n:a.Length>2&&a[2]!=""?Id(a[2],n.Count):-1);}).ToArray();
                        // Fan triangulation supports triangles and convex polygons (triangulate concave faces in the editor).
                        for(int i=1;i<corners.Length-1;i++)
                        {
                            var tri=new[]{corners[0],corners[i],corners[i+1]};var normal=Vector3.Cross(p[tri[1].p]-p[tri[0].p],p[tri[2].p]-p[tri[0].p]);normal=normal.LengthSquared()>1e-20f?Vector3.Normalize(normal):Vector3.UnitY;
                            foreach(var c in tri){vertices.Add(p[c.p]);tex.Add(c.t<0?Vector2.Zero:uv[c.t]);normals.Add(c.n<0?normal:n[c.n]);}
                        }
                        break;
                    case "o":case "g":case "s":case "usemtl":case "mtllib":case "l":case "p":break;
                    default:throw new InvalidDataException($"Unsupported OBJ statement on line {lineNumber}: {w[0]}");
                }
                if(vertices.Count>6000000||p.Count>6000000||uv.Count>6000000||n.Count>6000000)throw new InvalidDataException("Import exceeds the safety limit of 2 million triangles / 6 million vertices.");
            }
            if(vertices.Count==0)throw new InvalidDataException("OBJ contains no faces.");
            return new ModelMesh { Positions=vertices.ToArray(),Normals=normals.ToArray(),Uvs=tex.ToArray(),Triangles=Enumerable.Range(0,vertices.Count).ToArray() };
        }
        private byte[] Rebuild(ModelMesh imported)
        {
            // Wrapped scene resources contain additional relative offsets; do not silently corrupt those.
            if(Bytes.Length<48||BitConverter.ToUInt32(Bytes,40)!=0xFADA)throw new InvalidDataException("Topology replacement currently requires a standalone Alchemy resource. Select a battle-unit or map-unit model.");
            var block=new Block(Bytes,0);block.Read();
            if(Bytes.AsSpan(block.End).IndexOfAnyExcept((byte)0)>=0)throw new InvalidDataException("This resource contains a wrapper or additional blocks. Topology replacement is not supported for it.");
            return block.Rebuild(imported);
        }
        public void ValidateReplacement(byte[] replacement)
        {
            try { ValidateSameTopology(replacement);return; }catch(InvalidDataException) { }
            if(ValidateMapTextureReplacement(replacement))return;
            var decoded=Decode(replacement);
            var first=decoded.Meshes[0];
            // Reproduce the entire resource from trusted metadata. No arbitrary material, rig or executable bytes may be imported in configurations.
            var expected=Rebuild(first);
            if(!expected.AsSpan().SequenceEqual(replacement) && !ValidateDiffuseReplacement(expected,replacement))throw new InvalidDataException("Model replacement does not match a validated geometry/material rebuild.");
        }
        private sealed partial class Block
        {
            public byte[] Rebuild(ModelMesh imported)
            {
                var edited=new Dictionary<int,byte[]>();var objectBytes=_bytes.AsSpan(_objectStart,_memoryStart-_objectStart).ToArray();
                void Set(Record r,int slot,int value){if(!r.Offsets.TryGetValue(slot,out var off))throw new InvalidDataException($"Missing {r.Type} field {slot}.");BitConverter.GetBytes(value).CopyTo(objectBytes,off-_objectStart);}
                void Memory(int id,byte[] bytes){Ref(id,"memory");if(edited.TryGetValue(id,out var prior)&&!prior.AsSpan().SequenceEqual(bytes))throw new InvalidDataException("Shared vertex buffers need incompatible replacements.");edited[id]=bytes;}
                byte[] V3(Vector3[] values){var b=new byte[checked(values.Length*12)];for(int i=0;i<values.Length;i++){BitConverter.GetBytes(values[i].X).CopyTo(b,i*12);BitConverter.GetBytes(values[i].Y).CopyTo(b,i*12+4);BitConverter.GetBytes(values[i].Z).CopyTo(b,i*12+8);}return b;}
                byte[] V2(Vector2[] values){var b=new byte[checked(values.Length*8)];for(int i=0;i<values.Length;i++){BitConverter.GetBytes(values[i].X).CopyTo(b,i*8);BitConverter.GetBytes(values[i].Y).CopyTo(b,i*8+4);}return b;}
                var geometry=Objects.Where(r=>r?.Type=="igGeometryAttr1_5").ToArray();if(geometry.Length==0)throw new InvalidDataException("No geometry to replace.");
                for(int gi=0;gi<geometry.Length;gi++)
                {
                    var g=geometry[gi];var va=Ref(g.Get(4),"igVertexArray1_1");int oldCount=va.Get(3);
                    var mesh=gi==0?imported:new ModelMesh { Positions=new Vector3[3],Normals=new Vector3[3],Uvs=new Vector2[3] };
                    int count=mesh.Positions.Length;var channels=Data(va.Get(2),76);
                    for(int channel=0;channel<19;channel++)
                    {
                        int id=I(channels.Offset+channel*4);if(id<0)continue;
                        if(channel==0)Memory(id,V3(mesh.Positions));
                        else if(channel==1)Memory(id,V3(mesh.Normals??new Vector3[count]));
                        else if(channel==11)Memory(id,V2(mesh.Uvs??new Vector2[count]));
                        else Resize(id);
                    }
                    // Keep valid per-vertex auxiliary data even when animation is disabled by the user.
                    foreach(int slot in new[]{7,8,10})if(va.Values.TryGetValue(slot,out int id)&&id>=0)Resize(id);
                    void Resize(int id){var r=Ref(id,"memory");if(oldCount<=0||r.Size%oldCount!=0)throw new InvalidDataException("Unrecognized auxiliary vertex stream.");int stride=r.Size/oldCount;var data=new byte[checked(count*stride)];for(int i=0;i<count;i++)Buffer.BlockCopy(_bytes,r.Offset,data,i*stride,stride);Memory(id,data);}
                    Set(va,3,count);Set(g,5,-1);Set(g,6,3);Set(g,7,count/3);Set(g,13,-1);
                }
                // Every logical reference gets its own directory descriptor, avoiding shared-size aliasing.
                if(_dirs.Length>ushort.MaxValue)throw new InvalidDataException("Alchemy directory reference limit exceeded.");
                using var directories=new MemoryStream();
                for(int i=0;i<_dirs.Length;i++)
                {
                    var d=_dirs[i];var b=_bytes.AsSpan(d.Offset,d.Size).ToArray();
                    if(edited.TryGetValue(i,out var data))BitConverter.GetBytes(data.Length).CopyTo(b,d.Offsets[7]-d.Offset);
                    directories.Write(b);
                }
                using var memory=new MemoryStream();
                for(int i=0;i<Objects.Length;i++)if(Objects[i]?.Type=="memory")
                {
                    var r=Objects[i];memory.Write(edited.TryGetValue(i,out var b)?b:_bytes.AsSpan(r.Offset,r.Size).ToArray());while(memory.Length%4!=0)memory.WriteByte(0);
                }
                using var output=new MemoryStream();output.Write(_bytes.AsSpan(_start,_dirStart-_start));output.Write(directories.ToArray());
                using(var writer=new BinaryWriter(output,Encoding.UTF8,true))
                {
                    int indexSize=8+2*_dirs.Length;writer.Write(indexSize);writer.Write(_dirs.Length);for(int i=0;i<_dirs.Length;i++)writer.Write((ushort)i);while((output.Length-(_dirStart-_start)-directories.Length)<indexSize)writer.Write((byte)0);
                }
                output.Write(_bytes.AsSpan(_indexEnd,_objectStart-_indexEnd));output.Write(objectBytes);output.Write(memory.ToArray());
                var result=output.ToArray();BitConverter.GetBytes(checked((int)directories.Length)).CopyTo(result,0);BitConverter.GetBytes(_dirs.Length).CopyTo(result,4);BitConverter.GetBytes(checked((int)memory.Length)).CopyTo(result,24);
                // Archive members occupy full CD sectors.
                Array.Resize(ref result,checked((result.Length+2047)&~2047));return result;
            }
        }
    }
}
