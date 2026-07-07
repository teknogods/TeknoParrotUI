using System.Linq;
using Avalonia.Controls;
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

    public MainWindow()
    {
        InitializeComponent();

        JoystickHelper.DeSerialize();

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

        Show(_library, "Library");
    }

    private void Show(Control view, string title)
    {
        ContentHost.Content = view;
        PageTitle.Text = title;
    }

    private void ShowLibrary()
    {
        Show(_library, "Library");
        _library.Refresh();
        SetActiveNav(NavLibrary);
    }

    private void SetActiveNav(Button active)
    {
        foreach (var button in new[] { NavLibrary, NavOnline, NavUpdates, NavMods, NavSubscription, NavAccount, NavSettings, NavAbout })
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

    private void NavAbout_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Show(_about, "About");
        SetActiveNav(NavAbout);
    }
}
