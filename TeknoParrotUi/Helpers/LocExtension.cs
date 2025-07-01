using System;
using System.Globalization;
using System.Windows.Markup;

namespace TeknoParrotUi.Helpers
{
    public class LocExtension : MarkupExtension
    {
        public string Key { get; set; }

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return TeknoParrotUi.Properties.Resources.ResourceManager.GetString(
                Key,
                TeknoParrotUi.Properties.Resources.Culture ?? CultureInfo.CurrentUICulture
            ) ?? $"!{Key}!";
        }
    }
}