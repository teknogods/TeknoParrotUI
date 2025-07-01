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
        private readonly string _output;
        private readonly bool _inMemory;
        public byte[] data;

        public DownloadWindow(string link, string output, bool inMemory)
        {
            InitializeComponent();
            statusText.Text = string.Format(Properties.Resources.DownloadWindowDownloadingFile, output);
            _link = link;
            _output = output;
            _inMemory = inMemory;
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
        
                try
                {
                    File.Delete(_output);
                }
                catch
                {
                    // ignored
                }
                

                return;
            }

            if (e.Error != null) // We have an error! Retry a few times, then abort.
            {
                statusText.Text = Properties.Resources.DownloaderError;

                try
                {
                    File.Delete(_output);
                }
                catch
                {
                    // ignored
                }
                

                return;
            }

            statusText.Text = Properties.Resources.DownloaderComplete;
            Close();
        }

        /// <summary>
        /// When the download is completed, this is executed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wc_DownloadDataCompleted(object sender, DownloadDataCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                statusText.Text = Properties.Resources.DownloaderCancelled;

                return;
            }

            if (e.Error != null) // We have an error! Retry a few times, then abort.
            {
                statusText.Text = Properties.Resources.DownloaderError;

                return;
            }

            data = e.Result;

            statusText.Text = Properties.Resources.DownloaderComplete;
            Close();
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
                    Debug.WriteLine($"Downloading {_link} {(!_inMemory ? $"to {_output}" : "")}");
                    _wc.DownloadProgressChanged += wc_DownloadProgressChanged;
                    // download byte array instead of dropping a file
                    if (_inMemory)
                    {
                        _wc.DownloadDataCompleted += wc_DownloadDataCompleted;
                        _wc.DownloadDataAsync(new Uri(_link));
                    }
                    else
                    {
                        _wc.DownloadFileCompleted += wc_DownloadFileCompleted;
                        _wc.DownloadFileAsync(new Uri(_link), _output);
                    }
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