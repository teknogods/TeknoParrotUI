using Avalonia.Controls;
using Avalonia.Input;
using System.Linq;
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
    private static MainWindow? _instance;

    public MainWindow()
    {
        InitializeComponent();

        _instance = this;
        TeknoParrotUi.Common.Proton.LinuxDisplayResolver.AvaloniaScreenProvider = GetCurrentScreenPixelSize;

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

    /// <summary>
    /// Physical pixel bounds of the monitor currently containing this window -
    /// feeds TeknoParrotUi.Common.Proton.LinuxDisplayResolver so the Gamescope
    /// fullscreen-scaling wrapper (Linux only) targets the correct monitor
    /// instead of a combined multi-monitor desktop size. Avalonia's
    /// Screen.Bounds is already device/physical pixels, not logical/DIP
    /// units - deliberately NOT multiplied by Scaling again. Re-evaluated on
    /// every call (never cached here) so moving the window to another
    /// monitor or changing resolution between launches is picked up
    /// automatically - see LinuxDisplayResolver's class docs.
    /// </summary>
    private static (int Width, int Height)? GetCurrentScreenPixelSize()
    {
        var window = _instance;
        var screens = window?.Screens?.All;
        if (screens == null || screens.Count == 0)
            return null;

        var position = window!.Position;
        global::Avalonia.Platform.Screen? match = null;
        foreach (var screen in screens)
        {
            if (screen.Bounds.Contains(position))
            {
                match = screen;
                break;
            }
        }
        match ??= screens.FirstOrDefault(s => s.IsPrimary) ?? screens[0];
        return (match.Bounds.Width, match.Bounds.Height);
    }

    private void ToggleFullscreen() =>
        WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;

    /// <summary>Opens the TeknoParrot Online page (used by --tponline and deep links).</summary>
    public void NavigateToTpo() => Root.NavigateToTpo();
}
