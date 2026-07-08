using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TeknoParrotUi.Avalonia.Services;
using TeknoParrotUi.Avalonia.Views;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia;

public partial class MainWindow : Window
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
    private readonly UiNavigationService _uiNav = new();

    private static bool WizardActive => !Lazydata.ParrotData.FirstTimeSetupComplete;

    public MainWindow()
    {
        InitializeComponent();

        JoystickHelper.DeSerialize();

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
        Title = $"TeknoParrot UI {version}";
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

        // Privacy policy gate (first run)
        Opened += async (_, _) => await ShowPoliciesGateAsync();

        // Fullscreen + player-configurable controller navigation
        _uiOptions.Saved += options =>
        {
            StatusBar.Text = "UI options saved";
            _uiNav.Restart(options);
        };
        _uiNav.ActionTriggered += action => Dispatcher.UIThread.Post(() => PerformNavAction(action));
        var uiOptions = UiOptions.Load();
        _uiNav.Restart(uiOptions);
        if (uiOptions.StartFullscreen)
            WindowState = WindowState.FullScreen;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.F11 || (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
            {
                ToggleFullscreen();
                e.Handled = true;
            }
        };
        Closed += (_, _) => _uiNav.Dispose();

        if (WizardActive)
            ShowWizard();
        else
            Show(_library, "Library");
    }

    private void ShowWizard()
    {
        _wizard.ReturnFromStep();
        Show(_wizard, "First-Time Setup");
    }

    /// <summary>
    /// Shows the privacy notice on first run — Accept continues (flag persisted),
    /// Quit closes the app. Same gate as the classic UI.
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

        var dialog = new Window
        {
            Title = Loc.T("AppPrivacyNoticeTitle", "Privacy Notice"),
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
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
            }
        };

        bool accepted = false;
        accept.Click += (_, _) => { accepted = true; dialog.Close(); };
        quit.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);

        if (accepted)
        {
            Lazydata.ParrotData.HasReadPoliciesNew = true;
            JoystickHelper.Serialize();
        }
        else
        {
            Close();
        }
    }

    private void ToggleFullscreen() =>
        WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;

    private void PerformNavAction(UiNavAction action)
    {
        switch (action)
        {
            case UiNavAction.ToggleFullscreen:
                ToggleFullscreen();
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
        switch (FocusManager?.GetFocusedElement())
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
        var focused = FocusManager?.GetFocusedElement() as Control;

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
        // Localized navigation labels (classic translation keys)
        NavLibrary.Content = "🎮  " + Loc.T("MainLibrary", "Library");
        NavOnline.Content = "🌐  " + Loc.T("MainTPOnlineNew", "TeknoParrot Online");
        NavUpdates.Content = "⬇  " + Loc.T("MainCheckUpdates", "Updates");
        NavMods.Content = "🧩  " + Loc.T("MainMods", "Mods");
        NavSubscription.Content = "⭐  " + Loc.T("PatreonSubscriptionExclusiveGames", "Subscription").TrimEnd(':');
        NavAccount.Content = "👤  " + Loc.T("MainAccount", "Account");
        NavSettings.Content = "⚙  " + Loc.T("MainSettings", "Settings");
        NavAbout.Content = "ℹ  " + Loc.T("MainAbout", "About");
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
        SubStatusText.Text = subbed ? "⭐ Subscribed" : "Free";
        SubBadge.Background = subbed
            ? new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#50FFD54F"))
            : new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#30FFFFFF"));
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
        foreach (var button in new[] { NavLibrary, NavOnline, NavUpdates, NavMods, NavSubscription, NavAccount, NavSettings, NavUiOptions, NavAbout })
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
}
