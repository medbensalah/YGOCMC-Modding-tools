using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Security;
using CMCStudio.Models;
using static CMCStudio.Models.OpponentsViewModel;

namespace CMCStudio.Data
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
            {"Ishizu 2",        new OpponentDataAddresses(0x5E7582,   0x44D00F,   0x5E892D,   0x5E8CB4,   0x5E900C,   0x5E900C,   8)},
            {"Yami Marik 2",    new OpponentDataAddresses(0x5E9D36,   0x44D01D,   0x5EC040,   0x5EC398,   0x5EC6A1,   0x5EC6A1,   7)},
            {"Shadi 2",         new OpponentDataAddresses(0x5EDD19,   0x44D02B,   0x5EEDCA,   0x5EF0ED,   0x5EF3C6,   0x5EF3C6,   7)},
            {"kaiba 2",         new OpponentDataAddresses(0x5F25A8,   0x44D001,   0x5F3BD2,   0x5F4056,   0x5F3F41,   0x5F43C5,   9)}
        };

        public static OpponentDataAddresses ResolveAddresses(string path,OpponentDataAddresses a)
        {
            using var f=File.OpenRead(path);
            long R(long off)=>CMCStudio.Engine.ModelArchives.ResolveScriptOffset(f,off);
            return new OpponentDataAddresses(R(a.MapData),a.SymbolData,R(a.MonstersRed),R(a.MonstersBlue),R(a.MonstersRedLv),R(a.MonstersBlueLv),a.MonsterCount);
        }

        private static Dictionary<byte, long> LevelOperands(BinaryReader reader, long address)
        {
            var result = new Dictionary<byte, long>();
            for (int i = 0; i < 16; i++)
            {
                reader.BaseStream.Position = address - 3 + 5 * i;
                if (reader.ReadByte() != 0x99 || reader.ReadByte() != 1) break;
                byte slot = reader.ReadByte();
                if (slot < 0x11 || slot > 0x1F || result.ContainsKey(slot)) throw new InvalidDataException("Invalid level assignment stream.");
                result.Add(slot, address + 5 * i);
            }
            if (result.Count == 0) throw new InvalidDataException("Missing level assignment stream.");
            return result;
        }

        private static List<EnemyMonsterEntry> ReadArmy(BinaryReader reader, long address, long levels, int count, bool red, bool allowUnassigned = false)
        {
            var levelMap = LevelOperands(reader, levels);
            var army = new List<EnemyMonsterEntry>();
            for (int i = 0; i < count; i++)
            {
                reader.BaseStream.Position = address + 99 * i - 4;
                if (reader.ReadByte() != 0x92 || reader.ReadByte() != 0x22) throw new InvalidDataException("Unexpected monster spawn instruction.");
                byte slot = reader.ReadByte();
                if (reader.ReadByte() != 1) throw new InvalidDataException("Unexpected spawn operand.");
                bool assigned = levelMap.ContainsKey(slot);
                // Kaiba 1 has seven level operands but slot 0x15 is unused;
                // its seventh spawn uses slot 0x18 instead.
                if (!assigned && allowUnassigned && slot == 0x18 && levelMap.TryGetValue(0x15, out long spare))
                {
                    levelMap[slot] = spare;
                    assigned = true;
                }
                if (!assigned && !(allowUnassigned && slot == 0x18)) throw new InvalidDataException("Spawn has no matching level assignment.");
                ushort id = reader.ReadUInt16();
                if (id >= 277) throw new InvalidDataException("Army ID must be an ordinary monster (0-276).");
                byte level = 1;
                if (assigned) { reader.BaseStream.Position = levelMap[slot]; level = (byte)(reader.ReadByte() & 15); }
                if (level < 1 || level > 10) throw new InvalidDataException("Unsupported army level; expected 1-10.");
                var unit = new EnemyMonsterEntry { UnitSlot = slot, LevelRed = (byte)level, LevelAssigned = assigned };
                if (red) unit.IDRed = id; else unit.IDBlue = id;
                army.Add(unit);
            }
            return army;
        }

        public List<Opponent> ReadAll(string path)
        {
            IsoGuard.Validate(path);
            var opponents = new List<Opponent>();
            using (var reader = new BinaryReader(File.OpenRead(path)))
            foreach (var entry in OpponentNames)
            {
                var a = ResolveAddresses(path,entry.Value);
                var o = new Opponent { Name = entry.Key, LevelsShared = a.MonstersRedLv == a.MonstersBlueLv };
                reader.BaseStream.Position = a.MapData; o.MonsterCap = reader.ReadByte();
                reader.BaseStream.Position = a.MapData + 9; o.MaxAP = reader.ReadUInt16();
                reader.BaseStream.Position = a.MapData + 17; o.MaxMP = reader.ReadUInt16();
                reader.BaseStream.Position = a.SymbolData + 1; o.SymbolDF = reader.ReadUInt16();
                reader.BaseStream.Position = a.SymbolData + 3; o.SymbolPP = reader.ReadUInt16();
                o.SymbolBaseAP = reader.ReadUInt16(); o.SymbolAttribute = reader.ReadByte();
                reader.BaseStream.Position++; o.SymbolEffect = reader.ReadByte();
                o.Reds = ReadArmy(reader, a.MonstersRed, a.MonstersRedLv, a.MonsterCount, true, entry.Key == "Kaiba 1");
                o.Blues = ReadArmy(reader, a.MonstersBlue, a.MonstersBlueLv, a.MonsterCount, false, entry.Key == "Kaiba 1");
                if (o.LevelsShared)
                    foreach (var blue in o.Blues)
                        blue.AttachLevel(o.Reds.First(r => r.UnitSlot == blue.UnitSlot).SharedLevel);
                opponents.Add(o);
            }
            return opponents;
        }

        public void WriteAll(string path, IList<Opponent> opponents)
        {
            // Resolve instruction slots in the destination and validate every edit before writing.
            var current = ReadAll(path);
            var patches = new PatchSet();
            foreach (var entry in OpponentNames)
            {
                var o = opponents.SingleOrDefault(x => x.Name == entry.Key);
                if (o == null) continue;
                var a = ResolveAddresses(path,entry.Value);
                var existing = current.Single(x => x.Name == entry.Key);
                bool expanded=entry.Key=="joey" && o.Reds?.Count==8 && o.Blues?.Count==8;
                if (!expanded && (o.Reds == null || o.Blues == null || o.Reds.Count != a.MonsterCount || o.Blues.Count != a.MonsterCount))
                    throw new InvalidDataException($"{entry.Key}: army size must remain {a.MonsterCount}.");
                patches.Add(a.MapData, new[] { o.MonsterCap });
                patches.Add(a.MapData + 9, BitConverter.GetBytes(o.MaxAP));
                patches.Add(a.MapData + 17, BitConverter.GetBytes(o.MaxMP));
                patches.Add(a.SymbolData + 1, BitConverter.GetBytes(o.SymbolDF));
                patches.Add(a.SymbolData + 3, BitConverter.GetBytes(o.SymbolPP));
                patches.Add(a.SymbolData + 5, BitConverter.GetBytes(o.SymbolBaseAP));
                patches.Add(a.SymbolData + 7, new[] { o.SymbolAttribute });
                patches.Add(a.SymbolData + 9, new[] { o.SymbolEffect });
                using (var reader = new BinaryReader(File.OpenRead(path)))
                {
                    var redLevels = LevelOperands(reader, a.MonstersRedLv);
                    var blueLevels = LevelOperands(reader, a.MonstersBlueLv);
                    if (entry.Key == "Kaiba 1" && !redLevels.ContainsKey(0x18) && redLevels.TryGetValue(0x15, out long spare))
                    {
                        patches.Add(spare - 1, new byte[] { 0x18 });
                        redLevels[0x18] = spare;
                        blueLevels[0x18] = spare;
                    }
                    for (int i = 0; i < a.MonsterCount; i++)
                    {
                        var red = o.Reds[i]; var blue = o.Blues[i];
                        if (red.IDRed >= 277 || blue.IDBlue >= 277 || red.LevelRed < 1 || red.LevelRed > 10 || blue.LevelBlue < 1 || blue.LevelBlue > 10)
                            throw new InvalidDataException($"{entry.Key}: IDs must be 0-276 and levels 1-10.");
                        byte rs = existing.Reds[i].UnitSlot, bs = existing.Blues[i].UnitSlot;
                        patches.Add(a.MonstersRed + 99 * i, BitConverter.GetBytes(red.IDRed));
                        patches.Add(a.MonstersBlue + 99 * i, BitConverter.GetBytes(blue.IDBlue));
                        void AddLevel(Dictionary<byte,long> levels, byte slot, byte value)
                        {
                            if (!levels.TryGetValue(slot, out long offset))
                            {
                                if (value != 1) throw new InvalidDataException("Cannot edit a unit without an explicit level operand.");
                                return;
                            }
                            reader.BaseStream.Position = offset;
                            byte prior = reader.ReadByte();
                            patches.Add(offset, new[] { (byte)((prior & 0xF0) | value) });
                        }
                        AddLevel(redLevels, rs, red.LevelRed);
                        AddLevel(blueLevels, bs, blue.LevelBlue);
                    }
                }
            }
            patches.Apply(path);
        }
    }
}
