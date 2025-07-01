using MaterialDesignColors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;

namespace TeknoParrotUi.UserControls
{
    /// <summary>
    /// Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        ContentControl _contentControl;
        Views.Library _library;
        bool isInitialized = false;
        public SettingsControl(ContentControl control, Views.Library library)
        {
            InitializeComponent();

            // reload ParrotData from file
            JoystickHelper.DeSerialize();

            ChkUseSto0ZCheckBox.IsChecked = Lazydata.ParrotData.UseSto0ZDrivingHack;
            sTo0zZonePercent.Value = Lazydata.ParrotData.StoozPercent;
            ChkSaveLastPlayed.IsChecked = Lazydata.ParrotData.SaveLastPlayed;
            ChkUseDiscordRPC.IsChecked = Lazydata.ParrotData.UseDiscordRPC;
            ChkConfirmExit.IsChecked = Lazydata.ParrotData.ConfirmExit;
            ChkCheckForUpdates.IsChecked = Lazydata.ParrotData.CheckForUpdates;
            ChkDownloadIcons.IsChecked = Lazydata.ParrotData.DownloadIcons;
            ChkSilentMode.IsChecked = Lazydata.ParrotData.SilentMode;
            ChkUiDisableHardwareAcceleration.IsChecked = Lazydata.ParrotData.UiDisableHardwareAcceleration;
            ChkFullAxisGas.IsChecked = Lazydata.ParrotData.FullAxisGas;
            ChkFullAxisBrake.IsChecked = Lazydata.ParrotData.FullAxisBrake;
            ChkReverseAxisGas.IsChecked = Lazydata.ParrotData.ReverseAxisGas;
            ChkReverseAxisBrake.IsChecked = Lazydata.ParrotData.ReverseAxisBrake;
            textBoxScoreSubmissionID.Text = Lazydata.ParrotData.ScoreSubmissionID;
            ChkHideVanguardWarning.IsChecked = Lazydata.ParrotData.HideVanguardWarning;
            ChkUiElf2LogToFile.IsChecked = Lazydata.ParrotData.Elfldr2LogToFile;
            ChkHideDolphinGUI.IsChecked = Lazydata.ParrotData.HideDolphinGUI;
            textBoxDatXmlLocation.Text = Lazydata.ParrotData.DatXmlLocation;

            if (int.TryParse(Lazydata.ParrotData.ScoreCollapseGUIKey.Replace("0x", ""),
                System.Globalization.NumberStyles.HexNumber, null, out int collapseKey))
            {
                keyCaptureScoreCollapseKey.VirtualKey = collapseKey;
            }

            if (int.TryParse(Lazydata.ParrotData.ExitGameKey.Replace("0x", ""),
                 System.Globalization.NumberStyles.HexNumber, null, out int exitKey))
            {
                keyExitGameKey.VirtualKey = exitKey;
            }

            if (int.TryParse(Lazydata.ParrotData.PauseGameKey.Replace("0x", ""),
                System.Globalization.NumberStyles.HexNumber, null, out int pauseKey))
            {
                keyPauseGameKey.VirtualKey = pauseKey;
            }



            UiColour.ItemsSource = new SwatchesProvider().Swatches.Select(a => a.Name).ToList();
            UiColour.SelectedItem = Lazydata.ParrotData.UiColour;
            ChkUiDarkMode.IsChecked = Lazydata.ParrotData.UiDarkMode;
            ChkUiHolidayThemes.IsChecked = Lazydata.ParrotData.UiHolidayThemes;

            if (App.IsPatreon())
            {
                UiPatreon.Visibility = Visibility.Visible;
            }
            else
            {
                UiPatreon.Visibility = Visibility.Collapsed;
            }

            _contentControl = control;
            _library = library;
            LoadLanguageSetting();
            isInitialized = true;
        }

        private void LoadLanguageSetting()
        {
            string currentLanguage = Lazydata.ParrotData.Language ?? "en-US";
            
            foreach (ComboBoxItem item in LanguageSelector.Items)
            {
                if (item.Tag.ToString() == currentLanguage)
                {
                    LanguageSelector.SelectedItem = item;
                    break;
                }
            }
        }

        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageSelector.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedLanguage = selectedItem.Tag.ToString();
                
                if (Lazydata.ParrotData.Language != selectedLanguage)
                {
                    Lazydata.ParrotData.Language = selectedLanguage;
                    
                    // Show restart required message
                    MessageBoxHelper.InfoOK(Properties.Resources.SettingsLanguageRestartRequired);
                }
            }
        }

        private ComboBoxItem CreateJoystickItem(string joystickName, string extraString = "")
        {
            string content;
            if (string.IsNullOrWhiteSpace(joystickName) && extraString != "")
            {
                content = extraString;
            }
            else if (string.IsNullOrWhiteSpace(extraString) && joystickName != "")
            {
                content = joystickName;
            }
            else
            {
                content = joystickName + " - " + extraString;
            }
            return new ComboBoxItem
            {
                Tag = joystickName,
                Content = content
            };
        }

        private void BtnSaveSettings(object sender, RoutedEventArgs e)
        {
            try
            {
                Lazydata.ParrotData.UseSto0ZDrivingHack = ChkUseSto0ZCheckBox.IsChecked != null &&
                                                  ChkUseSto0ZCheckBox.IsChecked.Value;
                Lazydata.ParrotData.StoozPercent = (int)sTo0zZonePercent.Value;


                if (ChkFullAxisGas.IsChecked.HasValue)
                    Lazydata.ParrotData.FullAxisGas = ChkFullAxisGas.IsChecked.Value;
                if (ChkReverseAxisGas.IsChecked.HasValue)
                    Lazydata.ParrotData.ReverseAxisGas = ChkReverseAxisGas.IsChecked.Value;
                if (ChkFullAxisBrake.IsChecked.HasValue)
                    Lazydata.ParrotData.FullAxisBrake = ChkFullAxisBrake.IsChecked.Value;
                if (ChkReverseAxisBrake.IsChecked.HasValue)
                    Lazydata.ParrotData.ReverseAxisBrake = ChkReverseAxisBrake.IsChecked.Value;

                Lazydata.ParrotData.ExitGameKey = $"0x{keyExitGameKey.VirtualKey:X}";
                Lazydata.ParrotData.PauseGameKey = $"0x{keyPauseGameKey.VirtualKey:X}";
                Lazydata.ParrotData.ScoreSubmissionID = textBoxScoreSubmissionID.Text;
                //Lazydata.ParrotData.ScoreCollapseGUIKey = textBoxScoreCollapseKey.Text;
                Lazydata.ParrotData.ScoreCollapseGUIKey = $"0x{keyCaptureScoreCollapseKey.VirtualKey:X}";
                Lazydata.ParrotData.SaveLastPlayed = ChkSaveLastPlayed.IsChecked.Value;
                Lazydata.ParrotData.UseDiscordRPC = ChkUseDiscordRPC.IsChecked.Value;
                Lazydata.ParrotData.CheckForUpdates = ChkCheckForUpdates.IsChecked.Value;
                Lazydata.ParrotData.SilentMode = ChkSilentMode.IsChecked.Value;
                Lazydata.ParrotData.ConfirmExit = ChkConfirmExit.IsChecked.Value;
                Lazydata.ParrotData.DownloadIcons = ChkDownloadIcons.IsChecked.Value;
                Lazydata.ParrotData.UiDisableHardwareAcceleration = ChkUiDisableHardwareAcceleration.IsChecked.Value;

                Lazydata.ParrotData.UiColour = UiColour.SelectedItem.ToString();
                Lazydata.ParrotData.UiDarkMode = ChkUiDarkMode.IsChecked.Value;
                Lazydata.ParrotData.UiHolidayThemes = ChkUiHolidayThemes.IsChecked.Value;

                Lazydata.ParrotData.HideVanguardWarning = ChkHideVanguardWarning.IsChecked.Value;
                Lazydata.ParrotData.Elfldr2NetworkAdapterName = Elfldr2NetworkAdapterCombobox.SelectedAdapterName;
                Lazydata.ParrotData.Elfldr2LogToFile = ChkUiElf2LogToFile.IsChecked.Value;
                Lazydata.ParrotData.HideDolphinGUI = ChkHideDolphinGUI.IsChecked.Value;

                if (!string.IsNullOrEmpty(textBoxDatXmlLocation.Text))
                {
                    if (IsValidDatXmlFile(textBoxDatXmlLocation.Text))
                    {
                        Lazydata.ParrotData.DatXmlLocation = textBoxDatXmlLocation.Text;
                    }
                    else
                    {
                        MessageBox.Show(Properties.Resources.SettingsDATXMLPathInvalid, Properties.Resources.SettingsInvalidFile, MessageBoxButton.OK, MessageBoxImage.Warning);
                        // Return focus to the textbox
                        textBoxDatXmlLocation.Focus();
                        return;
                    }
                }
                else
                {
                    Lazydata.ParrotData.DatXmlLocation = "";
                }

                DiscordRPC.StartOrShutdown();

                JoystickHelper.Serialize();

                Application.Current.Windows.OfType<MainWindow>().Single().ShowMessage(string.Format(Properties.Resources.SuccessfullySaved, Properties.Resources.SettingsParrotDataFileName));
                _contentControl.Content = _library;
            }
            catch (Exception exception)
            {
                MessageBoxHelper.ErrorOK(string.Format(Properties.Resources.ErrorCantSaveParrotData, exception.ToString()));
            }
        }

        private void BtnGoBack(object sender, RoutedEventArgs e)
        {
            _contentControl.Content = _library;
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            var value = Int16.TryParse(((TextBox)sender).Text + e.Text, out short result);
            if (value && result >= 0 && result <= 8191)
            {
                e.Handled = false;
                return;
            }
            MessageBoxHelper.ErrorOK(Properties.Resources.ErrorAllowedRange);
            e.Handled = true;
        }

        private void Txt_OnMouseMove(object sender, MouseEventArgs e)
        {
            ((TextBox)sender).SelectionLength = 0;
        }

        private void BtnFfbProfiles(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/Boomslangnz/FFBArcadePlugin/releases");
        }

        // reload theme
        private void UiColour_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitialized)
            {
                App.LoadTheme(UiColour.SelectedItem.ToString(), ChkUiDarkMode.IsChecked.Value, ChkUiHolidayThemes.IsChecked.Value);
            }
        }

        private void ChkTheme_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitialized)
            {
                App.LoadTheme(UiColour.SelectedItem.ToString(), ChkUiDarkMode.IsChecked.Value, ChkUiHolidayThemes.IsChecked.Value);
            }
        }

        private void BtnVKCPage(object sender, RoutedEventArgs e)
        {
            Process.Start("https://docs.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes");
        }

        private void BtnMultiGameButtonConfig_Click(object sender, RoutedEventArgs e)
        {
            _contentControl.Content = new MultiGameButtonConfig(_contentControl, _library);
        }

        private void BtnBrowseDatXml_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = Properties.Resources.SettingsDATXMLFilter,
                Title = Properties.Resources.SettingsSelectDATXMLFile
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // Check if the file is valid before setting the text
                if (IsValidDatXmlFile(openFileDialog.FileName))
                {
                    textBoxDatXmlLocation.Text = openFileDialog.FileName;
                }
                else
                {
                    MessageBox.Show(Properties.Resources.SettingsInvalidDATXMLFile, Properties.Resources.SettingsInvalidFile, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private bool IsValidDatXmlFile(string filePath)
        {
            try
            {
                // Try to read the file as XML to validate it
                using (var reader = new System.Xml.XmlTextReader(filePath))
                {
                    while (reader.Read()) { }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
