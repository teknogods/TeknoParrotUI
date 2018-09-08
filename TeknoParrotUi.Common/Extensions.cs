using System;
using System.ComponentModel;

namespace TeknoParrotUi.Common
{

    /// <summary>
    /// Used for enums to get the description..
    /// </summary>
    public static class AttributesHelperExtension
    {
        public static string ToDescription(this Enum value)
        {
            var da = (DescriptionAttribute[])(value.GetType().GetField(value.ToString())).GetCustomAttributes(typeof(DescriptionAttribute), false);
            return da.Length > 0 ? da[0].Description : value.ToString();
        }
    }
}
