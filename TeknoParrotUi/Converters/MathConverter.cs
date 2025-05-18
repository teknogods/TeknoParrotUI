using System;
using System.Globalization;
using System.Windows.Data;

namespace TeknoParrotUi.Converters
{
    public class MathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double val = System.Convert.ToDouble(value);
            double param = System.Convert.ToDouble(parameter);

            switch (ConverterParameterType)
            {
                case MathConverterParameterType.Add:
                    return val + param;
                case MathConverterParameterType.Subtract:
                    return val - param;
                case MathConverterParameterType.Multiply:
                    return val * param;
                case MathConverterParameterType.Divide:
                    return val / param;
                default:
                    return val;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public MathConverterParameterType ConverterParameterType { get; set; }
    }

    public enum MathConverterParameterType
    {
        Add,
        Subtract,
        Multiply,
        Divide
    }
}