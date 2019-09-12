using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TeknoParrotUi.Common;
using static TeknoParrotUi.MainWindow;
using Application = System.Windows.Application;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for GitHubUpdates.xaml
    /// </summary>
    public partial class GitHubUpdates : Window
    {
        private readonly UpdaterComponent _componentUpdated;
        private readonly GithubRelease _latestRelease;
        private DownloadWindow downloadWindow;
        private string onlineVersion;
        public GitHubUpdates(UpdaterComponent componentUpdated, GithubRelease latestRelease, string local, string online)
        {
            InitializeComponent();
            _componentUpdated = componentUpdated;
            labelUpdated.Content = componentUpdated.name;
            labelVersion.Content = $"{(!string.IsNullOrEmpty(local) ? $"{local} to " : "")}{online}";
            _latestRelease = latestRelease;
            onlineVersion = online;
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnChangelog(object sender, RoutedEventArgs e)
        {
            Process.Start(_componentUpdated.fullUrl + (_componentUpdated.opensource ? "commits/master" : $"releases/{_componentUpdated.name}"));
        }

        private void ButtonUpdate_Click(object sender, RoutedEventArgs e)
        {
            downloadWindow = new DownloadWindow(_latestRelease.assets[0].browser_download_url, $"{_componentUpdated.name} {onlineVersion}", true);
            downloadWindow.Closed += (x, x2) =>
            {
                if (downloadWindow.data == null)
                    return;

                bool isUI = _componentUpdated.name == "TeknoParrotUI";
                bool isUsingFolderOverride = !string.IsNullOrEmpty(_componentUpdated.folderOverride);
                string destinationFolder = isUsingFolderOverride ? _componentUpdated.folderOverride : _componentUpdated.name;

                if (!isUI)
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    using (var memoryStream = new MemoryStream(downloadWindow.data))
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
                                // couldn't delete, just move for now
                                File.Move(dest, dest + ".bak");
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
                }).Start();

                if (_componentUpdated.name == "TeknoParrotUI")
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

                MessageBox.Show($"Sucessfully updated {_componentUpdated.name} to {onlineVersion}", "TeknoParrot Updater", MessageBoxButton.OK, MessageBoxImage.Information);

                this.Close();
            };
            downloadWindow.Show();
        }
    }
}
