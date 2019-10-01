using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.UserControls
{
    /// <summary>
    /// Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        ContentControl _contentControl;
        Views.Library _library;

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
            GunSensitivityPlayer1.Value = Lazydata.ParrotData.GunSensitivityPlayer1;
            GunSensitivityPlayer2.Value = Lazydata.ParrotData.GunSensitivityPlayer2;

            UiColour.ItemsSource = Enum.GetValues(typeof(UiColour));
            UiColour.SelectedIndex = (int) Lazydata.ParrotData.UiColour;

            _contentControl = control;
            _library = library;
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

                if (GunSensitivityPlayer1.Value != null)
                {
                    Lazydata.ParrotData.GunSensitivityPlayer1 = (int) GunSensitivityPlayer1.Value;
                }

                if (GunSensitivityPlayer2.Value != null)
                {
                    Lazydata.ParrotData.GunSensitivityPlayer2 = (int) GunSensitivityPlayer2.Value;
                }

                Lazydata.ParrotData.SaveLastPlayed = ChkSaveLastPlayed.IsChecked.Value;
                Lazydata.ParrotData.UseDiscordRPC = ChkUseDiscordRPC.IsChecked.Value;
                Lazydata.ParrotData.CheckForUpdates = ChkCheckForUpdates.IsChecked.Value;
                Lazydata.ParrotData.SilentMode = ChkSilentMode.IsChecked.Value;
                Lazydata.ParrotData.ConfirmExit = ChkConfirmExit.IsChecked.Value;
                Lazydata.ParrotData.DownloadIcons = ChkDownloadIcons.IsChecked.Value;
                Lazydata.ParrotData.UiDisableHardwareAcceleration = ChkUiDisableHardwareAcceleration.IsChecked.Value;
                Lazydata.ParrotData.UiColour = (UiColour) UiColour.SelectedIndex;

                DiscordRPC.StartOrShutdown();

                JoystickHelper.Serialize();

                MessageBox.Show("Successfully saved ParrotData.xml!");
            }
            catch (Exception exception)
            {
                MessageBox.Show($"Exception happened during ParrotData.xml saving!{Environment.NewLine}{Environment.NewLine}{exception}", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
            MessageBox.Show("Allowed range is 0-8191!");
            e.Handled = true;
        }

        private void Txt_OnMouseMove(object sender, MouseEventArgs e)
        {
            ((TextBox) sender).SelectionLength = 0;
        }

        private void BtnFfbProfiles(object sender, RoutedEventArgs e)
        {
            Process.Start("https://discord.gg/rTnNx2n");
        }
    }
}
