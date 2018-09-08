using System;
using System.Globalization;
using System.Windows.Data;

namespace TeknoParrotUi.Converters
{
    public class StringToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value as string;
            switch (str)
            {
                case null:
                    return false;
                case "1":
                case "true":
                    return true;
                default:
                    return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var b = value as bool? ?? false;
            return b ? "1" : "0";
        }
    }
}
