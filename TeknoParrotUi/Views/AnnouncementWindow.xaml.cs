using CefSharp;
using CefSharp.Handler;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using TeknoParrotUi.Helpers;
using UiResources = TeknoParrotUi.Properties.Resources;

namespace TeknoParrotUi.Views
{
    public partial class AnnouncementWindow : Window
    {
        private readonly Uri _pageUrl;
        private bool _closed;
        private bool _pageLoadFailed;

        public AnnouncementWindow(Uri pageUrl, bool isSubscribed)
        {
            if (pageUrl == null || !pageUrl.IsAbsoluteUri ||
                !AnnouncementService.TryGetNewsPostUrl(pageUrl.OriginalString, out _))
                throw new ArgumentException("Only HTTPS TeknoParrotTeam Patreon post URLs are allowed.", nameof(pageUrl));

            InitializeComponent();
            if (isSubscribed)
            {
                subscriptionIcon.Visibility = Visibility.Collapsed;
                subscriptionMessage.Visibility = Visibility.Collapsed;
                subscribeButton.Visibility = Visibility.Collapsed;
                closeButton.Margin = new Thickness(0);
                announcementFooter.Background = System.Windows.Media.Brushes.Transparent;
                announcementFooter.BorderThickness = new Thickness(0);
                announcementFooter.Padding = new Thickness(0);
            }
            _pageUrl = pageUrl;
            Width = Math.Min(Width, SystemParameters.WorkArea.Width - 32);
            Height = Math.Min(Height, SystemParameters.WorkArea.Height - 32);
            Browser.RequestHandler = new NewsPostRequestHandler(() => UpdatePageState(ShowLoadError));
            Browser.LifeSpanHandler = new NoPopupsLifeSpanHandler();
            // Announcement pages have no native JavaScript bindings (unlike TPOnline).
            Browser.LoadError += Browser_LoadError;
            Browser.FrameLoadStart += Browser_FrameLoadStart;
            Browser.FrameLoadEnd += Browser_FrameLoadEnd;
            Browser.Address = pageUrl.AbsoluteUri;
        }

        private void Browser_FrameLoadStart(object sender, FrameLoadStartEventArgs e)
        {
            if (e.Frame.IsMain)
                UpdatePageState(ShowLoading);
        }

        private void Browser_FrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            if (!e.Frame.IsMain) return;
            var failed = e.HttpStatusCode >= 400;
            UpdatePageState(() =>
            {
                if (failed)
                    ShowLoadError();
                else if (!_pageLoadFailed)
                {
                    loadingPanel.Visibility = Visibility.Collapsed;
                    loadingProgress.Visibility = Visibility.Hidden;
                }
            });
        }

        private void Browser_LoadError(object sender, LoadErrorEventArgs e)
        {
            if (!e.Frame.IsMain || e.ErrorCode == CefErrorCode.Aborted)
                return;
            UpdatePageState(ShowLoadError);
        }

        private void UpdatePageState(Action update)
        {
            if (Dispatcher.HasShutdownStarted) return;
            Dispatcher.InvokeAsync(() =>
            {
                if (!_closed) update();
            });
        }

        private void ShowLoading()
        {
            _pageLoadFailed = false;
            Browser.Visibility = Visibility.Visible;
            loadingPanel.Visibility = Visibility.Visible;
            loadingProgress.Visibility = Visibility.Visible;
            loadErrorPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowLoadError()
        {
            _pageLoadFailed = true;
            Browser.Visibility = Visibility.Collapsed;
            loadingPanel.Visibility = Visibility.Collapsed;
            loadingProgress.Visibility = Visibility.Hidden;
            loadErrorPanel.Visibility = Visibility.Visible;
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLoading();
            Browser.Load(_pageUrl.AbsoluteUri);
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        protected override void OnClosed(EventArgs e)
        {
            _closed = true;
            Browser.LoadError -= Browser_LoadError;
            Browser.FrameLoadStart -= Browser_FrameLoadStart;
            Browser.FrameLoadEnd -= Browser_FrameLoadEnd;
            Browser.Dispose();
            base.OnClosed(e);
        }

        private void OpenBrowser_Click(object sender, RoutedEventArgs e)
        {
            if (AnnouncementService.TryGetNewsPostUrl(_pageUrl.AbsoluteUri, out var allowedUrl))
                OpenInBrowser(allowedUrl.AbsoluteUri);
        }

        private void SubscribeButton_Click(object sender, RoutedEventArgs e)
        {
            OpenInBrowser("https://teknoparrot.com/Home/Subscription");
        }

        private void OpenInBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
            {
                MessageBox.Show(this, string.Format(UiResources.AnnouncementOpenError, ex.Message),
                    UiResources.AnnouncementTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Dismiss the announcement and resume startup, just like leaving the changelog view.
            Close();
        }

        private sealed class NewsPostRequestHandler : RequestHandler
        {
            private readonly Action _onBlocked;

            public NewsPostRequestHandler(Action onBlocked)
            {
                _onBlocked = onBlocked;
            }

            protected override bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser,
                IFrame frame, IRequest request, bool userGesture, bool isRedirect)
            {
                // about:blank is CEF's empty bootstrap page, never a feed target or external launch.
                if (request.Url == "about:blank" || AnnouncementService.TryGetNewsPostUrl(request.Url, out _))
                    return false;

                if (frame.IsMain) _onBlocked();
                return true;
            }
        }

        private sealed class NoPopupsLifeSpanHandler : LifeSpanHandler
        {
            protected override bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser,
                IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition,
                bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo,
                IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
            {
                newBrowser = null;
                return true;
            }
        }
    }
}
