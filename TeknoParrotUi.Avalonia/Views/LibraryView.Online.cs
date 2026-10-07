using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Online;
using TeknoParrotUi.Avalonia.Services;

namespace TeknoParrotUi.Avalonia.Views;

public partial class LibraryView
{
    private InitialDLanPresence.Session? _lanSession;
    private string _lanProfile = "";
    private bool _lanDialogOpen;

    private void UpdateLanPresence(GameProfile? profile)
    {
        var key = InitialDUnifiedMode.IsMatchmakingProfile(profile) ? InitialDUnifiedMode.ProfileKey(profile) : "";
        if (key != _lanProfile || key.Length > 0 && _lanSession == null)
        {
            StopLanPresence();
            if (key.Length > 0 && !OperatingSystem.IsAndroid())
            {
                _lanProfile = key;
                var session = new InitialDLanPresence.Session(profile);
                _lanSession = session;
                session.PeersChanged += () => Dispatcher.UIThread.Post(RefreshLanCard);
                session.StartRequested += peer => Dispatcher.UIThread.Post(() =>
                {
                    if (ReferenceEquals(_lanSession, session) && !_lanDialogOpen) LaunchSelected(false);
                });
                session.LinkAsked += peer => Dispatcher.UIThread.Post(async () =>
                {
                    if (!ReferenceEquals(_lanSession, session) || _lanDialogOpen || Selected is not { } selected) return;
                    _lanDialogOpen = true;
                    try
                    {
                        if (await OnlineLaunchFlow.AskLinkAsync(this, selected, peer) &&
                            ReferenceEquals(_lanSession, session)) LaunchSelected(false);
                    }
                    finally { _lanDialogOpen = false; }
                });
                session.Start();
            }
        }
        RefreshLanCard();
    }

    private void StopLanPresence()
    {
        var session = _lanSession;
        _lanSession = null;
        _lanProfile = "";
        session?.Dispose();
        LanPanel.IsVisible = false;
    }

    private void RefreshLanCard()
    {
        var session = _lanSession;
        if (session == null) { LanPanel.IsVisible = false; return; }
        var peers = session.Peers().Where(p => p.Ready && !p.LinkOff &&
            p.Consent != InitialDLanPresence.Consent.Never).ToList();
        if (!string.IsNullOrEmpty(session.StartError))
            LanText.Text = Loc.T("InitialDLanPortBusy");
        else if (session.LinkingOff) LanText.Text = Loc.T("InitialDLanPublicNetwork");
        else if (peers.Count > 0)
        {
            var name = string.IsNullOrWhiteSpace(peers[0].Name) ? peers[0].Address?.ToString() : peers[0].Name;
            LanText.Text = peers.Count == 1
                ? string.Format(Loc.T("InitialDLanReadyOne"), name, Selected?.GameNameInternal)
                : string.Format(Loc.T("InitialDLanReadyMany"), name, peers.Count - 1, Selected?.GameNameInternal);
        }
        LanPanel.IsVisible = peers.Count > 0 || !string.IsNullOrEmpty(session.StartError);
        BtnLanTogether.IsVisible = peers.Count > 0 && !session.LinkingOff;
    }

    private async void BtnLanTogether_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var session = _lanSession;
        if (session == null || Selected is not { } profile || _lanDialogOpen) return;
        _lanDialogOpen = true;
        try
        {
            var linked = new System.Collections.Generic.List<InitialDLanPresence.Peer>();
            foreach (var peer in session.Peers().Where(p => p.Ready && !p.LinkOff &&
                p.Consent != InitialDLanPresence.Consent.Never))
                if (peer.Consent == InitialDLanPresence.Consent.Always ||
                    session.Group.Length > 0 && peer.LinkAlways ||
                    await OnlineLaunchFlow.AskLinkAsync(this, profile, peer)) linked.Add(peer);
            if (!ReferenceEquals(_lanSession, session) || linked.Count == 0) return;
            session.SendStart(linked);
            LaunchSelected(false);
        }
        finally { _lanDialogOpen = false; }
    }

    private void BtnFriends_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => LaunchSelected(false, true);
    private void BtnPlayOnline_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => OnlineRequested?.Invoke();
    private async void BtnOnlineProfile_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Selected?.OnlineProfileURL is { Length: > 0 } url) await ExternalUrlLauncher.OpenAsync(this, url);
    }
    private void BtnGtOnTp_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Selected is { } profile)
        {
            try { StatusText.Text = GoldenTeeOnlineHelper.LaunchTool(profile) ?? ""; }
            catch (Exception error) { StatusText.Text = error.Message; }
        }
    }
}
