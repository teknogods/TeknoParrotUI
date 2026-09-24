using System;
using System.Collections.Generic;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.VisualTree;
using TeknoParrotUi.Avalonia.Services;

namespace TeknoParrotUi.Avalonia.Views;

/// <summary>Shared news page for desktop dialogs and Android's single-view lifetime.</summary>
public partial class AnnouncementView : UserControl
{
    private Uri _pageUrl = null!;
    private bool _loaded;
    private bool _browserAvailable = true;

    public event EventHandler? CloseRequested;

    protected override AutomationPeer OnCreateAutomationPeer()
        => OperatingSystem.IsAndroid() ? new NewsPageAutomationPeer(this) : base.OnCreateAutomationPeer();

    private sealed class NewsPageAutomationPeer : ControlAutomationPeer
    {
        private readonly AnnouncementView _view;

        public NewsPageAutomationPeer(AnnouncementView view) : base(view) => _view = view;

        public void RefreshChildren() => InvalidateChildren();

        protected override IReadOnlyList<AutomationPeer> GetChildrenCore()
        {
            // Avalonia Android 12 cannot enumerate the native WebView's
            // InteropAutomationPeer. Expose the news heading and actions while
            // leaving the browser subtree to Android's native accessibility.
            var children = new List<AutomationPeer>();
            children.Add(CreatePeerForElement(_view.Heading));
            if (_view.SupportPanel.IsVisible)
            {
                children.Add(CreatePeerForElement(_view.SupportTitle));
                children.Add(CreatePeerForElement(_view.SupportDescription));
            }
            children.Add(CreatePeerForElement(_view.CloseButton));
            if (_view.SubscribeButton.IsVisible)
                children.Add(CreatePeerForElement(_view.SubscribeButton));
            children.Add(CreatePeerForElement(_view.ExternalButton));
            if (_view.MessagePanel.IsVisible)
            {
                children.Add(CreatePeerForElement(_view.MessageText));
                if (_view.RetryButton.IsVisible)
                    children.Add(CreatePeerForElement(_view.RetryButton));
                children.Add(CreatePeerForElement(_view.OpenBrowserButton));
            }
            return children;
        }
    }

    public AnnouncementView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
        {
            if (!_loaded && _pageUrl != null)
            {
                _loaded = true;
                LoadPage();
            }
        };
        if (OperatingSystem.IsLinux())
        {
            App.WebViewInitFailed += OnWebViewInitFailed;
            DetachedFromVisualTree += (_, _) => App.WebViewInitFailed -= OnWebViewInitFailed;
        }
    }

    public AnnouncementView(Uri pageUrl, bool isSubscribed) : this() => Configure(pageUrl, isSubscribed);

    public void Configure(Uri pageUrl, bool isSubscribed)
    {
        if (pageUrl == null || !AnnouncementService.TryGetNewsPostUrl(pageUrl.AbsoluteUri, out _))
            throw new ArgumentException("Only HTTPS TeknoParrotTeam Patreon post URLs are allowed.", nameof(pageUrl));

        _pageUrl = pageUrl;
        Heading.Text = Loc.T("AnnouncementNetwork", "New TeknoParrot announcement");
        MessageText.Text = Loc.T("AnnouncementLoading", "Loading announcement...");
        RetryButton.Content = Loc.T("AnnouncementRetry", "Retry");
        OpenBrowserButton.Content = Loc.T("AnnouncementOpenBrowser", "Open in browser");
        ExternalButton.Content = OpenBrowserButton.Content;
        CloseButton.Content = Loc.T("AnnouncementClose", "Close");
        SupportTitle.Text = Loc.T("AnnouncementSupportTitle", "Support TeknoParrot");
        SupportDescription.Text = Loc.T("AnnouncementSupportDescription", "Subscribe to support development.");
        SubscribeButton.Content = Loc.T("AnnouncementSubscribe", "Subscribe");
        SupportPanel.IsVisible = !isSubscribed;
        SubscribeButton.IsVisible = !isSubscribed;
        if (OperatingSystem.IsAndroid())
        {
            foreach (var button in new[] { CloseButton, SubscribeButton, ExternalButton,
                         RetryButton, OpenBrowserButton })
                button.MinHeight = 48;
        }
        if (OperatingSystem.IsLinux() && !Common.Proton.LinuxEnvironmentCheck.CheckWebView().Found)
            ShowBrowserUnavailable("GTK3/WebKitGTK is missing. Open the announcement in your regular browser.");
    }

    private void LoadPage()
    {
        if (!_browserAvailable)
            return;
        try
        {
            MessagePanel.IsVisible = true;
            MessageText.Text = Loc.T("AnnouncementLoading", "Loading announcement...");
            RetryButton.IsVisible = false;
            RefreshAutomation();
            Browser.Navigate(_pageUrl);
        }
        catch (Exception error) { ShowError(error.Message); }
    }

    private void ShowError(string detail)
    {
        MessagePanel.IsVisible = true;
        MessageText.Text = Loc.T("AnnouncementLoadError", "Could not load the announcement.") + " " + detail;
        RetryButton.IsVisible = true;
        RefreshAutomation();
    }

    private void OnWebViewInitFailed()
        => ShowBrowserUnavailable("The embedded browser could not start. Open the announcement in your regular browser.");

    private void ShowBrowserUnavailable(string message)
    {
        // A hidden native host still initializes when attached. Remove it so
        // Linux without WebKit can display the fallback without crashing.
        if (Browser.Parent is Panel parent)
            parent.Children.Remove(Browser);
        _browserAvailable = false;
        MessagePanel.IsVisible = true;
        MessageText.Text = message;
        RetryButton.IsVisible = false;
        RefreshAutomation();
    }

    private void RefreshAutomation()
    {
        if (OperatingSystem.IsAndroid() &&
            ControlAutomationPeer.FromElement(this) is NewsPageAutomationPeer peer)
            peer.RefreshChildren();
    }

    private void Browser_NavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        if (e.Request == null || e.Request.AbsoluteUri == "about:blank")
            return;
        if (!AnnouncementService.TryGetNewsPostUrl(e.Request.AbsoluteUri, out _))
        {
            e.Cancel = true;
            ShowError(Loc.T("AnnouncementLoadError", "This page is unavailable."));
        }
    }

    private void Browser_NavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || e.Request == null ||
            !AnnouncementService.TryGetNewsPostUrl(e.Request.AbsoluteUri, out _))
        {
            ShowError(Loc.T("AnnouncementLoadError", "Could not load the announcement."));
            return;
        }
        MessagePanel.IsVisible = false;
        RefreshAutomation();
    }

    private void Browser_NewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs e) => e.Handled = true;
    private void Retry_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => LoadPage();
    private void Close_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => CloseRequested?.Invoke(this, EventArgs.Empty);
    private async void OpenBrowser_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => await ExternalUrlLauncher.OpenAsync(this, _pageUrl.AbsoluteUri);
    private async void Subscribe_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => await ExternalUrlLauncher.OpenAsync(this, "https://teknoparrot.com/Home/Subscription");
}
