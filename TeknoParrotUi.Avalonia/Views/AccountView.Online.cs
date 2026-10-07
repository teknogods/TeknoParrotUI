using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Activation;
using TeknoParrotUi.Common.Online;
using TeknoParrotUi.Avalonia.Services;

namespace TeknoParrotUi.Avalonia.Views;

public partial class AccountView
{
    private OnlineAccountProfile? _accountProfile;
    private bool _cardsBusy;
    private InitialDOnlineHelper.MachineInfo? _initialDMachine;
    private KizunaOnlineHelper.MachineInfo? _kizunaMachine;
    private bool _initialDKnown, _kizunaKnown;
    private readonly TextBlock _cardMessage = new() { TextWrapping = TextWrapping.Wrap };
    private InitialDOnlineHelper.ApiClient InitialDApi => new(_oauth.GetValidTokenAsync, FreshLoginAsync);
    private KizunaOnlineHelper.ApiClient KizunaApi => new(_oauth.GetValidTokenAsync, FreshLoginAsync);

    private async Task<bool> FreshLoginAsync()
    {
        _cardMessage.Text = Loc.T("InitialDOnlineFreshLoginPrompt");
        return !OperatingSystem.IsAndroid() && await _oauth.LoginAsync(true);
    }

    private async Task RefreshCardsAsync()
    {
        if (_cardsBusy) return;
        _cardsBusy = true;
        try
        {
            if (_oauth.IsLoggedIn && !OperatingSystem.IsAndroid())
            {
                _accountProfile = await OnlineAccountProfile.FetchAsync(await _oauth.GetValidTokenAsync());
                var initial = await InitialDApi.GetMachineAsync();
                _initialDKnown = initial.Ok || initial.Error == InitialDOnlineHelper.ApiError.NoMachine;
                _initialDMachine = initial.Ok ? initial.Value : null;
                var kizuna = await KizunaApi.GetMachineAsync();
                _kizunaKnown = kizuna.Ok || kizuna.Error == KizunaOnlineHelper.ApiError.NoMachine;
                _kizunaMachine = kizuna.Ok ? kizuna.Value : null;
                _cardMessage.Text = initial.Ok || initial.Error == InitialDOnlineHelper.ApiError.NoMachine
                    ? "" : InitialDOnlineHelper.DescribeError(initial);
            }
            else
            {
                _initialDKnown = _kizunaKnown = false;
                _initialDMachine = null;
                _kizunaMachine = null;
                _accountProfile = null;
            }
        }
        catch (Exception error) { _cardMessage.Text = error.Message; }
        finally { _cardsBusy = false; BuildCards(); }
    }

    private static TextBlock Text(string value, bool bold = false) => new()
    {
        Text = value, TextWrapping = TextWrapping.Wrap,
        FontWeight = bold ? FontWeight.Bold : FontWeight.Normal
    };

    private static Border Card(StackPanel content) => new()
    {
        Classes = { "card" }, Padding = new Thickness(16), Child = content
    };

    private Button ActionButton(string key, Func<Task> action)
    {
        var button = new Button { Content = Loc.T(key), Margin = new Thickness(0, 4, 8, 4), MinHeight = 40 };
        button.Click += async (_, _) =>
        {
            if (_cardsBusy) return;
            _cardsBusy = true;
            OnlineCards.IsEnabled = false;
            try { await action(); }
            catch (Exception error) { _cardMessage.Text = error.Message; }
            finally
            {
                var message = _cardMessage.Text;
                _cardsBusy = false;
                await RefreshCardsAsync();
                _cardMessage.Text = message;
                OnlineCards.IsEnabled = true;
            }
        };
        return button;
    }

    private void BuildCards()
    {
        OnlineCards.Children.Clear();
        OnlineCards.Children.Add(_cardMessage);
        if (_accountProfile is { } account)
        {
            var details = new StackPanel { Spacing = 6 };
            details.Children.Add(Text(string.Format(Loc.T("AccountPageTierPrefix"), account.Tier), true));
            foreach (var item in new[]
            {
                ("Sega ID", account.SegaId), ("Highscore serial", account.HighscoreSerial),
                ("Namco ID", account.NamcoId), ("Mario Kart ID", account.MarioKartId),
                ("Golden Tee PCB ID", account.GoldenTeePcbId?.ToString()), ("Golden Tee Card ID", account.GoldenTeeCardId)
            })
                details.Children.Add(new SelectableTextBlock { Text = item.Item1 + ": " + item.Item2, TextWrapping = TextWrapping.Wrap });
            if (account.IsSubscribed)
            {
                details.Children.Add(Text(account.ExpirationDate?.ToLocalTime().ToString("g") ?? Loc.T("AccountPageExpiryUnknown")));
                var serials = new ComboBox { ItemsSource = account.Serials,
                    SelectedItem = account.Serials.FirstOrDefault(s => !s.IsGifted && !s.IsInUse) };
                details.Children.Add(serials);
                if (!OperatingSystem.IsAndroid())
                    details.Children.Add(ActionButton("AccountPageRegisterButton", async () =>
                    {
                        if (serials.SelectedItem is not OnlineAccountProfile.AccountSerial serial || serial.IsGifted) return;
                        if (serial.IsInUse) { _cardMessage.Text = Loc.T("AccountPageSerialInUse"); return; }
                        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                        var current = await TeknoParrotActivation.GetStatusAsync(timeout.Token);
                        if (current.IsActivated)
                        {
                            var removed = await TeknoParrotActivation.DeactivateAsync(timeout.Token);
                            if (!removed.Success) { _cardMessage.Text = removed.Message; return; }
                        }
                        var result = await TeknoParrotActivation.ActivateAsync(serial.Serial, timeout.Token);
                        _cardMessage.Text = result.Message;
                    }));
            }
            OnlineCards.Children.Add(Card(details));
        }
        BuildOnlineCard(false);
        BuildOnlineCard(true);
    }

    private void BuildOnlineCard(bool kizuna)
    {
        var prefix = kizuna ? "Kizuna" : "InitialD";
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Text(Loc.T(prefix + "OnlineTitle"), true));
        panel.Children.Add(Text(Loc.T(prefix + "OnlineIntro")));
        var rank = InitialDOnlineHelper.Rank(_accountProfile?.InitialDRank);
        if (rank != null)
        {
            var ladder = new WrapPanel();
            var levels = kizuna
                ? KizunaOnlineHelper.Ladder.Select(level => (level.Tier, level.Display, level.Color, level.Color2, Stars: level.Tier >= 3, Motion: level.Tier >= 4))
                : InitialDOnlineHelper.Ladder.Select(level => (level.Tier, level.Display, level.Color, level.Color2, level.Stars, level.Motion));
            foreach (var level in levels)
            {
                var label = Text(level.Display, true);
                if (level.Color != null)
                    label.Foreground = new SolidColorBrush(Color.Parse("#" + level.Color));
                var badge = new Border { Child = label, CornerRadius = new CornerRadius(3), Padding = new Thickness(8, 4),
                    Margin = new Thickness(0, 0, 6, 4), Opacity = level.Tier == rank.Tier ? 1 : 0.45 };
                if (level.Color != null)
                {
                    badge.BorderBrush = new SolidColorBrush(Color.Parse("#" + level.Color));
                    badge.BorderThickness = new Thickness(level.Stars ? 2 : 1);
                    if (level.Color2 != null)
                        label.Foreground = new LinearGradientBrush
                        {
                            GradientStops = new GradientStops
                            {
                                new(Color.Parse("#" + level.Color2), 0),
                                new(Color.Parse("#" + level.Color), 0.5),
                                new(Color.Parse("#" + level.Color2), 1)
                            }
                        };
                }
                if (level.Tier == rank.Tier && level.Motion)
                {
                    var start = DateTime.UtcNow;
                    var pulse = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
                    pulse.Tick += (_, _) => badge.Opacity = 0.8 + 0.2 * Math.Cos((DateTime.UtcNow - start).TotalSeconds * 3);
                    badge.AttachedToVisualTree += (_, _) => pulse.Start();
                    badge.DetachedFromVisualTree += (_, _) => pulse.Stop();
                }
                ladder.Children.Add(badge);
            }
            panel.Children.Add(ladder);
            if (!kizuna && rank.Id7KingAura) panel.Children.Add(Text(Loc.T("InitialDOnlineKingAura")));
        }
        var local = kizuna ? KizunaOnlineHelper.LocalCredential() : InitialDOnlineHelper.LocalCredential();
        var pcb = kizuna ? _kizunaMachine?.PcbId : _initialDMachine?.PcbId;
        var status = kizuna ? _kizunaMachine?.Status : _initialDMachine?.Status;
        var last = kizuna ? _kizunaMachine?.LastSeenUtc : _initialDMachine?.LastSeenUtc;
        var known = kizuna ? _kizunaKnown : _initialDKnown;
        panel.Children.Add(Text(known
            ? pcb == null ? Loc.T(prefix + "OnlineNotRegistered")
                : Loc.T(prefix + (local.HasValue && InitialDOnlineHelper.SamePcbId(local.Value.PcbId, pcb)
                    ? "OnlineRegisteredHere" : "OnlineRegisteredElsewhere")) + " " + pcb + " " + status + " " + last
            : Loc.T(prefix + "ManualNotLoggedIn")));
        var visibility = new ComboBox { ItemsSource = new[] { "public", "own", "off" }, PlaceholderText = Loc.T("InitialDOnlineChooseVisibilityFirst") };
        var supporter = !kizuna && _accountProfile?.InitialDRank is >= 1 and <= 5;
        if (supporter)
        {
            panel.Children.Add(visibility);
            var preview = Text(rank?.Display ?? "");
            visibility.SelectionChanged += (_, _) => preview.Text =
                (visibility.SelectedItem as string) == "off" ? "" : rank?.Display ?? "";
            panel.Children.Add(preview);
        }
        var actions = new WrapPanel { IsVisible = known };
        actions.Children.Add(ActionButton("InitialDOnlineRegisterButton", async () =>
        {
            if (supporter && visibility.SelectedItem == null) { _cardMessage.Text = Loc.T("InitialDOnlineChooseVisibilityFirst"); return; }
            if (kizuna)
            {
                var result = await KizunaApi.ProvisionAsync(Environment.MachineName);
                _cardMessage.Text = result.Ok ? Saved(KizunaOnlineHelper.StoreCredential(result.Value.PcbId, result.Value.Secret), true)
                    : KizunaOnlineHelper.DescribeError(result);
            }
            else
            {
                var result = await InitialDApi.ProvisionAsync(Environment.MachineName, supporter
                    ? new InitialDOnlineHelper.ConsentChoice { Visibility = visibility.SelectedItem as string } : null);
                _cardMessage.Text = result.Ok ? Saved(InitialDOnlineHelper.StoreCredential(result.Value.PcbId, result.Value.Secret))
                    : InitialDOnlineHelper.DescribeError(result);
            }
        }));
        actions.Children[0].IsVisible = pcb == null;
        foreach (var operation in new[] { "UseHere", "Regenerate", "Remove" })
        {
            var action = operation;
            var button = ActionButton("InitialDOnline" + action + "Button", async () =>
            {
                if (await Dialogs.ChooseAsync(this, Loc.T(prefix + "OnlineTitle"), Loc.T(prefix + "OnlineConfirm" + action),
                    new[] { Loc.T("Yes", "Yes"), Loc.T("Cancel", "Cancel") }) != 0) return;
                if (kizuna)
                {
                    if (action == "Remove")
                    {
                        var result = await KizunaApi.RevokeAsync(pcb!);
                        _cardMessage.Text = result.Ok ? Removed(KizunaOnlineHelper.ClearCredential(pcb!), true) : KizunaOnlineHelper.DescribeError(result);
                    }
                    else
                    {
                        var result = action == "UseHere" ? await KizunaApi.GetCredentialAsync() : await KizunaApi.RegenerateAsync(pcb!);
                        _cardMessage.Text = result.Ok ? Saved(KizunaOnlineHelper.StoreCredential(result.Value.PcbId, result.Value.Secret), true) : KizunaOnlineHelper.DescribeError(result);
                    }
                }
                else if (action == "Remove")
                {
                    var result = await InitialDApi.RevokeAsync(pcb!);
                    _cardMessage.Text = result.Ok ? Removed(InitialDOnlineHelper.ClearCredential(pcb!)) : InitialDOnlineHelper.DescribeError(result);
                }
                else
                {
                    var result = action == "UseHere" ? await InitialDApi.GetCredentialAsync() : await InitialDApi.RegenerateAsync(pcb!);
                    _cardMessage.Text = result.Ok ? Saved(InitialDOnlineHelper.StoreCredential(result.Value.PcbId, result.Value.Secret)) : InitialDOnlineHelper.DescribeError(result);
                }
            });
            button.IsVisible = pcb != null;
            actions.Children.Add(button);
        }
        if (supporter && pcb != null)
            actions.Children.Add(ActionButton("InitialDOnlineSaveVisibility", async () =>
            {
                if (visibility.SelectedItem is not string choice) return;
                var result = await InitialDApi.SaveConsentAsync(new InitialDOnlineHelper.ConsentChoice { Visibility = choice });
                _cardMessage.Text = result.Ok ? Loc.T("InitialDOnlineVisibilitySaved") : InitialDOnlineHelper.DescribeError(result);
            }));
        panel.Children.Add(actions);
        var id = new TextBox { Text = local?.PcbId ?? "", PlaceholderText = Loc.T("InitialDOnlinePcbId") };
        var secret = new TextBox { PasswordChar = '●', PlaceholderText = Loc.T(prefix + "ManualSecret") };
        id.TextChanged += (_, _) =>
        {
            var parsed = kizuna ? KizunaOnlineHelper.ReadPasted(id.Text) : InitialDOnlineHelper.ReadPasted(id.Text);
            if (!string.IsNullOrEmpty(parsed.Secret) && parsed.PcbId != id.Text)
            { id.Text = parsed.PcbId; secret.Text = parsed.Secret; }
        };
        panel.Children.Add(id); panel.Children.Add(secret);
        panel.Children.Add(Text(Loc.T(prefix + "ManualHint")));
        var manual = new WrapPanel();
        manual.Children.Add(ActionButton(prefix + "ManualSave", () =>
        {
            var normalized = kizuna ? KizunaOnlineHelper.NormalizePcbId(id.Text) : InitialDOnlineHelper.NormalizePcbId(id.Text);
            if (normalized == null || !kizuna && normalized.StartsWith(KizunaOnlineHelper.PcbIdPrefix, StringComparison.Ordinal) ||
                kizuna && normalized.StartsWith(KizunaOnlineHelper.InitialDPcbIdPrefix, StringComparison.Ordinal))
            { _cardMessage.Text = Loc.T(prefix + "ManualBadId"); return Task.CompletedTask; }
            if (!(kizuna ? KizunaOnlineHelper.IsValidSecret(secret.Text) : InitialDOnlineHelper.IsValidSecret(secret.Text)))
            { _cardMessage.Text = Loc.T(prefix + "ManualBadSecret"); return Task.CompletedTask; }
            _cardMessage.Text = Saved(kizuna ? KizunaOnlineHelper.StoreCredential(normalized, secret.Text!)
                : InitialDOnlineHelper.StoreCredential(normalized, secret.Text!), kizuna);
            id.Text = normalized; secret.Clear();
            return Task.CompletedTask;
        }));
        manual.Children.Add(ActionButton(prefix + "ManualClear", () =>
        {
            var current = kizuna ? KizunaOnlineHelper.LocalCredential() : InitialDOnlineHelper.LocalCredential();
            var key = current?.PcbId ?? id.Text ?? "";
            _cardMessage.Text = Removed(kizuna ? KizunaOnlineHelper.ClearCredential(key) : InitialDOnlineHelper.ClearCredential(key), kizuna);
            id.Clear(); secret.Clear(); return Task.CompletedTask;
        }));
        manual.Children.Add(ActionButton(prefix + "ManualOpenWebsite", async () =>
            await ExternalUrlLauncher.OpenAsync(this, kizuna ? KizunaOnlineHelper.ProfilePageUrl : InitialDOnlineHelper.ProfilePageUrl)));
        panel.Children.Add(manual);
        if (!kizuna)
        {
            var latest = InitialDOnlineHelper.ReadNewestStatus();
            panel.Children.Add(Text(latest == null ? Loc.T("InitialDOnlineNoStatus") :
                string.Format(Loc.T("InitialDOnlineLastStatus"), latest.Title, latest.WrittenUtc.ToLocalTime().ToString("g"), InitialDOnlineHelper.Describe(latest))));
        }
        OnlineCards.Children.Add(Card(panel));
    }

    private static string Saved(int count, bool kizuna = false) => string.Format(Loc.T(kizuna ? "KizunaOnlineSaved" : "InitialDOnlineSaved"), count);
    private static string Removed(int _, bool kizuna = false) => Loc.T(kizuna ? "KizunaOnlineRemoved" : "InitialDOnlineRemoved");
}
