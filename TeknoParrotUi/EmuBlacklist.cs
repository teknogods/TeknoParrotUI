using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TeknoParrotUi
{
    public static class EmuBlacklist
    {
        public static List<string> Blacklist = new List<string>
        {
            "typex_*",
            "detoured.dll",
            "jconfig.exe",
            "jvsemuhq.dll",
            "ttx_*",
            "monitor_*"
        };

        public static bool CheckBlacklist(IEnumerable<string> fileNames)
        {
            var enumerable = fileNames as IList<string> ?? fileNames.ToList();
            return enumerable.Any(CheckBlacklist);
        }

        public static bool CheckBlacklist(string fileName)
        {
            var file = Path.GetFileName(fileName)?.ToLower();

            foreach (var t in Blacklist)
            {
                if (file != null && file.StartsWith(t.Replace("*", "").ToLower()))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
