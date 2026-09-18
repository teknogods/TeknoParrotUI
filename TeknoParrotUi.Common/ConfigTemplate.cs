using System.Collections.Generic;
using System.Linq;
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
        internal FieldInformation Clone()
        {
            var copy = (FieldInformation)MemberwiseClone();
            copy.FieldOptions = FieldOptions == null ? null : new List<string>(FieldOptions);
            copy.DynamicOptions = DynamicOptions?.Select(option => option == null ? null : new DynamicDropdownOption
            {
                DisplayName = option.DisplayName,
                Value = option.Value
            }).ToList();
            return copy;
        }

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
        // Optional XML-driven settings pages. Unmarked fields stay in Game Settings.
        public string SettingsPage { get; set; }
        public string EnabledBy { get; set; }
        public bool ShouldSerializeSettingsPage() => !string.IsNullOrEmpty(SettingsPage);
        public bool ShouldSerializeEnabledBy() => !string.IsNullOrEmpty(EnabledBy);
        public bool UseUnitySorting { get; set; } = false;
    }
}
