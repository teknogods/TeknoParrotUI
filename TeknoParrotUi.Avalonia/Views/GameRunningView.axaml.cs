using System;
using Avalonia.Controls;
using Avalonia.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;

namespace TeknoParrotUi.Avalonia.Views;

public partial class GameRunningView : UserControl
{
    private GameSession? _session;
    private bool _forceQuitRequested;

    public event Action? BackRequested;

    /// <summary>Raised with the exit code when the game process ends (CLI mode auto-close).</summary>
    public event Action<int>? GameExited;

    public GameRunningView()
    {
        InitializeComponent();
        Localize();
        Services.Loc.LanguageChanged += Localize;
    }

    private void Localize()
    {
        BtnForceQuit.Content = Services.Loc.T("GameRunningForceQuit", "Force Quit Game");
        BtnBack.Content = Services.Loc.T("Back", "Back");
    }

    public void StartGame(GameProfile profile, bool testMode, bool emuOnly = false)
    {
        _session?.Dispose();
        _forceQuitRequested = false;
        ConsoleText.Text = "";
        Header.Text = (profile.GameNameInternal ?? profile.ProfileName) + (emuOnly ? " (emulator only)" : "");
        StatusText.ClearValue(TextBlock.ForegroundProperty);
        BtnForceQuit.IsEnabled = true;
        BtnBack.IsEnabled = false;

        _session = new GameSession(profile, testMode, emuOnly);
        _session.OutputReceived += line => Dispatcher.UIThread.Post(() =>
        {
            ConsoleText.Text += line + Environment.NewLine;
            ConsoleScroll.ScrollToEnd();
        });
        _session.StateChanged += state => Dispatcher.UIThread.Post(() => StatusText.Text = state);
        _session.Exited += code => Dispatcher.UIThread.Post(() =>
        {
            BtnForceQuit.IsEnabled = false;
            BtnBack.IsEnabled = true;
            if (code != 0 && !_forceQuitRequested)
            {
                // Error exit: stay on this screen so the user can read the log,
                // and return only when they press Back.
                StatusText.Text = string.Format(
                    Services.Loc.T("GameRunningExitedWithError", "The game exited with an error (exit code {0}) — press Back to return"), code);
                StatusText.Foreground = global::Avalonia.Media.Brushes.OrangeRed;
                BtnBack.Focus();
                return;
            }
            GameExited?.Invoke(code);
        });

        if (!_session.Start())
        {
            // Launch failed before the game process even started — the reason is
            // already in StatusText (via StateChanged); stay here until Back.
            BtnForceQuit.IsEnabled = false;
            BtnBack.IsEnabled = true;
            StatusText.Foreground = global::Avalonia.Media.Brushes.OrangeRed;
            BtnBack.Focus();
        }
    }

    private void BtnForceQuit_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _forceQuitRequested = true;
        _session?.ForceQuit();
    }

    private void BtnBack_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _session?.Dispose();
        _session = null;
        BackRequested?.Invoke();
    }
}
