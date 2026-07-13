using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TeknoParrotUi.Avalonia.Services;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Views;

/// <summary>
/// The whole application shell (navigation, page hosting, status bar, wizard,
/// privacy gate, controller UI navigation). Extracted from MainWindow so the
/// same UI runs under both Avalonia lifetimes:
/// - desktop: hosted in <see cref="MainWindow"/> (classic desktop lifetime)
/// - Android: assigned to ISingleViewApplicationLifetime.MainView
///
/// Window-only concerns (fullscreen, closing) are surfaced as events that the
/// desktop host wires to the real Window; on Android they are no-ops.
/// </summary>
public partial class MainView : UserControl
{
    private readonly LibraryView _library = new();
    private readonly SettingsView _settings = new();
    private readonly AboutView _about = new();
    private readonly GameSettingsView _gameSettings = new();
    private readonly JoystickSetupView _joystickSetup = new();
    private readonly AddGameView _addGame = new();
    private readonly UpdatesView _updates = new();
    private readonly AccountView _account = new();
    private readonly VerifyGameView _verify = new();
    private readonly GameScannerView _scanner = new();
    private readonly ModsView _mods = new();
    private readonly TpoView _tpo = new();
    private readonly GameRunningView _gameRunning = new();
    private readonly SubscriptionView _subscription = new();
    private readonly MultiButtonConfigView _multiButton = new();
    private readonly UiOptionsView _uiOptions = new();
    private readonly SetupWizardView _wizard = new();
    private readonly LinuxSetupView _linuxSetup = new();
    private readonly UiNavigationService _uiNav = new();

    private static bool WizardActive => !Lazydata.ParrotData.FirstTimeSetupComplete;

    /// <summary>Raised when the user (or controller nav) asks to toggle fullscreen. Desktop-only concern.</summary>
    public event Action? FullscreenToggleRequested;

    /// <summary>Raised when the app should close (privacy policy declined). Desktop-only concern.</summary>
    public event Action? CloseRequested;

    public MainView()
    {
        InitializeComponent();

        JoystickHelper.DeSerialize();

        NavLinuxSetup.IsVisible = OperatingSystem.IsLinux();

        UpdateSubscriptionBadge();
        LocalizeChrome();
        Loc.LanguageChanged += () =>
        {
            LocalizeChrome();
            UpdateSubscriptionBadge();
        };

        _library.GameSettingsRequested += profile =>
        {
            _gameSettings.LoadProfile(profile);
            Show(_gameSettings, "Game Settings");
        };
        _library.ControlsSetupRequested += profile =>
        {
            _joystickSetup.LoadProfile(profile);
            Show(_joystickSetup, "Controls");
        };
        _library.VerifyRequested += profile =>
        {
            Show(_verify, "Verify Files");
            _verify.StartVerification(profile);
        };
        _library.AddGameRequested += () =>
        {
            _addGame.Refresh();
            Show(_addGame, "Add Game");
        };
        _library.ScannerRequested += () => Show(_scanner, "Game Scanner");
        _library.NativeLaunchRequested += (profile, testMode) =>
        {
            Show(_gameRunning, "Game Running");
            _gameRunning.StartGame(profile, testMode);
        };

        _gameSettings.BackRequested += ShowLibrary;
        _gameSettings.Saved += name => StatusBar.Text = $"Saved settings for {name}";
        _joystickSetup.BackRequested += ShowLibrary;
        _joystickSetup.Saved += name => StatusBar.Text = $"Saved controls for {name}";
        _addGame.BackRequested += ShowLibrary;
        _addGame.GameAdded += profile =>
        {
            StatusBar.Text = $"Added {profile.GameNameInternal ?? profile.ProfileName} — set the game path";
            _gameSettings.LoadProfile(profile);
            Show(_gameSettings, "Game Settings");
        };
        _verify.BackRequested += ShowLibrary;
        _scanner.BackRequested += () =>
        {
            if (WizardActive) ShowWizard();
            else ShowLibrary();
        };
        _scanner.GamesAdded += count => StatusBar.Text = $"Game scanner added {count} game(s)";
        _gameRunning.BackRequested += ShowLibrary;
        _gameRunning.GameExited += _ =>
        {
            // Return to the library (same game still selected) once the game stops
            if (ContentHost.Content == _gameRunning)
            {
                ShowLibrary();
                StatusBar.Text = "Game session ended";
            }
        };
        _settings.SavedNotification += () => StatusBar.Text = "Settings saved";
        _settings.MultiButtonConfigRequested += () =>
        {
            _multiButton.Refresh();
            Show(_multiButton, "Multi-Game Button Config");
        };
        _multiButton.BackRequested += () =>
        {
            if (WizardActive)
            {
                ShowWizard();
                return;
            }
            Show(_settings, "Settings");
            SetActiveNav(NavSettings);
        };
        _multiButton.Applied += count => StatusBar.Text = $"Applied bindings to {count} game(s)";

        // First-time setup wizard
        _wizard.ScannerRequested += () => Show(_scanner, "Game Scanner");
        _wizard.ButtonConfigRequested += () =>
        {
            _multiButton.Refresh();
            Show(_multiButton, "Multi-Game Button Config");
        };
        _wizard.AccountRequested += () => Show(_account, "Account");
        _wizard.SubscriptionRequested += () => Show(_subscription, "Subscription");
        _wizard.Finished += () =>
        {
            StatusBar.Text = "Setup complete — welcome to TeknoParrot!";
            ShowLibrary();
        };

        // Privacy policy gate (first run) — after we're attached so dialogs have an owner
        bool policiesShown = false;
        AttachedToVisualTree += async (_, _) =>
        {
            if (policiesShown)
                return;
            policiesShown = true;
            await ShowPoliciesGateAsync();
            await ShowPendingChangelogAsync();
        };

        // Player-configurable controller navigation (fullscreen is delegated to the host)
        _uiOptions.Saved += options =>
        {
            StatusBar.Text = "UI options saved";
            _uiNav.Restart(options);
            ApplyUiScale(options.UiScale);
        };
        // Defer the live preview: applying a layout transform synchronously
        // inside the ComboBox SelectionChanged (while its popup is closing)
        // crashes the layout pass — apply after the event has fully unwound.
        _uiOptions.TextSizePreview += scale =>
            Dispatcher.UIThread.Post(() => ApplyUiScale(scale), DispatcherPriority.Background);
        _uiNav.ActionTriggered += action => Dispatcher.UIThread.Post(() => PerformNavAction(action));
        var startupOptions = UiOptions.Load();
        _uiNav.Restart(startupOptions);
        ApplyUiScale(startupOptions.UiScale);

        if (WizardActive)
            ShowWizard();
        else
            Show(_library, "Library");
    }

    /// <summary>Stops background services (controller nav). Called by the desktop host on window close.</summary>
    public void Shutdown() => _uiNav.Dispose();

    /// <summary>
    /// Accessibility text-size zoom: scales the whole shell (fonts, icons and
    /// spacing together, like browser zoom) via a layout transform — CPU-cheap
    /// and works with every view without per-view font plumbing.
    /// </summary>
    private void ApplyUiScale(double scale)
    {
        scale = double.IsFinite(scale) ? Math.Clamp(scale, 1.0, 2.0) : 1.0;
        // Always assign a concrete transform (identity at 100%) — a null
        // LayoutTransform is not tolerated on every LayoutTransformControl path.
        RootScale.LayoutTransform = new global::Avalonia.Media.ScaleTransform(scale, scale);
    }

    private void ShowWizard()
    {
        _wizard.ReturnFromStep();
        Show(_wizard, "First-Time Setup");
    }

    /// <summary>
    /// Shows the privacy notice on first run — Accept continues (flag persisted),
    /// Quit closes the app. Same gate as the classic UI. On desktop this is a
    /// modal dialog; on single-view platforms (Android) it takes over the page
    /// host until answered (separate Windows do not exist there).
    /// </summary>
    private async System.Threading.Tasks.Task ShowPoliciesGateAsync()
    {
        if (Lazydata.ParrotData.HasReadPoliciesNew)
            return;

        var accept = new Button { Content = Loc.T("PoliciesAccept", "Accept"), MinWidth = 90, Classes = { "primary" } };
        var quit = new Button { Content = Loc.T("MainQuit", "Quit"), MinWidth = 90 };
        var link = new Button { Content = "View the policies at teknoparrot.com", Background = global::Avalonia.Media.Brushes.Transparent, Padding = new Thickness(0) };
        link.Click += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://teknoparrot.com/en/Home/Policies") { UseShellExecute = true });
            }
            catch { }
        };

        var body = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = Loc.T("AppPrivacyNoticeMessage", "TeknoParrotUI collects usage data to improve the software. By continuing, you agree to our privacy policy."),
                    TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
                },
                link,
                new StackPanel
                {
                    Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
                    Children = { quit, accept }
                }
            }
        };

        bool accepted = false;

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            // Desktop: classic modal dialog
            var dialog = new Window
            {
                Title = Loc.T("AppPrivacyNoticeTitle", "Privacy Notice"),
                Width = 440,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = body
            };
            accept.Click += (_, _) => { accepted = true; dialog.Close(); };
            quit.Click += (_, _) => dialog.Close();
            await dialog.ShowDialog(owner);
        }
        else
        {
            // Single-view (Android): occupy the page host until answered
            var previous = ContentHost.Content;
            var done = new System.Threading.Tasks.TaskCompletionSource();
            accept.Click += (_, _) => { accepted = true; done.TrySetResult(); };
            quit.Click += (_, _) => done.TrySetResult();
            ContentHost.Content = new Border { Child = body, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };
            await done.Task;
            ContentHost.Content = previous;
        }

        if (accepted)
        {
            Lazydata.ParrotData.HasReadPoliciesNew = true;
            JoystickHelper.Serialize();
        }
        else
        {
            CloseRequested?.Invoke();
        }
    }

    /// <summary>
    /// After a self-update restart (ParrotPatcher relaunches TeknoParrotUI once it
    /// finishes extracting), a ".lastupdate" marker sits next to the executable
    /// (component|version|base64-changelog — see UpdaterCore.LaunchSelfUpdate).
    /// Show a "what's new" popup with the release notes, then delete the marker.
    /// </summary>
    private async System.Threading.Tasks.Task ShowPendingChangelogAsync()
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, ".lastupdate");
        if (!System.IO.File.Exists(path))
            return;

        var entries = new List<(string Name, string Version, string Body)>();
        try
        {
            foreach (var line in System.IO.File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('|');
                if (parts.Length < 2) continue;

                string body = null;
                if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]))
                {
                    try { body = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[2])); }
                    catch { /* ignore malformed changelog payload */ }
                }
                entries.Add((parts[0], parts[1], body));
            }
        }
        catch { /* ignore unreadable marker */ }
        finally
        {
            try { System.IO.File.Delete(path); } catch { /* ignore */ }
        }

        if (entries.Count == 0 || TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var list = new StackPanel { Spacing = 16 };
        foreach (var entry in entries)
        {
            var header = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            header.Children.Add(new TextBlock { Text = entry.Name, FontWeight = global::Avalonia.Media.FontWeight.Bold, FontSize = 16 });
            header.Children.Add(new TextBlock { Text = entry.Version, Opacity = 0.7, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center });

            var card = new Border
            {
                BorderBrush = global::Avalonia.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new global::Avalonia.CornerRadius(6),
                Padding = new Thickness(16),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        header,
                        new TextBlock
                        {
                            Text = string.IsNullOrWhiteSpace(entry.Body)
                                ? Loc.T("ChangelogNoInformation", "No changelog information available.")
                                : entry.Body,
                            Opacity = string.IsNullOrWhiteSpace(entry.Body) ? 0.6 : 1.0,
                            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
                        }
                    }
                }
            };
            list.Children.Add(card);
        }

        var closeButton = new Button { Content = Loc.T("OK", "OK"), MinWidth = 90, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right, Classes = { "primary" } };
        var dialog = new Window
        {
            Title = Loc.T("ChangelogTitle", "What's New"),
            Width = 520,
            Height = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new DockPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    closeButton,
                    new ScrollViewer { Content = list }
                }
            }
        };
        DockPanel.SetDock(closeButton, global::Avalonia.Controls.Dock.Bottom);
        closeButton.Margin = new Thickness(0, 16, 0, 0);
        closeButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }

    private void PerformNavAction(UiNavAction action)
    {
        switch (action)
        {
            case UiNavAction.ToggleFullscreen:
                FullscreenToggleRequested?.Invoke();
                break;
            case UiNavAction.Back:
                ShowLibrary();
                break;
            case UiNavAction.Confirm:
                ActivateFocused();
                break;
            default:
                MoveFocus(action);
                break;
        }
    }

    private void ActivateFocused()
    {
        var focusManager = TopLevel.GetTopLevel(this)?.FocusManager;
        switch (focusManager?.GetFocusedElement())
        {
            case ToggleButton toggle: // CheckBox, ToggleSwitch, ...
                toggle.IsChecked = !(toggle.IsChecked ?? false);
                break;
            case Button button:
                button.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                break;
            case ComboBox combo:
                combo.IsDropDownOpen = !combo.IsDropDownOpen;
                break;
            case Expander expander:
                expander.IsExpanded = !expander.IsExpanded;
                break;
        }
    }

    private void MoveFocus(UiNavAction direction)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;

        // Inside a list, up/down move the selection
        var listBox = (focused as ListBoxItem)?.FindAncestorOfType<ListBox>() ?? focused as ListBox;
        if (listBox != null && direction is UiNavAction.Up or UiNavAction.Down && listBox.ItemCount > 0)
        {
            var index = listBox.SelectedIndex + (direction == UiNavAction.Down ? 1 : -1);
            if (index >= 0 && index < listBox.ItemCount)
            {
                listBox.SelectedIndex = index;
                listBox.ScrollIntoView(index);
                return;
            }
            // at the ends, fall through so focus can leave the list
        }

        var candidates = new List<Control>();
        CollectFocusable(this, candidates);
        if (candidates.Count == 0)
            return;

        if (focused == null || !candidates.Contains(focused))
        {
            candidates[0].Focus(NavigationMethod.Directional);
            return;
        }

        // Nearest focusable control in the requested direction
        var origin = Center(focused);
        Control? best = null;
        var bestScore = double.MaxValue;
        foreach (var candidate in candidates)
        {
            if (candidate == focused)
                continue;
            var point = Center(candidate);
            double dx = point.X - origin.X, dy = point.Y - origin.Y;
            double forward, sideways;
            switch (direction)
            {
                case UiNavAction.Up: forward = -dy; sideways = System.Math.Abs(dx); break;
                case UiNavAction.Down: forward = dy; sideways = System.Math.Abs(dx); break;
                case UiNavAction.Left: forward = -dx; sideways = System.Math.Abs(dy); break;
                default: forward = dx; sideways = System.Math.Abs(dy); break;
            }
            if (forward < 1)
                continue;
            var score = forward + sideways * 2.5;
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        best?.Focus(NavigationMethod.Directional);
        if (best is ListBoxItem item)
            item.FindAncestorOfType<ListBox>()?.ScrollIntoView(item.DataContext!);
    }

    private global::Avalonia.Point Center(Control control) =>
        control.TranslatePoint(new global::Avalonia.Point(control.Bounds.Width / 2, control.Bounds.Height / 2), this)
        ?? new global::Avalonia.Point(0, 0);

    private static void CollectFocusable(global::Avalonia.Visual root, List<Control> result)
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is Control { Focusable: true, IsEffectivelyVisible: true, IsEffectivelyEnabled: true } control)
                result.Add(control);
            CollectFocusable(child, result);
        }
    }

    private Func<string>? _titleProvider;

    private void LocalizeChrome()
    {
        // Localized navigation labels (classic translation keys) — icons are
        // fixed PathIcons in XAML; only the text is localized.
        NavLibraryText.Text = Loc.T("MainLibrary", "Library");
        NavOnlineText.Text = Loc.T("MainTPOnlineNew", "TeknoParrot Online");
        NavUpdatesText.Text = Loc.T("MainCheckUpdates", "Updates");
        NavModsText.Text = Loc.T("MainMods", "Mods");
        NavSubscriptionText.Text = Loc.T("LibraryGenreSubscription", "Subscription");
        NavAccountText.Text = Loc.T("MainAccount", "Account");
        NavSettingsText.Text = Loc.T("MainSettings", "Settings");
        NavAboutText.Text = Loc.T("MainAbout", "About");
        NavLinuxSetupText.Text = Loc.T("MainLinuxSetup", "Linux Setup");
        if (_titleProvider != null)
            PageTitle.Text = _titleProvider();
    }

    private void Show(Control view, string title) => Show(view, () => Loc.T(title, title));

    private void Show(Control view, Func<string> titleProvider)
    {
        _titleProvider = titleProvider;
        ContentHost.Content = view;
        PageTitle.Text = titleProvider();
        // Don't fight binding editors for input while they're capturing
        _uiNav.Suspended = view is JoystickSetupView or MultiButtonConfigView or UiOptionsView;
        UpdateSubscriptionBadge();
    }

    /// <summary>Whether a Patreon/subscription serial key is registered (same check as the classic App.IsPatreon).</summary>
    public static bool IsPatreon()
    {
        if (!OperatingSystem.IsWindows())
            return false;
        try
        {
            using var tp = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot");
            return tp?.GetValue("PatreonSerialKey") != null;
        }
        catch
        {
            return false;
        }
    }

    private void UpdateSubscriptionBadge()
    {
        var subbed = IsPatreon();
        SubStatusText.Text = subbed ? "Subscribed" : "Free";
        // "badge gold" tints via theme tokens — correct in both light and dark
        if (subbed)
        {
            if (!SubBadge.Classes.Contains("gold"))
                SubBadge.Classes.Add("gold");
        }
        else
        {
            SubBadge.Classes.Remove("gold");
        }
    }

    /// <summary>Opens the TeknoParrot Online page (used by --tponline and deep links).</summary>
    public void NavigateToTpo()
    {
        Show(_tpo, "TeknoParrot Online");
        SetActiveNav(NavOnline);
    }

    private void ShowLibrary()
    {
        Show(_library, "MainLibrary");
        _library.Refresh();
        SetActiveNav(NavLibrary);
    }

    private void SetActiveNav(Button active)
    {
        foreach (var button in new[] { NavLibrary, NavOnline, NavUpdates, NavMods, NavSubscription, NavAccount, NavSettings, NavUiOptions, NavAbout, NavLinuxSetup })
            button.Classes.Remove("active");
        active.Classes.Add("active");
    }

    private void BtnMenu_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        Sidebar.IsVisible = !Sidebar.IsVisible;

    private void NavLibrary_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShowLibrary();
    }

    private void NavOnline_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_tpo, "MainTPOnlineNew");
        SetActiveNav(NavOnline);
    }

    private void NavUpdates_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_updates, "MainCheckUpdates");
        SetActiveNav(NavUpdates);
    }

    private void NavMods_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_mods, "MainMods");
        SetActiveNav(NavMods);
    }

    private void NavSubscription_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_subscription, "Subscription");
        SetActiveNav(NavSubscription);
    }

    private void NavAccount_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_account, "MainAccount");
        SetActiveNav(NavAccount);
    }

    private void NavSettings_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_settings, "MainSettings");
        SetActiveNav(NavSettings);
    }

    private void NavUiOptions_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _uiOptions.Refresh();
        Show(_uiOptions, "UI Options");
        SetActiveNav(NavUiOptions);
    }

    private void NavAbout_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_about, "MainAbout");
        SetActiveNav(NavAbout);
    }

    private void NavLinuxSetup_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_linuxSetup, "Linux Setup");
        SetActiveNav(NavLinuxSetup);
    }

    private void NavExit_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShowExitConfirmationAsync();
    }

    private async void ShowExitConfirmationAsync()
    {
        var noButton = new Button { Content = Loc.T("AppNo", "No") };
        var yesButton = new Button { Content = Loc.T("AppYes", "Yes") };

        var body = new Border
        {
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = Loc.T("AppExitConfirmTitle", "Exit TeknoParrot?"),
                        FontSize = 16,
                        FontWeight = FontWeight.Bold,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.Parse("#FFFFFF"))
                    },
                    new TextBlock
                    {
                        Text = Loc.T("AppExitConfirmMessage", "Do you really want to exit the application?"),
                        FontSize = 14,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.Parse("#CCCCCC"))
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
                        Children = { noButton, yesButton }
                    }
                }
            }
        };

        bool confirmed = false;

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            // Desktop: classic modal dialog
            var dialog = new Window
            {
                Title = Loc.T("AppExitConfirmTitle", "Exit TeknoParrot?"),
                Width = 400,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = body
            };
            noButton.Click += (_, _) => dialog.Close();
            yesButton.Click += (_, _) => { confirmed = true; dialog.Close(); };
            await dialog.ShowDialog(owner);
        }
        else
        {
            // Single-view (Android): occupy the page host until answered
            var previous = ContentHost.Content;
            var done = new System.Threading.Tasks.TaskCompletionSource();
            noButton.Click += (_, _) => done.TrySetResult();
            yesButton.Click += (_, _) => { confirmed = true; done.TrySetResult(); };
            ContentHost.Content = new Border { Child = body, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };
            await done.Task;
            ContentHost.Content = previous;
        }

        if (confirmed)
        {
            CloseRequested?.Invoke();
        }
    }
}
