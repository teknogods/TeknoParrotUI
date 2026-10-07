using System;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using TeknoParrotUi.Common.Online;
using TeknoParrotUi.Avalonia.Services;

namespace TeknoParrotUi.Avalonia.Views;

public partial class GameRunningView
{
    private DispatcherTimer? _onlineTimer;
    private DateTime _onlineLaunchUtc;
    private bool _onlineActive;

    private void StartOnlineStatus(bool testMode)
    {
        _onlineLaunchUtc = default;
        OnlinePanel.IsVisible = !testMode && InitialDUnifiedMode.IsMatchmakingProfile(_profile);
        if (!OnlinePanel.IsVisible) return;
        _onlineActive = true;
        _onlineLaunchUtc = DateTime.UtcNow;
        _onlineTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _onlineTimer.Tick += (_, _) => UpdateOnlineStatus();
        _onlineTimer.Start();
        UpdateOnlineStatus();
    }

    private void UpdateOnlineStatus()
    {
        if (_profile == null) return;
        var live = InitialDOnlineHelper.BuildLiveStatus(InitialDOnlineHelper.ReadStatus(_profile, _onlineLaunchUtc.AddSeconds(-2)),
            InitialDUnifiedMode.EffectiveNetworkMode(_profile), DateTime.UtcNow - _onlineLaunchUtc);
        OnlineHeadline.Text = live.Headline;
        OnlineDetails.Text = string.Join(Environment.NewLine, live.Details);
        var code = InitialDUnifiedMode.CurrentPartyCode(_profile);
        PartyCodeText.Text = string.IsNullOrEmpty(code) ? "" : string.Format(Loc.T("InitialDFriendsCodeLine"), InitialDUnifiedMode.FormatPartyCode(code));
        BtnCopyInvite.IsVisible = !string.IsNullOrEmpty(code);
    }

    private void StopOnlineStatus()
    {
        var active = _onlineActive;
        _onlineActive = false;
        _onlineTimer?.Stop();
        _onlineTimer = null;
        if (active && _profile != null) InitialDUnifiedMode.EndLaunch(_profile);
    }

    private void PublishOnlineExit()
    {
        if (_profile?.OnlineIdType != Common.OnlineIdType.InitialD || _onlineLaunchUtc == default) return;
        var message = InitialDOnlineHelper.ExitToastText(_profile, _onlineLaunchUtc);
        if (message != null) OnlineMessage?.Invoke(message);
    }

    private async void BtnCopyInvite_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var code = InitialDUnifiedMode.CurrentPartyCode(_profile);
        if (code == null || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        await clipboard.SetTextAsync(InitialDUnifiedMode.InviteText(_profile, code));
        BtnCopyInvite.Content = Loc.T("InitialDFriendsCopied");
    }
}
