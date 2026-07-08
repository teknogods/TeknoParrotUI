using Avalonia.Controls;
using Avalonia.Input;
using TeknoParrotUi.Avalonia.Services;
using TeknoParrotUi.Avalonia.Views;

namespace TeknoParrotUi.Avalonia;

/// <summary>
/// Thin desktop shell around <see cref="MainView"/> (which contains the entire
/// application UI so it can also run under the Android single-view lifetime).
/// This window owns only desktop concerns: the title/version, fullscreen
/// toggling (F11 / Alt+Enter / controller nav), start-fullscreen option and
/// shutting the shared view down on close.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
        Title = $"TeknoParrot UI {version}";

        // Window-only concerns surfaced by the shared view
        Root.FullscreenToggleRequested += ToggleFullscreen;
        Root.CloseRequested += Close;

        if (UiOptions.Load().StartFullscreen)
            WindowState = WindowState.FullScreen;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.F11 || (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
            {
                ToggleFullscreen();
                e.Handled = true;
            }
        };

        Closed += (_, _) => Root.Shutdown();
    }

    private void ToggleFullscreen() =>
        WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;

    /// <summary>Opens the TeknoParrot Online page (used by --tponline and deep links).</summary>
    public void NavigateToTpo() => Root.NavigateToTpo();
}
