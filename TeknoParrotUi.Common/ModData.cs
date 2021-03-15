using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeknoParrotUi.Common
{
    [Serializable]
    //This is information that is uploaded to the GitHub repo
    public class ModData
    {
        public string GameXML { get; set; }
        public string ModName { get; set; }
        public string Creator { get; set; }
        public string GUID { get; set; }
        public string Description { get; set; }
    }
}
