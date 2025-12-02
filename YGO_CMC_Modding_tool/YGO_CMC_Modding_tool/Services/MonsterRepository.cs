using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace YGO_CMC_Modding_tool.Services
{
    public class MonsterRepository
    {
        public const long FirstMonsterOffset = 0x44D080;
        public const int MonsterCount = 277;
        public const int UsedMonstersAddress = 0x4D829E;
        public const int MaxUsedMonsters = 200;

        public List<ViewModels.MonstersViewModel.Monster> ReadAll(string path)
        {
            var monsters = new List<ViewModels.MonstersViewModel.Monster>(MonsterCount);
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(fs))
            {
                // 1. Read the list of used monster IDs first.
                fs.Seek(UsedMonstersAddress, SeekOrigin.Begin);
                var usedMonsterIds = new HashSet<ushort>();
                for (int i = 0; i < MaxUsedMonsters; i++)
                {
                    usedMonsterIds.Add(br.ReadUInt16());
                }

                // 2. Read all monster data.
                fs.Seek(FirstMonsterOffset, SeekOrigin.Begin);
                for (int i = 0; i < MonsterCount; i++)
                {
                    var m = new ViewModels.MonstersViewModel.Monster();
                    m.Type = br.ReadByte();
                    m.Attribute = br.ReadByte();
                    m.MovementPattern = br.ReadByte();
                    m.AttackPattern = br.ReadByte();
                    m.PP = br.ReadUInt16();
                    m.AT = br.ReadUInt16();
                    m.DF = br.ReadUInt16();
                    m.AP = br.ReadUInt16();
                    m.pAP = br.ReadUInt16();
                    m.MP = br.ReadUInt16();
                    m._0 = br.ReadUInt16();
                    m.AESymbol = br.ReadByte();
                    m.AELand = br.ReadByte();
                    m.AEMap = br.ReadByte();
                    m.Exp = br.ReadByte();
                    m._2 = br.ReadUInt32();
                    m._3 = br.ReadUInt16();

                    // 3. Set the IsUsed flag based on the list read earlier.
                    m.IsUsed = usedMonsterIds.Contains((ushort)i);

                    monsters.Add(m);
                }
            }

            // 4. Read comments from the associated text file, if it exists.
            string commentsPath = Path.ChangeExtension(path, ".txt");
            if (File.Exists(commentsPath))
            {
                var comments = File.ReadAllLines(commentsPath);
                for (int i = 0; i < monsters.Count && i < comments.Length; i++)
                {
                    monsters[i].Comment = comments[i];
                }
            }

            return monsters;
        }

        // Write all monsters back to the ISO at the same offsets
        public void WriteAll(string path, IList<ViewModels.MonstersViewModel.Monster> monsters)
        {
            if (monsters == null) throw new ArgumentNullException(nameof(monsters));
            if (monsters.Count < MonsterCount) throw new ArgumentException("Insufficient monster data.", nameof(monsters));

            // 1. Get the list of IDs for monsters marked as "used".
            var usedMonsterIds = monsters
                .Select((monster, id) => new { monster.IsUsed, ID = (ushort)id })
                .Where(m => m.IsUsed)
                .Select(m => m.ID)
                .ToList();

            // 2. Validate the count.
            if (usedMonsterIds.Count > MaxUsedMonsters)
            {
                MessageBox.Show($"The number of used monsters ({usedMonsterIds.Count}) exceeds the maximum of {MaxUsedMonsters}. Please uncheck some monsters before patching.", "Too Many Monsters", MessageBoxButton.OK);
                return; // Abort the write operation.
            }

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs))
            {
                // 3. Write the main monster data.
                fs.Seek(FirstMonsterOffset, SeekOrigin.Begin);
                for (int i = 0; i < MonsterCount; i++)
                {
                    var m = monsters[i];
                    bw.Write(m.Type);
                    bw.Write(m.Attribute);
                    bw.Write(m.MovementPattern);
                    bw.Write(m.AttackPattern);
                    bw.Write(m.PP);
                    bw.Write(m.AT);
                    bw.Write(m.DF);
                    bw.Write(m.AP);
                    bw.Write(m.pAP);
                    bw.Write(m.MP);
                    bw.Write(m._0);
                    bw.Write(m.AESymbol);
                    bw.Write(m.AELand);
                    bw.Write(m.AEMap);
                    bw.Write(m.Exp);
                    bw.Write(m._2);
                    bw.Write(m._3);
                }

                // 4. Write the list of used monster IDs.
                fs.Seek(UsedMonstersAddress, SeekOrigin.Begin);
                for (int i = 0; i < usedMonsterIds.Count; i++)
                {
                    bw.Write(usedMonsterIds[i]);
                }
            }


            // 5. Write comments to the associated text file.
            try
            {
                string commentsPath = Path.ChangeExtension(path, ".txt");
                var comments = monsters.Select(m => m.Comment ?? string.Empty).ToList();
                File.WriteAllLines(commentsPath, comments);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save comments file: {ex.Message}", "Comments Error", MessageBoxButton.OK);
            }
        }
    }
}
