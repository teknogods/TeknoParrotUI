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

        // Navigate every time the view is (re)shown — matches the classic view,
        // which reloaded on visibility changes to avoid ghost lobbies.
        AttachedToVisualTree += (_, _) => NavigateToStart();
        Localize();
        Services.Loc.LanguageChanged += Localize;
    }

    private void Localize()
    {
        ToolTip.SetTip(BtnReload, Services.Loc.T("TpoReload", "Reload"));
        ToolTip.SetTip(BtnOpenExternal, Services.Loc.T("TpoOpenInBrowser", "Open in web browser"));
    }

    private async void NavigateToStart()
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

            // Auto-login: exchange the desktop OAuth token for a TPO Identity
            // session inside the webview (form post → cookie → redirect to chat).
            var oauth = new Common.Auth.OAuthClient();
            if (oauth.IsLoggedIn)
            {
                var token = await oauth.GetValidTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _autoLoginTarget = targetUrl;
                    var origin = new Uri(targetUrl).GetLeftPart(UriPartial.Authority);
                    var returnUrl = new Uri(targetUrl).PathAndQuery;
                    var html =
                        "<html><body>" +
                        $"<form method=\"post\" action=\"{origin}/api/OAuth/session\">" +
                        $"<input type=\"hidden\" name=\"access_token\" value=\"{token}\"/>" +
                        $"<input type=\"hidden\" name=\"returnUrl\" value=\"{System.Net.WebUtility.HtmlEncode(returnUrl)}\"/>" +
                        "</form><script>document.forms[0].submit();</script></body></html>";
                    Browser.NavigateToString(html, new Uri(origin));
                    return;
                }
            }

            Browser.Navigate(new Uri(targetUrl));
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not start the embedded browser: {ex.Message}";
        }
    }

    // Set while an OAuth-token auto-login form post is in flight; used to fall
    // back to a plain navigation when the server does not support the endpoint.
    private string? _autoLoginTarget;

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

        // Auto-login fallback: if we're still sitting on the session endpoint the
        // server doesn't support it (not deployed yet) — continue without SSO.
        var url = e.Request?.ToString() ?? "";
        if (_autoLoginTarget != null && url.Contains("/api/OAuth/session", StringComparison.OrdinalIgnoreCase))
        {
            var target = _autoLoginTarget;
            _autoLoginTarget = null;
            Browser.Navigate(new Uri(target));
            return;
        }
        _autoLoginTarget = null;
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

    private void BtnOpenWeb_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(TPOConfig.ChatBaseUrl) { UseShellExecute = true });
}
