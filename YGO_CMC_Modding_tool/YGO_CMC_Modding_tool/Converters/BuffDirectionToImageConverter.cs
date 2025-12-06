using CommunityToolkit.Maui.Core.Primitives;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;
using YGO_CMC_Modding_tool.Controls;

namespace YGO_CMC_Modding_tool.Converters
{
    public class BuffDirectionToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is BuffCellControlViewModel.BuffDirection direction)
            {
                switch (direction)
                {
                    case BuffCellControlViewModel.BuffDirection.Up:
                        return new Uri($"/YGO_CMC_Modding_tool;component/Resources/Arrows/UpArrow.png", UriKind.Relative);
                    case BuffCellControlViewModel.BuffDirection.Down:
                        return new Uri($"/YGO_CMC_Modding_tool;component/Resources/Arrows/DownArrow.png", UriKind.Relative);
                    case BuffCellControlViewModel.BuffDirection.None:
                    default:
                        return new Uri($"/YGO_CMC_Modding_tool;component/Resources/Arrows/None.png", UriKind.Relative);
                }
            }
            return "pack://application:,,,/Assets/none.png";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}