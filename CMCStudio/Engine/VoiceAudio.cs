using System.IO;
using System.Text;
namespace CMCStudio.Engine;

// Native Sony HD/BD samples; metadata stays untouched. Each Vagi entry is mono.
public static class VoiceAudio
{
    public sealed record Clip(int Id,int Offset,int Size,int Rate,bool Loop)
    {
        public string Label=>$"Sample {Id} · {Rate} Hz · {Size/16*28.0/Rate:F2}s capacity{(Loop?" · loop":"")}";
    }
    static int U(byte[] b,int p)=>checked((int)BitConverter.ToUInt32(b,p));
    static bool Tag(byte[] b,int p,string tag)=>p>=0&&p+tag.Length<=b.Length&&Encoding.ASCII.GetString(b,p,tag.Length)==tag;
    public static Clip[] Clips(byte[] b)
    {
        int hd=U(b,0);if(!Tag(b,hd,"IECSsreV")||!Tag(b,hd+16,"IECSdaeH"))throw new InvalidDataException("Unsupported sound bank.");
        int size=U(b,hd+28),bd=U(b,hd+32),start=4+U(b,4),v=hd+U(b,hd+48);
        if(start!=hd+size||start+bd>b.Length||!Tag(b,v,"IECSigaV"))throw new InvalidDataException("Invalid sound bank boundaries.");
        int n=U(b,v+12)+1;if(n<1||n>1024||v+16+4*n>start)throw new InvalidDataException("Invalid sample table.");
        var infos=new List<(int p,int rate,bool loop)>();
        for(int i=0;i<n;i++){int p=v+U(b,v+16+4*i);if(p<v||p+8>start)throw new InvalidDataException("Invalid sample header.");int off=U(b,p),rate=BitConverter.ToUInt16(b,p+4);if(off>=bd||off%16!=0||rate<4000||rate>48000||b[p+6]>1)throw new InvalidDataException("Invalid sample metadata.");infos.Add((off,rate,b[p+6]!=0));}
        return infos.Select((x,i)=>{int end=infos.Where(y=>y.p>x.p).Select(y=>y.p).DefaultIfEmpty(bd).Min();if((end-x.p)%16!=0)throw new InvalidDataException("Unaligned sound sample.");return new Clip(i,start+x.p,end-x.p,x.rate,x.loop);}).ToArray();
    }
    static readonly int[] A={0,60,115,98,122},B={0,0,-52,-55,-60};
    static int Clamp(int x)=>Math.Clamp(x,-32768,32767);
    public static short[] Decode(byte[] bank,Clip c)
    {
        var samples=new List<short>();int h1=0,h2=0;
        for(int p=c.Offset;p<c.Offset+c.Size;p+=16)
        {
            int filter=bank[p]>>4,shift=bank[p]&15;if(filter>4||shift>12)throw new InvalidDataException("Invalid ADPCM frame.");
            for(int i=0;i<28;i++){int nib=(bank[p+2+i/2]>>(4*(i%2)))&15;if(nib>=8)nib-=16;int x=Clamp(((nib<<12)>>shift)+((h1*A[filter]+h2*B[filter]+32)>>6));samples.Add((short)x);h2=h1;h1=x;}
            if((bank[p+1]&1)!=0)break;
        }
        return samples.ToArray();
    }
    public static byte[] Wav(byte[] bank,Clip c)
    {
        var samples=Decode(bank,c);using var m=new MemoryStream();using var w=new BinaryWriter(m);w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+samples.Length*2);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(c.Rate);w.Write(c.Rate*2);w.Write((short)2);w.Write((short)16);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(samples.Length*2);foreach(short x in samples)w.Write(x);return m.ToArray();
    }
    static (double[] samples,int rate) ReadWav(byte[] b)
    {
        if(!Tag(b,0,"RIFF")||!Tag(b,8,"WAVE"))throw new InvalidDataException("Import a PCM 16-bit WAV (mono or stereo).");
        int rate=0,channels=0;byte[] data=null;
        for(int p=12;p+8<=b.Length;){int size=U(b,p+4),at=p+8;if(size>b.Length-at)throw new InvalidDataException("Truncated WAV.");if(Tag(b,p,"fmt ")){if(size<16||BitConverter.ToUInt16(b,at)!=1||BitConverter.ToUInt16(b,at+14)!=16)throw new InvalidDataException("Use PCM 16-bit WAV.");channels=BitConverter.ToUInt16(b,at+2);rate=U(b,at+4);if(channels is not (1 or 2)||rate<4000||rate>192000)throw new InvalidDataException("Unsupported WAV channels/rate.");}if(Tag(b,p,"data"))data=b[at..(at+size)];p=checked(at+size+(size&1));}
        if(data==null||rate==0||data.Length==0||data.Length%(channels*2)!=0)throw new InvalidDataException("Invalid WAV data.");var samples=new double[data.Length/(2*channels)];for(int i=0;i<samples.Length;i++){for(int ch=0;ch<channels;ch++)samples[i]+=BitConverter.ToInt16(data,(i*channels+ch)*2)/(double)channels;}return(samples,rate);
    }
    public static byte[] Import(byte[] original,int sample,byte[] wav)
    {
        return ImportClip(original,Clips(original).Single(x=>x.Id==sample),wav);
    }
    public static byte[] ImportClip(byte[] original,Clip c,byte[] wav)
    {
        int capacity=Decode(original,c).Length;var (input,rate)=ReadWav(wav);int length=checked((int)Math.Ceiling(input.Length*(double)c.Rate/rate));if(length>capacity)throw new InvalidDataException($"Recording is too long. Maximum: {capacity/(double)c.Rate:F3} seconds. Shorten the WAV first.");
        var pcm=new int[capacity];for(int i=0;i<length;i++){double t=i*(double)rate/c.Rate;int at=Math.Min((int)t,input.Length-1);pcm[i]=Clamp((int)Math.Round(input[at]+(input[Math.Min(at+1,input.Length-1)]-input[at])*(t-at)));}
        var result=(byte[])original.Clone();int h1=0,h2=0;
        for(int frame=0;frame<capacity/28;frame++)
        {
            long best=long.MaxValue;int best1=0,best2=0;byte header=0;var packed=new byte[14];
            for(int filter=0;filter<5;filter++)for(int shift=0;shift<=12;shift++)
            {
                int a=h1,b=h2;long error=0;var trial=new byte[14];int step=1<<(12-shift);
                for(int j=0;j<28;j++){int predict=(a*A[filter]+b*B[filter]+32)>>6;int n=Math.Clamp((int)Math.Round((pcm[frame*28+j]-predict)/(double)step),-8,7);int x=Clamp(n*step+predict);long delta=pcm[frame*28+j]-x;error+=delta*delta;trial[j/2]|=(byte)((n&15)<<(4*(j%2)));b=a;a=x;}
                if(error<best){best=error;best1=a;best2=b;header=(byte)(filter*16+shift);packed=trial;}
            }
            int p=c.Offset+frame*16;result[p]=header;packed.CopyTo(result,p+2);h1=best1;h2=best2;
        }
        return result;
    }
    public static void Validate(ModSession s,int id,byte[] b)
    {
        var original=EncounterContent.Read(s.Source,30,id);var clips=Clips(original);if(b.Length!=original.Length)throw new InvalidDataException("Sound bank allocation changed.");
        var allowed=new bool[b.Length];foreach(var c in clips){Decode(b,c);for(int p=c.Offset;p<c.Offset+c.Size;p+=16){allowed[p]=true;for(int j=2;j<16;j++)allowed[p+j]=true;}}
        for(int i=0;i<b.Length;i++)if(!allowed[i]&&b[i]!=original[i])throw new InvalidDataException("Sound bank metadata, sequence or playback flags changed.");
    }
}
