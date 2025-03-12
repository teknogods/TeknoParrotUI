using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia;  // For Avalonia base namespace
using TeknoParrotUi.Common;

namespace TeknoParrotUi.ViewModels
{
    public class GameSettingsVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return false;  // For IsVisible binding

            var src = parameter as string;
            var type = (FieldType)value;

            // Return a boolean for IsVisible
            return (type == FieldType.Text && src == "TextField") ||
                   (type == FieldType.Bool && src == "BoolField") ||
                   (type == FieldType.Dropdown && src == "DropdownField") ||
                   (type == FieldType.DropdownIndex && src == "DropdownField") ||
                   (type == FieldType.Slider && src == "SliderField");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}