using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace YGO_CMC_Modding_tool.ViewModels
{
    public class OpponentsListViewModel
    {
        public class OpponentDisplayItem : INotifyPropertyChanged
        {
            public string BaseName { get; }
            private bool _isDirty;
            public bool IsDirty
            {
                get => _isDirty;
                set
                {
                    if (_isDirty != value)
                    {
                        _isDirty = value;
                        OnPropertyChanged(nameof(IsDirty));
                        OnPropertyChanged(nameof(Display));
                    }
                }
            }

            public string Display => string.Format("{0:D3} {1}{2}", BaseName, IsDirty ? " *" : "");
            public OpponentDisplayItem(string name)
            {
                BaseName = name;
            }
            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        public static ObservableCollection<string> OpponentNames { get; } = new ObservableCollection<string>()
        {
            "joey", "Tristan", "Duke", "Tea", "Grandpa",
            "Weevil", "Mako", "Rex", "Mai", "Mokuba",
            "Bakura", "Keith", "Pegasus 1", "Odion", "Marik",
            "Shadi 1", "Kaiba 1", "Ishizu 1", "Yami Marik 1", "Yami Bakura",
            "Pegasus 2", "Ishizu 2", "Yami Marik 2", "Shadi 2", "kaiba 2"
        };

        public static ObservableCollection<OpponentDisplayItem> MonsterDisplayNames { get; } = new ObservableCollection<OpponentDisplayItem>();

        static OpponentsListViewModel()
        {
            for (int i = 0; i < OpponentNames.Count; i++)
            {
                MonsterDisplayNames.Add(new OpponentDisplayItem(OpponentNames[i]));
            }
        }

        public static void SetDirtyFlag(int index, bool dirty)
        {
            if (index < 0 || index >= MonsterDisplayNames.Count) return;
            MonsterDisplayNames[index].IsDirty = dirty;
        }

        public static IEnumerable<string> AttributeKeys => MonstersListViewModel.AttributeKeys;
    }
}
