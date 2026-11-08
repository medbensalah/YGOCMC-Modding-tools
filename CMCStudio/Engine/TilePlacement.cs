using System.IO;
namespace CMCStudio.Engine;
public sealed partial class ModSession
{
    int[] tileBanks;
    // EE 0036BE80: shared tile property bit 0 gates standing destinations.
    // Movement expansion 003626D4 and AI 003BD4D8 reject it when clear.
    // This describes base terrain, not occupancy or event-time overrides.
    public bool TerrainAllowsStanding(int mapId,int variant,int cell)
    {
        if(tileBanks==null){using var file=File.OpenRead(Source);file.Position=0x456E90;var b=new byte[32];file.ReadExactly(b);tileBanks=Enumerable.Range(0,16).Select(i=>(int)BitConverter.ToUInt16(b,i*2)).ToArray();}
        int code=int.Parse(Find($"tile/{mapId}/{variant}/{cell}/code").Value);
        int bank=code>>8;
        if(bank>=tileBanks.Length)return false;
        int shared=tileBanks[bank]+(code&255);
        return (int.Parse(Find($"tileflags/{shared}/0").Value)&1)!=0;
    }
}
