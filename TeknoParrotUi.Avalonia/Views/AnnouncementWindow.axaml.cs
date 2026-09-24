using System;
using Avalonia.Controls;
using TeknoParrotUi.Avalonia.Services;

namespace TeknoParrotUi.Avalonia.Views;

public partial class AnnouncementWindow : Window
{
    private readonly Uri _pageUrl = null!;

    public AnnouncementWindow() => InitializeComponent();

    public AnnouncementWindow(Uri pageUrl, bool isSubscribed) : this()
    {
        if (pageUrl == null || !AnnouncementService.TryGetNewsPostUrl(pageUrl.AbsoluteUri, out _))
            throw new ArgumentException("Only HTTPS TeknoParrotTeam Patreon post URLs are allowed.", nameof(pageUrl));

        _pageUrl = pageUrl;
        Title = Loc.T("AnnouncementTitle", "TeknoParrot announcement");
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

        Opened += (_, _) => LoadPage();
    }

    private void LoadPage()
    {
        try
        {
            MessagePanel.IsVisible = true;
            MessageText.Text = Loc.T("AnnouncementLoading", "Loading announcement...");
            RetryButton.IsVisible = false;
            Browser.Navigate(_pageUrl);
        }
        catch (Exception error)
        {
            ShowError(error.Message);
        }
    }

    private void ShowError(string detail)
    {
        MessagePanel.IsVisible = true;
        MessageText.Text = Loc.T("AnnouncementLoadError", "Could not load the announcement.") + " " + detail;
        RetryButton.IsVisible = true;
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
    }

    private void Browser_NewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs e)
    {
        e.Handled = true;
    }

    private void Retry_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => LoadPage();
    private void Close_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => Close();
    private async void OpenBrowser_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => await ExternalUrlLauncher.OpenAsync(this, _pageUrl.AbsoluteUri);
    private async void Subscribe_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => await ExternalUrlLauncher.OpenAsync(this, "https://teknoparrot.com/Home/Subscription");
}
