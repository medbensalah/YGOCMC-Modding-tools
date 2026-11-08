using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CMCStudio.Engine;

// Script 34 loads five map thumbnails and six character sheets (Yugi + five opponents)
// per area. These banks are independent of its encounter dispatch table.
public static class CampaignVisuals
{
    public static readonly int[] StockMaps = {1,2,3,4,5,6,7,8,9,12,11,10,13,14,24,16,17,15,23,20,21,22,19,18,25};
    static readonly byte[] Characters = {2,4,6,3,5,8,7,9,11,13,12,10,14,15,16,21,18,19,20,17,22,24,25,26,23};
    public static byte CharacterId(int script)=>Characters[script-1];
    public static void AddPatches(PatchSet plan, int[] order) =>
        plan.Add(0x4242a0 + 0x35880, order.Select(id => Characters[id-1]).ToArray());

    static int U(byte[] b,int p) => checked((int)BitConverter.ToUInt32(b,p));
    public static List<byte[]> Split(byte[] bytes)
    {
        int header=U(bytes,0),count=header/4-2;
        if(header%4!=0 || count<1 || count>32 || header>bytes.Length || U(bytes,header-4)!=0)
            throw new InvalidDataException("Unsupported campaign image bank.");
        var blocks=new List<byte[]>();
        for(int i=0;i<count;i++)
        {
            int start=checked(i*4+U(bytes,i*4)),end=checked((i+1)*4+U(bytes,(i+1)*4));
            if(start<header || end<=start || end>bytes.Length)throw new InvalidDataException("Invalid campaign image block.");
            int magic=BitConverter.ToUInt16(bytes,start);
            if(magic!=0x5f51 && magic!=0x5f71)throw new InvalidDataException("Unsupported campaign image format.");
            blocks.Add(bytes.AsSpan(start,end-start).ToArray());
        }
        return blocks;
    }
    public static byte[] Join(IReadOnlyList<byte[]> blocks)
    {
        int header=(blocks.Count+2)*4,position=header;
        var result=new byte[header+blocks.Sum(b=>b.Length)];
        for(int i=0;i<blocks.Count;i++)
        {
            BitConverter.GetBytes(position-i*4).CopyTo(result,i*4);
            blocks[i].CopyTo(result,position);position+=blocks[i].Length;
        }
        BitConverter.GetBytes(position-blocks.Count*4).CopyTo(result,blocks.Count*4);
        return result;
    }
    // Repacking appends the new archive. The original physical banks remain immutable
    // donors, including on reopening an exported ISO: permutations never compound and
    // assigning the same map twice cannot destroy the only copy of another thumbnail.
    public static Dictionary<string,byte[]> Build(ModSession session)
    {
        var order=session.Campaign.Select(s=>Catalog.Data.Encounters.Single(e=>e.Name.Equals(s.Enemy,StringComparison.OrdinalIgnoreCase)).Script).ToArray();
        using var source=File.OpenRead(session.Source);
        byte[] Read(Asset asset){source.Position=asset.Iso;var b=new byte[asset.Size];source.ReadExactly(b);return b;}
        Asset Asset(int id)=>Catalog.Data.Assets.Single(a=>a.Type==3&&a.Id==id);
        var originals=new Dictionary<int,List<byte[]>>();
        List<byte[]> Original(int id)
        {
            if(!originals.TryGetValue(id,out var blocks)) originals[id]=blocks=Split(Read(Asset(id)));
            return blocks;
        }
        var edits=new Dictionary<string,byte[]>();
        void Save(int id,List<byte[]> blocks)
        {
            var current=Split(Read(ModelArchives.Resolve(session.Source,Asset(id))));
            if(current.Count==blocks.Count && current.Zip(blocks).All(p=>p.First.AsSpan().SequenceEqual(p.Second)))return;
            edits.Add($"3/{id}",Join(blocks));
        }
        var thumbnails=new Dictionary<int,byte[]>();
        byte[] Thumbnail(int map)
        {
            if(thumbnails.TryGetValue(map,out var cached))return cached;
            int index=Array.IndexOf(StockMaps,map);
            byte[] block;
            if(index>=0)block=Original(81+index/5)[index%5];
            else
            {
                var asset=Catalog.Data.Assets.Single(a=>a.Type==0&&a.Id==map);
                var data=session.ModelEdits.TryGetValue(ModSession.ModelKey(asset),out var changed)?changed:AlchemyModel.ReadAsset(session.Source,asset);
                var png=CMCStudio.CampaignThumbnail.Render(AlchemyModel.Decode(data));
                block=(byte[])Original(81)[0].Clone();
                if(BitConverter.ToUInt16(block)!=0x5f51 || BitConverter.ToUInt16(block,2)!=1 || U(block,4)!=536)
                    throw new InvalidDataException("Unexpected map thumbnail layout.");
                var info=new PortraitInfo{Width=200,Height=152,PaletteAndPixels=block.AsSpan(24,512+200*152).ToArray()};
                using var image=new MemoryStream(png);PortraitCodec.Import(info,image,false).CopyTo(block,24);
            }
            return thumbnails[map]=block;
        }
        for(int area=0;area<5;area++)
        {
            var maps=new List<byte[]>();var people=new List<byte[]>{Original(88+area)[0]};
            for(int slot=0;slot<5;slot++)
            {
                int id=order[area*5+slot],index=id-1;
                maps.Add(Thumbnail(int.Parse(session.Find($"enemy/{id}/map").Value)));
                people.Add(EnemyPortraits.Block(session,id,false));
            }
            Save(81+area,maps);Save(88+area,people);
        }
        foreach(int bank in new[]{65,77})
        {
            var blocks=Original(bank).ToList();var names=(byte[])blocks[2].Clone();var donor=blocks[2];
            if(BitConverter.ToUInt16(names)!=0x5f71 || BitConverter.ToUInt16(names,2)!=25)throw new InvalidDataException("Unexpected enemy name sheet.");
            for(int i=0;i<25;i++)
            {
                int from=4+16*(order[i]-1),to=4+16*i;
                if(BitConverter.ToUInt16(names,to+8)!=240 || BitConverter.ToUInt16(names,to+10)!=32)throw new InvalidDataException("Unexpected enemy name dimensions.");
                donor.AsSpan(U(donor,from),240*32/2).CopyTo(names.AsSpan(U(names,to),240*32/2));
            }
            blocks[2]=names;Save(bank,blocks);
        }
        foreach(var edit in session.EnemyPortraitEdits.Where(e=>e.Key.EndsWith("/loading")))
        {
            EnemyPortraits.Validate(session,edit.Key,edit.Value);
            int script=int.Parse(edit.Key.Split('/')[0]);
            edits.Add($"3/{EnemyPortraits.LoadingResource(script)}",edit.Value);
        }
        return edits;
    }
}
