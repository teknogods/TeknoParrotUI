using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using System;
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
        TeknoParrotUi.Common.Proton.LinuxDisplayResolver.AvaloniaScreenIdentityProvider = GetCurrentScreenIdentity;
        TeknoParrotUi.Common.Proton.LinuxDisplayResolver.AvaloniaScreenCountProvider = GetScreenCount;

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
    /// instead of a combined multi-monitor desktop size.
    ///
    /// THREAD SAFETY: this delegate can be invoked from a background thread
    /// (GameSession's launch pipeline runs off the UI thread) - Avalonia UI
    /// objects (Window, Screens, PixelRect, etc.) must only ever be touched
    /// on the UI thread, so every actual property read happens inside
    /// <see cref="CaptureCurrentScreenPixelSizeOnUiThread"/>, invoked via
    /// <see cref="Dispatcher.UIThread"/> - directly if already on the UI
    /// thread, or via a blocking <see cref="Dispatcher.Invoke(Action)"/> hop
    /// otherwise. This is the actual fix for the "Avalonia monitor access
    /// occurs on the UI thread" requirement - a heavier alternative (Avalonia
    /// resolving a snapshot up-front and threading it through GameSession/
    /// ProtonLauncher/GamescopeLauncher call signatures) was considered but
    /// is a much larger refactor of the existing launch pipeline; this
    /// dispatcher-hop approach closes the actual thread-safety bug with a
    /// minimal, low-risk change.
    /// </summary>
    private static (int Width, int Height)? GetCurrentScreenPixelSize()
    {
        return Dispatcher.UIThread.CheckAccess()
            ? CaptureCurrentScreenPixelSizeOnUiThread()
            : Dispatcher.UIThread.Invoke(CaptureCurrentScreenPixelSizeOnUiThread);
    }

    /// <summary>
    /// Companion to <see cref="GetCurrentScreenPixelSize"/> - same UI-thread
    /// dispatch, same monitor SELECTION as that method (largest overlap ->
    /// center -> primary -> first), but returns identity/position/scaling
    /// instead of just width/height. See
    /// <see cref="TeknoParrotUi.Common.Proton.LinuxDisplayResolver.AvaloniaScreenIdentityProvider"/>.
    /// </summary>
    private static TeknoParrotUi.Common.Proton.AvaloniaScreenIdentity? GetCurrentScreenIdentity()
    {
        return Dispatcher.UIThread.CheckAccess()
            ? CaptureCurrentScreenIdentityOnUiThread()
            : Dispatcher.UIThread.Invoke(CaptureCurrentScreenIdentityOnUiThread);
    }

    /// <summary>
    /// Monitor selection order (never just the window's top-left corner):
    ///   1. The screen with the largest overlap area with this window.
    ///   2. If no screen overlaps at all, the screen containing the window's center point.
    ///   3. The primary screen.
    ///   4. The first available screen.
    /// Avalonia's Screen.Bounds is already physical/device pixels; the
    /// window's own ClientSize (DIPs) is converted to physical pixels via
    /// ITS OWN RenderScaling exactly once (never multiplied twice).
    /// Re-evaluated fresh on every call (never cached) so moving the window
    /// to another monitor or changing resolution between launches is picked
    /// up automatically - see LinuxDisplayResolver's class docs.
    /// </summary>
    private static (global::Avalonia.Platform.Screen Screen, TeknoParrotUi.Common.Proton.DisplaySelectionReason Reason)? SelectTargetScreenOnUiThread()
    {
        var window = _instance;
        var screens = window?.Screens?.All;
        if (window == null || screens == null || screens.Count == 0)
            return null;

        var windowSize = global::Avalonia.PixelSize.FromSize(window.ClientSize, window.RenderScaling);
        var windowRect = new global::Avalonia.PixelRect(window.Position, windowSize);

        global::Avalonia.Platform.Screen? best = null;
        long bestOverlapArea = 0;
        foreach (var screen in screens)
        {
            var overlap = windowRect.Intersect(screen.Bounds);
            long area = (long)Math.Max(0, overlap.Width) * Math.Max(0, overlap.Height);
            if (area > bestOverlapArea)
            {
                bestOverlapArea = area;
                best = screen;
            }
        }
        if (best != null)
            return (best, TeknoParrotUi.Common.Proton.DisplaySelectionReason.LargestOverlap);

        var center = windowRect.Center;
        best = screens.FirstOrDefault(s => s.Bounds.Contains(center));
        if (best != null)
            return (best, TeknoParrotUi.Common.Proton.DisplaySelectionReason.WindowCenterFallback);

        best = screens.FirstOrDefault(s => s.IsPrimary);
        if (best != null)
            return (best, TeknoParrotUi.Common.Proton.DisplaySelectionReason.PrimaryFallback);

        return (screens[0], TeknoParrotUi.Common.Proton.DisplaySelectionReason.FirstAvailableFallback);
    }

    private static (int Width, int Height)? CaptureCurrentScreenPixelSizeOnUiThread()
    {
        var selection = SelectTargetScreenOnUiThread();
        return selection == null ? null : (selection.Value.Screen.Bounds.Width, selection.Value.Screen.Bounds.Height);
    }

    private static TeknoParrotUi.Common.Proton.AvaloniaScreenIdentity? CaptureCurrentScreenIdentityOnUiThread()
    {
        var selection = SelectTargetScreenOnUiThread();
        if (selection == null)
            return null;
        var screen = selection.Value.Screen;
        return new TeknoParrotUi.Common.Proton.AvaloniaScreenIdentity
        {
            Identifier = string.IsNullOrEmpty(screen.DisplayName) ? null : screen.DisplayName,
            X = screen.Bounds.X,
            Y = screen.Bounds.Y,
            Scaling = screen.Scaling,
            SelectionReason = selection.Value.Reason
        };
    }

    private static int GetScreenCount()
    {
        return Dispatcher.UIThread.CheckAccess() ? CaptureScreenCountOnUiThread() : Dispatcher.UIThread.Invoke(CaptureScreenCountOnUiThread);
    }

    private static int CaptureScreenCountOnUiThread() => _instance?.Screens?.All?.Count ?? 0;

    private void ToggleFullscreen() =>
        WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;

    /// <summary>Opens the TeknoParrot Online page (used by --tponline and deep links).</summary>
    public void NavigateToTpo() => Root.NavigateToTpo();
}
