using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Web.WebView2.Core;

namespace TeknoParrotUi.Avalonia.Controls;

/// <summary>
/// Generic embedded browser host. On Windows it is backed by WebView2 (the Edge
/// runtime, preinstalled on Windows 10/11) so no CEF redistribution is needed.
/// The surface is intentionally small (Navigate / ExecuteScript / init scripts /
/// string messages) so a WebKitGTK backend can slot in for Linux later.
/// </summary>
public class NativeWebView : NativeControlHost
{
    private CoreWebView2Controller? _controller;
    private CoreWebView2? _core;
    private string? _pendingUrl;
    private readonly List<string> _initScripts = new();
    private bool _initStarted;

    /// <summary>Whether an embedded browser backend exists for this platform.</summary>
    public static bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>Raised on the UI thread with the string posted by page scripts.</summary>
    public event Action<string>? WebMessageReceived;

    /// <summary>Raised on the UI thread with the current URL after each navigation.</summary>
    public event Action<string>? NavigationCompleted;

    /// <summary>Raised on the UI thread when the browser backend could not start.</summary>
    public event Action<string>? InitializationFailed;

    /// <summary>Adds a script injected into every document before it loads (JS bridge shims).</summary>
    public void AddInitScript(string script) => _initScripts.Add(script);

    public void Navigate(string url)
    {
        if (_core != null)
            _core.Navigate(url);
        else
            _pendingUrl = url;
    }

    public void ExecuteScript(string script) => _ = _core?.ExecuteScriptAsync(script);

    public void Reload() => _core?.Reload();

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var handle = base.CreateNativeControlCore(parent);
        if (IsSupported && !_initStarted)
        {
            _initStarted = true;
            _ = InitializeAsync(handle.Handle);
        }
        return handle;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        try { _controller?.Close(); } catch { /* already gone */ }
        _controller = null;
        _core = null;
        _initStarted = false;
        base.DestroyNativeControlCore(control);
    }

    private async Task InitializeAsync(IntPtr hwnd)
    {
        try
        {
            var dataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TeknoParrot", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null, dataFolder);
            _controller = await environment.CreateCoreWebView2ControllerAsync(hwnd);
            _core = _controller.CoreWebView2;

            // Parity with the classic UI's context menu handler (menu disabled)
            _core.Settings.AreDefaultContextMenusEnabled = false;
            _core.Settings.IsStatusBarEnabled = false;

            _core.WebMessageReceived += (_, e) =>
            {
                string message;
                try { message = e.TryGetWebMessageAsString(); }
                catch { message = e.WebMessageAsJson; }
                Dispatcher.UIThread.Post(() => WebMessageReceived?.Invoke(message));
            };
            _core.NavigationCompleted += (_, _) =>
            {
                var url = _core?.Source ?? "";
                Dispatcher.UIThread.Post(() => NavigationCompleted?.Invoke(url));
            };
            // External links (Discord invites etc.) go to the system browser
            _core.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                try { Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true }); } catch { }
            };

            foreach (var script in _initScripts)
                await _core.AddScriptToExecuteOnDocumentCreatedAsync(script);

            UpdateBrowserBounds();

            if (_pendingUrl != null)
            {
                _core.Navigate(_pendingUrl);
                _pendingUrl = null;
            }
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() => InitializationFailed?.Invoke(ex.Message));
        }
    }

    protected override void OnSizeChanged(global::Avalonia.Controls.SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBrowserBounds();
    }

    private void UpdateBrowserBounds()
    {
        if (_controller == null)
            return;
        var scale = (VisualRoot as TopLevel)?.RenderScaling ?? 1.0;
        _controller.Bounds = new System.Drawing.Rectangle(0, 0,
            Math.Max(1, (int)(Bounds.Width * scale)),
            Math.Max(1, (int)(Bounds.Height * scale)));
    }
}
