using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace CMCStudio.Engine;
public sealed partial class ModSession
{
    public List<DeploymentLayout> MapDeployments {get;}=new();
    int Start(BinaryField field)=>int.Parse(Find(field.Key).Value);
    void InitializeMapDeployments()
    {
        BinaryField Virtual(string key,int value)
        {
            var field=new BinaryField(key,"Map starting layouts",key,-1,new[]{checked((byte)value)});
            Fields.Add(field);_byKey.Add(key,field);return field;
        }
        foreach(var map in Catalog.Data.Maps.GroupBy(m=>m.Id).Select(g=>g.First()))
        {
            var originals=Deployments.Where(d=>d.MapId==map.Id).ToList();
            // Maps without a campaign encounter get an editable edge-zone layout.
            if(originals.Count==0)originals.Add(new DeploymentLayout{MapId=map.Id,Width=map.Width,Height=map.Height});
            for(int variant=0;variant<originals.Count;variant++)
            {
                var original=originals[variant];var template=new DeploymentLayout{MapId=map.Id,Script=original.Script,Width=map.Width,Height=map.Height,Label=$"Map {map.Id} · starting layout {variant+1} ({map.Width} × {map.Height})"};
                string prefix=$"mapstart/{map.Id}/{variant}";
                if(original.Tiles.Count>0)template.Tiles.AddRange(original.Tiles);
                else for(int i=0;i<map.Width*map.Height;i++)template.Tiles.Add(Virtual($"{prefix}/tile/{i}",i/map.Width<2?1:i/map.Width>=map.Height-2?2:0));
                BinaryField[] Point(string key,int x,int y)=>new[]{Virtual(key+"/x",x),Virtual(key+"/y",y)};
                for(int side=0;side<2;side++)template.Symbols.Add(original.Symbols.Count>side?original.Symbols[side]:Point($"{prefix}/symbol/{side}",map.Width/2,side==0?0:map.Height-1));
                template.ScriptSymbols.AddRange(original.ScriptSymbols);
                foreach(bool red in new[]{false,true})for(int i=0;i<15;i++)
                {
                    var stored=original.Capsules.FirstOrDefault(c=>c.Red==red&&c.Index==i);
                    var zones=Enumerable.Range(0,template.Tiles.Count).Where(cell=>(Start(template.Tiles[cell])&(red?2:1))!=0).ToList();
                    int cell=zones.Count>0?zones[i%zones.Count]:(red?map.Width*map.Height-1-i:i);cell=Math.Clamp(cell,0,map.Width*map.Height-1);
                    template.Capsules.Add(new CapsuleStart{Red=red,Index=i,Slot=i+17,Position=stored?.Position??Point($"{prefix}/{(red?"red":"blue")}/{i}",cell%map.Width,cell/map.Width)});
                }
                MapDeployments.Add(template);
            }
        }
        foreach(var encounter in Deployments)
            foreach(var capsule in encounter.Capsules)
                for(int axis=0;axis<2;axis++)
                {
                    string key=CapsuleKey(encounter.Script,capsule.Red,capsule.Index,axis);
                    if(!_byKey.ContainsKey(key))Virtual(key,255);
                }
    }
    DeploymentLayout MapTemplate(DeploymentLayout encounter)
    {
        int map=int.Parse(Find($"enemy/{encounter.Script}/map").Value);
        var options=MapDeployments.Where(d=>d.MapId==map).ToList();
        int index=Deployments.Where(d=>d.Script==encounter.Script).ToList().IndexOf(encounter);
        return options[Math.Clamp(index,0,options.Count-1)];
    }
    string CapsuleKey(int script,bool red,int index,int axis)=>$"encountercapsule/{script}/{(red?1:0)}/{index}/{axis}";
    BinaryField[] CapsulePosition(int script,CapsuleStart capsule,BinaryField[] fallback,bool initialize)
    {
        var fields=Enumerable.Range(0,2).Select(axis=>Find(CapsuleKey(script,capsule.Red,capsule.Index,axis))).ToArray();
        if(fields.Any(f=>Start(f)==255))
        {
            if(!initialize)return fallback;
            for(int axis=0;axis<2;axis++)fields[axis].Value=Find(fallback[axis].Key).Value;
        }
        return fields;
    }
    public DeploymentLayout EffectiveDeployment(DeploymentLayout encounter)
    {
        var template=MapTemplate(encounter);
        var effective=new DeploymentLayout{Script=encounter.Script,MapId=template.MapId,Width=template.Width,Height=template.Height,Label=encounter.Label};
        effective.Tiles.AddRange(template.Tiles);effective.Symbols.AddRange(template.Symbols);effective.ScriptSymbols.AddRange(template.ScriptSymbols);
        foreach(var capsule in encounter.Capsules)
            effective.Capsules.Add(new CapsuleStart{Opponent=capsule.Opponent,Red=capsule.Red,Index=capsule.Index,Slot=capsule.Slot,Position=CapsulePosition(encounter.Script,capsule,template.Capsules.Single(c=>c.Red==capsule.Red&&c.Index==capsule.Index).Position,true)});
        return effective;
    }
    internal void RecoverMapStarts(ModSession donor,Action<string> warning)
    {
        var assigned=new Dictionary<string,string>();
        void Copy(BinaryField target,BinaryField source)
        {
            string value=donor.Find(source.Key).Value;
            if(assigned.TryGetValue(target.Key,out var previous)&&previous!=value)
                throw new InvalidDataException("Target ISO has conflicting starting layouts for a shared map: "+target.Key+". They cannot be represented by one shared map layout.");
            assigned[target.Key]=value;
        }
        void Point(BinaryField[] target,BinaryField[] source){for(int axis=0;axis<2;axis++)Copy(target[axis],source[axis]);}
        foreach(var group in donor.Deployments.GroupBy(d=>d.Script))
        {
            int variant=0;
            foreach(var layout in group)
            {
                var choices=MapDeployments.Where(d=>d.MapId==layout.MapId).ToList();
                if(choices.Count==0)throw new InvalidDataException($"No starting layout for Map {layout.MapId}.");
                var target=choices[Math.Min(variant++,choices.Count-1)];
                if(target.Tiles.Count!=layout.Tiles.Count)throw new InvalidDataException($"Map {layout.MapId}: deployment grid size differs.");
                for(int i=0;i<target.Tiles.Count;i++)Copy(target.Tiles[i],layout.Tiles[i]);
                for(int i=0;i<Math.Min(target.Symbols.Count,layout.Symbols.Count);i++)Point(target.Symbols[i],layout.Symbols[i]);
                for(int i=0;i<Math.Min(target.ScriptSymbols.Count,layout.ScriptSymbols.Count);i++)Point(target.ScriptSymbols[i],layout.ScriptSymbols[i]);
                foreach(var capsule in layout.Capsules)
                {
                    var position=Enumerable.Range(0,2).Select(axis=>Find(CapsuleKey(layout.Script,capsule.Red,capsule.Index,axis))).ToArray();
                    Point(position,capsule.Position);
                }
                if(layout.ScriptSymbols.Count>target.ScriptSymbols.Count)warning($"Map {layout.MapId}: additional opponent-specific scripted Symbols are not represented by the shared layout.");
            }
        }
        foreach(var pair in assigned)Find(pair.Key).Value=pair.Value;
    }
    public void AlignSymbolPairs(DeploymentLayout layout)
    {
        for(int n=0;n<Math.Min(2,Math.Min(layout.Symbols.Count,layout.ScriptSymbols.Count));n++)
            for(int axis=0;axis<2;axis++)
                Find(layout.ScriptSymbols[n][axis].Key).Value=Find(layout.Symbols[n][axis].Key).Value;
    }
    void ExportMapDeployments(string destination)
    {
        foreach(var layout in MapDeployments)AlignSymbolPairs(layout);
        var edits=new Dictionary<string,byte[]>();
        foreach(var group in Deployments.GroupBy(d=>d.Script))
        {
            bool moved=group.Any(d=>int.Parse(Find($"enemy/{d.Script}/map").Value)!=d.MapId);
            if(!moved&&!group.Any(d=>MapTemplate(d).Fields.Any(f=>Find(f.Key).Changed))&&!Fields.Any(f=>f.Key.StartsWith($"encountercapsule/{group.Key}/")&&f.Changed))continue;
            var asset=ModelArchives.Resolve(destination,Catalog.Data.Assets.Single(a=>a.Type==4&&a.Id==group.Key));
            var sourceAsset=ModelArchives.Resolve(Source,Catalog.Data.Assets.Single(a=>a.Type==4&&a.Id==group.Key));
            byte[] data;using(var file=File.OpenRead(destination)){file.Position=asset.Iso;data=new byte[asset.Size];file.ReadExactly(data);}
            using var output=new MemoryStream();output.Write(data);
            void SetPosition(BinaryField[] target,BinaryField[] value)
            {
                for(int axis=0;axis<2;axis++){long at=target[axis].Offset-sourceAsset.Iso;if(at<0||at>=data.Length)throw new InvalidDataException("Starting operand is outside its script.");output.Position=at;output.WriteByte((byte)Start(value[axis]));}
            }
            foreach(var original in group)
            {
                var template=MapTemplate(original);
                byte[] zone=template.Tiles.Select(f=>f.Encode(Find(f.Key).Value)[0]).Append((byte)255).ToArray();
                // Preserve script offsets: a larger table is appended, never inserted into instructions.
                long table;
                if(zone.Length<=original.Tiles.Count+1)table=original.Tiles[0].Offset-sourceAsset.Iso;
                else table=output.Length;
                foreach(int operand in original.PointerOperands)
                {
                    int relative=checked((int)table-operand);
                    if(relative<short.MinValue||relative>short.MaxValue)throw new InvalidDataException("Deployment table exceeds the script's relative-pointer range.");
                    output.Position=operand;output.Write(BitConverter.GetBytes((short)relative));
                }
                output.Position=table;output.Write(zone);
                for(int n=0;n<original.Symbols.Count;n++)SetPosition(original.Symbols[n],template.Symbols[n%2]);
                for(int n=0;n<original.ScriptSymbols.Count;n++)SetPosition(original.ScriptSymbols[n],n<2?template.Symbols[n]:template.ScriptSymbols.Count>n?template.ScriptSymbols[n]:template.Symbols[1]);
                foreach(var capsule in original.Capsules)SetPosition(capsule.Position,CapsulePosition(original.Script,capsule,template.Capsules.Single(c=>c.Red==capsule.Red&&c.Index==capsule.Index).Position,false));
            }
            edits[ModelKey(asset)]=output.ToArray();
        }
        if(edits.Count>0)ModelArchives.Repack(destination,edits);
    }
}
