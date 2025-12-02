using System;
using System.Collections.Generic;
using System.IO;

namespace YGO_CMC_Modding_tool.Services
{
    public class MonsterRepository
    {
        public const long FirstMonsterOffset = 0x44D080;
        public const int MonsterCount = 277;

        public List<ViewModels.MonstersViewModel.Monster> ReadAll(string path)
        {
            var monsters = new List<ViewModels.MonstersViewModel.Monster>(MonsterCount);
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(fs))
            {
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
                    m._exp = br.ReadByte();
                    m._2 = br.ReadUInt32();
                    m._3 = br.ReadUInt16();
                    monsters.Add(m);
                }
            }
            return monsters;
        }

        // Write all monsters back to the ISO at the same offsets
        public void WriteAll(string path, IList<ViewModels.MonstersViewModel.Monster> monsters)
        {
            if (monsters == null) throw new ArgumentNullException(nameof(monsters));
            if (monsters.Count < MonsterCount) throw new ArgumentException("Insufficient monster data.", nameof(monsters));

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs))
            {
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
                    bw.Write(m._exp);
                    bw.Write(m._2);
                    bw.Write(m._3);
                }
            }
        }
    }
}
