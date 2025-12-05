using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using YGO_CMC_Modding_tool.ViewModels;
using static YGO_CMC_Modding_tool.ViewModels.StarterKitstListViewModel;

namespace YGO_CMC_Modding_tool.Services
{
    public class ShopsRepoitory
    {

        public static Dictionary<string, long> ShopsAddresses { get; } = new Dictionary<string, long>()
        {
            {"LIGHT Custom Unit",       0x4D8062},  //34
            {"DARK Custom Unit",        0x4D80A8},  //34
            {"FIRE Custom Unit",        0x4D80EE},  //34
            {"WATER Custom Unit",       0x4D8134},  //36
            {"EARTH Custom Unit",       0x4D817E},  //33
            {"WIND Custom Unit",        0x4D81C2},  //36
            {"WOOD Custom Unit",        0x4D820C},  //35
            {"THUNDER Custom Unit",     0x4D8254},  //36
            {"Area 1 Custom Unit",      0x4D8430},  //58
            {"Area 2 Custom Unit",      0x4D84DC},  //33
            {"Area 3 Custom Unit",      0x4D8520},  //44
            {"Area 4 Custom Unit",      0x4D857A},  //21
            {"Area 5 Custom Unit",      0x4D85A6},  //21
        };

        public Dictionary<string, List<ushort>> ReadAll(string path)
        {
            var shops = new Dictionary<string, List<ushort>>();

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(fs))
            {
                foreach (var item in ShopsAddresses)
                {
                    var list = new List<ushort>();

                    var count = item.Key switch
                    {
                        "LIGHT Custom Unit" => 34,
                        "DARK Custom Unit" => 34,
                        "FIRE Custom Unit" => 34,
                        "WATER Custom Unit" => 36,
                        "EARTH Custom Unit" => 33,
                        "WIND Custom Unit" => 36,
                        "WOOD Custom Unit" => 35,
                        "THUNDER Custom Unit" => 36,
                        "Area 1 Custom Unit" => 58,
                        "Area 2 Custom Unit" => 33,
                        "Area 3 Custom Unit" => 44,
                        "Area 4 Custom Unit" => 21,
                        "Area 5 Custom Unit" => 21,
                        _ => 0
                    };

                    fs.Seek(item.Value, SeekOrigin.Begin);
                    for (int i = 0; i < count; i++)
                    {
                        list.Add(br.ReadUInt16());
                    }
                    shops[item.Key] = list;
                }
            }

            return shops;
        }


        public void WriteAll(string path, List<Shop> shops)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs))
            {
                foreach (var shop in shops)
                {
                    if (!ShopsAddresses.ContainsKey(shop.Name)) continue;

                    var address = ShopsAddresses[shop.Name];
                    fs.Seek(address, SeekOrigin.Begin);
                    foreach (var cardIdItem in shop.CardIDs)
                    {
                        bw.Write(cardIdItem.Value);
                    }
                }
            }
        }
    }
}