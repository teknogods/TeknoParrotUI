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

    public MainWindow()
    {
        InitializeComponent();

        JoystickHelper.DeSerialize();

        _library.GameSettingsRequested += profile =>
        {
            _gameSettings.LoadProfile(profile);
            ContentHost.Content = _gameSettings;
        };
        _library.ControlsSetupRequested += profile =>
        {
            _joystickSetup.LoadProfile(profile);
            ContentHost.Content = _joystickSetup;
        };
        _library.VerifyRequested += profile =>
        {
            ContentHost.Content = _verify;
            _verify.StartVerification(profile);
        };
        _verify.BackRequested += ShowLibrary;
        _library.AddGameRequested += () =>
        {
            _addGame.Refresh();
            ContentHost.Content = _addGame;
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
            ContentHost.Content = _gameSettings;
        };
        _settings.SavedNotification += () => StatusBar.Text = "Settings saved";

        ContentHost.Content = _library;
    }

    private void ShowLibrary()
    {
        ContentHost.Content = _library;
        _library.Refresh();
    }

    private void NavLibrary_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ContentHost.Content = _library;
        _library.Refresh();
    }

    private void NavSettings_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        ContentHost.Content = _settings;

    private void NavUpdates_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        ContentHost.Content = _updates;

    private void NavAccount_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        ContentHost.Content = _account;

    private void NavAbout_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        ContentHost.Content = _about;
}
