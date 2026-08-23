using System;
using System.Collections.Generic;
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
    /// Interaction logic for SunshineManagement.xaml
    /// </summary>
    public partial class SunshineManagement : UserControl
    {
        private readonly DispatcherTimer _statusTimer;
        private bool _actionInProgress;
        private bool _refreshInProgress;
        private bool _updatingConnectionMode;

        public SunshineManagement()
        {
            InitializeComponent();

            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _statusTimer.Tick += StatusTimer_Tick;

            Loaded += SunshineManagement_Loaded;
            Unloaded += SunshineManagement_Unloaded;
        }

        private async void SunshineManagement_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshAllAsync();
            _statusTimer.Start();
        }

        private void SunshineManagement_Unloaded(object sender, RoutedEventArgs e)
        {
            _statusTimer.Stop();
        }

        private async void StatusTimer_Tick(object sender, EventArgs e)
        {
            if (_actionInProgress || _refreshInProgress)
                return;

            // Refresh both host status and the paired-client list so Connected/Offline
            // state follows Moonlight sessions automatically without requiring Refresh.
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
            SetManagedControlsEnabled(false);

            ManagedApiDetailText.Text = string.IsNullOrWhiteSpace(error)
                ? "Waiting for API..."
                : "API: " + error;
        }

        private void ShowManagedStatus(SunshineStatus status)
        {
            SunshineStatusText.Text = "Running";
            SunshineStatusDetail.Text = string.IsNullOrWhiteSpace(status.Version)
                ? "Sunshine is running in TeknoParrot mode."
                : $"Sunshine is running in TeknoParrot mode.";
                //: $"Sunshine {status.Version} is running in TeknoParrot mode."; //If you want version

            SunshineHeaderStatusText.Text = "Running";
            SunshineStatusIndicator.Fill = Brushes.Green;

            BtnStartSunshine.IsEnabled = false;
            BtnStopSunshine.IsEnabled = !_actionInProgress;
            BtnRestartSunshine.IsEnabled = !_actionInProgress;
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

                BtnUnpairClient.IsEnabled = !_actionInProgress && ClientsListBox.SelectedItem != null;
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
                ShowError(ex);
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
                ShowError(ex);
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
                ShowError(ex);
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

            string mode;

            mode = RadioConnectionOpen.IsChecked == true
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
                ShowError(ex);
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
                ShowError(ex);
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
                ShowError(ex);
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
                ShowError(ex);
            }
            finally
            {
                _actionInProgress = false;
                await RefreshAllAsync(false);
            }
        }

        private void ClientsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BtnUnpairClient.IsEnabled = !_actionInProgress && ClientsListBox.SelectedItem != null;
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
            SetManagedControlsEnabled(enabled);
        }

        private static void ShowError(Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Sunshine",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }
}