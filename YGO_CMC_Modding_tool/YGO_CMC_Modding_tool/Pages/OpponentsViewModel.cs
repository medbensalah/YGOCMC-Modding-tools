using System;
using System.Collections.Generic;
using System.Linq;
using YGO_CMC_Modding_tool.ViewModels;

namespace YGO_CMC_Modding_tool.ViewModels
{
    public class OpponentsViewModel
    {
        public class EnemyMonsterEntry
        {
            public ushort IDRed { get; set; }
            public ushort IDBlue { get; set; }
            public byte LevelRed { get; set; }
            public byte LevelBlue { get; set; }

            public EnemyMonsterEntry Clone()
            {
                return (EnemyMonsterEntry)this.MemberwiseClone();
            }

            public bool IsEqual(EnemyMonsterEntry other)
            {
                if (other == null) return false;
                return this.IDRed == other.IDRed
                    && this.IDBlue == other.IDBlue
                    && this.LevelRed == other.LevelRed
                    && this.LevelBlue == other.LevelBlue;
            }
        }
        public class Opponent
        {
            public string Name { get; set; }
            public byte MonsterCap { get; set; }
            public ushort MaxAP { get; set; }
            public ushort MaxMP { get; set; }
            public ushort SymbolDF { get; set; }
            public UInt16 SymbolPP { get; set; }
            public UInt16 SymbolBaseAP { get; set; }
            public byte SymbolAttribute { get; set; }
            public byte SymbolEffect { get; set; }

            public List<EnemyMonsterEntry> Reds { get; set; } = new List<EnemyMonsterEntry>();
            public List<EnemyMonsterEntry> Blues { get; set; } = new List<EnemyMonsterEntry>();

            public Opponent Clone()
            {
                var clone = (Opponent)this.MemberwiseClone();
                // Perform a deep copy of the monster lists
                clone.Reds = this.Reds.Select(r => r.Clone()).ToList();
                clone.Blues = this.Blues.Select(b => b.Clone()).ToList();
                return clone;
            }
        }

        public class MonsterSelection
        {
            public ushort Id { get; set; }
            public string Name { get; set; }
        }

        public static List<MonsterSelection> MonsterList { get; } = MonstersListViewModel.MonsterNames
            .Select((name, index) => new MonsterSelection { Id = (ushort)index, Name = String.Format("{0:D3} {1}", index, name) })
            .ToList();

        public static List<Opponent> LastLoaded { get; private set; }
        private static List<Opponent> _original; // first loaded immutable snapshot
        public static IReadOnlyList<Opponent> OriginalMonsters => _original;

        public void Load(string isoPath, Services.OpponentRepository repository)
        {
            if (string.IsNullOrWhiteSpace(isoPath)) return;
            var list = repository.ReadAll(isoPath);
            if (_original == null)
            {
                // take a snapshot of original data
                _original = new List<Opponent>(list.Select(m => m.Clone()).ToList());
            }
            // Work on a deep copy so that edits don't affect the original snapshot
            LastLoaded = new List<Opponent>(list.Select(m => m.Clone()).ToList());
        }
    }
}
