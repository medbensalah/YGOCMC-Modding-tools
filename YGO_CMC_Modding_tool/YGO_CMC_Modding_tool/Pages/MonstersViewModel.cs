using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using YGO_CMC_Modding_tool.Pages;

namespace YGO_CMC_Modding_tool.ViewModels
{
    public class MonstersViewModel
    {
        public class Monster
        {
            public byte Type { get; set; }
            public byte Attribute { get; set; }
            public byte MovementPattern { get; set; }
            public byte AttackPattern { get; set; }
            public UInt16 PP { get; set; }
            public UInt16 AT { get; set; }
            public UInt16 DF { get; set; }
            public UInt16 AP { get; set; }
            public UInt16 pAP { get; set; }
            public UInt16 MP { get; set; }
            public UInt16 _0 { get; set; }
            public byte AESymbol { get; set; }
            public byte AELand { get; set; }
            public byte AEMap { get; set; }
            public byte Exp { get; set; }
            public UInt32 _2 { get; set; }
            public UInt16 _3 { get; set; }

            // This field will not be saved to the ISO
            public bool IsUsed { get; set; } = false;
            public string Comment { get; set; } = string.Empty;

            public Monster() { }
            public Monster(Monster m) {
                Type = m.Type;
                Attribute = m.Attribute;
                MovementPattern = m.MovementPattern;
                AttackPattern = m.AttackPattern;
                Exp = m.Exp;
                PP = m.PP;
                AT = m.AT;
                DF = m.DF;
                AP = m.AP;
                MP = m.MP;
                pAP = m.pAP;
                _0 = m._0;
                AESymbol = m.AESymbol;
                AELand = m.AELand;
                AEMap = m.AEMap;
                _2 = m._2;
                _3 = m._3;
                IsUsed = m.IsUsed;
                Comment = m.Comment;
            }

            public Monster Clone()
            {
                return (Monster)this.MemberwiseClone();
            }
        }

        public static List<Monster> LastLoaded { get; private set; }
        private static List<Monster> _original; // first loaded immutable snapshot
        public static IReadOnlyList<Monster> OriginalMonsters => _original;

        public void Load(string isoPath, Services.MonsterRepository repository)
        {
            if (string.IsNullOrWhiteSpace(isoPath)) return;
            var list = repository.ReadAll(isoPath);
            if (_original == null)
            {
                // take a snapshot of original data
                _original = new List<Monster>(list.Select(m => m.Clone()).ToList());
            }
            LastLoaded = new List<Monster>(list); // work on a mutable copy

            // Sync the display state with the newly loaded data.
            for (int i = 0; i < LastLoaded.Count; i++)
            {
                if (i < MonstersListViewModel.MonsterDisplayNames.Count)
                {
                    MonstersListViewModel.SetIsUsed(i, LastLoaded[i].IsUsed);
                }
            }
        }
    }
}
