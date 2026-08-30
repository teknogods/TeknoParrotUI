using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using TeknoParrotUi.Views;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections.Generic;
using System.ComponentModel;

namespace TeknoParrotUi.UserControls
{
    /// <summary>
    /// Interaction logic for GameSettingsControl.xaml
    /// </summary>
    public partial class GameSettingsControl : UserControl
    {
        public GameSettingsControl()
        {
            InitializeComponent();
        }

        private GameProfile _gameProfile;
        private ListBoxItem _comboItem;
        private ContentControl _contentControl;
        private Library _library;
        private InputApi _inputApi = InputApi.DirectInput;

        public void LoadNewSettings(GameProfile gameProfile, ListBoxItem comboItem, ContentControl contentControl, Library library)
        {
            _gameProfile = gameProfile;
            _comboItem = comboItem;

            GamePathBox.Text = _gameProfile.GamePath;
            GamePathBox2.Text = _gameProfile.GamePath2;

            GameSettingsList.ItemsSource = gameProfile.ConfigValues;
            _contentControl = contentControl;
            _library = library;

            // Wire up conditional field visibility (e.g. P2/P3/P4 Trackball Sensitivity
            // fields only showing when "Remote Local Play" is enabled). Any field with
            // VisibleWhenField/VisibleWhenValue set in the profile XML is handled here.
            foreach (var field in gameProfile.ConfigValues)
            {
                field.PropertyChanged -= ConfigValue_PropertyChanged;
                field.PropertyChanged += ConfigValue_PropertyChanged;
            }
            UpdateConditionalVisibility();

            // Ensure MergedInput is available in the Input API dropdown
            var inputApiField = gameProfile.ConfigValues.Find(cv => cv.FieldName == "Input API");
            if (inputApiField?.FieldOptions != null && !inputApiField.FieldOptions.Contains("MergedInput"))
            {
                inputApiField.FieldOptions.Add("MergedInput");
            }

            string exeName = "";

            if (!string.IsNullOrEmpty(_gameProfile.ExecutableName))
                exeName = $" ({_gameProfile.ExecutableName})".Replace(";", Properties.Resources.GameSettingsExecutableOr);

            GameExecutableText.Text = $"{Properties.Resources.GameSettingsGameExecutableLabel}{exeName}:";

            if (_gameProfile.HasTwoExecutables)
            {
                exeName = "";

                if (!string.IsNullOrEmpty(_gameProfile.ExecutableName2))
                    exeName = $" ({_gameProfile.ExecutableName2})".Replace(";", Properties.Resources.GameSettingsExecutableOr);

                GameExecutable2Text.Text = $"{Properties.Resources.GameSettingsSecondGameExecutableLabel}{exeName}:";

                GameExecutable2Text.Visibility = Visibility.Visible;
                GamePathBox2.Visibility = Visibility.Visible;
            }
            else
            {
                GameExecutable2Text.Visibility = Visibility.Collapsed;
                GamePathBox2.Visibility = Visibility.Collapsed;
            }
        }

        private void ConfigValue_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Only react to the underlying value changing, not to IsVisible flips we
            // trigger ourselves (avoids re-entrant recursion).
            if (e.PropertyName == nameof(FieldInformation.FieldValue))
            {
                UpdateConditionalVisibility();
            }
        }

        private void UpdateConditionalVisibility()
        {
            if (_gameProfile?.ConfigValues == null) return;

            foreach (var field in _gameProfile.ConfigValues)
            {
                if (string.IsNullOrEmpty(field.VisibleWhenField))
                {
                    // No dependency declared: always visible.
                    field.IsVisible = true;
                    continue;
                }

                var controller = _gameProfile.ConfigValues.Find(f => f.FieldName == field.VisibleWhenField);
                if (controller == null)
                {
                    // Referenced field doesn't exist in this profile; fail open.
                    field.IsVisible = true;
                    continue;
                }

                // VisibleWhenValue may list more than one acceptable value, comma-separated
                // (e.g. "On,Host Only"), so a field can stay visible for multiple states of
                // its controller - not just a single exact match.
                var acceptedValues = (field.VisibleWhenValue ?? string.Empty)
                    .Split(',')
                    .Select(v => v.Trim());

                field.IsVisible = acceptedValues.Any(v =>
                    string.Equals(controller.FieldValue, v, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void SelectExecutableForTextBox(object sender, MouseButtonEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = false,
                CheckFileExists = true,
                Title = Properties.Resources.GameSettingsSelectGameExecutable
            };

            if (!string.IsNullOrEmpty(_gameProfile.ExecutableName))
            {
                string[] fileNames = _gameProfile.ExecutableName.Split('|');
                string allFilesFilter = string.Join(";", fileNames);

                openFileDialog.Filter = $"{Properties.Resources.GameSettingsGameExecutableFilter} ({allFilesFilter})|{allFilesFilter}|" +
                                        string.Join("|", fileNames.Select(name => $"{name}|{name}")) +
                                        $"|{Properties.Resources.GameSettingsAllFiles}|*.*";
            }

            if (openFileDialog.ShowDialog() == true)
            {
                ((TextBox)sender).Text = openFileDialog.FileName;
            }
        }

        private void SelectExecutable2ForTextBox(object sender, MouseButtonEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = false,
                CheckFileExists = true,
                Title = Properties.Resources.GameSettingsSelectGameExecutable
            };

            if (!string.IsNullOrEmpty(_gameProfile.ExecutableName2))
            {
                string[] fileNames = _gameProfile.ExecutableName2.Split('|');
                string allFilesFilter = string.Join(";", fileNames);

                openFileDialog.Filter = $"{Properties.Resources.GameSettingsGameExecutableFilter} ({allFilesFilter})|{allFilesFilter}|" +
                                        string.Join("|", fileNames.Select(name => $"{name}|{name}")) +
                                        $"|{Properties.Resources.GameSettingsAllFiles}|*.*";
            }

            if (openFileDialog.ShowDialog() == true)
            {
                ((TextBox)sender).Text = openFileDialog.FileName;
            }
        }

        private void BtnSaveSettings(object sender, RoutedEventArgs e)
        {
            string inputApiString = _gameProfile.ConfigValues.Find(cv => cv.FieldName == "Input API")?.FieldValue;

            if (inputApiString != null)
                _inputApi = (InputApi)Enum.Parse(typeof(InputApi), inputApiString);

            foreach (var t in _gameProfile.JoystickButtons)
            {
                if (_inputApi == InputApi.DirectInput)
                    t.BindName = t.BindNameDi;
                else if (_inputApi == InputApi.XInput)
                    t.BindName = t.BindNameXi;
                else if (_inputApi == InputApi.RawInput)
                    t.BindName = t.BindNameRi;
                else if (_inputApi == InputApi.MergedInput)
                {
                    var inputApiField = _gameProfile.ConfigValues.Find(cv => cv.FieldName == "Input API");
                    bool hasRi = inputApiField?.FieldOptions?.Contains("RawInput") == true;

                    var parts = new List<string>();
                    if (!string.IsNullOrEmpty(t.BindNameXi)) parts.Add($"XI: {t.BindNameXi}");
                    if (!string.IsNullOrEmpty(t.BindNameDi)) parts.Add($"DI: {t.BindNameDi}");
                    if (hasRi && !string.IsNullOrEmpty(t.BindNameRi)) parts.Add($"RI: {t.BindNameRi}");
                    t.BindName = string.Join(" | ", parts);
                }
            }

            JoystickHelper.SerializeGameProfile(_gameProfile);
            _gameProfile.GamePath = GamePathBox.Text;
            _gameProfile.GamePath2 = GamePathBox2.Text;
            JoystickHelper.SerializeGameProfile(_gameProfile);
            _comboItem.Tag = _gameProfile;
            Application.Current.Windows.OfType<MainWindow>().Single().ShowMessage(string.Format(Properties.Resources.SuccessfullySaved, System.IO.Path.GetFileName(_gameProfile.FileName)));
            _library.ListUpdate(_gameProfile.GameNameInternal);
            _contentControl.Content = _library;
        }
        private void BtnGoBack(object sender, RoutedEventArgs e)
        {
            // Reload library to discard changes
            _library.ListUpdate(_gameProfile.GameNameInternal);

            _contentControl.Content = _library;
        }
    }
}
