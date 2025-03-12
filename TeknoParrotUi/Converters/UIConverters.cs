using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace TeknoParrotUi.Converters
{
    public class IsLessThanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue && parameter is double doubleParameter)
            {
                bool invert = false;

                // Check if we have a secondary parameter to invert the result
                if (parameter is object[] parameters && parameters.Length > 1 && parameters[1] is bool invertParam)
                {
                    invert = invertParam;
                    doubleParameter = System.Convert.ToDouble(parameters[0]);
                }

                bool result = doubleValue < doubleParameter;
                return invert ? !result : result;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
