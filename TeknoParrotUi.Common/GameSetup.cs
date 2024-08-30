using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TeknoParrotUi.Common
{
    [Serializable]
    [XmlRoot("GameSetup")]
    public class GameSetup
    {
        public string GameExecutableLocation { get; set; }
        public string GameExecutableLocation2 { get; set; }
        public string GameTestExecutableLocation { get; set; }
        public bool DevOnly { get; set; }
    }

    public class GameSetupContainer
    {
        public GameSetup GameSetupData { get; set; }
        public string GameId { get; set; }
    }
}
