using System.Collections.Generic;
using System.Xml.Serialization;

namespace TeknoParrotUi.Common
{
    public enum FieldType
    {
        Text = 0,
        Numeric = 1,
        Bool = 2,
        Dropdown = 3,
        Slider = 4,
        DropdownIndex = 5,
        KeyCapture = 6,
        MonitorSelection = 7,
        DynamicDropdown = 8,
    }
    public class DynamicDropdownOption
    {
        public string DisplayName { get; set; }
        public string Value { get; set; }
    }
    public class FieldInformation
    {
        public string CategoryName { get; set; }
        public string FieldName { get; set; }
        public string FieldValue { get; set; }
        public FieldType FieldType { get; set; }
        public int FieldMin { get; set; }
        public int FieldMax { get; set; }
        public int FieldStep { get; set; } = 0;
        public List<string> FieldOptions { get; set; }
        [XmlIgnore]
        public List<DynamicDropdownOption> DynamicOptions { get; set; }
        public string Hint { get; set; }
        public bool UseUnitySorting { get; set; } = false;
    }
}
