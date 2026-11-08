using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using CMCStudio.Models;

namespace CMCStudio.Models
{
    public class OpponentsViewModel
    {
        public class EnemyMonsterEntry : INotifyPropertyChanged
        {
            internal sealed class LevelValue
            {
                internal byte Value = 1;
                internal bool Editable = true;
                internal event Action Changed;
                internal void Set(byte value)
                {
                    if (!Editable && value != Value) throw new InvalidOperationException("This unit has no explicit level assignment in the recovered script.");
                    if (value < 1 || value > 10) throw new ArgumentOutOfRangeException(nameof(value), "Levels must be 1 through 10.");
                    Value = value;
                    Changed?.Invoke();
                }
            }
            private LevelValue _level;
            public EnemyMonsterEntry() { AttachLevel(new LevelValue()); }
            internal void AttachLevel(LevelValue level)
            {
                if (_level != null) _level.Changed -= NotifyLevel;
                _level = level;
                _level.Changed += NotifyLevel;
            }
            internal LevelValue SharedLevel => _level;
            private void NotifyLevel()
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LevelRed)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LevelBlue)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LevelTextRed)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LevelTextBlue)));
            }
            public event PropertyChangedEventHandler PropertyChanged;
            public byte UnitSlot { get; set; }
            public byte SpawnX { get; set; }
            public byte SpawnY { get; set; }
            public bool LevelAssigned { get => _level.Editable; internal set => _level.Editable = value; }
            public string LevelTextRed { get => LevelAssigned ? LevelRed.ToString() : "1 (default)"; set => LevelRed = byte.Parse(value); }
            public string LevelTextBlue { get => LevelAssigned ? LevelBlue.ToString() : "1 (default)"; set => LevelBlue = byte.Parse(value); }
            public ushort IDRed { get; set; }
            public ushort IDBlue { get; set; }
            public byte LevelRed { get => _level.Value; set => _level.Set(value); }
            public byte LevelBlue { get => _level.Value; set => _level.Set(value); }
            public EnemyMonsterEntry Clone()
            {
                var clone = new EnemyMonsterEntry { IDRed = IDRed, IDBlue = IDBlue, UnitSlot = UnitSlot, SpawnX=SpawnX, SpawnY=SpawnY };
                clone.LevelRed = LevelRed;
                clone.LevelAssigned = LevelAssigned;
                return clone;
            }
            public bool IsEqual(EnemyMonsterEntry other) => other != null && IDRed == other.IDRed && IDBlue == other.IDBlue && LevelRed == other.LevelRed && LevelBlue == other.LevelBlue;
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
            public bool LevelsShared { get; set; }
            public string LevelStorageNote => LevelsShared ? "Red and blue levels share script operands; edits update both." : "Red and blue levels are stored separately.";

            public List<EnemyMonsterEntry> Reds { get; set; } = new List<EnemyMonsterEntry>();
            public List<EnemyMonsterEntry> Blues { get; set; } = new List<EnemyMonsterEntry>();

            public Opponent Clone()
            {
                var clone = (Opponent)this.MemberwiseClone();
                // Perform a deep copy of the monster lists
                clone.Reds = this.Reds.Select(r => r.Clone()).ToList();
                clone.Blues = this.Blues.Select(b => b.Clone()).ToList();
                if (LevelsShared)
                    foreach (var blue in clone.Blues)
                        blue.AttachLevel(clone.Reds.First(r => r.UnitSlot == blue.UnitSlot).SharedLevel);
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

        public void Load(string isoPath, CMCStudio.Data.OpponentRepository repository)
        {
            if (string.IsNullOrWhiteSpace(isoPath)) return;
            var list = repository.ReadAll(isoPath);
            _original = list.Select(m => m.Clone()).ToList();
            // Work on a deep copy so that edits don't affect the original snapshot
            LastLoaded = new List<Opponent>(list.Select(m => m.Clone()).ToList());
        }
    }
}
