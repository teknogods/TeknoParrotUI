using System;
using System.Linq;
using System.Windows;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.ComponentModel;
using System.Diagnostics;
using TeknoParrotUi.Helpers;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for DownloadWindow.xaml
    /// </summary>
    public partial class DownloadWindow
    {
        private readonly WebClient _wc = new WebClient();
        private readonly string _link;
        public readonly string _output;

        public void Cleanup()
        {
            try
            {
                File.Delete(_output);
            }
            catch
            {
                Debug.WriteLine($"Failed to delete temporary file {_output}! Will clean up on UI start");
            }
        }

        public DownloadWindow(string link, string title)
        {
            InitializeComponent();
            statusText.Text = $"{Properties.Resources.DownloaderDownloading} {title}";
            _link = link;

            _output = Path.GetTempPath()
                                + new Random().Next(0, Int32.MaxValue)
                                + DateTimeOffset.Now.ToUnixTimeMilliseconds() + ".tptemp";
            File.Create(_output);
        }

        /// <summary>
        ///  Show the progress of the download in a progressbar
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            // In case you don't have a progressBar Log the value instead 
            // Console.WriteLine(e.ProgressPercentage);
            progressBar.Value = e.ProgressPercentage;
        }

        /// <summary>
        /// When the download is completed, this is executed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wc_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                statusText.Text = Properties.Resources.DownloaderCancelled;
                Cleanup();
                return;
            }

            if (e.Error != null) // We have an error! Retry a few times, then abort.
            {
                statusText.Text = Properties.Resources.DownloaderError;
                Cleanup();
                return;
            }

            statusText.Text = Properties.Resources.DownloaderComplete;
            Close();

            Cleanup();
        }

        /// <summary>
        /// This method downloads the update from the specified URL
        /// </summary>
        private void Download()
        {
            // This will download a large image from the web, you can change the value
            // i.e a textbox : textBox1.Text
            try
            {
                using (_wc)
                {
                    Debug.WriteLine($"Downloading {_link} to {_output}");
                    _wc.DownloadProgressChanged += wc_DownloadProgressChanged;
                    _wc.DownloadFileCompleted += wc_DownloadFileCompleted;
                    _wc.DownloadFileAsync(new Uri(_link), _output);
                }
            }
            catch (Exception ex)
            {
                MessageBoxHelper.ErrorOK(ex.ToString());
            }
        }

        /// <summary>
        /// This cancels the download
        /// </summary>
        private void CancelDownload()
        {
            _wc.CancelAsync();
            this.Close();
        }

        /// <summary>
        /// This does stuff once the window is actually drawn on screen.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MetroWindow_ContentRendered(object sender, EventArgs e)
        {
            try
            {
                Download();
            }
            catch (Exception ex)
            {
                MessageBoxHelper.ErrorOK(ex.ToString());
            }
        }

        /// <summary>
        /// When clicked, this cancels the download.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            CancelDownload();
        }
    }
}