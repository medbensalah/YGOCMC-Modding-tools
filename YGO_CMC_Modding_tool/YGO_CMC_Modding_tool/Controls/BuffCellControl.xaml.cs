using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using static YGO_CMC_Modding_tool.ViewModels.AttributeBuffsViewModel;

namespace YGO_CMC_Modding_tool.Controls
{
    /// <summary>
    /// Interaction logic for BuffCellControl.xaml
    /// </summary>
    public partial class BuffCellControl : UserControl
    {
        public static readonly DependencyProperty BuffInfoProperty =
            DependencyProperty.Register("BuffInfo", typeof(BuffInfo), typeof(BuffCellControl),
                new PropertyMetadata(null, OnBuffInfoChanged));

        public BuffInfo BuffInfo
        {
            get { return (BuffInfo)GetValue(BuffInfoProperty); }
            set {
                Debug.WriteLine(this.Name + " " + value);
                SetValue(BuffInfoProperty, value);
            }
        }

        private static void OnBuffInfoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BuffCellControl control && control.DataContext is BuffCellControlViewModel vm)
            {
                vm.BuffInfo = e.NewValue as BuffInfo;
            }
        }

        public BuffCellControl()
        {
            InitializeComponent();
            DataContext = new BuffCellControlViewModel();
            var options = Enum.GetValues(typeof(BuffCellControlViewModel.BuffDirection));
            AtkComboBox.ItemsSource = options;
            DefComboBox.ItemsSource = options;
            PpComboBox.ItemsSource = options;
            SpaComboBox.ItemsSource = options;
        }
    }
}