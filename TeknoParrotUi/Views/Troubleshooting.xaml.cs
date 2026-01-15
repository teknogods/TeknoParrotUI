using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using OSVersionExtension;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;

namespace TeknoParrotUi.Views
{
    public partial class Troubleshooting
    {
        private static readonly string[] filteredGameConfigValues = { "APM3ID", "OnlineId", "PlayerId", "Pass" };
        public Troubleshooting()
        {
            InitializeComponent();
            ObtainInformation();
            TroubleshootingSnackbar.MessageQueue = new SnackbarMessageQueue(TimeSpan.FromMilliseconds(2000));
        }

        private static string GetTpVersions()
        {
            StringBuilder versionInfo = new StringBuilder();
            foreach (var component in MainWindow.components)
            {
                versionInfo.AppendLine($"- {component.name}: {component._localVersion}");
            }

            return versionInfo.ToString();
        }

        private static string GetCpuName()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                {
                    foreach (var item in searcher.Get())
                    {
                        return item["Name"].ToString().Trim();
                    }
                }
            }
            catch
            {
                return "Unknown";
            }
            return "Unknown";
        }

        private static string GetTotalRam()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (var item in searcher.Get())
                    {
                        long bytes = Convert.ToInt64(item["TotalPhysicalMemory"]);
                        double gb = bytes / (1024.0 * 1024.0 * 1024.0);
                        return $"{Math.Round(gb, 2)} GB";
                    }
                }
            }
            catch
            {
                return "Unknown";
            }
            return "Unknown";
        }

        private static string GetGpuName()
        {
            try
            {
                var gpuList = new List<string>();
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                {
                    foreach (var item in searcher.Get())
                    {
                        string name = item["Name"]?.ToString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            gpuList.Add(name.Trim());
                        }
                    }
                }
                return gpuList.Count > 0 ? string.Join(", ", gpuList) : "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private static string GetMonitorResolutions()
        {
            try
            {
                var monitors = System.Windows.Forms.Screen.AllScreens;
                var resolutions = new StringBuilder();
                for (int i = 0; i < monitors.Length; i++)
                {
                    var screen = monitors[i];
                    string primary = screen.Primary ? " (Primary)" : "";
                    resolutions.AppendLine($"  Monitor {i + 1}: {screen.Bounds.Width}x{screen.Bounds.Height}{primary}");
                }
                return resolutions.ToString().TrimEnd('\r', '\n');
            }
            catch
            {
                return "  Unknown";
            }
        }

        private static string GetNetworkAdapters()
        {
            try
            {
                var adapters = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Select(n => new
                    {
                        Adapter = n,
                        Metric = GetAdapterMetric(n)
                    })
                    .OrderBy(x => x.Metric)
                    .ToList();

                var result = new StringBuilder();
                foreach (var item in adapters)
                {
                    var adapter = item.Adapter;
                    var ipProperties = adapter.GetIPProperties();
                    var ipv4Addresses = ipProperties.UnicastAddresses
                        .Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(ip => ip.Address.ToString())
                        .ToList();

                    if (ipv4Addresses.Count > 0)
                    {
                        result.AppendLine($"  {adapter.Name}: {string.Join(", ", ipv4Addresses)}");
                    }
                }

                return result.Length > 0 ? result.ToString().TrimEnd('\r', '\n') : "  None";
            }
            catch
            {
                return "  Unknown";
            }
        }

        private static int GetAdapterMetric(NetworkInterface adapter)
        {
            try
            {
                return adapter.GetIPProperties().GetIPv4Properties()?.Index ?? int.MaxValue;
            }
            catch
            {
                return int.MaxValue;
            }
        }

        private static string GetSerialPorts()
        {
            try
            {
                var ports = SerialPort.GetPortNames();
                if (ports.Length > 0)
                {
                    return "  " + string.Join(", ", ports.OrderBy(p => p));
                }
                return "  None";
            }
            catch
            {
                return "  Unknown";
            }
        }

        private static string GetLastPlayedGameInfo()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Lazydata.ParrotData?.LastPlayed))
                {
                    return "  None";
                }

                var result = new StringBuilder();
                result.AppendLine($"  Last Played: {Lazydata.ParrotData.LastPlayed}");

                GameProfile userProfile = null;

                if (GameProfileLoader.UserProfiles != null)
                {
                    userProfile = GameProfileLoader.UserProfiles.FirstOrDefault(p => p.GameNameInternal == Lazydata.ParrotData.LastPlayed);
                }

                if (userProfile == null && GameProfileLoader.GameProfiles != null)
                {
                    userProfile = GameProfileLoader.GameProfiles.FirstOrDefault(p => p.GameNameInternal == Lazydata.ParrotData.LastPlayed);
                }

                if (userProfile != null)
                {
                    result.AppendLine($"  Profile: {userProfile.ProfileName}");
                    result.AppendLine($"  Game Path: {userProfile.GamePath ?? "Not set"}");
                    if(userProfile.HasTwoExecutables)
                    {
                        result.AppendLine($"  Second Game Path: {userProfile.GamePath2 ?? "Not set"}");
                    }
                    result.AppendLine($"  Emulator: {userProfile.EmulatorType}");
                    if (userProfile.ConfigValues != null && userProfile.ConfigValues.Count > 0)
                    {
                        result.AppendLine($"  Config Values:");
                        foreach (var config in userProfile.ConfigValues)
                        {
                            bool isFiltered = false;
                            foreach (var filter in filteredGameConfigValues)
                            {
                                if (string.Equals(config.FieldName, filter, StringComparison.OrdinalIgnoreCase))
                                {
                                    isFiltered = true;
                                    break;
                                }
                            }

                            string normalizedValue = config.FieldValue;
                            if(config.FieldType == FieldType.Bool)
                            {
                                if (config.FieldValue == "1" || config.FieldValue.Equals("true", StringComparison.OrdinalIgnoreCase))
                                {
                                    normalizedValue = "True";
                                }
                                else if (config.FieldValue == "0" || config.FieldValue.Equals("false", StringComparison.OrdinalIgnoreCase))
                                {
                                    normalizedValue = "False";
                                }
                            }

                            if(isFiltered)
                            {
                                normalizedValue = "CENSORED FOR PRIVACY";
                            }

                            result.AppendLine($"    - [{config.CategoryName}] {config.FieldName}: {normalizedValue}");
                        }
                    }
                }
                else
                {
                    result.AppendLine($"  Profile not found in loaded games");
                }

                return result.ToString().TrimEnd('\r', '\n');
            }
            catch (Exception ex)
            {
                return $"  Error retrieving last played game info: {ex.Message}";
            }
        }

        private static string CheckVCRuntimes()
        {
            StringBuilder runtimeVersions = new StringBuilder();
            bool isInstalledX86 = ReadyCheck.IsVCRuntimeInstalled("x86");
            bool isInstalledX64 = ReadyCheck.IsVCRuntimeInstalled("x64");
            string versionX86 = ReadyCheck.GetVCRuntimeVersion("x86");
            string versionX64 = ReadyCheck.GetVCRuntimeVersion("x64");
            runtimeVersions.AppendLine($"  Visual C++ 2015-2019 Redistributable (x86): {(isInstalledX86 ? "Installed" : "Not Installed")} (Version: {versionX86})");
            runtimeVersions.AppendLine($"  Visual C++ 2015-2019 Redistributable (x64): {(isInstalledX64 ? "Installed" : "Not Installed")} (Version: {versionX64})");

            return runtimeVersions.ToString();
        }

        private static string GetLastPlayedRawXml()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Lazydata.ParrotData?.LastPlayed))
                {
                    return "  No last played game recorded";
                }

                GameProfile userProfile = null;
                string xmlFilePath = null;

                if (GameProfileLoader.UserProfiles != null)
                {
                    userProfile = GameProfileLoader.UserProfiles.FirstOrDefault(p => p.GameNameInternal == Lazydata.ParrotData.LastPlayed);
                    if (userProfile != null && !string.IsNullOrWhiteSpace(userProfile.FileName))
                    {
                        xmlFilePath = userProfile.FileName;
                    }
                }

                if (xmlFilePath == null && GameProfileLoader.GameProfiles != null)
                {
                    userProfile = GameProfileLoader.GameProfiles.FirstOrDefault(p => p.GameNameInternal == Lazydata.ParrotData.LastPlayed);
                    if (userProfile != null && !string.IsNullOrWhiteSpace(userProfile.FileName))
                    {
                        xmlFilePath = userProfile.FileName;
                    }
                }

                if (!string.IsNullOrWhiteSpace(xmlFilePath) && File.Exists(xmlFilePath))
                {
                    var xmlContent = File.ReadAllText(xmlFilePath);
                    return $"File: {xmlFilePath}\n\n{xmlContent}";
                }

                return "  Profile XML file not found";
            }
            catch (Exception ex)
            {
                return $"  Error reading XML file: {ex.Message}";
            }
        }

        private void ObtainInformation()
        {
            string CurrentDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            StringBuilder systemInfo = new StringBuilder();
            systemInfo.AppendLine("TeknoParrot System Information");
            systemInfo.AppendLine($"Generated on: {CurrentDate}");

            systemInfo.AppendLine();
            systemInfo.AppendLine("=== TeknoParrot Version Info ===");
            systemInfo.Append(GetTpVersions());

            systemInfo.AppendLine();
            systemInfo.AppendLine("=== System Information ===");

            string windowsVersion = "";
            if (OSVersion.GetOSVersion().Version.Major >= 10)
            {
                windowsVersion = $"{OSVersion.GetOperatingSystem()} Version {OSVersion.MajorVersion10Properties().DisplayVersion} (Build {OSVersion.GetOSVersion().Version.Build}.{OSVersion.MajorVersion10Properties().UBR})";
            }
            else
            {
                windowsVersion = $"{OSVersion.GetOperatingSystem()} Version {OSVersion.GetOSVersion().Version.Major}.{OSVersion.GetOSVersion().Version.Minor}.{OSVersion.GetOSVersion().Version.Build}";
            }
            systemInfo.AppendLine(windowsVersion);
            systemInfo.AppendLine($"CPU: {GetCpuName()}");
            systemInfo.AppendLine($"RAM: {GetTotalRam()}");
            systemInfo.AppendLine($"GPU: {GetGpuName()}");
            systemInfo.AppendLine($"Architecture: {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")}");
            systemInfo.AppendLine($"Monitor Resolution(s):");
            systemInfo.AppendLine(GetMonitorResolutions());
            systemInfo.AppendLine($"Network Adapters:");
            systemInfo.AppendLine(GetNetworkAdapters());
            systemInfo.AppendLine($"Serial Ports:");
            systemInfo.AppendLine(GetSerialPorts());
            systemInfo.AppendLine("Prerequisites:");
            systemInfo.AppendLine(CheckVCRuntimes());

            systemInfo.AppendLine();
            systemInfo.AppendLine("=== Last Played Game ===");
            systemInfo.AppendLine(GetLastPlayedGameInfo());

            // This bloats the size quite a bit, making it hard to just copy paste into discord.
            // For now, might be better to ask for the xml seperately if really needed
            /*            systemInfo.AppendLine();
                        systemInfo.AppendLine("=== Last Played Game Profile (Raw XML) ===");
                        systemInfo.Append(GetLastPlayedRawXml());*/

            TextBoxSystemInfo.Text = systemInfo.ToString();
        }

        private void BtnCopyToClipboard(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(TextBoxSystemInfo.Text);
            TroubleshootingSnackbar.MessageQueue.Enqueue("System info copied to clipboard");
        }

        private void BtnSaveToFile(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "TeknoParrot_SystemInfo.txt",
                    DefaultExt = ".txt",
                    Filter = "Text documents (.txt)|*.txt"
                };

                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    System.IO.File.WriteAllText(dialog.FileName, TextBoxSystemInfo.Text);
                    TroubleshootingSnackbar.MessageQueue.Enqueue("System info saved to file");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving system info to file: {ex}");
                TroubleshootingSnackbar.MessageQueue.Enqueue("Error saving system info to file");
            }
        }
    }
}
