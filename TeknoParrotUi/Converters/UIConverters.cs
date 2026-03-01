using System;
using System.Globalization;
using System.Windows.Data;

namespace TeknoParrotUi.Converters
{
    public class IsLessThanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
            {
                return false;
            }

            int firstOperand;
            int secondOperand;

            if (!int.TryParse(value.ToString(), out firstOperand))
            {
                return false;
            }

            if (!int.TryParse(parameter.ToString(), out secondOperand))
            {
                return false;
            }

            return firstOperand < secondOperand;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
