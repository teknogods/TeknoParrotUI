using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using TeknoParrotUi.Helpers;
using R = TeknoParrotUi.Properties.Resources;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Initial D: the live online status while one of the five matchmaking titles runs (InitialDServer
    /// docs/UNIFIED_MODE.md 2.4 / 2.7, FRAMELOCK.md 7.4). It polls the DLL's idonline_status.json once a second (only a
    /// file written after this launch counts) and shows whether this start is online and why not, the LAN line,
    /// "Friends code active", "Online is back" and the frame-lock state. With a friends code it shows the code and
    /// "Copy invite" so it can still be shared while the game runs. Every other game leaves the panel collapsed.
    /// </summary>
    public partial class GameRunning
    {
        private DispatcherTimer _initialDStatusTimer;
        private DateTime _initialDLaunchUtc;
        private string _initialDPartyCode;

        private void InitialDOnlinePanel_OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_initialDStatusTimer != null || _isTest || !InitialDUnifiedMode.IsMatchmakingProfile(_gameProfile))
                    return;
                _initialDLaunchUtc = DateTime.UtcNow;
                _initialDPartyCode = InitialDUnifiedMode.CurrentPartyCode(_gameProfile);
                initialDOnlinePanel.Visibility = Visibility.Visible;
                UpdateInitialDOnlineStatus();
                _initialDStatusTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromSeconds(1) };
                _initialDStatusTimer.Tick += (s, a) => UpdateInitialDOnlineStatus();
                _initialDStatusTimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDOnline: status panel: {ex.Message}");
            }
        }

        private void InitialDOnlinePanel_OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_initialDStatusTimer == null)
                return;
            _initialDStatusTimer.Stop();
            _initialDStatusTimer = null;
            InitialDUnifiedMode.EndLaunch(_gameProfile);
        }

        private void UpdateInitialDOnlineStatus()
        {
            try
            {
                var status = InitialDOnlineHelper.ReadStatus(_gameProfile, _initialDLaunchUtc.AddSeconds(-2));
                var live = InitialDOnlineHelper.BuildLiveStatus(status, InitialDUnifiedMode.EffectiveNetworkMode(_gameProfile),
                    DateTime.UtcNow - _initialDLaunchUtc);
                initialDOnlineHeadline.Text = live.Headline;
                initialDOnlineDetails.Text = string.Join(Environment.NewLine, live.Details);
                initialDOnlineDetails.Visibility = live.Details.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

                _initialDPartyCode ??= InitialDUnifiedMode.CurrentPartyCode(_gameProfile);
                if (!string.IsNullOrEmpty(_initialDPartyCode))
                {
                    initialDOnlinePartyText.Text = string.Format(R.InitialDFriendsCodeLine, InitialDUnifiedMode.FormatPartyCode(_initialDPartyCode));
                    initialDOnlinePartyPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    initialDOnlinePartyPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDOnline: status panel update: {ex.Message}");
            }
        }

        private void InitialDOnlineCopyInvite_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_initialDPartyCode))
                return;
            try
            {
                Clipboard.SetText(InitialDUnifiedMode.InviteText(_gameProfile, _initialDPartyCode));
                initialDOnlineCopyInvite.Content = R.InitialDFriendsCopied;
            }
            catch (Exception ex)
            {
                // the clipboard can be held by another program for a moment
                Debug.WriteLine($"InitialDOnline: clipboard: {ex.Message}");
            }
        }
    }
}
