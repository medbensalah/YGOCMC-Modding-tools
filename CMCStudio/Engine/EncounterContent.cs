using System.IO;
using System.Text;
using System.Text.RegularExpressions;
namespace CMCStudio.Engine;

public static class EncounterContent
{
    public static Asset Asset(int type,int id)=>Catalog.Data.Assets.Single(a=>a.Type==type&&a.Id==id);
    public static byte[] Read(string path,int type,int id){var a=ModelArchives.Resolve(path,Asset(type,id));using var f=File.OpenRead(path);f.Position=a.Iso;var b=new byte[a.Size];f.ReadExactly(b);return b;}
    public static byte[][] Messages(byte[] b)
    {
        if(b.Length<2050||BitConverter.ToUInt16(b)!=2050)throw new InvalidDataException("Unsupported encounter text bank.");
        return Enumerable.Range(0,1025).Select(i=>{int p=2*i+BitConverter.ToUInt16(b,2*i);if(p<2050||p>=b.Length)throw new InvalidDataException("Invalid text pointer.");int end=Array.IndexOf(b,(byte)31,p);if(end<0)throw new InvalidDataException("Unterminated text.");return b[p..end];}).ToArray();
    }
    public static string Decode(byte[] b)=>string.Concat(b.Select(x=>x==29?"\n":x==145?"\u00a0":x>=32&&x<=126&&x!=123&&x!=125?((char)x).ToString():$"{{{x:X2}}}"));
    public static byte[] Encode(string text)
    {
        var bytes=new List<byte>();text=text.Replace("\r\n","\n");
        for(int i=0;i<text.Length;i++)
        {
            char c=text[i];if(c=='{') {if(i+3>=text.Length||text[i+3]!='}'||!byte.TryParse(text.Substring(i+1,2),System.Globalization.NumberStyles.HexNumber,null,out byte v)||v==31)throw new InvalidDataException("Control codes must be {XX}; {1F} is reserved.");bytes.Add(v);i+=3;}
            else if(c=='\n')bytes.Add(29);else if(c=='\u00a0')bytes.Add(145);else if(c>=32&&c<=126&&c!='}')bytes.Add((byte)c);else throw new InvalidDataException("Use the game's ASCII characters or {XX} byte codes.");
        }
        return bytes.ToArray();
    }
    static byte[] Controls(byte[] b)=>b.Where(x=>x<32&&x!=29||x>126&&x!=145).ToArray();
    public static byte[] ReplaceText(byte[] original,string text)
    {
        var b=Encode(text);if(!Controls(original).SequenceEqual(Controls(b)))throw new InvalidDataException("Keep the original control/glyph codes in their original order. Edit the visible wording and line breaks.");return b;
    }
    public static byte[] BuildMessages(byte[][] lines)
    {
        if(lines.Length!=1025)throw new InvalidDataException("Text IDs must be preserved.");
        using var m=new MemoryStream();m.SetLength(2050);m.Position=2050;var offsets=new Dictionary<string,int>();var ptr=new ushort[1025];
        for(int i=0;i<lines.Length;i++)
        {
            string key=Convert.ToHexString(lines[i]);if(!offsets.TryGetValue(key,out int p)||p<2*i){p=checked((int)m.Length);offsets[key]=p;m.Position=p;m.Write(lines[i]);m.WriteByte(31);}
            if(p-2*i>65535)throw new InvalidDataException("Text bank exceeds the 16-bit pointer limit.");ptr[i]=(ushort)(p-2*i);
        }
        m.SetLength((m.Length+2047)/2048*2048);var b=m.ToArray();for(int i=0;i<ptr.Length;i++)BitConverter.GetBytes(ptr[i]).CopyTo(b,2*i);return b;
    }
    static (int,int) Key(string key,int maxFirst,int maxSecond)
    {
        var p=key.Split('/');if(p.Length!=2||!int.TryParse(p[0],out int a)||!int.TryParse(p[1],out int b)||a<1||a>maxFirst||b<0||b>maxSecond||key!=$"{a}/{b}")throw new InvalidDataException("Invalid encounter content key.");return(a,b);
    }
    public static Dictionary<string,byte[]> TextBanks(ModSession s)
    {
        var banks=new Dictionary<int,byte[][]>();
        foreach(var e in s.DialogueEdits){var (script,id)=Key(e.Key,25,1024);if(!banks.TryGetValue(script,out var lines))banks[script]=lines=Messages(Read(s.Source,14,16+script));lines[id]=ReplaceText(lines[id],e.Value);}
        return banks.ToDictionary(e=>$"14/{16+e.Key}",e=>BuildMessages(e.Value));
    }
    public static int[] Entries(byte[] b)
    {
        if(b.Length<161)throw new InvalidDataException("Truncated encounter script.");
        return Enumerable.Range(0,40).Select(i=>{long p=4L*i+BitConverter.ToUInt32(b,4*i);if(p<160||p>=b.Length)throw new InvalidDataException("Invalid event entry.");return(int)p;}).ToArray();
    }
    public static void Validate(ModSession s)
    {
        TextBanks(s);
        foreach(var e in s.TriggerRoutes){var (script,entry)=Key(e.Key,25,39);var b=Read(s.Source,4,script);var entries=Entries(b);if(!entries.Contains(e.Value))throw new InvalidDataException("Trigger destination must be an existing entry in the same encounter.");if(entry is 0 or 16)throw new InvalidDataException("Battle initialization entries cannot be rerouted.");}
        foreach(var e in s.VoiceEdits)VoiceAudio.Validate(s,e.Key,e.Value);
    }
    public static void ApplyFixed(string path,ModSession s)
    {
        using var f=new FileStream(path,FileMode.Open,FileAccess.ReadWrite);
        foreach(var e in s.TriggerRoutes){var (script,entry)=Key(e.Key,25,39);var a=Asset(4,script);f.Position=ModelArchives.ResolveScriptOffset(f,a.Iso+4*entry);f.Write(BitConverter.GetBytes(checked((uint)(e.Value-4*entry))));}
        foreach(var e in s.VoiceEdits){var a=Asset(30,e.Key);f.Position=a.Iso;f.Write(e.Value);}
    }
}
