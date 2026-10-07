using System;

namespace TeknoParrotUi.Common.Online
{
    public static class OnlineText
    {
        public static Func<string, string> Translate { get; set; } = key => key;
        public static string Get(string key) => Translate(key);
    }
}
