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
        private string _currentGame;
        private readonly string _link;
        private readonly string _output;
        private readonly bool _isUpdate;

        public DownloadWindow(string link, string output, bool isUpdate)
        {
            InitializeComponent();
            _link = link;
            _output = output;
            _isUpdate = isUpdate;
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
            if (_isUpdate)
            {
                ExtractUpdate();
            }
            else
            {
                Close();
            }
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
        /// This extracts a zip file.
        /// </summary>
        /// <param name="archive">The source zip file</param>
        /// <param name="destinationDirectoryName">The directory the zip file is to be extracted to</param>
        /// <param name="overwrite">Whether or not you want files to be overwritten</param>
        private void Extract(ZipArchive archive, string destinationDirectoryName, bool overwrite)
        {
            var current = 0;
            var count = 0;
            try
            {
                _currentGame = MainWindow.ParrotData.LastPlayed ?? "abc";

                foreach (var file in archive.Entries)
                {
                    Console.WriteLine(file.Name);
                    if (file.Name == "")
                    {
                        // Is a directory
                        count += 1;
                    }
                    else if (file.Name == _currentGame + ".png")
                    {
                        count += 1;
                    }
                    else
                    {
                        count += 1;
                        try
                        {
                            File.Move(file.FullName, file.FullName + ".bak");
                        }
                        catch
                        {
                            //most likely either the file doesn't exist (so it's new in this release) or it's in use so we'll skip it
                        }
                    }
                }

                foreach (var file in archive.Entries)
                {
                    if (file.Name == _currentGame + ".png")
                    {
                        current += 1;
                    }
                    else
                    {
                        Console.WriteLine(file.Name);
                        var completeFileName = Path.Combine(destinationDirectoryName, file.FullName);
                        if (file.Name == "")
                        {
                            //Assuming Empty for Directory
                            Directory.CreateDirectory(Path.GetDirectoryName(completeFileName) ??
                                                      throw new InvalidOperationException());
                            continue;
                        }

                        try
                        {
                            file.ExtractToFile(completeFileName, true);
                        }
                        catch
                        {
                            //most likely the file is in use, this should've been solved by moving in use files.
                        }

                        current += 1;
                        progressBar.Value = current / count * 100;
                    }
                }

                UpdateCleanup();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// This sets up the extractor and restarts the UI when completed.
        /// </summary>
        private void ExtractUpdate()
        {
            //this initial cleanup is to remove left over files
            UpdateCleanup();
            progressBar.Value = 0;
            statusText.Text = "Extracting update...";
            ZipArchive archive = ZipFile.OpenRead(Environment.GetEnvironmentVariable("TEMP") + "\\teknoparrot.zip");
            string myExeDir = AppDomain.CurrentDomain.BaseDirectory;


            Extract(archive, myExeDir, true);
            if (MessageBox.Show(
                    $"Would you like to restart me to finish the update? Otherwise, I will close TeknoParrotUi for you to reopen.",
                    "Update Complete", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                string[] psargs = Environment.GetCommandLineArgs();
                System.Diagnostics.Process.Start(Application.ResourceAssembly.Location, psargs[0]);
                Application.Current.Shutdown();
            }
            else
            {
                Application.Current.Shutdown();
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