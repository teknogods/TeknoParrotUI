using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;

namespace TeknoParrotUi.Avalonia.Views;

public partial class GameRunningView : UserControl
{
    private const int MaxConsoleLines = 2000;
    private const int MaxConsoleCharacters = 512 * 1024;
    private const int MaxConsoleLineCharacters = 8192;

    private readonly BoundedLineBuffer _consoleBuffer = new(
        MaxConsoleLines,
        MaxConsoleCharacters,
        MaxConsoleLineCharacters);
    private IGameSession? _session;
    private bool _forceQuitRequested;

    public event Action? BackRequested;

    /// <summary>Raised with the exit code when the game process ends (CLI mode auto-close).</summary>
    public event Action<int>? GameExited;

    public GameRunningView()
    {
        InitializeComponent();
        if (OperatingSystem.IsAndroid())
        {
            ActionsPanel.Orientation = Orientation.Vertical;
            ActionsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            foreach (var button in new[] { BtnForceQuit, BtnBack })
            {
                button.Width = double.NaN;
                button.MinHeight = 48;
                button.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
        }
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
        _consoleBuffer.Clear();
        ConsoleText.Text = "";
        Header.Text = (profile.GameNameInternal ?? profile.ProfileName) + (emuOnly ? " (emulator only)" : "");
        StatusText.ClearValue(TextBlock.ForegroundProperty);
        BtnForceQuit.IsEnabled = true;
        BtnBack.IsEnabled = false;

        var session = GameSessionFactory.Create(profile, testMode, emuOnly);
        _session = session;
        session.OutputReceived += line => Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(_session, session))
                return;
            AppendConsoleLine(line);
            ConsoleScroll.ScrollToEnd();
        });
        session.StateChanged += state => Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(_session, session))
                StatusText.Text = state;
        });
        session.Exited += code => Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(_session, session))
                return;
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

        bool started;
        try
        {
            started = session.Start();
        }
        catch (Exception ex)
        {
            // Defense-in-depth: GameSession.Start() already catches launch-time
            // exceptions internally, but a crash here must never take the whole
            // app down (this used to be an unhandled exception on the UI thread).
            AppendConsoleLine("ERROR: " + ex.Message);
            started = false;
        }
        if (!started)
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

    private void AppendConsoleLine(string line)
    {
        _consoleBuffer.AppendLine(line);
        ConsoleText.Text = _consoleBuffer.GetText();
    }

    private void BtnBack_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _session?.Dispose();
        _session = null;
        BackRequested?.Invoke();
    }
}
