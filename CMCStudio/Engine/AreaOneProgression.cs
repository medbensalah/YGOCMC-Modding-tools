using System.IO;
namespace CMCStudio.Engine;
internal static class AreaOneProgression
{
    sealed class Profile { public int Script {get;set;} public int Start {get;set;} public string Original {get;set;} public string Payload {get;set;} }
    static readonly Profile[] Profiles=Load();
    static Profile[] Load()
    {
        using var stream=typeof(AreaOneProgression).Assembly.GetManifestResourceStream("CampaignProgression.json")??throw new InvalidDataException("Missing campaign progression profiles.");
        return System.Text.Json.JsonSerializer.Deserialize<Profile[]>(stream);
    }
    public static void Apply(string path,ModSession session)
    {
        var order=session.Campaign.Select(c=>Catalog.Data.Encounters.Single(e=>e.Name.Equals(c.Enemy,StringComparison.OrdinalIgnoreCase)).Script).ToArray();
        if(order.SequenceEqual(Enumerable.Range(1,25)))return;
        var edits=new Dictionary<string,byte[]>();
        for(int slot=0;slot<25;slot++)
        {
            int script=order[slot];if(script==slot+1)continue;
            var asset=ModelArchives.Resolve(path,Catalog.Data.Assets.Single(a=>a.Type==4&&a.Id==script));
            byte[] bytes;using(var f=File.OpenRead(path)){f.Position=asset.Iso;bytes=new byte[asset.Size];f.ReadExactly(bytes);}
            var profile=Profiles.Single(p=>p.Script==script);
            int start=profile.Start;var original=Convert.FromHexString(profile.Original);var replacement=Convert.FromHexString(Profiles.Single(p=>p.Script==slot+1).Payload);
            if(!bytes.AsSpan(start,original.Length).SequenceEqual(original))throw new InvalidDataException("The encounter's victory progression is already modified; use the original ISO as the project source.");
            void Jump(int at,int target){bytes[at]=2;BitConverter.GetBytes(target-at-1).CopyTo(bytes,at+1);}
            if(original.Length==replacement.Length)replacement.CopyTo(bytes,start);
            else if(replacement.Length+5<=original.Length)
            {replacement.CopyTo(bytes,start);Jump(start+replacement.Length,start+original.Length);}
            else
            {
                int appended=bytes.Length;
                if(appended+replacement.Length+5>0x7000)throw new InvalidDataException("Slot progression does not fit this encounter's script buffer.");
                Array.Resize(ref bytes,appended+replacement.Length+5);replacement.CopyTo(bytes,appended);
                Jump(appended+replacement.Length,start+original.Length);Jump(start,appended);
            }
            edits[ModSession.ModelKey(asset)]=bytes;
        }
        ModelArchives.Repack(path,edits);
    }
}
