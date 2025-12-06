using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using static YGO_CMC_Modding_tool.ViewModels.AttributeBuffsViewModel;

namespace YGO_CMC_Modding_tool.Controls
{

    public class BuffCellControlViewModel : INotifyPropertyChanged
    {
        public enum BuffDirection
        {
            None,
            Up,
            Down
        }

        private BuffInfo _buffInfo;
        public BuffInfo BuffInfo
        {
            get => _buffInfo;
            set
            {
                _buffInfo = value;
                OnPropertyChanged(string.Empty); // Update all properties
            }
        }

        public BuffDirection Atk
        {
            get
            {
                if (_buffInfo == null) return BuffDirection.None;
                if (_buffInfo.Stats.HasFlag(StatBuffs.AtUp)) return BuffDirection.Up;
                if (_buffInfo.Stats.HasFlag(StatBuffs.AtDown)) return BuffDirection.Down;
                return BuffDirection.None;
            }
            set
            {
                if (_buffInfo == null) return;
                _buffInfo.Stats &= ~(StatBuffs.AtUp | StatBuffs.AtDown); // Clear current state
                if (value == BuffDirection.Up) _buffInfo.Stats |= StatBuffs.AtUp;
                else if (value == BuffDirection.Down) _buffInfo.Stats |= StatBuffs.AtDown;
                OnPropertyChanged();
            }
        }

        public BuffDirection Def
        {
            get
            {
                if (_buffInfo == null) return BuffDirection.None;
                if (_buffInfo.Stats.HasFlag(StatBuffs.DfUp)) return BuffDirection.Up;
                if (_buffInfo.Stats.HasFlag(StatBuffs.DfDown)) return BuffDirection.Down;
                return BuffDirection.None;
            }
            set
            {
                if (_buffInfo == null) return;
                _buffInfo.Stats &= ~(StatBuffs.DfUp | StatBuffs.DfDown);
                if (value == BuffDirection.Up) _buffInfo.Stats |= StatBuffs.DfUp;
                else if (value == BuffDirection.Down) _buffInfo.Stats |= StatBuffs.DfDown;
                OnPropertyChanged();
            }
        }

        public BuffDirection Pp
        {
            get
            {
                if (_buffInfo == null) return BuffDirection.None;
                if (_buffInfo.Stats.HasFlag(StatBuffs.PpUp)) return BuffDirection.Up;
                if (_buffInfo.Stats.HasFlag(StatBuffs.PpDown)) return BuffDirection.Down;
                return BuffDirection.None;
            }
            set
            {
                if (_buffInfo == null) return;
                _buffInfo.Stats &= ~(StatBuffs.PpUp | StatBuffs.PpDown);
                if (value == BuffDirection.Up) _buffInfo.Stats |= StatBuffs.PpUp;
                else if (value == BuffDirection.Down) _buffInfo.Stats |= StatBuffs.PpDown;
                OnPropertyChanged();
            }
        }

        public BuffDirection Spa
        {
            get
            {
                if (_buffInfo == null) return BuffDirection.None;
                if (_buffInfo.Special.HasFlag(SpaBuffs.SpaUp)) return BuffDirection.Up;
                if (_buffInfo.Special.HasFlag(SpaBuffs.SpaDown)) return BuffDirection.Down;
                return BuffDirection.None;
            }
            set
            {
                if (_buffInfo == null) return;
                _buffInfo.Special &= ~(SpaBuffs.SpaUp | SpaBuffs.SpaDown);
                if (value == BuffDirection.Up) _buffInfo.Special |= SpaBuffs.SpaUp;
                else if (value == BuffDirection.Down) _buffInfo.Special |= SpaBuffs.SpaDown;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}