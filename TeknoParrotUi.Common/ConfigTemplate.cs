using System.Collections.Generic;
using System.ComponentModel;
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
        Password = 8,
    }
    public class FieldInformation : INotifyPropertyChanged
    {
        private string _fieldValue;
        private bool _isVisible = true;

        public string CategoryName { get; set; }
        public string FieldName { get; set; }

        public string FieldValue
        {
            get => _fieldValue;
            set
            {
                if (_fieldValue == value) return;
                _fieldValue = value;
                OnPropertyChanged(nameof(FieldValue));
            }
        }

        public FieldType FieldType { get; set; }
        public int FieldMin { get; set; }
        public int FieldMax { get; set; }
        public int FieldStep { get; set; } = 0;
        public List<string> FieldOptions { get; set; }
        public string Hint { get; set; }
        public bool UseUnitySorting { get; set; } = false;

        /// <summary>
        /// Optional: the FieldName of another FieldInformation entry (usually a Bool field)
        /// that controls whether this field is shown. Leave empty/null for "always visible".
        /// e.g. VisibleWhenField = "Remote Local Play"
        /// </summary>
        public string VisibleWhenField { get; set; }

        /// <summary>
        /// Optional: the FieldValue that VisibleWhenField must equal for this field to be shown.
        /// For Bool fields, TeknoParrot stores "1" for checked and "0" for unchecked.
        /// e.g. VisibleWhenValue = "1"
        /// </summary>
        public string VisibleWhenValue { get; set; }

        /// <summary>
        /// Computed at runtime by GameSettingsControl based on VisibleWhenField/VisibleWhenValue.
        /// Not persisted to the profile XML.
        /// </summary>
        [XmlIgnore]
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible == value) return;
                _isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
