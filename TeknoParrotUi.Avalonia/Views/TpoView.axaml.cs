using System;
using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Views;

/// <summary>
/// TeknoParrot Online — hosts the TPO web interface
/// (teknoparrot.com:3333/Home/Chat) in the official Avalonia NativeWebView,
/// which uses each platform's native engine (WebView2 on Windows, WKWebView on
/// macOS/iOS, WPE WebKit on Linux, WebView on Android). The JavaScript bridge
/// matches the classic CefSharp view: the site calls callbackObj.startGame(...)
/// which spawns a second instance of this exe with --profile=X --tponline and
/// the TP_TPONLINE2 session environment variable; when the game process exits,
/// onGameProcessExited() is invoked on the page.
/// </summary>
public partial class TpoView : UserControl
{
    // The page expects a callbackObj like CefSharp's legacy binding; this shim
    // forwards calls to the host through the cross-platform
    // invokeCSharpAction(body) channel provided by NativeWebView.
    private const string BridgeScript =
        "window.callbackObj = window.callbackObj || {" +
        "  showMessage: function(msg){" +
        "    invokeCSharpAction(JSON.stringify({method:'showMessage',args:[String(msg)]}));" +
        "  }," +
        "  startGame: function(uniqueRoomName, realRoomName, gameId, playerId, playerName, playerCount){" +
        "    invokeCSharpAction(JSON.stringify({method:'startGame',args:[" +
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

        if (OperatingSystem.IsLinux() && !Common.Proton.LinuxEnvironmentCheck.CheckWebView().Found)
        {
            // Don't even try attaching NativeWebView - on systems without a
            // working GTK/WebKitGTK stack it fails silently (blank panel)
            // instead of throwing, since App.axaml.cs's dispatcher exception
            // guard now swallows the "Unable to initialize GTK" crash that
            // used to happen here. Show an actionable fallback instead.
            ShowNoWebViewFallback("This system is missing GTK3/WebKitGTK, so TeknoParrot Online can't show " +
                                  "its embedded browser here. Install gtk3 + webkit2gtk via your distro's " +
                                  "package manager (see the Linux Setup page), or use the button below to " +
                                  "open it in your regular web browser.");
            return;
        }

        // GTK3/WebKitGTK can be installed and still fail to initialize at
        // runtime (some window-manager/session setups) - the library check
        // above can't catch that. If it happens, App.axaml.cs's dispatcher
        // exception guard suppresses the crash and raises this instead.
        App.WebViewInitFailed += OnWebViewInitFailed;

        // Navigate every time the view is (re)shown — matches the classic view,
        // which reloaded on visibility changes to avoid ghost lobbies.
        AttachedToVisualTree += (_, _) => NavigateToStart();
        Localize();
        Services.Loc.LanguageChanged += Localize;
    }

    private void OnWebViewInitFailed()
    {
        Dispatcher.UIThread.Post(() => ShowNoWebViewFallback(
            "The embedded browser failed to start (GTK/WebKitGTK didn't initialize on this system, even " +
            "though it's installed). Use the button below to open TeknoParrot Online in your regular web browser instead."));
    }

    private void ShowNoWebViewFallback(string detail)
    {
        // Just hiding BrowserPanel (IsVisible=false) isn't enough - Avalonia
        // still keeps NativeWebView attached to the visual tree, so it kept
        // retrying (and failing) GTK init every time this page was reopened,
        // spamming the console. Fully detach it so it never gets a chance to.
        if (BrowserPanel.Parent is Panel parent)
            parent.Children.Remove(BrowserPanel);
        NoWebViewPanel.IsVisible = true;
        NoWebViewDetail.Text = detail;
        Localize();
    }

    private void Localize()
    {
        ToolTip.SetTip(BtnReload, Services.Loc.T("TpoReload", "Reload"));
        ToolTip.SetTip(BtnOpenExternal, Services.Loc.T("TpoOpenInBrowser", "Open in web browser"));
    }

    private void NavigateToStart()
    {
        try
        {
            string targetUrl;
            if (TPOConfig.IsConfigured)
            {
                // CLI args or tponline:// deep link: navigate straight into the room
                _autoSession = true;
                targetUrl = TPOConfig.BuildChatUrl();
                // Consume the config so leaving the room doesn't re-join on refresh
                TPOConfig.Clear();
            }
            else
            {
                targetUrl = TPOConfig.ChatBaseUrl;
            }

            // No SSO here: the desktop OAuth login is for teknoparrot.com, while
            // TPO (TPOnlineService) is a separate site with its own Identity —
            // the user logs in on the TPO page itself.
            Browser.Navigate(new Uri(targetUrl));
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not start the embedded browser: {ex.Message}";
        }
    }

    private async void Browser_NavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        // (Re)install the JS bridge after every navigation
        try
        {
            await Browser.InvokeScript(BridgeScript);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TPO] Bridge injection failed: {ex.Message}");
        }

        if (!e.IsSuccess)
        {
            StatusText.Text = "Could not reach TeknoParrot Online — check your connection and reload.";
            return;
        }

        var url = e.Request?.ToString() ?? "";
        if (_autoSession && !_loginNoticeShown &&
            (url.Contains("LoginMinimalist", StringComparison.OrdinalIgnoreCase) ||
             url.Contains("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase)))
        {
            _loginNoticeShown = true;
            StatusText.Text = "You must log in before you can play — log in on this page and you will be taken to your room automatically.";
        }
    }

    private void Browser_NewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs e)
    {
        // External links (Discord invites etc.) go to the system browser
        e.Handled = true;
        try
        {
            if (e.Request != null)
                Process.Start(new ProcessStartInfo(e.Request.ToString()) { UseShellExecute = true });
        }
        catch
        {
            // no browser available
        }
    }

    private void Browser_WebMessageReceived(object? sender, WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.Body);
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

        if (!OperatingSystem.IsWindows())
        {
            StatusText.Text = "Games can only be launched on Windows.";
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
        _launcherProcess.Exited += (_, _) => Dispatcher.UIThread.Post(async () =>
        {
            _launcherProcess = null;
            // Notify the website so the room state resets
            try { await Browser.InvokeScript("onGameProcessExited();"); } catch { }
            StatusText.Text = "";
        });
        StatusText.Text = $"Game running — room: {realRoomName}";
    }

    private void BtnReload_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        NavigateToStart();

    private void BtnOpenWeb_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            OpenUrl(TPOConfig.ChatBaseUrl);
        }
        catch (Exception ex)
        {
            var message = $"Could not open a browser automatically ({ex.Message}). Open this URL manually: {TPOConfig.ChatBaseUrl}";
            if (NoWebViewPanel.IsVisible)
                NoWebViewDetail.Text = message;
            else
                StatusText.Text = message;
        }
    }

    private static void OpenUrl(string url)
    {
        if (OperatingSystem.IsLinux())
        {
            // ProcessStartInfo's UseShellExecute=true -> xdg-open bridging is
            // unreliable across desktop/session setups - invoke xdg-open
            // directly instead (xdg-utils is a near-universal baseline on
            // Linux desktops, and was confirmed present/working here).
            Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
        }
        else
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }
}
