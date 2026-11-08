using System.IO;
using System.Text.RegularExpressions;
namespace CMCStudio.Engine;
public static class DialogueAudio
{
    // EE 2b1fe0/2b2130: group boundaries at 40c810, 8-byte records at 40c850.
    // EE 2f76b0 unpacks script voice IDs into group (>>7) and sample (&127).
    // IOP SNDCTLTX 23d0 sets voice pitch from EE 302314: 0xc99 (~37.8 kHz).
    public const int Rate=37800;
    public sealed record Recording(int Id,long Offset,int Size)
    {
        public VoiceAudio.Clip Clip=>new(Id,0,Size,Rate,false);
    }
    public sealed record Link(int Script,int Message,int Recording,int CueOffset);
    public static Recording[] Recordings(string source)
    {
        var file=ModelArchives.FileExtent(source,"VOICE.BIN");using var f=File.OpenRead(source);f.Position=0x40c810+0x35880;var header=new byte[64];f.ReadExactly(header);
        if(BitConverter.ToUInt16(header)!=26||Enumerable.Range(0,27).Any(i=>BitConverter.ToUInt16(header,2+2*i)!=128*i))throw new InvalidDataException("Unsupported dialogue voice index.");
        var data=new byte[3328*8];f.ReadExactly(data);var rows=new List<Recording>();
        for(int i=0;i<3328;i++){int p=i*8,n=data[p+1]|data[p+2]<<8|data[p+3]<<16;long at=BitConverter.ToUInt32(data,p+4)*2048L;if(n<16||n%16!=0||at+n>file.Size)throw new InvalidDataException("Invalid dialogue voice allocation.");rows.Add(new Recording(i,file.Offset+at,n));}
        var ordered=rows.OrderBy(r=>r.Offset).ToArray();for(int i=1;i<ordered.Length;i++)if(ordered[i].Offset<ordered[i-1].Offset+ordered[i-1].Size)throw new InvalidDataException("Overlapping dialogue recordings.");return rows.ToArray();
    }
    public static byte[] Read(string source,Recording r){using var f=File.OpenRead(source);f.Position=r.Offset;var b=new byte[r.Size];f.ReadExactly(b);return b;}
    public static Link[] Links(string source)
    {
        var links=new List<Link>();
        for(int script=1;script<=25;script++){
            var b=EncounterContent.Read(source,4,script);var lines=EncounterContent.Messages(EncounterContent.Read(source,14,16+script));
            for(int at=160;at+4<b.Length;at++){
                if(b[at]!=0x20||b[at+1] is not (10 or 11))continue;
                int voice=BitConverter.ToUInt16(b,at+2);if(voice>=3328)continue;int p=at+4;
                // Accept only a consecutive known command sequence ending in an encounter text command.
                while(p+12<=b.Length&&p-at<128){int op=b[p],sub=b[p+1];
                    if(op==0x31&&sub==1){int packed=BitConverter.ToUInt16(b,p+3),message=packed>>6;if((packed&63)==10&&message<lines.Length&&!lines[message].SequenceEqual(lines[1024]))links.Add(new Link(script,message,voice,at));break;}
                    int size=op==0x38&&sub is 14 or 15?3:op==0x50&&sub==12?4:op==0x50&&sub==16?6:0;
                    if(size==0)break;p+=size;
                }
            }
        }
        return links.DistinctBy(l=>(l.Script,l.Message,l.Recording)).ToArray();
    }
    public static string Plain(string text)=>Regex.Replace(text,@"\{[0-9A-Fa-f]{2}\}","").Replace('\u00a0',' ').Trim();
    public static void Validate(ModSession session)
    {
        if(session.DialogueVoiceEdits.Count==0)return;var rows=Recordings(session.Source);
        foreach(var e in session.DialogueVoiceEdits){if(e.Key<0||e.Key>=rows.Length)throw new InvalidDataException("Unknown dialogue recording.");var r=rows[e.Key];var original=Read(session.Source,r);if(e.Value.Length!=r.Size)throw new InvalidDataException("Dialogue recording allocation changed.");VoiceAudio.Decode(e.Value,r.Clip);for(int p=1;p<r.Size;p+=16)if(e.Value[p]!=original[p])throw new InvalidDataException("Dialogue playback flags changed.");}
    }
    public static void Apply(string path,ModSession session)
    {
        if(session.DialogueVoiceEdits.Count==0)return;var rows=Recordings(path);using var f=new FileStream(path,FileMode.Open,FileAccess.Write);foreach(var e in session.DialogueVoiceEdits){f.Position=rows[e.Key].Offset;f.Write(e.Value);}
    }
}
