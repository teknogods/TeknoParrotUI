using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TeknoParrotUi
{
    public static class EmuBlacklist
    {
        public static List<string> BlacklistedList = new List<string>
        {
            "typex_*",
            "detoured.dll",
            "jvsemuhq.dll",
            "ttx_*",
            "monitor_*"
        };

        public static bool CheckForBlacklist(IEnumerable<string> fileNames)
        {
            var enumerable = fileNames as IList<string> ?? fileNames.ToList();
            return enumerable.Any(CheckForBlacklist);
        }

        public static bool CheckForBlacklist(string fileName)
        {
            if (BlacklistedList.Any(x => x.Replace("*", "").ToLower().Contains(Path.GetFileName(fileName).ToLower())) || Path.GetFileName(fileName) == "jconfig.exe")
            {
                return true;
            }

            return false;
        }
    }
}
