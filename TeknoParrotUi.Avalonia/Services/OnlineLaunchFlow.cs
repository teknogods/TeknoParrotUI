using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Online;

namespace TeknoParrotUi.Avalonia.Services;

internal static class OnlineLaunchFlow
{
    public static async Task<bool> BeforeLaunchAsync(Control host, GameProfile profile, bool friends, Action openAccount)
    {
        JoystickHelper.AutoFillOnlineId(profile);
        if (profile.OnlineIdType == OnlineIdType.Kizuna &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TP_ONLINE_CRED")) &&
            !KizunaOnlineHelper.ProfileHasCredential(profile))
        {
            if (await Dialogs.ChooseAsync(host, Loc.T("KizunaRegisterTitle"),
                string.Format(Loc.T("KizunaRegisterText"), profile.GameNameInternal),
                new[] { Loc.T("KizunaRegisterOpenAccount"), Loc.T("InitialDCancel") }) == 0) openAccount();
            return false;
        }
        if (!InitialDUnifiedMode.IsMatchmakingProfile(profile))
        {
            InitialDUnifiedMode.PrepareLaunch(profile, null);
            return true;
        }
        var data = Lazydata.ParrotData;
        if (!data.InitialDUnifiedNoticeShown)
        {
            if (await Dialogs.ChooseAsync(host, Loc.T("InitialDFirstRunTitle"),
                    Loc.T("InitialDFirstRunText") + Environment.NewLine + Loc.T("InitialDAutomaticOnlineNote"),
                    new[] { Loc.T("InitialDOk") }) < 0) return false;
            data.InitialDUnifiedNoticeShown = true;
            JoystickHelper.Serialize();
        }
        var server = InitialDUnifiedMode.ServerOf(profile);
        var credential = profile.ConfigValues.FirstOrDefault(x => x.FieldName == InitialDOnlineHelper.IdFieldName)?.FieldValue;
        var secret = profile.ConfigValues.FirstOrDefault(x => x.FieldName == InitialDOnlineHelper.SecretFieldName)?.FieldValue;
        if (server.Official && (InitialDOnlineHelper.NormalizePcbId(credential) == null || !InitialDOnlineHelper.IsValidSecret(secret)))
        {
            if (friends)
            {
                if (await Dialogs.ChooseAsync(host, Loc.T("InitialDFriendsTitle"), Loc.T("InitialDFriendsNeedsRegistration"),
                    new[] { Loc.T("InitialDOnlineRegisterButton"), Loc.T("InitialDCancel") }) == 0) openAccount();
                return false;
            }
            if (!data.InitialDRegisterPromptOff)
            {
                var choice = await Dialogs.ChooseAsync(host, Loc.T("InitialDRegisterTitle"),
                    string.Format(Loc.T("InitialDRegisterText"), profile.GameNameInternal),
                    new[] { Loc.T("InitialDRegisterPlayOnline"), Loc.T("InitialDRegisterPlayOffline"), Loc.T("InitialDRegisterDontAsk") });
                if (choice == 0) { openAccount(); return false; }
                if (choice < 0) return false;
                if (choice == 2) { data.InitialDRegisterPromptOff = true; JoystickHelper.Serialize(); }
            }
            InitialDUnifiedMode.PrepareLaunch(profile, null);
            return true;
        }
        string? code = null;
        if (friends)
        {
            var entry = new TextBox { Text = InitialDUnifiedMode.FormatPartyCode(InitialDUnifiedMode.GeneratePartyCode()) };
            var copy = new Button { Content = Loc.T("InitialDFriendsCopyInvite") };
            copy.Click += async (_, _) =>
            {
                var normalized = InitialDUnifiedMode.NormalizePartyCode(entry.Text);
                var clipboard = TopLevel.GetTopLevel(host)?.Clipboard;
                if (normalized != null && clipboard != null)
                    await clipboard.SetTextAsync(InitialDUnifiedMode.InviteText(profile, normalized));
            };
            var panel = new StackPanel { Spacing = 8, Children = { entry, copy } };
            while (true)
            {
                if (await Dialogs.ChooseAsync(host, Loc.T("InitialDFriendsTitle"),
                    string.Format(Loc.T("InitialDFriendsIntro"), profile.GameNameInternal) + Environment.NewLine + Loc.T("InitialDFriendsNote"),
                    new[] { Loc.T("InitialDFriendsStart"), Loc.T("InitialDCancel") }, panel) != 0) return false;
                code = InitialDUnifiedMode.NormalizePartyCode(entry.Text);
                if (code != null && (!InitialDUnifiedMode.IsWeakPartyCode(code) ||
                    await Dialogs.ChooseAsync(host, Loc.T("InitialDFriendsTitle"),
                        string.Format(Loc.T("InitialDFriendsWeakCode"), code),
                        new[] { Loc.T("InitialDFriendsStart"), Loc.T("InitialDCancel") }) == 0)) break;
                await Dialogs.ChooseAsync(host, Loc.T("InitialDFriendsTitle"), Loc.T("InitialDFriendsBadCode"), new[] { Loc.T("InitialDOk") });
            }
        }
        var ping = await InitialDUnifiedMode.PingAsync(server, TimeSpan.FromSeconds(1.5));
        if (ping?.Maintenance == true)
        {
            var choice = await Dialogs.ChooseAsync(host, Loc.T("InitialDOnlineTitle"),
                string.Format(Loc.T("InitialDMaintenanceText"), profile.GameNameInternal),
                friends ? new[] { Loc.T("InitialDOk") } : new[] { Loc.T("InitialDStartOffline"), Loc.T("InitialDCancel") });
            if (friends || choice != 0) return false;
        }
        else if (friends && ping?.Party == false)
        {
            await Dialogs.ChooseAsync(host, Loc.T("InitialDFriendsTitle"), Loc.T("InitialDFriendsOff"), new[] { Loc.T("InitialDOk") });
            return false;
        }
        InitialDUnifiedMode.PrepareLaunch(profile, code);
        return true;
    }

    public static async Task<bool> AskLinkAsync(Control host, GameProfile profile, InitialDLanPresence.Peer peer)
    {
        var name = string.IsNullOrWhiteSpace(peer.Name) ? peer.Address?.ToString() : peer.Name;
        var result = await Dialogs.ChooseAsync(host, Loc.T("InitialDLanLinkTitle"),
            string.Format(Loc.T("InitialDLanLinkText"), name, profile.GameNameInternal),
            new[] { Loc.T("InitialDLanLinkAlways"), Loc.T("InitialDLanLinkOnce"), Loc.T("InitialDLanLinkNever") });
        if (result == 0) InitialDLanPresence.SetConsent(peer.InstallId, peer.Consent = InitialDLanPresence.Consent.Always);
        if (result == 1) InitialDLanPresence.NoteConsentOnce(peer.InstallId);
        if (result == 2) InitialDLanPresence.SetConsent(peer.InstallId, peer.Consent = InitialDLanPresence.Consent.Never);
        return result is 0 or 1;
    }

    public static async Task OfferFirewallAsync(Control host, GameProfile profile)
    {
        if (!OperatingSystem.IsWindows() || !InitialDFirewall.ShouldOffer(profile)) return;
        var rules = InitialDFirewall.Rules(profile);
        if (rules.Count == 0) return;
        InitialDFirewall.MarkOffered();
        if (await Dialogs.ChooseAsync(host, Loc.T("InitialDFirewallTitle"),
            string.Format(Loc.T("InitialDFirewallText"), string.Join(Environment.NewLine, InitialDFirewall.Describe(rules))),
            new[] { Loc.T("InitialDFirewallAdd"), Loc.T("InitialDFirewallSkip") }) == 0 &&
            !InitialDFirewall.Apply(rules))
            await Dialogs.ChooseAsync(host, Loc.T("InitialDFirewallTitle"), Loc.T("InitialDFirewallFailed"), new[] { Loc.T("InitialDOk") });
    }
}
