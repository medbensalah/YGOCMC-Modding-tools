using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace YGO_CMC_Modding_tool.ViewModels
{
    public class MonstersViewModel
    {
        public struct Monster
        {
            public byte Type;
            public byte Attribute;
            public byte MovementPattern;
            public byte AttackPattern;
            public UInt16 PP;
            public UInt16 AT;
            public UInt16 DF;
            public UInt16 AP;
            public UInt16 pAP;
            public UInt16 MP;
            public UInt16 _0;
            public byte AESymbol;
            public byte AELand;
            public byte AEMap;
            public byte _exp;
            public UInt32 _2;
            public UInt16 _3;

            public Monster(byte type, byte attribute, byte movementPattern, byte attackPattern, ushort pP, ushort aT, ushort dF, ushort aP, ushort pAP, ushort mP, byte aESymbol, byte aELand, byte aEMap) : this()
            {
                Type = type;
                Attribute = attribute;
                MovementPattern = movementPattern;
                AttackPattern = attackPattern;
                PP = pP;
                AT = aT;
                DF = dF;
                AP = aP;
                this.pAP = pAP;
                MP = mP;
                AESymbol = aESymbol;
                AELand = aELand;
                AEMap = aEMap;
            }
        }

        public static List<Monster> LastLoaded { get; private set; }
        private static List<Monster> _original; // first loaded immutable snapshot
        public static IReadOnlyList<Monster> OriginalMonsters => _original;

        public ObservableCollection<Monster> Monsters { get; } = new ObservableCollection<Monster>();

        // Change notification event for UI grids
        public static event Action MonstersChanged;
        internal static void NotifyChanged() => MonstersChanged?.Invoke();

        public void Load(string isoPath, Services.MonsterRepository repository)
        {
            if (string.IsNullOrWhiteSpace(isoPath)) return;
            var list = repository.ReadAll(isoPath);
            if (_original == null)
            {
                // take a snapshot of original data
                _original = new List<Monster>(list);
            }
            LastLoaded = new List<Monster>(list); // work on a mutable copy
            Monsters.Clear();
            foreach (var m in LastLoaded)
            {
                Monsters.Add(m);
            }
            NotifyChanged();
        }
    }
}
