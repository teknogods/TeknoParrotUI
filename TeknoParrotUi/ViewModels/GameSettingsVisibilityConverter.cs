using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.ViewModels
{
    public class GameSettingsVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;
            var src = parameter as string;
            var type = (FieldType) value;
            if (type == FieldType.Text)
            {
                if (src == "TextField")
                    return Visibility.Visible;
            }
            else if (type == FieldType.Bool)
            {
                if (src == "BoolField")
                    return Visibility.Visible;
            }
            //if (value is bool)
            //{
            //    return ((bool) value) || DesignerProperties.GetIsInDesignMode(Application.Current.MainWindow)
            //        ? Visibility.Visible
            //        : Visibility.Collapsed;
            //}
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}