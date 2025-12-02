using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using YGO_CMC_Modding_tool.ViewModels;

namespace YGO_CMC_Modding_tool.Pages
{
    // Convert type byte -> type name string
    public class TypeByteToNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is byte b)
            {
                var kv = MonstersListViewModel._typeMap.FirstOrDefault(x => x.Value == b);
                return string.IsNullOrEmpty(kv.Key) ? b.ToString() : kv.Key;
            }
            return value?.ToString();
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Convert attribute byte -> attribute name string
    public class AttributeByteToNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is byte b)
            {
                var kv = MonstersListViewModel._attributeMap.FirstOrDefault(x => x.Value == b);
                return string.IsNullOrEmpty(kv.Key) ? b.ToString() : kv.Key;
            }
            return value?.ToString();
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Convert movement/attack pattern byte -> BitmapImage
    public class PatternByteToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is byte b)
            {
                var kv = MonstersListViewModel._patternMap.FirstOrDefault(x => x.Value == b);
                if (!string.IsNullOrEmpty(kv.Key))
                {
                    var type = (parameter as string) ?? "MV"; // MV or ATK
                    var uri = new Uri($"/YGO_CMC_Modding_tool;component/Resources/Patterns/{type}/Pattern_{kv.Key}.png", UriKind.Relative);
                    return new BitmapImage(uri);
                }
            }
            return null;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
