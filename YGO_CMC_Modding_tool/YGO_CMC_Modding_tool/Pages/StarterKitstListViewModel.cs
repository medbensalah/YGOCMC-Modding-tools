using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace YGO_CMC_Modding_tool.ViewModels
{
    public class CardIdItem
    {
        public ushort Value { get; set; }
    }

    public class StarterKitstListViewModel
    {
        public class StarterKitDisplayItem
        {
            public string BaseName { get; }
            public ushort PP { get; set; }
            public ushort DF { get; set; }
            public ushort AP { get; set; }
            public ObservableCollection<CardIdItem> CardIDs { get; set; } = new ObservableCollection<CardIdItem>();

            public StarterKitDisplayItem(string name) { BaseName = name; }

            public StarterKitDisplayItem Clone()
            {
                var clone = (StarterKitDisplayItem)this.MemberwiseClone();
                clone.CardIDs = new ObservableCollection<CardIdItem>(this.CardIDs.Select(c => new CardIdItem { Value = c.Value }));
                return clone;
            }

            public bool IsEqual(StarterKitDisplayItem other)
            {
                if (other == null) return false;
                return BaseName == other.BaseName
                    && PP == other.PP
                    && DF == other.DF
                    && AP == other.AP
                    && CardIDs.Select(c => c.Value).SequenceEqual(other.CardIDs.Select(c => c.Value));
            }
        }

        public class DisplayItem : INotifyPropertyChanged
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

            public string Display => $"{BaseName}{(_isDirty ? " *" : "")}";

            public DisplayItem(string name)
            {
                BaseName = name;
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        public static ObservableCollection<DisplayItem> KitDisplayNames { get; private set; }

        public static void Initialize(List<StarterKitDisplayItem> kits)
        {
            if (KitDisplayNames == null)
            {
                KitDisplayNames = new ObservableCollection<DisplayItem>(kits.Select(k => new DisplayItem(k.BaseName)));
            }
        }

        public static void SetDirtyFlag(int index, bool isDirty)
        {
            if (KitDisplayNames != null && index >= 0 && index < KitDisplayNames.Count)
            {
                KitDisplayNames[index].IsDirty = isDirty;
            }
        }
    }
}
