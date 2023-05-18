using System;
using System.Linq;
using System.Windows;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Helpers;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for DownloadControl.xaml
    /// </summary>
    public partial class DownloadControl
    {
        private readonly WebClient _wc = new WebClient();
        private readonly string _link;
        private readonly string _output;
        private readonly string _onlineVersion;
        public bool isFinished = false;
        private readonly MainWindow.UpdaterComponent _componentUpdated;

        public void Cleanup()
        {
            try
            {
                Debug.WriteLine($"Deleting temporary file {_output}");
                File.Delete(_output);
            }
            catch
            {
                Debug.WriteLine($"Failed to delete temporary file {_output}! Will clean up on UI start");
            }
        }

        public DownloadControl(string link, string title, MainWindow.UpdaterComponent componentUpdated, string onlineVersion = "")
        {
            InitializeComponent();
            statusText.Text = $"{Properties.Resources.DownloaderDownloading} {title}";
            _link = link;
            _output = App.GenerateTPTempFilename();
            Debug.WriteLine($"New updater temp file: {_output}");
            _componentUpdated = componentUpdated;
            _onlineVersion = onlineVersion;
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
                Debug.WriteLine($"Download for {_link} cancelled");
                statusText.Text = Properties.Resources.DownloaderCancelled;
                Cleanup();
                return;
            }

            if (e.Error != null) // We have an error! Retry a few times, then abort.
            {
                Debug.WriteLine($"Download for {_link} failed: {e}");
                statusText.Text = Properties.Resources.DownloaderError;
                Cleanup();
                return;
            }

            statusText.Text = Properties.Resources.DownloaderComplete;

            Debug.WriteLine($"Download for {_link} completed!");
            DoComplete();
        }

        private async void DoComplete()
        {
            bool isDone = false;
            bool isUI = _componentUpdated.name == "TeknoParrotUI";
            bool isUsingFolderOverride = !string.IsNullOrEmpty(_componentUpdated.folderOverride);
            string destinationFolder = isUsingFolderOverride ? _componentUpdated.folderOverride : _componentUpdated.name;
            statusText.Text = "Extracting files...";
            progressBar.IsIndeterminate = true;
            if (!isUI)
            {
                Directory.CreateDirectory(destinationFolder);
            }

            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                using (var memoryStream = new FileStream(_output, FileMode.Open))
                using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                    {
                        var name = entry.FullName;

                        // directory
                        if (name.EndsWith("/"))
                        {
                            name = isUsingFolderOverride ? Path.Combine(_componentUpdated.folderOverride, name) : name;
                            Directory.CreateDirectory(name);
                            Debug.WriteLine($"Updater directory entry: {name}");
                            continue;
                        }

                        var dest = isUI ? name : Path.Combine(destinationFolder, name);
                        Debug.WriteLine($"Updater file: {name} extracting to: {dest}");

                        try
                        {
                            if (File.Exists(dest))
                                File.Delete(dest);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // couldn't delete, just move to temp for now
                            File.Move(dest, App.GenerateTPTempFilename());
                        }

                        try
                        {
                            using (var entryStream = entry.Open())
                            using (var dll = File.Create(dest))
                            {
                                entryStream.CopyTo(dll);
                            }
                        }
                        catch
                        {
                            // ignore..?
                        }
                    }
                }

                isDone = true;
                Debug.WriteLine($"{_output} finished extracting");
                if (_componentUpdated.manualVersion)
                {
                    File.WriteAllText(_componentUpdated.folderOverride + "\\.version", _onlineVersion);
                }

                Cleanup();

            }).Start();

            while (!isDone)
            {
                await Task.Delay(25);
            }

            progressBar.IsIndeterminate = false;
            progressBar.Value = 100;
            
            //MessageBoxHelper.InfoOK(string.Format(Properties.Resources.UpdaterSuccess, _componentUpdated.name, onlineVersion));
            statusText.Text = _componentUpdated.name + " has been downloaded and extracted successfully!";
            isFinished = true;
            //this.Close();
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
                    string filename = Path.GetFileName(_link);
                    statusText.Text = $"Downloading {filename}...";
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
            //this.Close();
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

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
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
    }
}
