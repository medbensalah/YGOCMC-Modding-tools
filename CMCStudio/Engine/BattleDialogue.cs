using System.IO;
using CMCStudio.Data;

namespace CMCStudio.Engine;

internal static class BattleDialogue
{
    // FUN_0039DE40 selects eight reaction-rule lists by map number. Their
    // records supply the portrait, message and voice together. Use the actual
    // loaded D_SCE ID (0048FF24), not the saved selection index (0048FF2A):
    // campaign reordering can dispatch a different opponent from that selection.
    const int Table = 0x441aa2;
    const int Selector = 0x39df3c;
    const int IsoDelta = 0x35880;

    public static void AddPatches(string source, PatchSet plan)
    {
        using var resource = typeof(BattleDialogue).Assembly.GetManifestResourceStream("BattleDialogueTable.bin")
            ?? throw new InvalidDataException("Missing battle dialogue table.");
        var original = new byte[400];
        resource.ReadExactly(original);
        var reordered = new byte[400];
        for (int encounter = 0; encounter < 25; encounter++)
            for (int reaction = 0; reaction < 8; reaction++)
            {
                int from = (CampaignVisuals.StockMaps[encounter] - 1) * 16 + reaction * 2;
                int to = encounter * 16 + reaction * 2;
                // Each u16 is relative to its own address, not the table base.
                ushort relative = checked((ushort)(from + BitConverter.ToUInt16(original, from) - to));
                BitConverter.GetBytes(relative).CopyTo(reordered, to);
            }
        // Replace map - 1 with loaded encounter script ID - 1. The
        // existing map-zero fallback (row 6) remains unchanged: both orderings
        // have Mako in that row. Intro dialogue uses a separate script path.
        byte[] stock = Convert.FromHexString("FFFF4224FF004230");
        byte[] previous = Convert.FromHexString("2AFF2290FF004230");
        byte[] patched = Convert.FromHexString("24FF2294FFFF4224"); // lhu v0,-0xdc(at); addiu v0,v0,-1
        using var file = File.OpenRead(source);
        var current = new byte[8];
        file.Position = Selector + IsoDelta; file.ReadExactly(current);
        if (!current.SequenceEqual(stock) && !current.SequenceEqual(previous) && !current.SequenceEqual(patched))
            throw new InvalidDataException("Unsupported battle dialogue selector.");
        plan.Add(Selector + IsoDelta, patched);
        plan.Add(Table + IsoDelta, reordered);
    }
}
