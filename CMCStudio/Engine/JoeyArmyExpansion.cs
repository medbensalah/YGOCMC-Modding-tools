using System.IO;
using CMCStudio.Models;
namespace CMCStudio.Engine;

// First supported resize: append four complete Joey capsule setups without moving
// existing bytecode or changing the player deployment cap.
internal static class JoeyArmyExpansion
{
    // Decode the expansion emitted by Apply, not the player's deployment cap.
    public static void Recover(string path, OpponentsViewModel.Opponent enemy)
    {
        var asset=ModelArchives.Resolve(path,Catalog.Data.Assets.Single(a=>a.Type==4&&a.Id==1));
        using var f=File.OpenRead(path);f.Position=asset.Iso;
        var bytes=new byte[asset.Size];f.ReadExactly(bytes);
        var recovered=new List<OpponentsViewModel.EnemyMonsterEntry>[2];
        for(int side=0;side<2;side++)
        {
            bool red=side==1;int hook=red?0x3c89:0x3850,start=red?0x3aca:0x3685;
            var army=(red?enemy.Reds:enemy.Blues).Take(4).Select(u=>u.Clone()).ToList();
            for(int n=0;n<4;n++){army[n].SpawnX=bytes[start+99*n+26];army[n].SpawnY=bytes[start+99*n+27];}
            if(!bytes.AsSpan(hook,3).SequenceEqual(new byte[]{1,5,2}))
            {
                if(bytes[hook]!=1)throw new InvalidDataException("Unknown Joey army hook; cannot recover capsule count safely.");
                int begin=hook+1+BitConverter.ToInt16(bytes,hook+1);
                if(begin<0x6500||begin+4*67+36>Math.Min(bytes.Length,0x6800))throw new InvalidDataException("Unrecognized Joey expansion location.");
                for(int n=0;n<4;n++)
                {
                    int p=begin+n*67,level=begin+268+n*9;byte slot=(byte)(0x15+n);
                    foreach(int offset in new[]{2,8,18,25,30,39,47})
                        if(bytes[p+offset]!=slot)throw new InvalidDataException("Unrecognized Joey capsule setup layout.");
                    if(bytes[p+40]!=3||p+41+BitConverter.ToInt32(bytes,p+41)!=0x6500||
                        !bytes.AsSpan(level,3).SequenceEqual(new byte[]{0x99,1,slot})||
                        !bytes.AsSpan(level+4,5).SequenceEqual(new byte[]{0,0xb0,0x0a,slot,1}))
                        throw new InvalidDataException("Unrecognized Joey capsule initialization.");
                    ushort id=BitConverter.ToUInt16(bytes,p+4);
                    if(id>=277||bytes[level+3]<1||bytes[level+3]>10)throw new InvalidDataException("Invalid Joey expanded monster or level.");
                    var unit=new OpponentsViewModel.EnemyMonsterEntry{UnitSlot=slot,IDRed=id,IDBlue=id,SpawnX=bytes[p+26],SpawnY=bytes[p+27],LevelAssigned=true};
                    unit.LevelRed=bytes[level+3];army.Add(unit);
                }
            }
            recovered[side]=army;
        }
        if(recovered[0].Count!=recovered[1].Count)throw new InvalidDataException("Joey has different expansion counts on each side.");
        enemy.Blues=recovered[0];enemy.Reds=recovered[1];
    }

    public static void Apply(string path, OpponentsViewModel.Opponent enemy, int mapId)
    {
        if(enemy.Reds.Count==4 && enemy.Blues.Count==4)return;
        if(enemy.Reds.Count!=8 || enemy.Blues.Count!=8)throw new InvalidDataException("Joey supports 4 or 8 enemy capsules.");
        var map=Catalog.Data.Maps.First(m=>m.Id==mapId);
        var asset=ModelArchives.Resolve(path,Catalog.Data.Assets.Single(a=>a.Type==4&&a.Id==1));
        byte[] bytes;using(var f=File.OpenRead(path)){f.Position=asset.Iso;bytes=new byte[asset.Size];f.ReadExactly(bytes);}
        // Run after map deployment export so original capsules already use the assigned map.
        using var output=new MemoryStream();output.Write(bytes);
        // Original Joey bytecode ends at 0x64ed with return instructions. The
        // remaining archive alignment padding is unused; never overwrite a mod.
        const int storage=0x6500, storageEnd=0x6800;
        if(bytes.Length<storageEnd || bytes.AsSpan(storage,storageEnd-storage).IndexOfAnyExcept((byte)0)>=0)
            throw new InvalidDataException("Joey's reserved script padding is already occupied by another modification.");
        output.Position=storage;
        // This camera/audio sequence is identical for every capsule and has no
        // relative operands. Share it using the VM's normal call/return commands.
        var common=bytes.AsSpan(0x3685+40,37).ToArray();
        output.Write(common);output.WriteByte(4);
        foreach(bool red in new[]{false,true})
        {
            int start=red?0x3aca:0x3685, hook=red?0x3c89:0x3850;
            if(!bytes.AsSpan(hook,3).SequenceEqual(new byte[]{1,5,2}))throw new InvalidDataException("Joey's army continuation is modified; expansion cannot be applied safely.");
            int target=hook+1+BitConverter.ToInt16(bytes,hook+1);
            int begin=checked((int)output.Position);
            var army=red?enemy.Reds:enemy.Blues;
            var occupied=new HashSet<(byte,byte)>();
            for(int n=0;n<4;n++)occupied.Add((bytes[start+99*n+26],bytes[start+99*n+27]));
            for(int n=0;n<4;n++)
            {
                var unit=army[n+4];byte slot=(byte)(0x15+n);
                if(unit.UnitSlot!=slot || unit.SpawnX>=map.Width || unit.SpawnY>=map.Height || !occupied.Add((unit.SpawnX,unit.SpawnY)))throw new InvalidDataException($"Joey's extra capsules need distinct positions inside Map {mapId} ({map.Width} x {map.Height}). Edit Extra X/Y in Enemy armies.");
                var block=bytes.AsSpan(start+99*n,88).ToArray();
                // Complete final spawn ends with 70 01 42 00; preceding records
                // have an additional 11-byte camera/setup separator.
                foreach(int offset in new[]{2,8,18,25,30,39,79})
                {
                    if(block[offset]!=(byte)(0x11+n))throw new InvalidDataException("Unexpected Joey capsule setup layout.");
                    block[offset]=slot;
                }
                BitConverter.GetBytes(red?unit.IDRed:unit.IDBlue).CopyTo(block,4);
                block[26]=unit.SpawnX;block[27]=unit.SpawnY;
                if(!block.AsSpan(40,37).SequenceEqual(common))throw new InvalidDataException("Joey's capsule animation sequence was modified.");
                output.Write(block.AsSpan(0,40));
                int call=checked((int)output.Position);output.WriteByte(3);output.Write(BitConverter.GetBytes(storage-(call+1)));
                output.Write(block.AsSpan(77));
                // Preserve native camera reset between capsule animations.
                output.Write(bytes.AsSpan(start+88,11));
            }
            for(int n=0;n<4;n++)
            {
                byte slot=(byte)(0x15+n);var unit=army[n+4];
                output.Write(new byte[]{0x99,1,slot,red?unit.LevelRed:unit.LevelBlue,0,0xb0,0x0a,slot,1});
            }
            // Copy the actual per-unit AI policies, including side-specific targets.
            for(int p=start+3*99+88;p<hook;)
            {
                int length=bytes[p] switch {
                    0x99=>bytes[p+1]==1?5:bytes[p+1]==2?4:0,
                    0xb0=>bytes[p+1] switch {1=>4,2=>6,3=>5,0x0a=>4,_=>0},_=>0};
                if(length==0 || p+length>hook)throw new InvalidDataException("Unrecognized Joey initialization command; expansion stopped.");
                if(bytes[p]==0xb0 && bytes[p+1]!=0x0a && bytes[p+2]>=0x11 && bytes[p+2]<=0x14)
                {var command=bytes.AsSpan(p,length).ToArray();command[2]+=4;output.Write(command);}
                p+=length;
            }
            int end=checked((int)output.Position);output.WriteByte(2);output.Write(BitConverter.GetBytes(target-(end+1)));
            int resume=checked((int)output.Position);
            if(resume>storageEnd)throw new InvalidDataException("Joey's expanded army exceeds its reserved script space.");
            output.Position=hook;output.WriteByte(1);output.Write(BitConverter.GetBytes(checked((short)(begin-(hook+1)))));
            output.Position=resume;
        }
        ModelArchives.Repack(path,new Dictionary<string,byte[]>{{ModSession.ModelKey(asset),output.ToArray()}});
    }
}
