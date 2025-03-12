using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Threading;
using TeknoParrotUi.Components; // Add this import

namespace TeknoParrotUi.Views
{
    public partial class DownloadControl : UserControl
    {
        private string _downloadUrl;
        private string _saveLocation;
        private HttpClient _httpClient;
        public bool Complete = false;
        private readonly string _link;
        private readonly string _output;
        private readonly bool _inMemory;
        private readonly string _onlineVersion;
        public bool isFinished = false;
        private readonly UpdaterComponent _componentUpdated;

        public DownloadControl(string link, string output, bool inMemory, UpdaterComponent componentUpdated, string onlineVersion = "")
        {
            InitializeComponent();
            statusText.Text = $"{Properties.Resources.DownloaderDownloading} {output}";
            _link = link;
            _output = output;
            _inMemory = inMemory;
            _componentUpdated = componentUpdated;
            _onlineVersion = onlineVersion;
            _httpClient = new HttpClient();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Find references to controls
            progressBar = this.FindControl<ProgressBar>("progressBar");
            statusText = this.FindControl<TextBlock>("statusText");
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_downloadUrl) || string.IsNullOrEmpty(_saveLocation))
                return;

            await DownloadFileAsync(_downloadUrl, _saveLocation);
        }

        private async Task DownloadFileAsync(string url, string destinationPath)
        {
            try
            {
                // Create directory if it doesn't exist
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

                // Download the file with progress reporting
                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192];
                        var totalBytesRead = 0L;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;

                            if (totalBytes != -1)
                            {
                                var progressPercentage = (double)totalBytesRead / totalBytes;
                                await Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    progressBar.Value = progressPercentage * 100;
                                    statusText.Text = $"Downloading... {progressPercentage:P0}";
                                });
                            }
                        }
                    }
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    progressBar.Value = 100;
                    statusText.Text = "Download complete!";
                    Complete = true;
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    statusText.Text = $"Error: {ex.Message}";
                });
                Debug.WriteLine($"Download error: {ex}");
            }
        }
    }
}