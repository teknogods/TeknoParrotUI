using System;
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
        private ParrotData _parrotData;

        public SettingsControl()
        {
            InitializeComponent();
        }

        public void LoadStuff(ParrotData parrotData)
        {
            _parrotData = parrotData;

            ChkUseSto0ZCheckBox.IsChecked = _parrotData.UseSto0ZDrivingHack;
            sTo0zZonePercent.Value = _parrotData.StoozPercent;
            ChkSaveLastPlayed.IsChecked = _parrotData.SaveLastPlayed;
            ChkUseDiscordRPC.IsChecked = _parrotData.UseDiscordRPC;
            ChkCheckForUpdates.IsChecked = _parrotData.CheckForUpdates;
            ChkSilentMode.IsChecked = _parrotData.SilentMode;
            ChkFullAxisGas.IsChecked = _parrotData.FullAxisGas;
            ChkFullAxisBrake.IsChecked = _parrotData.FullAxisBrake;
            ChkReverseAxisGas.IsChecked = _parrotData.ReverseAxisGas;
            ChkReverseAxisBrake.IsChecked = _parrotData.ReverseAxisBrake;
            GunSensitivityPlayer1.Value = _parrotData.GunSensitivityPlayer1;
            GunSensitivityPlayer2.Value = _parrotData.GunSensitivityPlayer2;
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
                if (_parrotData == null)
                {
                    _parrotData = new ParrotData();
                    Lazydata.ParrotData = _parrotData;
                }
                _parrotData.UseSto0ZDrivingHack = ChkUseSto0ZCheckBox.IsChecked != null &&
                                                  ChkUseSto0ZCheckBox.IsChecked.Value;
                _parrotData.StoozPercent = (int)sTo0zZonePercent.Value;


                if (ChkFullAxisGas.IsChecked.HasValue)
                    _parrotData.FullAxisGas = ChkFullAxisGas.IsChecked.Value;
                if (ChkReverseAxisGas.IsChecked.HasValue)
                    _parrotData.ReverseAxisGas = ChkReverseAxisGas.IsChecked.Value;
                if (ChkFullAxisBrake.IsChecked.HasValue)
                    _parrotData.FullAxisBrake = ChkFullAxisBrake.IsChecked.Value;
                if (ChkReverseAxisBrake.IsChecked.HasValue)
                    _parrotData.ReverseAxisBrake = ChkReverseAxisBrake.IsChecked.Value;

                if (GunSensitivityPlayer1.Value != null)
                {
                    _parrotData.GunSensitivityPlayer1 = (int) GunSensitivityPlayer1.Value;
                }

                if (GunSensitivityPlayer2.Value != null)
                {
                    _parrotData.GunSensitivityPlayer2 = (int) GunSensitivityPlayer2.Value;
                }

                _parrotData.SaveLastPlayed = ChkSaveLastPlayed.IsChecked.Value;
                _parrotData.UseDiscordRPC = ChkUseDiscordRPC.IsChecked.Value;
                _parrotData.CheckForUpdates = ChkCheckForUpdates.IsChecked.Value;
                _parrotData.SilentMode = ChkSilentMode.IsChecked.Value;

                JoystickHelper.Serialize(_parrotData);
                DiscordRPC.Shutdown();
                string[] psargs = Environment.GetCommandLineArgs();
                System.Diagnostics.Process.Start(Application.ResourceAssembly.Location, psargs[0]);
                Application.Current.Shutdown();
               
            }
            catch (Exception exception)
            {
                MessageBox.Show($"Exception happened during ParrotData.xml saving!{Environment.NewLine}{Environment.NewLine}{exception}", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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
