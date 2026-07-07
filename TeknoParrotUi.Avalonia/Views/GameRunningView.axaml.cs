using System;
using Avalonia.Controls;
using Avalonia.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;

namespace TeknoParrotUi.Avalonia.Views;

public partial class GameRunningView : UserControl
{
    private GameSession? _session;

    public event Action? BackRequested;

    /// <summary>Raised with the exit code when the game process ends (CLI mode auto-close).</summary>
    public event Action<int>? GameExited;

    public GameRunningView()
    {
        InitializeComponent();
    }

    public void StartGame(GameProfile profile, bool testMode, bool emuOnly = false)
    {
        _session?.Dispose();
        ConsoleText.Text = "";
        Header.Text = (profile.GameNameInternal ?? profile.ProfileName) + (emuOnly ? " (emulator only)" : "");
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
            if (code != 0)
                StatusText.Text = $"Game stopped (exit code {code})";
            GameExited?.Invoke(code);
        });

        if (!_session.Start())
        {
            BtnForceQuit.IsEnabled = false;
            BtnBack.IsEnabled = true;
        }
    }

    private void BtnForceQuit_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        _session?.ForceQuit();

    private void BtnBack_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _session?.Dispose();
        _session = null;
        BackRequested?.Invoke();
    }
}
