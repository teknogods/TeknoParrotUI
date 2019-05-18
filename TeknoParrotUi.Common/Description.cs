using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TeknoParrotUi.Common
{
    [Serializable]
    [XmlRoot("Description")]
    public class Description
    {
        public string SmallText { get; set; }

        public static Description NO_DATA = new Description() { SmallText = "NO DATA" };
    }
}
