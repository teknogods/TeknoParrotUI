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
                throw new InvalidOperationException("The value could not be converted to an integer");
            }

            if (!int.TryParse(parameter.ToString(), out secondOperand))
            {
                throw new InvalidOperationException("The parameter could not be converted to an integer");
            }

            return firstOperand < secondOperand;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
