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
        public override string ToString() => DisplayName ?? Value ?? "";
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
        public string VisibleWhen { get; set; }
        public string VisibleWhenValue { get; set; }
        public bool ShouldSerializeVisibleWhen() => !string.IsNullOrEmpty(VisibleWhen);
        public bool ShouldSerializeVisibleWhenValue() => !string.IsNullOrEmpty(VisibleWhenValue);
        public bool IsVisible(IEnumerable<FieldInformation> settings) => SettingVisibility.IsVisible(settings, VisibleWhen, VisibleWhenValue);
        public bool ShouldSerializeSettingsPage() => !string.IsNullOrEmpty(SettingsPage);
        public bool ShouldSerializeEnabledBy() => !string.IsNullOrEmpty(EnabledBy);
        public bool UseUnitySorting { get; set; } = false;
    }

    public static class SettingVisibility
    {
        public static bool IsVisible(IEnumerable<FieldInformation> settings, string field, string value) =>
            string.IsNullOrEmpty(field) || settings?.Any(setting => setting.FieldName == field && setting.FieldValue == value) == true;
    }
}
