using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using R = TeknoParrotUi.Properties.Resources;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Initial D LAN presence in the library (InitialDServer docs/UNIFIED_MODE.md 2.7 "LAN presence", 3.4 step 2a;
    /// LAN_AGENT.md, phase P2b): while one of the five matchmaking titles is selected, TeknoParrotUI beacons on the
    /// agent port, shows the other TeknoParrot PCs that are ready for the same game, asks the one-time link question,
    /// and starts both games together.
    /// <para>
    /// The session is stopped before any launch, so the game's own LAN agent gets the port (LAN_AGENT.md 1), and when
    /// the library is left.
    /// </para>
    /// </summary>
    public partial class Library
    {
        private InitialDLanPresence.Session _lanSession;
        private string _lanSessionProfile = "";
        private bool _lanLinkDialogOpen;

        private void UserControl_Unloaded(object sender, RoutedEventArgs e) => StopInitialDLanPresence();

        /// <summary>Starts, keeps or stops the presence session for the profile the library shows now.</summary>
        private void UpdateInitialDLanPresence(GameProfile selected)
        {
            try
            {
                var key = InitialDUnifiedMode.IsMatchmakingProfile(selected) ? InitialDUnifiedMode.ProfileKey(selected) : "";
                if (key == _lanSessionProfile && (_lanSession != null || key.Length == 0))
                {
                    RefreshInitialDLanCard();
                    return;
                }
                StopInitialDLanPresence();
                _lanSessionProfile = key;
                if (key.Length == 0 || string.IsNullOrEmpty(InitialDLanPresence.TitleCode(selected)))
                {
                    RefreshInitialDLanCard();
                    return;
                }
                var session = new InitialDLanPresence.Session(selected);
                session.PeersChanged += () => Dispatcher.BeginInvoke(new Action(RefreshInitialDLanCard));
                session.StartRequested += p => Dispatcher.BeginInvoke(new Action(() => OnLanStartRequested(session, p)));
                session.LinkAsked += p => Dispatcher.BeginInvoke(new Action(() => OnLanLinkAsked(session, p)));
                if (!session.Start())
                    Debug.WriteLine($"InitialDLan: the agent port is not free ({session.StartError})");
                _lanSession = session;
                RefreshInitialDLanCard();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDLan: presence: {ex.Message}");
            }
        }

        private void StopInitialDLanPresence()
        {
            var session = _lanSession;
            _lanSession = null;
            _lanSessionProfile = "";
            try
            {
                session?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDLan: stop: {ex.Message}");
            }
        }

        private GameProfile SelectedProfile() =>
            gameList?.SelectedItem is System.Windows.Controls.ListBoxItem item ? item.Tag as GameProfile : null;

        /// <summary>The card under the game info: who is ready, or why linking is off.</summary>
        private void RefreshInitialDLanCard()
        {
            try
            {
                if (initialDLanPanel == null)
                    return;
                var session = _lanSession;
                if (session == null)
                {
                    initialDLanPanel.Visibility = Visibility.Collapsed;
                    return;
                }
                var name = SelectedProfile()?.GameNameInternal ?? _lanSessionProfile;
                var peers = session.Peers().Where(p => p.Consent != InitialDLanPresence.Consent.Never && !p.LinkOff).ToList();
                var ready = peers.Where(p => p.Ready).ToList();
                if (!string.IsNullOrEmpty(session.StartError))
                {
                    initialDLanText.Text = R.InitialDLanPortBusy;
                    initialDLanButtons.Visibility = Visibility.Collapsed;
                    initialDLanPanel.Visibility = Visibility.Visible;
                    return;
                }
                if (session.LinkingOff)
                {
                    // the beacons still go out so the other side can explain itself, but this PC never links
                    initialDLanText.Text = R.InitialDLanPublicNetwork;
                    initialDLanButtons.Visibility = Visibility.Collapsed;
                    initialDLanPanel.Visibility = peers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                    return;
                }
                if (ready.Count == 0)
                {
                    initialDLanPanel.Visibility = Visibility.Collapsed;
                    return;
                }
                var first = string.IsNullOrWhiteSpace(ready[0].Name) ? ready[0].Address?.ToString() ?? "?" : ready[0].Name;
                initialDLanText.Text = ready.Count == 1
                    ? string.Format(R.InitialDLanReadyOne, first, name)
                    : string.Format(R.InitialDLanReadyMany, first, ready.Count - 1, name);
                initialDLanButtons.Visibility = Visibility.Visible;
                initialDLanPanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDLan: card: {ex.Message}");
            }
        }

        /// <summary>"Start together": ask for any missing consent, send START, then start this PC's game.</summary>
        private async void BtnInitialDLanStartTogether(object sender, RoutedEventArgs e)
        {
            var session = _lanSession;
            var profile = SelectedProfile();
            if (session == null || profile == null)
                return;
            var ready = session.Peers().Where(p => p.Ready && !p.LinkOff).ToList();
            var linked = new List<InitialDLanPresence.Peer>();
            foreach (var p in ready)
            {
                if (p.Consent == InitialDLanPresence.Consent.Never)
                    continue;
                if (p.Consent == InitialDLanPresence.Consent.Always ||
                    (session.Group.Length != 0 && p.LinkAlways) ||
                    InitialDLaunchFlow.AskLink(Window.GetWindow(this), profile, p))
                    linked.Add(p);
            }
            if (linked.Count == 0)
            {
                RefreshInitialDLanCard();
                return;
            }
            session.SendStart(linked);
            await LaunchSelectedGame(false);
        }

        private async void BtnInitialDLanStartAlone(object sender, RoutedEventArgs e)
        {
            if (SelectedProfile() != null)
                await LaunchSelectedGame(false);
        }

        /// <summary>
        /// True while <paramref name="session"/> is this library's live session and the selected game is still the one
        /// it beacons for: a message of a session the selection already replaced never starts anything.
        /// </summary>
        private bool LanSessionCurrent(InitialDLanPresence.Session session) =>
            session != null && ReferenceEquals(session, _lanSession) &&
            InitialDLanPresence.TitleCode(SelectedProfile()) == session.Title;

        /// <summary>A consented peer pressed "Start together" on its PC (LAN_AGENT.md 5).</summary>
        private async void OnLanStartRequested(InitialDLanPresence.Session session, InitialDLanPresence.Peer peer)
        {
            if (peer == null || _lanLinkDialogOpen || !LanSessionCurrent(session) || !gameLaunchButton.IsEnabled)
                return;
            await LaunchSelectedGame(false);
        }

        /// <summary>A peer wants to start together and has no kept answer yet: ask once, then start.</summary>
        private async void OnLanLinkAsked(InitialDLanPresence.Session session, InitialDLanPresence.Peer peer)
        {
            var profile = SelectedProfile();
            if (peer == null || profile == null || _lanLinkDialogOpen || !LanSessionCurrent(session))
                return;
            _lanLinkDialogOpen = true;
            bool go;
            try
            {
                go = InitialDLaunchFlow.AskLink(Window.GetWindow(this), profile, peer);
            }
            finally
            {
                _lanLinkDialogOpen = false;
            }
            RefreshInitialDLanCard();
            if (go && gameLaunchButton.IsEnabled)
                await LaunchSelectedGame(false);
        }

        /// <summary>
        /// Before a library launch of a matchmaking title: offer the inbound firewall rules once when another
        /// TeknoParrot PC is on the LAN (UNIFIED_MODE.md 3.8), then give the port back to the game's own agent.
        /// </summary>
        private void InitialDLanBeforeLaunch(GameProfile profile)
        {
            try
            {
                if (_lanSession != null && _lanSession.Peers().Count > 0)
                    InitialDLaunchFlow.OfferFirewall(Window.GetWindow(this), profile);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDLan: firewall offer: {ex.Message}");
            }
            StopInitialDLanPresence();
            RefreshInitialDLanCard();
        }
    }
}
