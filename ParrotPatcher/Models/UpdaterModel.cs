using System;
using System.Threading.Tasks;

namespace ParrotPatcher.Models
{
    public class UpdaterModel
    {
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string UpdateUrl { get; set; }

        public event Action<string> UpdateAvailable;

        public void CheckForUpdates()
        {
            // Logic to check for updates
            // If an update is available, raise the UpdateAvailable event
            if (LatestVersion != CurrentVersion)
            {
                UpdateAvailable?.Invoke(LatestVersion);
            }
        }

        public void DownloadUpdate()
        {
            // Logic to download the update from UpdateUrl
        }

        public void InstallUpdate()
        {
            // Logic to install the downloaded update
        }

        internal async Task<bool> CheckForUpdatesAsync()
        {
            if (LatestVersion != CurrentVersion)
            {
                UpdateAvailable?.Invoke(LatestVersion);
            }
            return true;
        }

    }
}