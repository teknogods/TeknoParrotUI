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
using TeknoParrotUi.Helpers;
using static TeknoParrotUi.MainWindow;
using Application = System.Windows.Application;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for GitHubUpdates.xaml
    /// </summary>
    public partial class GitHubUpdates : UserControl
    {
        public readonly UpdaterComponent _componentUpdated;
        private readonly GithubRelease _latestRelease;
        private DownloadControl downloadWindow;
        private string onlineVersion;
        public GitHubUpdates(UpdaterComponent componentUpdated, GithubRelease latestRelease, string local, string online)
        {
            InitializeComponent();
            _componentUpdated = componentUpdated;
            if (componentUpdated.name == "TeknoParrotUI")
            {
                labelUpdated.Content = componentUpdated.name + " (Requires App Restart)";
            }
            else
            {
                labelUpdated.Content = componentUpdated.name;
            }

            labelVersion.Content = $"{(local != Properties.Resources.UpdaterNotInstalled ? $"{local} to " : "")}{online}";
            _latestRelease = latestRelease;
            onlineVersion = online;
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            //this.Close();
        }

        private void BtnChangelog(object sender, RoutedEventArgs e)
        {
            Process.Start(_componentUpdated.fullUrl + (_componentUpdated.opensource ? "commits/master" : $"releases/{_componentUpdated.name}"));
        }

        public DownloadControl DoUpdate()
        {
            downloadWindow = new DownloadControl(_latestRelease.assets[0].browser_download_url, $"./cache/{_componentUpdated.name}{onlineVersion}.zip", false, _componentUpdated, onlineVersion);
            return downloadWindow;
        }
    }
}
