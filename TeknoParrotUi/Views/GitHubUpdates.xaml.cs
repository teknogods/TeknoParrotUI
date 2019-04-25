using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using TeknoParrotUi.Common;
using Application = System.Windows.Application;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for GitHubUpdates.xaml
    /// </summary>
    public partial class GitHubUpdates : Window
    {
        private readonly string _componentUpdated;
        private readonly GithubRelease _latestRelease;
        public GitHubUpdates(string componentUpdated, GithubRelease latestRelease)
        {
            InitializeComponent();
            _componentUpdated = componentUpdated;
            labelUpdated.Content = componentUpdated;
            _latestRelease = latestRelease;
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnChangelog(object sender, RoutedEventArgs e)
        {
            var repo = _componentUpdated.Contains("OpenParrot") ? "OpenParrot" : _componentUpdated;
            System.Diagnostics.Process.Start("https://github.com/teknogods/" + repo + "/commits/master");
        }

        private void ButtonUpdate_Click(object sender, RoutedEventArgs e)
        {
            DownloadWindow downloadWindow = new DownloadWindow(_latestRelease.assets[0].browser_download_url, _componentUpdated + ".zip");
            downloadWindow.Closed += afterDownload;
            downloadWindow.Show();
        }

        private void afterDownload(object sender, EventArgs e)
        {
            ZipArchive archive = ZipFile.OpenRead(_componentUpdated + ".zip");
            string myExeDir = AppDomain.CurrentDomain.BaseDirectory;
            
            Extract(archive, myExeDir);

            if (_componentUpdated == "TeknoParrotUI")
            {
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
        }

        private void UpdateCleanup()
        {
            try
            {  
                File.Delete(_componentUpdated + ".zip");
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

        private void Extract(ZipArchive archive, string destinationDirectoryName)
        {
            try
            {
                int count = 0;
                int current = 0;
                string openParrot = "";

                if (_componentUpdated != "TeknoParrotUI")
                {
                    openParrot = ".\\" + _componentUpdated + "\\";
                }

                if (_componentUpdated == "OpenSegaAPI")
                {
                    openParrot = ".\\TeknoParrot\\";
                }

                if (_componentUpdated != "TeknoParrotUI")
                {
                    Directory.CreateDirectory(openParrot);
                }

                foreach (ZipArchiveEntry file in archive.Entries)
                {
                    Debug.WriteLine(file.Name);
                    if (file.Name == openParrot + "")
                    {
                        //issa directory
                        count += 1;
                    }
                    else
                    {
                        count += 1;

                        try
                        {
                            File.Move(openParrot + file.FullName, openParrot + file.FullName + ".bak");
                        }
                        catch
                        {
                            //most likely either the file doesn't exist (so it's new in this release) or it's in use so we'll skip it
                        }
                    }
                }

                foreach (var file in archive.Entries)
                {
                    Debug.WriteLine(file.Name);

                    string completeFileName = System.IO.Path.Combine(destinationDirectoryName, openParrot + file.FullName);
                    if (file.Name == string.Empty)
                    { 
                        //Assuming Empty for Directory
                        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(completeFileName));
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
                }

                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\TeknoGods\TeknoParrot", true))
                    key.SetValue(_componentUpdated, _latestRelease.id);
                
                archive.Dispose();
                UpdateCleanup();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
    }
}
