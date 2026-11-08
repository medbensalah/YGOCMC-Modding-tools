using System;
using System.IO;
using System.Linq;
namespace CMCStudio.Data
{
    public static class IsoGuard
    {
        public const long Size = 2079490048;
        public static void Validate(string path)
        {
            using var f = File.OpenRead(path);
            if (f.Length < Size || f.Length%2048!=0) throw new InvalidDataException("Unsupported ISO size; this editor targets the investigated US SLUS_209.40 layout.");
            f.Position=0x8050;var sectors=new byte[8];f.ReadExactly(sectors);var big=sectors.AsSpan(4).ToArray();Array.Reverse(big);
            if(BitConverter.ToUInt32(sectors,0)*2048L!=f.Length || BitConverter.ToUInt32(big,0)!=BitConverter.ToUInt32(sectors,0))throw new InvalidDataException("ISO volume size is inconsistent.");
            f.Position = 0x8001;
            var magic = new byte[5]; f.ReadExactly(magic);
            if (System.Text.Encoding.ASCII.GetString(magic) != "CD001") throw new InvalidDataException("Expected an ISO9660 image.");
            f.Position = 0x8028;
            var volume = new byte[32]; f.ReadExactly(volume);
            if (System.Text.Encoding.ASCII.GetString(volume).Trim() != "SLUS_20940") throw new InvalidDataException("Expected US SLUS_20940.");
            f.Position = 0x135800;
            var elf = new byte[32]; f.ReadExactly(elf);
            if (!elf.SequenceEqual(Convert.FromHexString("7f454c460101010000000000000000000200080001000000b890270034000000")))
                throw new InvalidDataException("Unsupported executable layout.");
            // Boundary tables define every archive extent used by the recovered editors.
            foreach (var check in CMCStudio.Engine.Catalog.Data.Layout)
            {
                if(CMCStudio.Engine.ModelArchives.ValidateLayout(f,check))continue;
                var expected = Convert.FromHexString(check.Bytes);
                var actual = new byte[expected.Length]; f.Position = check.Offset; f.ReadExactly(actual);
                if (!actual.SequenceEqual(expected)) throw new InvalidDataException("Archive layout differs from the investigated image.");
            }
        }
    }
    public sealed class PatchSet
    {
        readonly System.Collections.Generic.SortedList<long,byte[]> ranges=new();
        public int Count {get;private set;}
        int LowerBound(long offset){int lo=0,hi=ranges.Count;while(lo<hi){int mid=lo+(hi-lo)/2;if(ranges.Keys[mid]<offset)lo=mid+1;else hi=mid;}return lo;}
        public void Add(long offset,byte[] data)
        {
            if(offset<0||offset>long.MaxValue-data.Length)throw new InvalidDataException("Patch outside ISO.");if(data.Length==0)return;
            int first=LowerBound(offset);if(first>0&&ranges.Keys[first-1]+ranges.Values[first-1].Length>offset)first--;
            long start=offset,end=offset+data.Length;int last=first;
            while(last<ranges.Count&&ranges.Keys[last]<end){long at=ranges.Keys[last],oldEnd=at+ranges.Values[last].Length;long overlapStart=Math.Max(offset,at),overlapEnd=Math.Min(offset+data.Length,oldEnd);if(overlapEnd>overlapStart&&!data.AsSpan((int)(overlapStart-offset),(int)(overlapEnd-overlapStart)).SequenceEqual(ranges.Values[last].AsSpan((int)(overlapStart-at),(int)(overlapEnd-overlapStart))))throw new InvalidDataException($"Conflicting edits at ISO 0x{overlapStart:X}; shared operands must agree.");start=Math.Min(start,at);end=Math.Max(end,oldEnd);last++;}
            var merged=new byte[checked((int)(end-start))];for(int i=first;i<last;i++){ranges.Values[i].CopyTo(merged,checked((int)(ranges.Keys[i]-start)));Count-=ranges.Values[i].Length;}data.CopyTo(merged,checked((int)(offset-start)));for(int i=first;i<last;i++)ranges.RemoveAt(first);ranges.Add(start,merged);Count=checked(Count+merged.Length);
        }
        public void Apply(string path,bool verify=true)
        {
            if(verify)IsoGuard.Validate(path);
            using var f=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
            if(ranges.Count>0&&ranges.Keys[^1]+ranges.Values[^1].Length>f.Length)throw new InvalidDataException("Patch outside ISO.");
            // Script archives may grow when a map needs a larger deployment grid.
            // Legacy editors still use catalog addresses; route those to the current member.
            var relocated=new PatchSet();bool moved=false;
            foreach(var range in ranges){long at=CMCStudio.Engine.ModelArchives.ResolveScriptOffset(f,range.Key);moved|=at!=range.Key;relocated.Add(at,range.Value);}
            if(moved){f.Dispose();relocated.Apply(path,verify);return;}
            var buffer=new byte[1024*1024];var proof=verify?new byte[buffer.Length]:null;
            for(int i=0;i<ranges.Count;){long start=ranges.Keys[i],end=start+ranges.Values[i].Length;int next=i+1;while(next<ranges.Count&&ranges.Keys[next]-end<=4096&&ranges.Keys[next]+ranges.Values[next].Length-start<=buffer.Length){end=ranges.Keys[next]+ranges.Values[next].Length;next++;}
                int length=checked((int)(end-start));var block=length<=buffer.Length?buffer:new byte[length];f.Position=start;f.ReadExactly(block.AsSpan(0,length));for(int n=i;n<next;n++)ranges.Values[n].CopyTo(block,checked((int)(ranges.Keys[n]-start)));f.Position=start;f.Write(block,0,length);
                if(verify){var check=length<=proof.Length?proof:new byte[length];f.Position=start;f.ReadExactly(check.AsSpan(0,length));if(!check.AsSpan(0,length).SequenceEqual(block.AsSpan(0,length)))throw new IOException("Patch verification failed.");}i=next;
            }
        }
    }
}
