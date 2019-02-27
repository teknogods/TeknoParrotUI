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
        private bool _xinputMode;

        public SettingsControl()
        {
            InitializeComponent();
        }

        public void LoadStuff(ParrotData parrotData)
        {
            _parrotData = parrotData;

            BtnRefreshHaptic(null, null);
            if(_parrotData.SineBase != 0)
                TxtSine.Text = _parrotData.SineBase.ToString();
            if (_parrotData.FrictionBase != 0)
                TxtFriction.Text = _parrotData.FrictionBase.ToString();
            if (_parrotData.ConstantBase != 0)
                TxtConstant.Text = _parrotData.ConstantBase.ToString();
            if (_parrotData.SpringBase != 0)
                TxtSpring.Text = _parrotData.SpringBase.ToString();

            ChkUseFfb.IsChecked = _parrotData.UseHaptic;
            ChkThrustmasterFix.IsChecked = _parrotData.HapticThrustmasterFix;
            ChkUseSto0ZCheckBox.IsChecked = _parrotData.UseSto0ZDrivingHack;
            sTo0zZonePercent.Value = _parrotData.StoozPercent;
            ChkUseMouse.IsChecked = _parrotData.UseMouse;
            ChkSaveLastPlayed.IsChecked = _parrotData.SaveLastPlayed;
            ChkUseDiscordRPC.IsChecked = _parrotData.UseDiscordRPC;
            ChkSilentMode.IsChecked = _parrotData.SilentMode;     
            CmbJoystickInterface.SelectedIndex = _parrotData.XInputMode ? 1 : 0;
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
                if (ChkThrustmasterFix.IsChecked.HasValue)
                    _parrotData.HapticThrustmasterFix = ChkThrustmasterFix.IsChecked.Value;

                _parrotData.ConstantBase = Convert.ToInt16(TxtConstant.Text);
                _parrotData.SineBase = Convert.ToInt16(TxtSine.Text);
                _parrotData.FrictionBase = Convert.ToInt16(TxtFriction.Text);
                _parrotData.SpringBase = Convert.ToInt16(TxtSpring.Text);

                if (ChkUseFfb.IsChecked.HasValue)
                    _parrotData.UseHaptic = ChkUseFfb.IsChecked.Value;
                _parrotData.HapticDevice = (string)((ComboBoxItem)HapticComboBox.SelectedItem).Tag;
                _parrotData.UseSto0ZDrivingHack = ChkUseSto0ZCheckBox.IsChecked != null &&
                                                  ChkUseSto0ZCheckBox.IsChecked.Value;
                _parrotData.StoozPercent = (int)sTo0zZonePercent.Value;
                _parrotData.UseMouse = ChkUseMouse.IsChecked != null && ChkUseMouse.IsChecked.Value;
                _parrotData.XInputMode = _xinputMode;

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
                _parrotData.SilentMode = ChkSilentMode.IsChecked.Value;

                JoystickHelper.Serialize(_parrotData);
                MessageBox.Show("Generation of ParrotData.xml was succesful, please restart me!", "Save Complete", MessageBoxButton.OK,
                    MessageBoxImage.Information);

                MainWindow.SafeExit();
            }
            catch (Exception exception)
            {
                MessageBox.Show($"Exception happened during ParrotData.xml saving!{Environment.NewLine}{Environment.NewLine}{exception}", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ComboBox)e.Source).SelectedIndex == 0)
            {
                _xinputMode = false;
                _parrotData.XInputMode = false;
            }
            if (((ComboBox)e.Source).SelectedIndex == 1)
            {
                _xinputMode = true;
                _parrotData.XInputMode = true;
            }
        }

        private void BtnRefreshHaptic(object sender, RoutedEventArgs e)
        {
            HapticComboBox.Items.Clear();
            if (!string.IsNullOrWhiteSpace(_parrotData.HapticDevice))
                HapticComboBox.Items.Add(CreateJoystickItem(_parrotData.HapticDevice, "Saved Haptic Device"));

            HapticComboBox.Items.Add(CreateJoystickItem("", "No Haptic Device"));
            var joysticks = ForceFeedbackJesus.BasicInformation.GetHapticDevices();
            foreach (var joystickProfile in joysticks)
            {
                HapticComboBox.Items.Add(CreateJoystickItem(joystickProfile));
            }
            HapticComboBox.SelectedIndex = 0;
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
            Process.Start("https://www.reddit.com/r/teknoparrot/comments/84ahe1/teknoparrot_force_feedback_profiles/");
        }
    }
}
