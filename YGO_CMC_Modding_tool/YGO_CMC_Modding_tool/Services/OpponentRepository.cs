using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Windows;
using YGO_CMC_Modding_tool.ViewModels;
using static YGO_CMC_Modding_tool.ViewModels.OpponentsViewModel;

namespace YGO_CMC_Modding_tool.Services
{
    public class OpponentRepository
    {
        public struct OpponentDataAddresses
        {
            public long MapData;
            public long SymbolData;
            public long MonstersRed;
            public long MonstersRedLv;
            public long MonstersBlue;
            public long MonstersBlueLv;
            public int MonsterCount;

            public OpponentDataAddresses(long mapData, long symbolData, long monstersRed, long monstersBlue, long monstersRedLv, long monstersBlueLv, int monsterCount)
            {
                MapData = mapData;
                SymbolData = symbolData;
                MonstersRed = monstersRed;
                MonstersBlue = monstersBlue;
                MonstersRedLv = monstersRedLv;
                MonstersBlueLv = monstersBlueLv;
                MonsterCount = monsterCount;
            }
        }

        public static Dictionary<string, OpponentDataAddresses> OpponentNames { get; } = new Dictionary<string, OpponentDataAddresses>()
        {
            { "joey",           new OpponentDataAddresses(0x5AAA7C,   0x44CEDB,   0x5ACACE,   0x5AC689,   0x5ACC62,   0x5AC80D,   4)},
            {"Tristan",         new OpponentDataAddresses(0x5AFEE7,   0x44CEF7,   0x5B1176,   0x5B13BF,   0x5B153F,   0x5B153F,   4)},
            {"Duke",            new OpponentDataAddresses(0x5B27D4,   0x44CF13,   0x5B3993,   0x5B3C0D,   0x5B3E06,   0x5B3E06,   5)},
            {"Tea",             new OpponentDataAddresses(0x5B4564,   0x44CEE9,   0x5B59CF,   0x5B5CA5,   0x5B5F21,   0x5B5F21,   6)},
            {"Grandpa",         new OpponentDataAddresses(0x5B6C77,   0x44CF05,   0x5B7FC3,   0x5B8223,   0x5B8416,   0x5B8416,   5)},
            {"Weevil",          new OpponentDataAddresses(0x5B8F88,   0x44CF2F,   0x5BA4FE,   0x5BA76F,   0x5BA976,   0x5BA976,   5)},
            {"Mako",            new OpponentDataAddresses(0x5BCD64,   0x44CF21,   0x5BE077,   0x5BE36D,   0x5BE615,   0x5BE615,   6)},
            {"Rex",             new OpponentDataAddresses(0x5BF5FB,   0x44CF3D,   0x5C0727,   0x5C0A65,   0x5C0D59,   0x5C0D59,   7)},
            {"Mai",             new OpponentDataAddresses(0x5C1D6A,   0x44CF59,   0x5C2FF5,   0x5C332C,   0x5C3619,   0x5C3619,   7)},
            {"Mokuba",          new OpponentDataAddresses(0x5C45DD,   0x44CF75,   0x5C5BE0,   0x5C5EAA,   0x5C612A,   0x5C612A,   6)},
            {"Bakura",          new OpponentDataAddresses(0x5C7648,   0x44CF67,   0x5C882C,   0x5C8B0A,   0x5C8D80,   0x5C8D80,   6)},
            {"Keith",           new OpponentDataAddresses(0x5C9640,   0x44CF4B,   0x5CA9D6,   0x5CAD2F,   0x5CB03E,   0x5CB03E,   7)},
            {"Pegasus 1",       new OpponentDataAddresses(0x5CC599,   0x44CF83,   0x5CD692,   0x5CD999,   0x5CDC36,   0x5CDC36,   6)},
            {"Odion",           new OpponentDataAddresses(0x5CE51B,   0x44CF91,   0x5CF95D,   0x5CFC85,   0x5CFF5E,   0x5CFF5E,   7)},
            {"Marik",           new OpponentDataAddresses(0x5D0CAC,   0x44CF9F,   0x5D21C7,   0x5D2527,   0x5D27D0,   0x5D27D0,   7)},
            {"Shadi 1",         new OpponentDataAddresses(0x5D64CC,   0x44CFE5,   0x5D74B9,   0x5D77F1,   0x5D7ADF,   0x5D7ADF,   7)},
            {"Kaiba 1",         new OpponentDataAddresses(0x5D8574,   0x44CFBB,   0x5D9AB2,   0x5D9E05,   0x5DA10E,   0x5DA10E,   7)},
            {"Ishizu 1",        new OpponentDataAddresses(0x5DAD80,   0x44CFC9,   0x5DBF55,   0x5DC230,   0x5DC4AB,   0x5DC4AB,   6)},
            {"Yami Marik 1",    new OpponentDataAddresses(0x5DCCE2,   0x44CFD7,   0x5DE3DD,   0x5DE739,   0x5DE9E2,   0x5DE9E2,   7)},
            {"Yami Bakura",     new OpponentDataAddresses(0x5DFCDB,   0x44CFAD,   0x5E1186,   0x5E14AE,   0x5E1790,   0x5E1790,   7)},
            {"Pegasus 2",       new OpponentDataAddresses(0x5E24AF,   0x44CFF3,   0x5E34C0,   0x5E38A4,   0x5E3C46,   0x5E3C46,   9)},
            {"Ishizu 2",        new OpponentDataAddresses(0x5E7582,   0x44D00F,   0x5E892D,   0x5E8CB4,   0x5E900C,   0x5E900C,   7)},
            {"Yami Marik 2",    new OpponentDataAddresses(0x5E9D36,   0x44D01D,   0x5EC040,   0x5EC398,   0x5EC6A1,   0x5EC6A1,   7)},
            {"Shadi 2",         new OpponentDataAddresses(0x5EDD19,   0x44D02B,   0x5EEDCA,   0x5EF0ED,   0x5EF3C6,   0x5EF3C6,   7)},
            {"kaiba 2",         new OpponentDataAddresses(0x5F25A8,   0x44D001,   0x5F3BD2,   0x5F4056,   0x5F3F41,   0x5F43C5,   9)}
        };

        public List<ViewModels.OpponentsViewModel.Opponent> ReadAll(string path)
        {
            var opponents = new List<ViewModels.OpponentsViewModel.Opponent>();

            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(stream))
            {
                foreach (var entry in OpponentNames)
                {
                    var opponent = new ViewModels.OpponentsViewModel.Opponent();
                    opponent.Name = entry.Key;
                    reader.BaseStream.Seek(entry.Value.MapData, SeekOrigin.Begin);
                    opponent.MonsterCap = reader.ReadByte();
                    reader.BaseStream.Seek(entry.Value.MapData + 9, SeekOrigin.Begin);
                    opponent.MaxAP = reader.ReadUInt16();
                    reader.BaseStream.Seek(entry.Value.MapData + 17, SeekOrigin.Begin);
                    opponent.MaxMP = reader.ReadUInt16();


                    reader.BaseStream.Seek(entry.Value.SymbolData + 1, SeekOrigin.Begin);
                    opponent.SymbolDF = reader.ReadUInt16();
                    reader.BaseStream.Seek(entry.Value.SymbolData + 3, SeekOrigin.Begin);
                    opponent.SymbolPP = reader.ReadUInt16();
                    opponent.SymbolBaseAP = reader.ReadUInt16();
                    opponent.SymbolAttribute = reader.ReadByte();
                    reader.BaseStream.Seek(1, SeekOrigin.Current);
                    opponent.SymbolEffect = reader.ReadByte();


                    reader.BaseStream.Seek(entry.Value.MonstersRed, SeekOrigin.Begin);
                    List<EnemyMonsterEntry> reds = new List<EnemyMonsterEntry>();
                    for (int i = 0; i < entry.Value.MonsterCount; i++)
                    {
                        EnemyMonsterEntry enemyMonsterEntry = new EnemyMonsterEntry();
                        reds.Add(enemyMonsterEntry);
                        enemyMonsterEntry.IDRed = reader.ReadUInt16();
                        reader.BaseStream.Seek(97, SeekOrigin.Current);
                    }

                    reader.BaseStream.Seek(entry.Value.MonstersRedLv, SeekOrigin.Begin);
                    for (int i = 0; i < entry.Value.MonsterCount; i++)
                    {
                        reds[i].LevelRed = reader.ReadByte();
                        reader.BaseStream.Seek(4, SeekOrigin.Current);
                    }
                    opponent.Reds = reds;

                    reader.BaseStream.Seek(entry.Value.MonstersBlue, SeekOrigin.Begin);
                    List<EnemyMonsterEntry> blues = new List<EnemyMonsterEntry>();
                    for (int i = 0; i < entry.Value.MonsterCount; i++)
                    {
                        EnemyMonsterEntry enemyMonsterEntry = new EnemyMonsterEntry();
                        blues.Add(enemyMonsterEntry);
                        enemyMonsterEntry.IDBlue = reader.ReadUInt16();
                        reader.BaseStream.Seek(97, SeekOrigin.Current);
                    }
                    reader.BaseStream.Seek(entry.Value.MonstersBlueLv, SeekOrigin.Begin);
                    for (int i = 0; i < entry.Value.MonsterCount; i++)
                    {
                        blues[i].LevelBlue = reader.ReadByte();
                        reader.BaseStream.Seek(4, SeekOrigin.Current);
                    }
                    opponent.Blues = blues;

                    opponents.Add(opponent);
                }
            }

            return opponents;
        }


        public void WriteAll(string path, IList<ViewModels.OpponentsViewModel.Opponent> Opponents)
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                foreach (var entry in OpponentNames)
                {
                    var opponent = Opponents.FirstOrDefault(o => o.Name == entry.Key);
                    if (opponent == null)
                        continue;

                    // Write MapData
                    writer.BaseStream.Seek(entry.Value.MapData, SeekOrigin.Begin);
                    writer.Write((byte)opponent.MonsterCap);
                    writer.BaseStream.Seek(entry.Value.MapData + 9, SeekOrigin.Begin);
                    writer.Write((ushort)opponent.MaxAP);
                    writer.BaseStream.Seek(entry.Value.MapData + 17, SeekOrigin.Begin);
                    writer.Write((ushort)opponent.MaxMP);

                    // Write SymbolData
                    writer.BaseStream.Seek(entry.Value.SymbolData + 1, SeekOrigin.Begin);
                    writer.Write((ushort)opponent.SymbolDF);
                    writer.BaseStream.Seek(entry.Value.SymbolData + 3, SeekOrigin.Begin);
                    writer.Write((ushort)opponent.SymbolPP);
                    writer.Write((ushort)opponent.SymbolBaseAP);
                    writer.Write(opponent.SymbolAttribute);
                    writer.BaseStream.Seek(1, SeekOrigin.Current);
                    writer.Write(opponent.SymbolEffect);

                    // Write MonstersRed
                    writer.BaseStream.Seek(entry.Value.MonstersRed, SeekOrigin.Begin);
                    for (int i = 0; i < entry.Value.MonsterCount; i++)
                    {
                        var red = opponent.Reds != null && opponent.Reds.Count > i ? opponent.Reds[i] : null;
                        writer.Write((ushort)(red?.IDRed ?? 0));
                        writer.BaseStream.Seek(97, SeekOrigin.Current);
                    }
                    // Write MonstersRedLv
                    writer.BaseStream.Seek(entry.Value.MonstersRedLv, SeekOrigin.Begin);
                    for (int i = 0; i < entry.Value.MonsterCount; i++)
                    {
                        var red = opponent.Reds != null && opponent.Reds.Count > i ? opponent.Reds[i] : null;
                        writer.Write((byte)(red?.LevelRed ?? 1));
                        writer.BaseStream.Seek(4, SeekOrigin.Current);
                    }
                    //yami bakura special case write the last lv again
                    if (entry.Key == "Yami Bakura")
                    {
                        var lastRed = opponent.Reds?.LastOrDefault();
                        writer.Write((byte)(lastRed?.LevelRed ?? 1));
                    }


                    // Write MonstersBlue
                    writer.BaseStream.Seek(entry.Value.MonstersBlue, SeekOrigin.Begin);
                    for (int i = 0; i < entry.Value.MonsterCount; i++)
                    {
                        var blue = opponent.Blues != null && opponent.Blues.Count > i ? opponent.Blues[i] : null;
                        writer.Write((ushort)(blue?.IDBlue ?? 0));
                        writer.BaseStream.Seek(97, SeekOrigin.Current);
                    }
                    // Write MonstersBlueLv
                    writer.BaseStream.Seek(entry.Value.MonstersBlueLv, SeekOrigin.Begin);
                    for (int i = 0; i < entry.Value.MonsterCount; i++)
                    {
                        var blue = opponent.Blues != null && opponent.Blues.Count > i ? opponent.Blues[i] : null;
                        writer.Write((byte)(blue?.LevelBlue ?? 1));
                        writer.BaseStream.Seek(4, SeekOrigin.Current);
                    }
                    if (entry.Key == "Yami Bakura")
                    {
                        var lastBlue = opponent.Blues?.LastOrDefault();
                        writer.Write((byte)(lastBlue?.LevelBlue ?? 1));
                    }
                }
            }
        }
    }
}