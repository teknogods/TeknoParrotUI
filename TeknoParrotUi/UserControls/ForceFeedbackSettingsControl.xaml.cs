using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using TeknoParrotUi.Common;
using TeknoParrotUi.Converters;

namespace TeknoParrotUi.UserControls
{
    public partial class ForceFeedbackSettingsControl : UserControl
    {
        public const string PageName = "ForceFeedback";
        private readonly GameProfile _profile;
        private readonly Action _back;
        private readonly List<FieldInformation> _fields;

        public static bool HasSettings(GameProfile profile) =>
            profile.ConfigValues?.Any(f => f.FieldName == "Force Feedback Device") == true &&
            profile.ConfigValues.Any(f => f.SettingsPage == PageName);

        public ForceFeedbackSettingsControl(GameProfile profile, Action back)
        {
            InitializeComponent();
            _profile = profile;
            _back = back;
            GameTitle.Text = profile.GameNameInternal ?? profile.ProfileName;
            // Edit copies: Back discards only unsaved force-feedback edits.
            _fields = profile.ConfigValues.Where(f => f.SettingsPage == PageName)
                .Select(f => new FieldInformation
                {
                    CategoryName = f.CategoryName, FieldName = f.FieldName,
                    FieldValue = f.FieldValue, FieldType = f.FieldType,
                    FieldMin = f.FieldMin, FieldMax = f.FieldMax, FieldStep = f.FieldStep,
                    FieldOptions = f.FieldOptions, Hint = f.Hint,
                    SettingsPage = f.SettingsPage, EnabledBy = f.EnabledBy
                }).ToList();
            // Keep profiles saved with the old label on the same effect mode.
            foreach (var field in _fields.Where(f => f.FieldName == "Force Feedback Spring Effect" &&
                                                     f.FieldValue == "Constant Spring"))
                field.FieldValue = "Spring using Constant Force";
            var inlineFields = new HashSet<string>(_fields.Where(f => !string.IsNullOrEmpty(f.EnabledBy))
                .Select(f => f.EnabledBy));
            var enableBoxes = new Dictionary<string, CheckBox>();
            foreach (var field in _fields.Where(f => !inlineFields.Contains(f.FieldName)))
            {
                var row = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
                var heading = new DockPanel { LastChildFill = true };
                CheckBox enableBox = null;
                if (!string.IsNullOrEmpty(field.EnabledBy))
                {
                    if (!enableBoxes.TryGetValue(field.EnabledBy, out enableBox))
                    {
                        var enabled = _fields.Single(f => f.FieldName == field.EnabledBy);
                        enableBox = new CheckBox { Content = "Enabled", Margin = new Thickness(12, 0, 0, 0) };
                        enableBox.SetBinding(ToggleButton.IsCheckedProperty, ValueBinding(enabled, true));
                        DockPanel.SetDock(enableBox, Dock.Right);
                        heading.Children.Add(enableBox);
                        enableBoxes.Add(field.EnabledBy, enableBox);
                    }
                }
                heading.Children.Add(new TextBlock { Text = field.FieldName, FontSize = 15,
                    FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
                row.Children.Add(heading);
                if (!string.IsNullOrEmpty(field.Hint))
                    row.Children.Add(new TextBlock { Text = field.Hint, Opacity = 0.7,
                        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 6) });

                FrameworkElement editor;
                if (field.FieldType == FieldType.Slider || field.FieldType == FieldType.Numeric)
                {
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition());
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });
                    var slider = new Slider { Minimum = field.FieldMin, Maximum = field.FieldMax,
                        TickFrequency = Math.Max(1, field.FieldStep), IsSnapToTickEnabled = true,
                        SmallChange = Math.Max(1, field.FieldStep), LargeChange = 10,
                        AutoToolTipPlacement = AutoToolTipPlacement.TopLeft,
                        Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
                    slider.SetBinding(RangeBase.ValueProperty, ValueBinding(field));
                    var value = new TextBlock { HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center };
                    value.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = slider,
                        StringFormat = field.FieldMax == 100 ? "{0:0}%" : "{0:0} ms" });
                    Grid.SetColumn(value, 1);
                    grid.Children.Add(slider);
                    grid.Children.Add(value);
                    editor = grid;
                }
                else if (field.FieldType == FieldType.Dropdown)
                {
                    var combo = new ComboBox { ItemsSource = field.FieldOptions, MinWidth = 180,
                        HorizontalAlignment = HorizontalAlignment.Left };
                    combo.SetBinding(Selector.SelectedItemProperty, ValueBinding(field));
                    editor = combo;
                }
                else if (field.FieldType == FieldType.Bool)
                {
                    var check = new CheckBox { Content = "Enabled" };
                    check.SetBinding(ToggleButton.IsCheckedProperty, ValueBinding(field, true));
                    editor = check;
                }
                else
                {
                    var text = new TextBox();
                    text.SetBinding(TextBox.TextProperty, ValueBinding(field));
                    editor = text;
                }
                if (enableBox != null)
                    editor.SetBinding(IsEnabledProperty, new Binding("IsChecked") { Source = enableBox });
                row.Children.Add(editor);
                Fields.Children.Add(row);
            }
        }

        private static Binding ValueBinding(FieldInformation field, bool boolean = false) =>
            new Binding(nameof(FieldInformation.FieldValue)) { Source = field,
                Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                ConverterCulture = CultureInfo.InvariantCulture,
                Converter = boolean ? new StringToBoolConverter() : null };

        private void SaveSettings(object sender, RoutedEventArgs e)
        {
            var originals = _profile.ConfigValues.Where(f => f.SettingsPage == PageName).ToList();
            var previous = originals.Select(f => f.FieldValue).ToList();
            try
            {
                foreach (var field in originals)
                    field.FieldValue = _fields.Single(f => f.FieldName == field.FieldName).FieldValue;
                JoystickHelper.SerializeGameProfile(_profile);
                SaveStatus.Text = "Force feedback settings saved.";
            }
            catch (Exception ex)
            {
                for (var i = 0; i < originals.Count; i++) originals[i].FieldValue = previous[i];
                SaveStatus.Text = "Could not save settings: " + ex.Message;
            }
        }

        private void GoBack(object sender, RoutedEventArgs e) => _back();
    }
}
