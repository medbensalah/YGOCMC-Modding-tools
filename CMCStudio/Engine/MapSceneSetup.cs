using System.IO;
using System.Text.Json;

namespace CMCStudio.Engine;

// Battlefield geometry and its auxiliary D_MAP objects are loaded separately.
// Keep the character's script offsets intact by redirecting only its map setup.
public static class MapSceneSetup
{
    sealed class Encounter
    {
        public int Script { get; set; }
        public int Map { get; set; }
        public Setup[] Setups { get; set; }
        public Setup[] LateSetups { get; set; } = Array.Empty<Setup>();
    }
    sealed class Setup
    {
        public int Start { get; set; }
        public int End { get; set; }
        public string Bytes { get; set; }
    }
    static readonly Encounter[] Templates = Load();
    static readonly byte[] Marker = "CMCScene"u8.ToArray();
    const int Capacity = 1024;
    static Encounter[] Load()
    {
        using var stream=typeof(MapSceneSetup).Assembly.GetManifestResourceStream("MapSceneSetups.json")
            ?? throw new InvalidDataException("Missing map scenery templates.");
        return JsonSerializer.Deserialize<Encounter[]>(stream);
    }
    // Opcode 02 advances from the operand's address by a signed 32-bit offset.
    // Verified at EE 0x2DF4E0; the VM advances its instruction base at 0x2DE470.
    static void Jump(byte[] bytes,int at,int target)
    {
        bytes[at]=2;
        BitConverter.GetBytes(checked(target-at-1)).CopyTo(bytes,at+1);
    }
    static int Existing(byte[] bytes,int start)
    {
        if(bytes[start]!=2)return -1;
        long target=(long)start+1+BitConverter.ToInt32(bytes,start+1);
        if(target<16||target>bytes.Length)return -1;
        int p=(int)target;
        int capacity=BitConverter.ToInt32(bytes,p-4);
        return bytes.AsSpan(p-16,8).SequenceEqual(Marker)
            &&BitConverter.ToInt32(bytes,p-8)==start
            &&capacity>0&&capacity<=Capacity&&target+capacity<=bytes.Length ? p : -1;
    }
    public static void Apply(string destination,ModSession session)
    {
        var edits=new Dictionary<string,byte[]>();
        foreach(var original in Templates)
        {
            int map=int.Parse(session.Find($"enemy/{original.Script}/map").Value);
            MapRuntimeEvents.RequireSupported(original.Map,map);
            var asset=ModelArchives.Resolve(destination,Catalog.Data.Assets.Single(a=>a.Type==4&&a.Id==original.Script));
            byte[] bytes;
            using(var file=File.OpenRead(destination)){file.Position=asset.Iso;bytes=new byte[asset.Size];file.ReadExactly(bytes);}
            bool changed=false;
            // Grandpa's entry 6 checks outcomes, then invokes his original
            // map's 89/34 hazard handler. Frozen-state evidence places the VM
            // here on swapped Map 1, not in the water callback. Keep outcome
            // checks, but return before the map-specific tail on reassignment.
            if(original.Script==5)
            {
                if(bytes[0x174]!=0x34 || bytes[0x173] is not (0x89 or 0x04))
                    throw new InvalidDataException("Unsupported Grandpa map-hazard hook.");
                byte opcode=map==original.Map?(byte)0x89:(byte)0x04;
                if(bytes[0x173]!=opcode){bytes[0x173]=opcode;changed=true;}
            }
            var receiverSetups=original.Setups.Concat(original.LateSetups).ToArray();
            for(int variant=0;variant<receiverSetups.Length;variant++)
            {
                var setup=receiverSetups[variant];
                int target=Existing(bytes,setup.Start);
                if(map==original.Map)
                {
                    if(target>=0){Convert.FromHexString(setup.Bytes).CopyTo(bytes,setup.Start);changed=true;}
                    continue;
                }
                if(target<0&&!bytes.AsSpan(setup.Start,5).SequenceEqual(Convert.FromHexString(setup.Bytes).AsSpan(0,5)))
                    throw new InvalidDataException($"Encounter {original.Script} has an unsupported map initialization.");
                var donor=Templates.FirstOrDefault(e=>e.Map==map);
                // Unused maps have no encounter-specific auxiliary objects.
                byte[] payload=donor==null?Convert.FromHexString("8105010380008900000001810101")
                    :Convert.FromHexString(donor.Setups[MapRuntimeEvents.HasEvent(map) || map==24 ? donor.Setups.Length-1 : Math.Min(variant,donor.Setups.Length-1)].Bytes);
                // Water's first-visit script initializes animation states later in
                // its character intro. The rematch prefix contains those states.
                // Reset its four-bit cycle counter explicitly for a new encounter.
                if(map==1)payload=[..payload,0x12,0x04,0xc0,0x01,0x00,0x00];
                payload=[..payload,..MapRuntimeEvents.Initialization(map)];
                // Marik replaces temporary intro objects here. Clear those slots
                // before reloading the assigned map, rather than his cube scenery.
                if(variant>=original.Setups.Length)payload=[0x81,0x03,0x82,0x81,0x03,0x81,0x81,0x03,0x80,..payload];
                if(payload.Length+5>Capacity)throw new InvalidDataException("Map scenery initialization is too large.");
                int required=payload.Length+5;
                if(target<0 || BitConverter.ToInt32(bytes,target-4)<required)
                {
                    // The redirect skips this entire verified setup prefix. Reuse
                    // its body when it fits; restoring the map restores the full prefix.
                    int header=setup.End-setup.Start>=21+required ? setup.Start+5 : bytes.Length;
                    target=checked(header+16);
                    if(target+required>bytes.Length)Array.Resize(ref bytes,checked(target+required));
                    Marker.CopyTo(bytes,header);
                    BitConverter.GetBytes(setup.Start).CopyTo(bytes,header+8);
                    BitConverter.GetBytes(required).CopyTo(bytes,header+12);
                }
                payload.CopyTo(bytes,target);
                Jump(bytes,target+payload.Length,setup.End);
                Jump(bytes,setup.Start,target);
                changed=true;
            }
            changed |= MapRuntimeEvents.Apply(ref bytes,original.Map,map);
            if(changed && bytes.Length>0x7000)
                throw new InvalidDataException($"Encounter {original.Script} exceeds the game's 28 KiB script buffer after map reassignment. Export from the original ISO.");
            if(changed)edits[ModSession.ModelKey(asset)]=bytes;
        }
        if(edits.Count>0)ModelArchives.Repack(destination,edits);
    }
}
