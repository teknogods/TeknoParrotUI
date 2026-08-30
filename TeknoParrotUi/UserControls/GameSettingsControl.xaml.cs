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
using System.IO;

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
        private GameProfile _stockGoldenTeeProfile;
        private FieldInformation _rodPreferredSetupAnchor;
        private bool _applyingRodPreferredSetup;

        public void LoadNewSettings(GameProfile gameProfile, ListBoxItem comboItem, ContentControl contentControl, Library library)
        {
            _gameProfile = gameProfile;
            _comboItem = comboItem;

            GamePathBox.Text = _gameProfile.GamePath;
            GamePathBox2.Text = _gameProfile.GamePath2;

            GameSettingsList.ItemsSource = gameProfile.ConfigValues;
            ConfigureRodPreferredSetup();

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
            if (!(sender is FieldInformation field))
                return;

            // Golden Tee has three valid setup states:
            //   Default = Rod OFF, Override Default Outfit OFF
            //   Rod     = Rod ON,  Override Default Outfit OFF
            //   Custom  = Rod OFF, Override Default Outfit ON
            //
            // Rod and Custom are mutually exclusive, but neither one is required.
            if (!_applyingRodPreferredSetup &&
                e.PropertyName == nameof(FieldInformation.RodPreferredSetup) &&
                field.ShowRodPreferredSetup)
            {
                if (field.RodPreferredSetup)
                {
                    if (HasCustomGoldenTeeSettings())
                    {
                        var result = MessageBox.Show(
                            "You currently have custom Golden Tee settings.\n\n" +
                            "Using Rod's Preferred Setup will reset those custom settings " +
                            "back to their default values.\n\n" +
                            "Do you want to continue?",
                            "Use Rod's Preferred Setup?",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result != MessageBoxResult.Yes)
                        {
                            SetRodToggleState(false, "0");
                            return;
                        }
                    }

                    ApplyRodPreferredSetup();
                }
                else
                {
                    // Turning Rod off does NOT force Custom on. This leaves the normal/default
                    // state when Override Default Outfit is also off.
                    SetRodToggleState(false, "0");
                    UpdateConditionalVisibility();
                }

                return;
            }

            // Selecting Custom turns Rod off. Turning Custom off does NOT turn Rod on;
            // that intentionally leaves both boxes unchecked for the normal/default setup.
            if (!_applyingRodPreferredSetup &&
                e.PropertyName == nameof(FieldInformation.FieldValue) &&
                ReferenceEquals(field, _rodPreferredSetupAnchor))
            {
                if (string.Equals(field.FieldValue, "1", StringComparison.OrdinalIgnoreCase))
                    SetRodToggleState(false, "0");

                UpdateConditionalVisibility();
                return;
            }

            if (e.PropertyName == nameof(FieldInformation.FieldValue))
                UpdateConditionalVisibility();
        }

        private void ConfigureRodPreferredSetup()
        {
            _stockGoldenTeeProfile = null;
            _rodPreferredSetupAnchor = null;

            if (_gameProfile?.ConfigValues == null || !IsGoldenTeeProfile(_gameProfile))
                return;

            // The memorial checkbox is hosted on the existing Override Default Outfit item so
            // no fake OpenParrot setting is added to ConfigValues.
            _rodPreferredSetupAnchor = _gameProfile.ConfigValues.FirstOrDefault(field =>
                string.Equals(field.CategoryName, "Customization", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(field.FieldName, "Override Default Outfit", StringComparison.OrdinalIgnoreCase));

            if (_rodPreferredSetupAnchor == null)
                return;

            _stockGoldenTeeProfile = LoadStockGoldenTeeProfile(_gameProfile);
            if (_stockGoldenTeeProfile == null)
                return;

            foreach (var field in _gameProfile.ConfigValues)
            {
                field.ShowRodPreferredSetup = false;
                field.IsEditorVisible = true;
                field.IsEditorEnabled = true;
            }

            _rodPreferredSetupAnchor.ShowRodPreferredSetup = true;

            // First-ever load for this user profile defaults to the normal Golden Tee setup:
            // neither Rod's Preferred Setup nor Override Default Outfit is forced on.
            if (string.IsNullOrWhiteSpace(_rodPreferredSetupAnchor.RodPreferredSetupSaved))
            {
                _rodPreferredSetupAnchor.RodPreferredSetupSaved = "0";
                _rodPreferredSetupAnchor.RodPreferredSetup = false;
                UpdateConditionalVisibility();
                return;
            }

            // Honor a previously saved Rod choice. If Rod is selected, ApplyRodPreferredSetup()
            // also guarantees that Override Default Outfit is off, so Rod and Custom can never
            // both be active.
            _rodPreferredSetupAnchor.RodPreferredSetup =
                string.Equals(_rodPreferredSetupAnchor.RodPreferredSetupSaved, "1", StringComparison.Ordinal);

            if (_rodPreferredSetupAnchor.RodPreferredSetup)
                ApplyRodPreferredSetup();
            else
                UpdateConditionalVisibility();
        }

        private static bool IsGoldenTeeProfile(GameProfile profile)
        {
            if (profile == null)
                return false;

            var fileName = Path.GetFileName(profile.FileName ?? string.Empty);

            return fileName.StartsWith("GoldenTeeLive20", StringComparison.OrdinalIgnoreCase) &&
                   fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
        }

        private static GameProfile LoadStockGoldenTeeProfile(GameProfile currentProfile)
        {
            try
            {
                var profileFileName = Path.GetFileName(currentProfile.FileName);
                if (string.IsNullOrWhiteSpace(profileFileName))
                    return null;

                var stockPath = Path.Combine("GameProfiles", profileFileName);
                if (!File.Exists(stockPath))
                    return null;

                return JoystickHelper.DeSerializeGameProfile(stockPath, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not load stock Golden Tee profile for Rod preferred setup: {ex.Message}");
                return null;
            }
        }

        private void ApplyRodPreferredSetup()
        {
            if (_gameProfile?.ConfigValues == null || _rodPreferredSetupAnchor == null)
                return;

            try
            {
                _applyingRodPreferredSetup = true;

                // Restore the stock customization values for this exact Golden Tee profile when
                // available. Trackball 25/25 is NOT dependent on this stock-profile lookup.
                if (_stockGoldenTeeProfile?.ConfigValues != null)
                {
                    foreach (var stockField in _stockGoldenTeeProfile.ConfigValues.Where(field =>
                                 string.Equals(field.CategoryName, "Customization", StringComparison.OrdinalIgnoreCase)))
                    {
                        var currentField = _gameProfile.ConfigValues.FirstOrDefault(field =>
                            string.Equals(field.CategoryName, stockField.CategoryName, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(field.FieldName, stockField.FieldName, StringComparison.OrdinalIgnoreCase));

                        if (currentField != null)
                            currentField.FieldValue = stockField.FieldValue;
                    }
                }

                // Rod mode and normal outfit customization are mutually exclusive.
                _rodPreferredSetupAnchor.FieldValue = "0";

                // Rod was adamant about Player 1 being 25/25. Force these every time Rod mode
                // is selected, regardless of whatever values the user had before.
                SetFieldValue("Trackball Sensitivity X", "25");
                SetFieldValue("Trackball Sensitivity Y", "25");

                // Persist the user's Rod-mode choice in the UserProfile XML.
                _rodPreferredSetupAnchor.RodPreferredSetupSaved = "1";
                _rodPreferredSetupAnchor.RodPreferredSetup = true;
            }
            finally
            {
                _applyingRodPreferredSetup = false;
            }

            // Apply the lock immediately. Do not rely on another PropertyChanged event firing.
            UpdateConditionalVisibility();
        }

        private void SetRodToggleState(bool enabled, string savedValue)
        {
            if (_rodPreferredSetupAnchor == null)
                return;

            try
            {
                _applyingRodPreferredSetup = true;
                _rodPreferredSetupAnchor.RodPreferredSetupSaved = savedValue;
                _rodPreferredSetupAnchor.RodPreferredSetup = enabled;
            }
            finally
            {
                _applyingRodPreferredSetup = false;
            }
        }

        private void SetFieldValue(string fieldName, string value)
        {
            var field = _gameProfile?.ConfigValues?.FirstOrDefault(item =>
                string.Equals(item.FieldName?.Trim(), fieldName, StringComparison.OrdinalIgnoreCase));

            if (field != null)
                field.FieldValue = value;
        }

        private bool HasCustomGoldenTeeSettings()
        {
            if (_gameProfile?.ConfigValues == null ||
                _stockGoldenTeeProfile?.ConfigValues == null)
                return false;

            foreach (var stockField in _stockGoldenTeeProfile.ConfigValues.Where(field =>
                         string.Equals(field.CategoryName, "Customization", StringComparison.OrdinalIgnoreCase)))
            {
                var currentField = _gameProfile.ConfigValues.FirstOrDefault(field =>
                    string.Equals(field.CategoryName, stockField.CategoryName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(field.FieldName, stockField.FieldName, StringComparison.OrdinalIgnoreCase));

                if (currentField == null)
                    continue;

                if (!string.Equals(
                        currentField.FieldValue,
                        stockField.FieldValue,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRodTrackballSensitivityField(FieldInformation field)
        {
            return field != null &&
                   (string.Equals(field.FieldName, "Trackball Sensitivity X", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(field.FieldName, "Trackball Sensitivity Y", StringComparison.OrdinalIgnoreCase));
        }

        private void UpdateConditionalVisibility()
        {
            if (_gameProfile?.ConfigValues == null) return;

            var useRodPreferredSetup = _rodPreferredSetupAnchor?.RodPreferredSetup == true;

            foreach (var field in _gameProfile.ConfigValues)
            {
                field.IsEditorVisible = true;
                field.IsEditorEnabled = true;

                if (string.IsNullOrEmpty(field.VisibleWhenField))
                {
                    // No dependency declared: always visible.
                    field.IsVisible = true;
                }
                else
                {
                    var controller = _gameProfile.ConfigValues.Find(f => f.FieldName == field.VisibleWhenField);
                    if (controller == null)
                    {
                        // Referenced field doesn't exist in this profile; fail open.
                        field.IsVisible = true;
                    }
                    else
                    {
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

                // Rod's preferred setup also locks Player 1's X/Y sensitivity at 25.
                // Leave the sliders visible so the chosen value is obvious, but disable them
                // until the user opts out of Rod's setup.
                if (useRodPreferredSetup && IsRodTrackballSensitivityField(field))
                {
                    field.FieldValue = "25";
                    field.IsEditorEnabled = false;
                }

                if (!useRodPreferredSetup ||
                    !string.Equals(field.CategoryName, "Customization", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (ReferenceEquals(field, _rodPreferredSetupAnchor))
                {
                    // Keep the anchor item alive so the Rod checkbox remains on screen, but hide
                    // its normal "Override Default Outfit" editor until the user opts to customize.
                    field.IsVisible = true;
                    field.IsEditorVisible = false;
                }
                else
                {
                    // Rod used the stock Golden Tee setup, so there is nothing else to configure
                    // while this mode is selected.
                    field.IsVisible = false;
                }
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
            // Rod mode is an invariant: even if some binding or external code changed these
            // values, saving while Rod is selected must persist 25/25 and stock customization.
            if (_rodPreferredSetupAnchor?.RodPreferredSetup == true)
                ApplyRodPreferredSetup();

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
