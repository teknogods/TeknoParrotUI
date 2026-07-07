using System;
using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using TeknoParrotUi.Avalonia.Controls;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Views;

/// <summary>
/// TeknoParrot Online — hosts the TPO web interface
/// (teknoparrot.com:3333/Home/Chat) in the internal browser with the same
/// JavaScript bridge as the classic CefSharp view: the site calls
/// callbackObj.startGame(...) which spawns a second instance of this exe with
/// --profile=X --tponline and the TP_TPONLINE2 session environment variable;
/// when the game process exits, onGameProcessExited() is invoked on the page.
/// </summary>
public partial class TpoView : UserControl
{
    // The page expects a synchronous-looking callbackObj like CefSharp's legacy
    // binding; this shim forwards calls as JSON messages to the host.
    private const string BridgeScript =
        "window.callbackObj = {" +
        "  showMessage: function(msg){" +
        "    window.chrome.webview.postMessage(JSON.stringify({method:'showMessage',args:[String(msg)]}));" +
        "  }," +
        "  startGame: function(uniqueRoomName, realRoomName, gameId, playerId, playerName, playerCount){" +
        "    window.chrome.webview.postMessage(JSON.stringify({method:'startGame',args:[" +
        "      String(uniqueRoomName), String(realRoomName), String(gameId)," +
        "      String(playerId), String(playerName), String(playerCount)]}));" +
        "  }" +
        "};";

    private static Process? _launcherProcess;
    private bool _autoSession;
    private bool _loginNoticeShown;

    public TpoView()
    {
        InitializeComponent();

        if (!NativeWebView.IsSupported)
        {
            ShowFallback("The embedded browser is not available on this platform yet — " +
                         "open TeknoParrot Online in your web browser instead.", showRuntimeButton: false);
            return;
        }

        Browser.AddInitScript(BridgeScript);
        Browser.WebMessageReceived += OnWebMessage;
        Browser.NavigationCompleted += OnNavigationCompleted;
        Browser.InitializationFailed += message =>
            ShowFallback("The embedded browser could not start: " + message +
                         "\n\nInstall the Microsoft WebView2 Runtime and reopen this page.", showRuntimeButton: true);

        // Navigate every time the view is (re)shown — matches the classic view,
        // which reloaded on visibility changes to avoid ghost lobbies.
        AttachedToVisualTree += (_, _) => NavigateToStart();
    }

    private void NavigateToStart()
    {
        if (TPOConfig.IsConfigured)
        {
            // CLI args or tponline:// deep link: navigate straight into the room
            _autoSession = true;
            Browser.Navigate(TPOConfig.BuildChatUrl());
            // Consume the config so leaving the room doesn't re-join on refresh
            TPOConfig.Clear();
        }
        else
        {
            Browser.Navigate(TPOConfig.ChatBaseUrl);
        }
    }

    private void OnWebMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var method = doc.RootElement.GetProperty("method").GetString();
            var args = doc.RootElement.GetProperty("args");
            switch (method)
            {
                case "showMessage":
                    StatusText.Text = args[0].GetString();
                    break;
                case "startGame":
                    StartGame(args[0].GetString() ?? "", args[1].GetString() ?? "",
                              args[2].GetString() ?? "", args[3].GetString() ?? "",
                              args[4].GetString() ?? "", args[5].GetString() ?? "");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TPO] Bad bridge message: {ex.Message}");
        }
    }

    private void StartGame(string uniqueRoomName, string realRoomName, string gameId,
                           string playerId, string playerName, string playerCount)
    {
        if (_launcherProcess != null && !_launcherProcess.HasExited)
        {
            StatusText.Text = "A TeknoParrot Online game is already running.";
            return;
        }

        var exe = Environment.ProcessPath;
        if (exe == null)
            return;

        var info = new ProcessStartInfo(exe, $"--profile={gameId}.xml --tponline")
        {
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory
        };
        info.EnvironmentVariables["TP_TPONLINE2"] = $"{uniqueRoomName}|{playerId}|{playerName}|{playerCount}";

        _launcherProcess = Process.Start(info);
        if (_launcherProcess == null)
            return;

        _launcherProcess.EnableRaisingEvents = true;
        _launcherProcess.Exited += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            _launcherProcess = null;
            // Notify the website so the room state resets
            Browser.ExecuteScript("onGameProcessExited();");
            StatusText.Text = "";
        });
        StatusText.Text = $"Game running — room: {realRoomName}";
    }

    private void OnNavigationCompleted(string url)
    {
        // If launched via CLI/deep link and the server bounced us to the login page,
        // tell the user they must log in before they can play.
        if (_autoSession && !_loginNoticeShown &&
            (url.Contains("LoginMinimalist", StringComparison.OrdinalIgnoreCase) ||
             url.Contains("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase)))
        {
            _loginNoticeShown = true;
            StatusText.Text = "You must log in before you can play — log in on this page and you will be taken to your room automatically.";
        }
    }

    private void ShowFallback(string message, bool showRuntimeButton)
    {
        BrowserPanel.IsVisible = false;
        FallbackPanel.IsVisible = true;
        FallbackText.Text = message;
        BtnInstallRuntime.IsVisible = showRuntimeButton;
    }

    private void BtnReload_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        NavigateToStart();

    private void BtnOpenWeb_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(TPOConfig.ChatBaseUrl) { UseShellExecute = true });

    private void BtnInstallRuntime_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://developer.microsoft.com/en-us/microsoft-edge/webview2/") { UseShellExecute = true });
}
