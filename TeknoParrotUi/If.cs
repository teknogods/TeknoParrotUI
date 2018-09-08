using System;
using System.Windows.Markup;

namespace TeknoParrotUi
{
    public class IfDebug : MarkupExtension
    {
        public object Debug { get; set; }
        public object Release { get; set; }

        public override object ProvideValue(IServiceProvider sp)
        {
#if DEBUG
            return this.Debug;
#else
            return this.Release;
#endif
        }
    }
}