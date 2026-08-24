using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TeknoParrotUi.Helpers;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for RemotePlayManagement.xaml
    /// </summary>
    public partial class RemotePlayManagement : UserControl
    {
        private readonly DispatcherTimer _statusTimer;
        private bool _actionInProgress;
        private bool _refreshInProgress;
        private bool _updatingConnectionMode;
        private bool _moonlightActionInProgress;
        private bool _moonlightReady;

        public RemotePlayManagement()
        {
            InitializeComponent();

            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _statusTimer.Tick += StatusTimer_Tick;

            Loaded += RemotePlayManagement_Loaded;
            Unloaded += RemotePlayManagement_Unloaded;
        }

        private async void RemotePlayManagement_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshMoonlightInstallState();
            await RefreshAllAsync();
            _statusTimer.Start();
        }

        private void RemotePlayManagement_Unloaded(object sender, RoutedEventArgs e)
        {
            _statusTimer.Stop();
        }

        private async void StatusTimer_Tick(object sender, EventArgs e)
        {
            if (_actionInProgress || _refreshInProgress)
                return;

            await RefreshAllAsync(true);
        }

        private async Task RefreshAllAsync(bool refreshClients = true)
        {
            if (_refreshInProgress)
                return;

            _refreshInProgress = true;

            try
            {
                if (!SunshineManager.IsInstalled())
                {
                    ShowNotInstalledState();
                    return;
                }

                if (!SunshineManager.IsRunning())
                {
                    ShowStoppedState();
                    return;
                }

                try
                {
                    var status = await SunshineManager.GetStatusAsync();
                    ShowManagedStatus(status);

                    if (refreshClients)
                        await RefreshClientsAsync();
                }
                catch (Exception ex)
                {
                    ShowApiUnavailableState(ex.Message);
                }
            }
            finally
            {
                _refreshInProgress = false;
            }
        }

        private void ShowNotInstalledState()
        {
            SunshineStatusText.Text = "Sunshine not found";
            SunshineStatusDetail.Text = $"Expected: {SunshineManager.SunshineExecutablePath}";
            SunshineHeaderStatusText.Text = "Not Installed";
            SunshineStatusIndicator.Fill = Brushes.Gray;

            BtnStartSunshine.IsEnabled = false;
            BtnStopSunshine.IsEnabled = false;
            BtnRestartSunshine.IsEnabled = false;
            BtnOpenSunshineWebUi.IsEnabled = false;
            SetManagedControlsEnabled(false);
            ClearManagedData();
        }

        private void ShowStoppedState()
        {
            SunshineStatusText.Text = "Stopped";
            SunshineStatusDetail.Text = "Sunshine is ready to start.";
            SunshineHeaderStatusText.Text = "Stopped";
            SunshineStatusIndicator.Fill = Brushes.Gray;

            BtnStartSunshine.IsEnabled = !_actionInProgress;
            BtnStopSunshine.IsEnabled = false;
            BtnRestartSunshine.IsEnabled = false;
            BtnOpenSunshineWebUi.IsEnabled = false;
            SetManagedControlsEnabled(false);
            ClearManagedData();
        }

        private void ShowApiUnavailableState(string error)
        {
            SunshineStatusText.Text = "Running";
            SunshineStatusDetail.Text = "Sunshine is running, but the API is not available yet.";
            SunshineHeaderStatusText.Text = "API Unavailable";
            SunshineStatusIndicator.Fill = Brushes.Goldenrod;

            BtnStartSunshine.IsEnabled = false;
            BtnStopSunshine.IsEnabled = !_actionInProgress;
            BtnRestartSunshine.IsEnabled = !_actionInProgress;
            BtnOpenSunshineWebUi.IsEnabled = !_actionInProgress;
            SetManagedControlsEnabled(false);

            ManagedApiDetailText.Text = string.IsNullOrWhiteSpace(error)
                ? "Waiting for API..."
                : "API: " + error;
        }

        private void ShowManagedStatus(SunshineStatus status)
        {
            SunshineStatusText.Text = "Running";
            SunshineStatusDetail.Text = "Sunshine is running in TeknoParrot mode.";

            SunshineHeaderStatusText.Text = "Running";
            SunshineStatusIndicator.Fill = Brushes.Green;

            BtnStartSunshine.IsEnabled = false;
            BtnStopSunshine.IsEnabled = !_actionInProgress;
            BtnRestartSunshine.IsEnabled = !_actionInProgress;
            BtnOpenSunshineWebUi.IsEnabled = !_actionInProgress;
            SetManagedControlsEnabled(!_actionInProgress);

            _updatingConnectionMode = true;
            try
            {
                RadioConnectionOpen.IsChecked = status.ConnectionMode == "open";
                RadioConnectionClosed.IsChecked = status.ConnectionMode != "open";
            }
            finally
            {
                _updatingConnectionMode = false;
            }

            ConnectionStateText.Text = status.ConnectionOpen ? "Open" : "Closed";
            ActiveSessionsText.Text = status.ActiveSessions.ToString();
            PairedClientsText.Text = status.PairedClients.ToString();

            ConnectionModeDetailText.Text = "Managed by TeknoParrot while the UI is running.";

            if (!_actionInProgress)
            {
                PairingStatusText.Text = status.PairingPending
                    ? "Pairing request is currently waiting"
                    : "Waiting on pairing requests";
            }

            ManagedApiDetailText.Text = "Managed API connected";
        }

        private void ClearManagedData()
        {
            _updatingConnectionMode = true;
            try
            {
                RadioConnectionOpen.IsChecked = false;
                RadioConnectionClosed.IsChecked = false;
            }
            finally
            {
                _updatingConnectionMode = false;
            }

            ConnectionStateText.Text = "—";
            ActiveSessionsText.Text = "—";
            PairedClientsText.Text = "—";
            ConnectionModeDetailText.Text = "Sunshine is not running.";
            ManagedApiDetailText.Text = "Managed API Disconnected";
            ClientsListBox.ItemsSource = null;
            ClientListStatusText.Text = "Sunshine is not running.";
        }

        private void SetManagedControlsEnabled(bool enabled)
        {
            RadioConnectionOpen.IsEnabled = enabled;
            RadioConnectionClosed.IsEnabled = enabled;

            PairingPinTextBox.IsEnabled = enabled;
            PairingNameTextBox.IsEnabled = enabled;
            BtnPairClient.IsEnabled = enabled;

            ClientsListBox.IsEnabled = enabled;
            BtnRefreshClients.IsEnabled = enabled;
            BtnDisconnectAll.IsEnabled = enabled;
            BtnUnpairClient.IsEnabled = enabled && ClientsListBox.SelectedItem != null;
        }

        private async Task RefreshClientsAsync()
        {
            try
            {
                var clients = await SunshineManager.GetClientsAsync();
                var selectedUuid = (ClientsListBox.SelectedItem as SunshineClientInfo)?.Uuid;

                ClientsListBox.ItemsSource = clients;

                if (!string.IsNullOrWhiteSpace(selectedUuid))
                {
                    ClientsListBox.SelectedItem = clients.FirstOrDefault(c =>
                        string.Equals(c.Uuid, selectedUuid, StringComparison.OrdinalIgnoreCase));
                }

                if (clients.Count == 0)
                {
                    ClientListStatusText.Text = "No paired Moonlight clients.";
                }
                else
                {
                    var connectedCount = clients.Count(c => c.Connected);
                    ClientListStatusText.Text =
                        $"{clients.Count} paired client(s) • {connectedCount} connected";
                }

                BtnUnpairClient.IsEnabled =
                    !_actionInProgress && ClientsListBox.SelectedItem != null;
            }
            catch (Exception ex)
            {
                ClientListStatusText.Text = "Unable to load clients: " + ex.Message;
            }
        }

        private async void BtnStartSunshine_Click(object sender, RoutedEventArgs e)
        {
            if (_actionInProgress)
                return;

            try
            {
                _actionInProgress = true;
                SetAllControlsForAction(false);
                SetProcessTransitionState("Starting...", "Launching Sunshine in TeknoParrot Mode.");

                SunshineManager.Start();
                await SunshineManager.WaitForRunningStateAsync(true, TimeSpan.FromSeconds(5));

                var deadline = DateTime.UtcNow.AddSeconds(8);
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        var status = await SunshineManager.GetStatusAsync();
                        if (status.Running && status.Managed)
                            break;
                    }
                    catch
                    {
                        // Sunshine can take a moment to bring up the HTTPS server.
                    }

                    await Task.Delay(200);
                }
            }
            catch (Exception ex)
            {
                ShowError(ex, "Sunshine");
            }
            finally
            {
                _actionInProgress = false;
                await RefreshAllAsync();
            }
        }

        private async void BtnStopSunshine_Click(object sender, RoutedEventArgs e)
        {
            if (_actionInProgress)
                return;

            try
            {
                _actionInProgress = true;
                SetAllControlsForAction(false);
                SetProcessTransitionState("Stopping...", "Requesting a graceful Sunshine shutdown.");

                await SunshineManager.StopAsync();
            }
            catch (Exception ex)
            {
                ShowError(ex, "Sunshine");
            }
            finally
            {
                _actionInProgress = false;
                await RefreshAllAsync();
            }
        }

        private async void BtnRestartSunshine_Click(object sender, RoutedEventArgs e)
        {
            if (_actionInProgress)
                return;

            try
            {
                _actionInProgress = true;
                SetAllControlsForAction(false);
                SetProcessTransitionState("Restarting...", "Gracefully restarting Sunshine.");

                await SunshineManager.RestartAsync();
            }
            catch (Exception ex)
            {
                ShowError(ex, "Sunshine");
            }
            finally
            {
                _actionInProgress = false;
                await RefreshAllAsync();
            }
        }

        private async void ConnectionMode_Checked(object sender, RoutedEventArgs e)
        {
            if (_updatingConnectionMode || _actionInProgress || !IsLoaded)
                return;

            var mode = RadioConnectionOpen.IsChecked == true
                ? "open"
                : "closed";

            try
            {
                _actionInProgress = true;
                SetManagedControlsEnabled(false);
                await SunshineManager.SetConnectionModeAsync(mode);
            }
            catch (Exception ex)
            {
                ShowError(ex, "Sunshine");
            }
            finally
            {
                _actionInProgress = false;
                await RefreshAllAsync(false);
            }
        }

        private async void BtnPairClient_Click(object sender, RoutedEventArgs e)
        {
            if (_actionInProgress)
                return;

            var pin = (PairingPinTextBox.Text ?? string.Empty).Trim();
            var name = (PairingNameTextBox.Text ?? string.Empty).Trim();

            try
            {
                _actionInProgress = true;
                SetManagedControlsEnabled(false);
                PairingStatusText.Text = "Pairing...";

                await SunshineManager.PairAsync(pin, name);

                PairingPinTextBox.Clear();
                PairingNameTextBox.Clear();

                PairingStatusText.Text = "Pairing accepted by Sunshine.";
                await RefreshClientsAsync();

                await Task.Delay(2000);
                PairingStatusText.Text = "Waiting on pairing requests";
            }
            catch (Exception ex)
            {
                PairingStatusText.Text = "Pairing failed.";
                ShowError(ex, "Sunshine");
            }
            finally
            {
                _actionInProgress = false;
                await RefreshAllAsync(false);
            }
        }

        private async void BtnRefreshClients_Click(object sender, RoutedEventArgs e)
        {
            if (_actionInProgress)
                return;

            await RefreshClientsAsync();
        }

        private async void BtnDisconnectAll_Click(object sender, RoutedEventArgs e)
        {
            if (_actionInProgress)
                return;

            try
            {
                _actionInProgress = true;
                SetManagedControlsEnabled(false);
                await SunshineManager.DisconnectAllAsync();
            }
            catch (Exception ex)
            {
                ShowError(ex, "Sunshine");
            }
            finally
            {
                _actionInProgress = false;
                await RefreshAllAsync();
            }
        }

        private async void BtnUnpairClient_Click(object sender, RoutedEventArgs e)
        {
            if (_actionInProgress || !(ClientsListBox.SelectedItem is SunshineClientInfo client))
                return;

            var result = MessageBox.Show(
                $"Unpair {client.DisplayName}?",
                "Sunshine",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                _actionInProgress = true;
                SetManagedControlsEnabled(false);
                await SunshineManager.UnpairAsync(client.Uuid);
                await RefreshClientsAsync();
            }
            catch (Exception ex)
            {
                ShowError(ex, "Sunshine");
            }
            finally
            {
                _actionInProgress = false;
                await RefreshAllAsync(false);
            }
        }

        private void ClientsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BtnUnpairClient.IsEnabled =
                !_actionInProgress && ClientsListBox.SelectedItem != null;
        }

        private void BtnOpenSunshineWebUi_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://localhost:47990",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowError(ex, "Sunshine");
            }
        }

        private void SetProcessTransitionState(string status, string detail)
        {
            SunshineStatusText.Text = status;
            SunshineStatusDetail.Text = detail;
            SunshineHeaderStatusText.Text = status;
            SunshineStatusIndicator.Fill = Brushes.Goldenrod;
        }

        private void SetAllControlsForAction(bool enabled)
        {
            BtnStartSunshine.IsEnabled = enabled;
            BtnStopSunshine.IsEnabled = enabled;
            BtnRestartSunshine.IsEnabled = enabled;
            BtnOpenSunshineWebUi.IsEnabled = enabled;
            SetManagedControlsEnabled(enabled);
        }

        // ================================================================
        // Moonlight client
        // ================================================================

        private void RefreshMoonlightInstallState()
        {
            var installed = MoonlightManager.IsInstalled();

            if (!installed)
            {
                _moonlightReady = false;
                MoonlightInstallStatusText.Text = "Moonlight portable not found";
                MoonlightStatusDetailText.Text =
                    "Download the Moonlight portable and place the Moonlight folder next to TeknoParrotUi.exe.";

                MoonlightPathText.Text = $"Expected: {MoonlightManager.MoonlightExecutablePath}";
                MoonlightPathText.Visibility = Visibility.Visible;
            }
            else if (_moonlightReady)
            {
                MoonlightInstallStatusText.Text = "Ready";
                MoonlightStatusDetailText.Text =
                    "Moonlight is enabled for TeknoParrot.";

                MoonlightPathText.Text = string.Empty;
                MoonlightPathText.Visibility = Visibility.Collapsed;
            }
            else
            {
                MoonlightInstallStatusText.Text = "Stopped";
                MoonlightStatusDetailText.Text =
                    "Moonlight is installed and ready to enable.";

                MoonlightPathText.Text = string.Empty;
                MoonlightPathText.Visibility = Visibility.Collapsed;
            }

            BtnStartMoonlight.IsEnabled =
                installed && !_moonlightReady && !_moonlightActionInProgress;
            BtnStopMoonlight.IsEnabled =
                installed && _moonlightReady && !_moonlightActionInProgress;
            BtnOpenMoonlight.IsEnabled =
                installed && !_moonlightActionInProgress;

            UpdateMoonlightOperationalControls();
        }

        private void UpdateMoonlightOperationalControls()
        {
            var enabled =
                MoonlightManager.IsInstalled() &&
                _moonlightReady &&
                !_moonlightActionInProgress;

            MoonlightHostTextBox.IsEnabled = enabled;
            BtnMoonlightPair.IsEnabled = enabled;
            BtnMoonlightRefreshApps.IsEnabled = enabled;
            BtnMoonlightQuitStream.IsEnabled = enabled;
            MoonlightAppsListBox.IsEnabled = enabled;

            BtnMoonlightStartStream.IsEnabled =
                enabled &&
                MoonlightAppsListBox.SelectedItem != null &&
                !string.IsNullOrWhiteSpace(MoonlightHostTextBox.Text);
        }

        private void SetMoonlightBusy(bool busy)
        {
            _moonlightActionInProgress = busy;
            RefreshMoonlightInstallState();
        }

        private static string GenerateMoonlightPairingPin()
        {
            // Four-digit PIN used by Moonlight's pairing flow. TeknoParrot generates
            // it so the normal Moonlight pairing UI does not need to be shown.
            var random = new Random(unchecked(Environment.TickCount * 31 + Guid.NewGuid().GetHashCode()));
            return random.Next(0, 10000).ToString("D4");
        }

        private string GetMoonlightHost()
        {
            var host = (MoonlightHostTextBox.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(host))
                throw new InvalidOperationException("Enter a Moonlight host IP address or host name.");

            return host;
        }

        private void BtnStartMoonlight_Click(object sender, RoutedEventArgs e)
        {
            if (!MoonlightManager.IsInstalled())
            {
                RefreshMoonlightInstallState();
                return;
            }

            _moonlightReady = true;
            RefreshMoonlightInstallState();
        }

        private void BtnStopMoonlight_Click(object sender, RoutedEventArgs e)
        {
            _moonlightReady = false;

            try
            {
                MoonlightManager.StopAll();
            }
            catch (Exception ex)
            {
                ShowError(ex, "Moonlight");
            }

            MoonlightAppsListBox.ItemsSource = null;
            MoonlightGeneratedPinText.Text = "----";
            MoonlightPairStatusText.Text =
                "Start pairing to generate a PIN, then enter that PIN on the Sunshine host.";
            MoonlightConnectionStatusText.Text = "Enter a host address to begin.";
            RefreshMoonlightInstallState();
        }

        private void BtnOpenMoonlight_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MoonlightManager.Open();
            }
            catch (Exception ex)
            {
                ShowError(ex, "Moonlight");
            }
        }

        private async void BtnMoonlightPair_Click(object sender, RoutedEventArgs e)
        {
            if (_moonlightActionInProgress)
                return;

            try
            {
                var host = GetMoonlightHost();
                var pin = GenerateMoonlightPairingPin();

                MoonlightGeneratedPinText.Text = pin;
                MoonlightPairStatusText.Text =
                    $"Enter PIN {pin} on the Sunshine host to approve this client.";

                SetMoonlightBusy(true);

                var result = await MoonlightManager.PairAsync(host, pin);

                if (result.ExitCode != 0)
                    throw new InvalidOperationException(
                        result.GetBestError("Moonlight pairing failed.")
                    );

                MoonlightPairStatusText.Text = "Paired successfully.";

                var apps = await MoonlightManager.ListAppsAsync(host);
                MoonlightAppsListBox.ItemsSource = apps
                    .Select(app => string.Equals(app, "Desktop", StringComparison.OrdinalIgnoreCase)
                        ? "Desktop - TeknoParrot"
                        : app)
                    .ToList();

                MoonlightConnectionStatusText.Text =
                    apps.Count == 0
                        ? $"Paired with {host}, but no applications were returned."
                        : $"Connected to {host}.";
            }
            catch (Exception ex)
            {
                MoonlightPairStatusText.Text = "Pairing failed.";
                ShowError(ex, "Moonlight");
            }
            finally
            {
                SetMoonlightBusy(false);
            }
        }

        private async void BtnMoonlightRefreshApps_Click(object sender, RoutedEventArgs e)
        {
            if (_moonlightActionInProgress)
                return;

            try
            {
                var host = GetMoonlightHost();

                _moonlightActionInProgress = true;
                SetMoonlightBusy(true);

                var apps = await MoonlightManager.ListAppsAsync(host);

                MoonlightAppsListBox.ItemsSource = apps
                    .Select(app => string.Equals(app, "Desktop", StringComparison.OrdinalIgnoreCase)
                        ? "Desktop - TeknoParrot"
                        : app)
                    .ToList();

                MoonlightConnectionStatusText.Text =
                    apps.Count == 0
                        ? $"Connected to {host}, but no applications were returned."
                        : $"Connected to {host}.";
            }
            catch (Exception ex)
            {
                MoonlightAppsListBox.ItemsSource = null;
                ShowError(ex, "Moonlight");
            }
            finally
            {
                _moonlightActionInProgress = false;
                SetMoonlightBusy(false);
            }
        }

        private void MoonlightAppsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateMoonlightOperationalControls();
        }

        private void MoonlightHostTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var host = (MoonlightHostTextBox.Text ?? string.Empty).Trim();

            MoonlightConnectionStatusText.Text = string.IsNullOrWhiteSpace(host)
                ? "Enter a host address to begin."
                : $"Target host: {host}";

            UpdateMoonlightOperationalControls();
        }

        private void BtnMoonlightStartStream_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_moonlightReady)
                    throw new InvalidOperationException("Start Moonlight before launching a stream.");
                var host = GetMoonlightHost();
                var selectedAppName = MoonlightAppsListBox.SelectedItem as string;

                if (string.IsNullOrWhiteSpace(selectedAppName))
                    throw new InvalidOperationException("Select an application to stream.");

                var moonlightAppName =
                    string.Equals(selectedAppName, "Desktop - TeknoParrot", StringComparison.OrdinalIgnoreCase)
                        ? "Desktop"
                        : selectedAppName;

                MoonlightManager.StartStream(host, moonlightAppName);
                MoonlightConnectionStatusText.Text =
                    $"Streaming {selectedAppName} from {host}.";
            }
            catch (Exception ex)
            {
                ShowError(ex, "Moonlight");
            }
        }

        private async void BtnMoonlightQuitStream_Click(object sender, RoutedEventArgs e)
        {
            if (_moonlightActionInProgress)
                return;

            try
            {
                var host = GetMoonlightHost();

                _moonlightActionInProgress = true;
                SetMoonlightBusy(true);

                var result = await MoonlightManager.QuitStreamAsync(host);

                if (result.ExitCode != 0)
                    throw new InvalidOperationException(
                        result.GetBestError("Moonlight could not quit the remote application.")
                    );

                MoonlightConnectionStatusText.Text = $"Connected to {host}.";

            }
            catch (Exception ex)
            {
                ShowError(ex, "Moonlight");
            }
            finally
            {
                _moonlightActionInProgress = false;
                SetMoonlightBusy(false);
            }
        }

        private static void ShowError(Exception ex, string title)
        {
            MessageBox.Show(
                ex.Message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }
}
