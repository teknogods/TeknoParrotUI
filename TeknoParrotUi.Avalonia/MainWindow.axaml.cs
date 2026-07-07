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
    private readonly UiNavigationService _uiNav = new();

    public MainWindow()
    {
        InitializeComponent();

        JoystickHelper.DeSerialize();

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
        Title = $"TeknoParrot UI {version}";
        UpdateSubscriptionBadge();

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
        _scanner.BackRequested += ShowLibrary;
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
            Show(_settings, "Settings");
            SetActiveNav(NavSettings);
        };
        _multiButton.Applied += count => StatusBar.Text = $"Applied bindings to {count} game(s)";

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

        Show(_library, "Library");
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

    private void Show(Control view, string title)
    {
        ContentHost.Content = view;
        PageTitle.Text = title;
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
        Show(_library, "Library");
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
        Show(_tpo, "TeknoParrot Online");
        SetActiveNav(NavOnline);
    }

    private void NavUpdates_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_updates, "Updates");
        SetActiveNav(NavUpdates);
    }

    private void NavMods_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_mods, "Mods");
        SetActiveNav(NavMods);
    }

    private void NavSubscription_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_subscription, "Subscription");
        SetActiveNav(NavSubscription);
    }

    private void NavAccount_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_account, "Account");
        SetActiveNav(NavAccount);
    }

    private void NavSettings_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_settings, "Settings");
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
        Show(_about, "About");
        SetActiveNav(NavAbout);
    }
}
