using Avalonia.Controls;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace TeknoParrotUi.Converters
{
    public class WidthToSplitViewModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                if (width < 640)
                    return SplitViewDisplayMode.Overlay;
                else if (width < 1024)
                    return SplitViewDisplayMode.CompactOverlay;
                else
                    return SplitViewDisplayMode.Inline;
            }
            return SplitViewDisplayMode.Overlay;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class WidthToOpenPaneLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                if (width < 640)
                    return 200.0;
                else if (width < 1024)
                    return 250.0;
                else
                    return 300.0;
            }
            return 200.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}