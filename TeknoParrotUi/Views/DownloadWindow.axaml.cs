using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using TeknoParrotUi.Helpers;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for DownloadWindow.axaml
    /// </summary>
    public partial class DownloadWindow : Window
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _link;
        private readonly string _output;
        private readonly bool _inMemory;
        private bool _isCancelled = false;
        public byte[] data;

        public DownloadWindow(string link, string output, bool inMemory)
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            _link = link;
            _output = output;
            _inMemory = inMemory;
            statusText.Text = $"Downloading {output}...";
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Find control references
            progressBar = this.FindControl<ProgressBar>("progressBar");
            statusText = this.FindControl<TextBlock>("statusText");
            buttonCancel = this.FindControl<Button>("buttonCancel");
        }

        private async void Window_Opened(object sender, EventArgs e)
        {
            try
            {
                await Download();
            }
            catch (Exception ex)
            {
                await ShowErrorMessage(ex.ToString());
            }
        }

        /// <summary>
        /// This method downloads the update from the specified URL
        /// </summary>
        private async Task Download()
        {
            try
            {
                Debug.WriteLine($"Downloading {_link} {(!_inMemory ? $"to {_output}" : "")}");

                // Create directory if needed
                if (!_inMemory && !string.IsNullOrEmpty(Path.GetDirectoryName(_output)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_output));
                }

                // Download with progress reporting
                using var response = await _httpClient.GetAsync(_link, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using var contentStream = await response.Content.ReadAsStreamAsync();

                if (_inMemory)
                {
                    // Download to memory
                    var memoryStream = new MemoryStream();

                    var buffer = new byte[8192];
                    var totalBytesRead = 0L;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                    {
                        if (_isCancelled) break;

                        await memoryStream.WriteAsync(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            var progressPercentage = (double)totalBytesRead / totalBytes;
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                progressBar.Value = progressPercentage * 100;
                            });
                        }
                    }

                    if (!_isCancelled)
                    {
                        data = memoryStream.ToArray();
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            Close();
                        });
                    }
                }
                else
                {
                    // Download to file
                    using var fileStream = new FileStream(_output, FileMode.Create, FileAccess.Write, FileShare.None);

                    var buffer = new byte[8192];
                    var totalBytesRead = 0L;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                    {
                        if (_isCancelled) break;

                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            var progressPercentage = (double)totalBytesRead / totalBytes;
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                progressBar.Value = progressPercentage * 100;
                            });
                        }
                    }

                    if (!_isCancelled)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            Close();
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowErrorMessage(ex.ToString());
            }
        }

        /// <summary>
        /// This cancels the download
        /// </summary>
        private void CancelDownload()
        {
            _isCancelled = true;
            Close();
        }

        /// <summary>
        /// When clicked, this cancels the download.
        /// </summary>
        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            CancelDownload();
        }

        private async Task ShowErrorMessage(string message)
        {
            // Create simple error dialog
            var messageBox = new Window
            {
                Title = "Error",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var grid = new Grid();
            var textBlock = new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(10)
            };

            var button = new Button
            {
                Content = "OK",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 10)
            };

            button.Click += (s, e) => messageBox.Close();
            grid.Children.Add(textBlock);
            grid.Children.Add(button);
            messageBox.Content = grid;

            await messageBox.ShowDialog(this);
        }
    }
}