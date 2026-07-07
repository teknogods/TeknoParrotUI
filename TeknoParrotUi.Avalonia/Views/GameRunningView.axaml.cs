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

    public GameRunningView()
    {
        InitializeComponent();
    }

    public void StartGame(GameProfile profile, bool testMode)
    {
        _session?.Dispose();
        ConsoleText.Text = "";
        Header.Text = profile.GameNameInternal ?? profile.ProfileName;
        BtnForceQuit.IsEnabled = true;
        BtnBack.IsEnabled = false;

        _session = new GameSession(profile, testMode);
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
