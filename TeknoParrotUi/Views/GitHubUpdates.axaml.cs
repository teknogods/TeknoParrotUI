using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using TeknoParrotUi.Common;
using TeknoParrotUi.Components;
using TeknoParrotUi.Helpers;
using static TeknoParrotUi.MainWindow;
namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for GitHubUpdates.axaml
    /// </summary>
    public partial class GitHubUpdates : UserControl
    {
        public readonly UpdaterComponent _componentUpdated;
        private readonly GithubRelease _latestRelease;
        private DownloadControl _downloadControl;
        private string _onlineVersion;

        public GitHubUpdates()
        {
            InitializeComponent();
        }

        public GitHubUpdates(UpdaterComponent componentUpdated, GithubRelease latestRelease, string local, string online)
        {
            InitializeComponent();

            _componentUpdated = componentUpdated;
            _latestRelease = latestRelease;
            _onlineVersion = online;

            // Set UI texts
            if (componentUpdated.name == "TeknoParrotUI")
            {
                labelUpdated.Text = componentUpdated.name + " (Requires App Restart)";
            }
            else
            {
                labelUpdated.Text = componentUpdated.name;
            }

            // Show version info
            labelVersion.Text = $"{(local != "Not Installed" ? $"{local} to " : "")}{online}";
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Find control references
            labelUpdated = this.FindControl<TextBlock>("labelUpdated");
            labelVersion = this.FindControl<TextBlock>("labelVersion");
            isSelectedForUpdate = this.FindControl<CheckBox>("isSelectedForUpdate");
        }

        private async void BtnChangelog(object sender, RoutedEventArgs e)
        {
            try
            {
                var htmlUrl = _componentUpdated.fullUrl + (_componentUpdated.opensource ? "commits/master" : $"releases/{_componentUpdated.name}");
                // Open changelog in browser
                if (_latestRelease != null && !string.IsNullOrEmpty(htmlUrl))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = htmlUrl,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening changelog: {ex.Message}");
            }
        }

        public DownloadControl DoUpdate()
        {
            // Create download control for this update
            _downloadControl = new DownloadControl(_latestRelease.assets[0].browser_download_url, $"./cache/{_componentUpdated.name}{_onlineVersion}.zip", false, _componentUpdated, _onlineVersion);

            return _downloadControl;
        }
    }


}