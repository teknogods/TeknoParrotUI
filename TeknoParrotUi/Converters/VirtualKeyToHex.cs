using System;
using System.Globalization;
using System.Windows.Data;

namespace TeknoParrotUi.Converters
{
    public class VirtualKeyToHexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hexString && hexString.StartsWith("0x"))
            {
                // Convert from hex string to int
                if (int.TryParse(hexString.Substring(2), NumberStyles.HexNumber, null, out int vKey))
                {
                    return vKey;
                }
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int vKey)
            {
                return $"0x{vKey:X}";
            }
            return "0x0";
        }
    }
}