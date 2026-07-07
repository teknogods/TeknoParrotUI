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

    public MainWindow()
    {
        InitializeComponent();

        JoystickHelper.DeSerialize();

        _library.GameSettingsRequested += profile =>
        {
            _gameSettings.LoadProfile(profile);
            ContentHost.Content = _gameSettings;
        };
        _gameSettings.BackRequested += () =>
        {
            ContentHost.Content = _library;
            _library.Refresh();
        };
        _gameSettings.Saved += name => StatusBar.Text = $"Saved settings for {name}";
        _settings.SavedNotification += () => StatusBar.Text = "Settings saved";

        ContentHost.Content = _library;
    }

    private void NavLibrary_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        ContentHost.Content = _library;
        _library.Refresh();
    }

    private void NavSettings_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        ContentHost.Content = _settings;

    private void NavAbout_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        ContentHost.Content = _about;
}
