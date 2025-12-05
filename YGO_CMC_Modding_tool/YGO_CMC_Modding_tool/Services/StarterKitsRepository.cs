using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using YGO_CMC_Modding_tool.ViewModels;
using static YGO_CMC_Modding_tool.ViewModels.StarterKitstListViewModel;

namespace YGO_CMC_Modding_tool.Services
{
    public class StarterKitsRepository
    {
        public struct SKDataAddresses
        {
            public long StarterUnit;
            public long BaseStats;

            public SKDataAddresses(long starterUnit, long baseStats)
            {
                StarterUnit = starterUnit;
                BaseStats = baseStats;
            }
        }

        public static Dictionary<string, SKDataAddresses> StarterKitsAddresses { get; } = new Dictionary<string, SKDataAddresses>()
        {
            {"LIGHT",       new SKDataAddresses(0x4D860A, 0x44EF90)},
            {"DARK",        new SKDataAddresses(0x4D862A, 0x44EFAC)},
            {"FIRE",        new SKDataAddresses(0x4D8646, 0x44EFC8)},
            {"WATER",       new SKDataAddresses(0x4D8662, 0x44EFE4)},
            {"EARTH",       new SKDataAddresses(0x4D8680, 0x44F000)},
            {"WIND",        new SKDataAddresses(0x4D86A0, 0x44F01C)},
            {"WOOD",        new SKDataAddresses(0x4D86B8, 0x44F038)},
            {"THUNDER",     new SKDataAddresses(0x4D86DC, 0x44F054)},
        };

        public Dictionary<string, StarterKitDisplayItem> ReadAll(string path)
        {
            var kits = new Dictionary<string, StarterKitDisplayItem>();

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(fs))
            {
                foreach (var item in StarterKitsAddresses)
                {
                    var kitName = item.Key;
                    var kitAddress = item.Value;
                    var kitCardCount = item.Key switch
                    {
                        "LIGHT" => 15, "DARK" => 13, "FIRE" => 13, "WATER" => 14,
                        "EARTH" => 15, "WIND" => 11, "WOOD" => 17, "THUNDER" => 11,
                        _ => 0
                    };
                    StarterKitDisplayItem kitItem = new StarterKitDisplayItem(kitName);

                    fs.Seek(kitAddress.StarterUnit, SeekOrigin.Begin);
                    for (int i = 0; i < kitCardCount; i++)
                    {
                        kitItem.CardIDs.Add(new CardIdItem { Value = br.ReadUInt16() });
                    }

                    fs.Seek(kitAddress.BaseStats + 4, SeekOrigin.Begin);
                    kitItem.PP = br.ReadUInt16();

                    fs.Seek(kitAddress.BaseStats + 8, SeekOrigin.Begin);
                    kitItem.DF = br.ReadUInt16();

                    fs.Seek(kitAddress.BaseStats + 12, SeekOrigin.Begin);
                    kitItem.AP = br.ReadUInt16();
                    
                    kits[kitName] = kitItem;
                }
            }

            return kits;
        }

        public void WriteAll(string path, List<StarterKitDisplayItem> kits)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs))
            {
                foreach (var kit in kits)
                {
                    if (!StarterKitsAddresses.ContainsKey(kit.BaseName)) continue;

                    var addresses = StarterKitsAddresses[kit.BaseName];

                    // Write BaseStats
                    fs.Seek(addresses.BaseStats + 4, SeekOrigin.Begin);
                    bw.Write(kit.PP);
                    fs.Seek(addresses.BaseStats + 8, SeekOrigin.Begin);
                    bw.Write(kit.DF);
                    fs.Seek(addresses.BaseStats + 12, SeekOrigin.Begin);
                    bw.Write(kit.AP);

                    // Write StarterUnit
                    fs.Seek(addresses.StarterUnit, SeekOrigin.Begin);
                    foreach (var cardIdItem in kit.CardIDs)
                    {
                        bw.Write(cardIdItem.Value);
                    }
                }
            }
        }
    }
}