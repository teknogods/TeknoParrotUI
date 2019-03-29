using System;
using System.Linq;
using System.Windows;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.ComponentModel;

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

        public DownloadWindow(string link, string output)
        {
            InitializeComponent();
            _link = link;
            _output = output;
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
                statusText.Text = "Download Cancelled";
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
                statusText.Text = "Error Downloading";
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

            statusText.Text = "Download Complete";
            Close();
        }

        /// <summary>
        /// This method downloads the update from the TeknoParrot server.
        /// </summary>
        private void Download()
        {
            File.Delete(Environment.GetEnvironmentVariable("TEMP") + "\\teknoparrot.zip");

            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            // This will download a large image from the web, you can change the value
            // i.e a textbox : textBox1.Text
            try
            {
                using (_wc)
                {
                    _wc.Headers.Add("Referer", "https://teknoparrot.com/download");
                    _wc.DownloadProgressChanged += wc_DownloadProgressChanged;
                    _wc.DownloadFileCompleted += wc_DownloadFileCompleted;
                    _wc.DownloadFileAsync(new Uri(_link), _output);

                    //wc.DownloadFileAsync(new Uri("https://teknoparrot.com/files/TeknoParrot_" + versionText.Text + ".zip"), Environment.GetEnvironmentVariable("TEMP") + "\\teknoparrot.zip");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// This cancels the download
        /// </summary>
        private void CancelDownload()
        {
            _wc.CancelAsync();
        }

        /// <summary>
        /// This removes any backup files left over in the teknoparrot folder (it doesn't grab everything)
        /// </summary>
        private void UpdateCleanup()
        {
            try
            {
                foreach (var file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.bak")
                    .Where(item => item.EndsWith(".bak")))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                foreach (var file in Directory
                    .GetFiles(AppDomain.CurrentDomain.BaseDirectory + "\\GameProfiles", "*.bak")
                    .Where(item => item.EndsWith(".bak")))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                foreach (var file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "\\Icons", "*.bak")
                    .Where(item => item.EndsWith(".bak")))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                this.Close();
            }
            catch
            {
                // ignored
            }
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
                MessageBox.Show(ex.Message);
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