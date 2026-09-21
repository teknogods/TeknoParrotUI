using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.UserControls
{
    public partial class OutputSettingsControl : UserControl
    {
        private readonly GameProfile _profile;
        private readonly Action _back;

        public OutputSettingsControl(GameProfile profile, Action back)
        {
            InitializeComponent();
            _profile = profile;
            if (profile.CabinetOutputSettings == null) profile.CabinetOutputSettings = new CabinetOutputSettings();
            _back = back;
            GameTitle.Text = profile.GameNameInternal ?? profile.ProfileName;
            var settings = profile.CabinetOutputSettings ?? new CabinetOutputSettings();
            var port = settings.ListenerPort >= 1 && settings.ListenerPort <= 65535 ? settings.ListenerPort : 8000;
            ListenerPort.Text = port.ToString(CultureInfo.InvariantCulture);
            Discovery.IsChecked = settings.Discovery;
            var discoveryPort = settings.DiscoveryPort >= 1 && settings.DiscoveryPort <= 65535 ? settings.DiscoveryPort : 8001;
            DiscoveryPort.Text = discoveryPort.ToString(CultureInfo.InvariantCulture);
            LineEnding.ItemsSource = new[] { "CR", "LF", "CRLF" };
            LineEnding.SelectedItem = settings.LineEnding == "LF" || settings.LineEnding == "CRLF" ? settings.LineEnding : "CR";
        }

        private void SaveSettings(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(ListenerPort.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) ||
                port < 1 || port > 65535)
            {
                SaveStatus.Text = "Enter a port number between 1 and 65535.";
                return;
            }
            if (!int.TryParse(DiscoveryPort.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var discoveryPort) ||
                discoveryPort < 1 || discoveryPort > 65535)
            {
                SaveStatus.Text = "Enter a discovery port between 1 and 65535.";
                return;
            }
            var ending = (string)LineEnding.SelectedItem;
            if (ending != "CR" && ending != "LF" && ending != "CRLF")
            {
                SaveStatus.Text = "Choose a line ending.";
                return;
            }
            var settings = _profile.CabinetOutputSettings;
            var previous = settings.ListenerPort;
            var previousEnding = settings.LineEnding;
            var previousDiscovery = settings.Discovery;
            var previousDiscoveryPort = settings.DiscoveryPort;
            try
            {
                settings.ListenerPort = port;
                settings.LineEnding = ending;
                settings.Discovery = Discovery.IsChecked == true;
                settings.DiscoveryPort = discoveryPort;
                JoystickHelper.SerializeGameProfile(_profile);
            }
            catch (Exception ex)
            {
                settings.ListenerPort = previous;
                settings.LineEnding = previousEnding;
                settings.Discovery = previousDiscovery;
                settings.DiscoveryPort = previousDiscoveryPort;
                SaveStatus.Text = "Could not save settings: " + ex.Message;
                return;
            }
            _back();
        }

        private void GoBack(object sender, RoutedEventArgs e) => _back();
    }
}
