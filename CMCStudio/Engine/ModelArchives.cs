using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace CMCStudio.Engine
{
    // Relocate whole archives: the game uses ISO file extents plus executable-relative member boundaries.
    public static class ModelArchives
    {
        public static bool CanResize(int type)=>type==0||type==1||type==2||type==3||type==4||type==7||type==10||type==11||type==14;
        private static uint U(Stream f,long p){f.Position=p;Span<byte>b=stackalloc byte[4];f.ReadExactly(b);return BitConverter.ToUInt32(b);}
        private static void Put(Stream f,long p,uint n){f.Position=p;f.Write(BitConverter.GetBytes(n));}
        private static void Both(Stream f,long p,uint n){Put(f,p,n);var b=BitConverter.GetBytes(n);Array.Reverse(b);f.Write(b);}
        private sealed record Entry(long Record,long Offset,uint Size);
        public static (long Offset,int Size) FileExtent(string path,string name) { using var f=File.OpenRead(path);var e=Find(f,name);return(e.Offset,checked((int)e.Size)); }
        private static Entry Find(Stream f,string name)
        {
            long root=U(f,0x809e)*2048L;uint size=U(f,0x80a6);
            if(size>1024*1024 || root<0 || root+size>f.Length)throw new InvalidDataException("Invalid ISO directory.");
            var data=new byte[size];f.Position=root;f.ReadExactly(data);
            for(int p=0;p<data.Length;)
            {
                int length=data[p];if(length==0){p=(p/2048+1)*2048;continue;}
                if(length<34||p+length>data.Length||33+data[p+32]>length)throw new InvalidDataException("Invalid ISO directory record.");
                string current=Encoding.ASCII.GetString(data,p+33,data[p+32]).Split(';')[0];
                if(current==name)return new Entry(root+p,BitConverter.ToUInt32(data,p+2)*2048L,BitConverter.ToUInt32(data,p+10));p+=length;
            }
            throw new InvalidDataException("Missing archive: "+name);
        }
        private static (long table,uint[] offsets,LayoutCheck check,Entry file) Read(Stream f,int type)
        {
            long table=U(f,0x44A8B0+4*type)+0x35880L;
            var check=Catalog.Data.Layout.SingleOrDefault(c=>c.Offset==table)??throw new InvalidDataException("Unknown archive boundary table.");
            var asset=Catalog.Data.Assets.First(a=>a.Type==type);var file=Find(f,asset.File);
            byte[] bytes=new byte[check.Bytes.Length/2];f.Position=table;f.ReadExactly(bytes);uint[] offsets=Enumerable.Range(0,bytes.Length/4).Select(i=>BitConverter.ToUInt32(bytes,i*4)).ToArray();
            if(file.Offset+file.Size>f.Length || file.Size>int.MaxValue)throw new InvalidDataException("Archive outside ISO.");
            for(int i=0;i<offsets.Length;i++)
            {
                uint at=offsets[i]&0x7fffffff;
                if(at>file.Size || (i>0&&at<(offsets[i-1]&0x7fffffff)))throw new InvalidDataException("Invalid archive member boundary.");
            }
            if((offsets[^1]&0x7fffffff)!=file.Size)throw new InvalidDataException("Archive size disagrees with boundary table.");
            return(table,offsets,check,file);
        }
        public static Asset Resolve(string path,Asset asset)
        {
            if(!CanResize(asset.Type))return asset;
            using var f=File.OpenRead(path);var a=Read(f,asset.Type);uint start=a.offsets[asset.Id]&0x7fffffff,end=a.offsets[asset.Id+1]&0x7fffffff;
            return new Asset {Type=asset.Type,Id=asset.Id,File=asset.File,Iso=a.file.Offset+start,Size=checked((int)(end-start)),Flag=(a.offsets[asset.Id]&0x80000000)!=0};
        }
        static readonly Asset[] ScriptAssets=Catalog.Data.Assets.Where(a=>a.Type==4).ToArray();
        public static long ResolveScriptOffset(Stream stream,long original)
        {
            var asset=ScriptAssets.FirstOrDefault(a=>original>=a.Iso&&original<a.Iso+a.Size);
            if(asset==null)return original;
            var archive=Read(stream,4);
            return archive.file.Offset+(archive.offsets[asset.Id]&0x7fffffff)+(original-asset.Iso);
        }
        public static long ResolveScriptOffset(string path,long original)
        {
            using var f=File.OpenRead(path);return ResolveScriptOffset(f,original);
        }
        public static long ResolveMessageOffset(Stream stream,long original)
        {
            var asset=Catalog.Data.Assets.SingleOrDefault(a=>a.Type==14&&original>=a.Iso&&original<a.Iso+a.Size);
            if(asset==null)return original;
            var archive=Read(stream,14);
            return archive.file.Offset+(archive.offsets[asset.Id]&0x7fffffff)+(original-asset.Iso);
        }
        public static bool ValidateLayout(Stream f,LayoutCheck check)
        {
            foreach(int type in new[]{0,1,2,3,4,7,10,11,14})
            {
                long table=U(f,0x44A8B0+4*type)+0x35880L;if(table!=check.Offset)continue;
                var a=Read(f,type);var original=Convert.FromHexString(check.Bytes);
                for(int i=0;i<a.offsets.Length;i++)
                {
                    uint old=BitConverter.ToUInt32(original,4*i);
                    if((old&0x80000000)!=(a.offsets[i]&0x80000000))throw new InvalidDataException("Archive member flags changed.");
                    if(i>0 && ((old&0x7fffffff)==(BitConverter.ToUInt32(original,4*(i-1))&0x7fffffff)) != ((a.offsets[i]&0x7fffffff)==(a.offsets[i-1]&0x7fffffff)))throw new InvalidDataException("Archive empty-member layout changed.");
                }
                return true;
            }
            return false;
        }
        public static void Repack(string path,Dictionary<string,byte[]> edits,bool forceRebuild=false)
        {
            foreach(var group in edits.Select(e=>(asset:Catalog.Data.Assets.Single(a=>ModSession.ModelKey(a)==e.Key),bytes:e.Value)).GroupBy(e=>e.asset.Type))
            {
                using var f=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.Read);
                var a=Read(f,group.Key);var replacements=group.ToDictionary(e=>e.asset.Id,e=>e.bytes);
                // Keep the existing archive when every replacement fits its current slot.
                bool aligned=Enumerable.Range(0,a.offsets.Length-1).All(i=>(a.offsets[i]&0x7fffffff)==(a.offsets[i+1]&0x7fffffff)||(a.offsets[i]&2047)==0);
                if(!forceRebuild&&aligned&&replacements.All(e=>e.Value.Length<=(a.offsets[e.Key+1]&0x7fffffff)-(a.offsets[e.Key]&0x7fffffff))){
                    var zeros=new byte[2048];foreach(var edit in replacements){long at=a.file.Offset+(a.offsets[edit.Key]&0x7fffffff);int size=checked((int)((a.offsets[edit.Key+1]&0x7fffffff)-(a.offsets[edit.Key]&0x7fffffff)));f.Position=at;f.Write(edit.Value);int remaining=size-edit.Value.Length;while(remaining>0){int n=Math.Min(remaining,zeros.Length);f.Write(zeros,0,n);remaining-=n;}}continue;
                }
                using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite,1024*1024,FileOptions.SequentialScan);
                long start=(f.Length+2047)&~2047L,write=start;uint[] newOffsets=(uint[])a.offsets.Clone();
                byte[] buffer=new byte[1024*1024];
                // Preserve a possible prefix before the first member.
                void Copy(long read,long length){input.Position=read;f.Position=write;while(length>0){int n=(int)Math.Min(buffer.Length,length);input.ReadExactly(buffer.AsSpan(0,n));f.Write(buffer,0,n);write+=n;length-=n;}}
                Copy(a.file.Offset,a.offsets[0]&0x7fffffff);
                for(int i=0;i<a.offsets.Length-1;i++)
                {
                    newOffsets[i]=(uint)(write-start)|(a.offsets[i]&0x80000000);
                    if(replacements.TryGetValue(i,out var b))
                    {
                        f.Position=write;f.Write(b);write+=b.Length;int pad=(int)((2048-write%2048)%2048);if(pad>0){f.Write(new byte[pad]);write+=pad;}
                    }
                    else Copy(a.file.Offset+(a.offsets[i]&0x7fffffff),(a.offsets[i+1]&0x7fffffff)-(a.offsets[i]&0x7fffffff));
                    // CD reads use memberOffset >> 11. Padding only replacements
                    // shifts following original members when old terminal padding differs.
                    int alignmentPad=(int)((2048-write%2048)%2048);if(alignmentPad>0){f.Position=write;f.Write(new byte[alignmentPad]);write+=alignmentPad;}
                    if(write-start>int.MaxValue)throw new InvalidDataException("Archive exceeds its 31-bit size limit.");
                }
                newOffsets[^1]=(uint)(write-start)|(a.offsets[^1]&0x80000000);
                for(int i=0;i<newOffsets.Length;i++)Put(f,a.table+4*i,newOffsets[i]);
                Both(f,a.file.Record+2,checked((uint)(start/2048)));Both(f,a.file.Record+10,checked((uint)(write-start)));
                f.SetLength((f.Length+2047)&~2047L);
                Both(f,0x8050,checked((uint)(f.Length/2048)));
            }
        }
    }
}


