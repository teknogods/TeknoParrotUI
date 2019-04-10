using System;

namespace TeknoParrotUi
{
    public static class GameVersion
    {
        static readonly System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
        static Version _version = assembly.GetName().Version;
        public static string CurrentVersion = _version.ToString();
    }
}
