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
        private bool _isEditorVisible = true;
        private bool _isEditorEnabled = true;
        private bool _showRodPreferredSetup;
        private bool _rodPreferredSetup;
        private string _rodPreferredSetupSaved;

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

        public string VisibleWhenField { get; set; }
        public string VisibleWhenValue { get; set; }

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

        [XmlIgnore]
        public bool ShowRodPreferredSetup
        {
            get => _showRodPreferredSetup;
            set
            {
                if (_showRodPreferredSetup == value) return;
                _showRodPreferredSetup = value;
                OnPropertyChanged(nameof(ShowRodPreferredSetup));
            }
        }

        [XmlIgnore]
        public bool RodPreferredSetup
        {
            get => _rodPreferredSetup;
            set
            {
                if (_rodPreferredSetup == value) return;
                _rodPreferredSetup = value;
                OnPropertyChanged(nameof(RodPreferredSetup));
            }
        }

        // Persisted in UserProfiles XML so the user's Rod's Preferred Setup
        // selection is preserved independently of the normal customization toggle.
        public string RodPreferredSetupSaved
        {
            get => _rodPreferredSetupSaved;
            set
            {
                if (_rodPreferredSetupSaved == value) return;
                _rodPreferredSetupSaved = value;
                OnPropertyChanged(nameof(RodPreferredSetupSaved));
            }
        }

        [XmlIgnore]
        public bool IsEditorVisible
        {
            get => _isEditorVisible;
            set
            {
                if (_isEditorVisible == value) return;
                _isEditorVisible = value;
                OnPropertyChanged(nameof(IsEditorVisible));
            }
        }

        [XmlIgnore]
        public bool IsEditorEnabled
        {
            get => _isEditorEnabled;
            set
            {
                if (_isEditorEnabled == value) return;
                _isEditorEnabled = value;
                OnPropertyChanged(nameof(IsEditorEnabled));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
