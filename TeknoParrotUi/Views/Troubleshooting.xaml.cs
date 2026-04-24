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
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using OSVersionExtension;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;

namespace TeknoParrotUi.Views
{
    public partial class Troubleshooting
    {
        private static readonly string[] filteredGameConfigValues = { "APM3ID", "OnlineId", "PlayerId", "Pass", "PCB ID", "Card ID", "Card ID P1", "Card ID P2" };
        private static readonly int[] commonAudioSampleRates = { 8000, 11025, 16000, 22050, 32000, 44100, 48000, 88200, 96000, 176400, 192000 };
        private static readonly PROPERTYKEY PKEY_Device_FriendlyName = new PROPERTYKEY
        {
            fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
            pid = 14
        };

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

        private static string GetAudioDevices()
        {
            IMMDeviceEnumerator enumerator = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();

                var result = new StringBuilder();
                result.AppendLine("Playback Devices:");
                result.AppendLine(GetAudioDevicesForFlow(enumerator, EDataFlow.eRender));
                result.AppendLine("Recording Devices:");
                result.Append(GetAudioDevicesForFlow(enumerator, EDataFlow.eCapture));

                return result.ToString().TrimEnd('\r', '\n');
            }
            catch (Exception ex)
            {
                return $"  Error retrieving audio devices: {ex.Message}";
            }
            finally
            {
                ReleaseComObject(enumerator);
            }
        }

        private static string GetAudioDevicesForFlow(IMMDeviceEnumerator enumerator, EDataFlow dataFlow)
        {
            IMMDeviceCollection collection = null;
            IMMDevice defaultDevice = null;

            try
            {
                enumerator.EnumAudioEndpoints(dataFlow, DeviceStateMask.Active, out collection);

                string defaultDeviceId = null;
                try
                {
                    enumerator.GetDefaultAudioEndpoint(dataFlow, ERole.eMultimedia, out defaultDevice);
                    defaultDeviceId = GetDeviceId(defaultDevice);
                }
                catch
                {
                    defaultDeviceId = null;
                }

                collection.GetCount(out var count);
                if (count == 0)
                {
                    return "  None";
                }

                var activeDevices = new List<string>();
                string defaultActiveDevice = null;
                for (uint index = 0; index < count; index++)
                {
                    IMMDevice device = null;
                    try
                    {
                        collection.Item(index, out device);
                        string deviceId = GetDeviceId(device);
                        string friendlyName = GetDeviceFriendlyName(device);
                        string defaultSuffix = string.Equals(deviceId, defaultDeviceId, StringComparison.OrdinalIgnoreCase) ? " [Default]" : string.Empty;

                        device.GetState(out var state);
                        var entry = new StringBuilder();
                        entry.AppendLine($"  {friendlyName}{defaultSuffix} ({GetDeviceStateName(state)})");
                        if (ShouldShowAudioFormatDetails(state))
                        {
                            entry.AppendLine($"    Current Format: {GetCurrentAudioFormat(device)}");
                            entry.AppendLine($"    Supported Shared Rates: {GetSupportedAudioRates(device)}");
                        }

                        string deviceEntry = entry.ToString().TrimEnd('\r', '\n');
                        if (state == (uint)DeviceStateMask.Active)
                        {
                            if (string.Equals(deviceId, defaultDeviceId, StringComparison.OrdinalIgnoreCase))
                            {
                                defaultActiveDevice = deviceEntry;
                            }
                            else
                            {
                                activeDevices.Add(deviceEntry);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        activeDevices.Add($"  Device {index + 1}: Error retrieving details ({ex.Message})");
                    }
                    finally
                    {
                        ReleaseComObject(device);
                    }
                }

                var result = new StringBuilder();
                // Let's only do active devices because on my system adding all the disabled and unplugged ones
                // clutters this up like CRAZY. (I use my PC for guitar stuff and so on)
                // Unless I am wrong, I don't think games can or will try disabled and unplugged devices anyway. I hope.
                if (!string.IsNullOrWhiteSpace(defaultActiveDevice))
                {
                    result.AppendLine(defaultActiveDevice);
                }

                foreach (var activeDevice in activeDevices)
                {
                    result.AppendLine(activeDevice);
                }

                return result.ToString().TrimEnd('\r', '\n');
            }
            catch (Exception ex)
            {
                return $"  Error retrieving devices: {ex.Message}";
            }
            finally
            {
                ReleaseComObject(defaultDevice);
                ReleaseComObject(collection);
            }
        }

        private static string GetCurrentAudioFormat(IMMDevice device)
        {
            IAudioClient audioClient = null;
            IntPtr mixFormatPointer = IntPtr.Zero;

            try
            {
                audioClient = ActivateAudioClient(device);
                audioClient.GetMixFormat(out mixFormatPointer);
                if (mixFormatPointer == IntPtr.Zero)
                {
                    return "Unknown";
                }

                var mixFormat = (WAVEFORMATEX)Marshal.PtrToStructure(mixFormatPointer, typeof(WAVEFORMATEX));
                return FormatWaveFormat(mixFormat);
            }
            catch (Exception ex)
            {
                return $"Unknown ({ex.Message})";
            }
            finally
            {
                if (mixFormatPointer != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(mixFormatPointer);
                }

                ReleaseComObject(audioClient);
            }
        }

        private static string GetSupportedAudioRates(IMMDevice device)
        {
            IAudioClient audioClient = null;
            IntPtr mixFormatPointer = IntPtr.Zero;

            try
            {
                audioClient = ActivateAudioClient(device);
                audioClient.GetMixFormat(out mixFormatPointer);
                if (mixFormatPointer == IntPtr.Zero)
                {
                    return "Unknown";
                }

                byte[] mixFormatBytes = GetWaveFormatBytes(mixFormatPointer);
                var mixFormat = (WAVEFORMATEX)Marshal.PtrToStructure(mixFormatPointer, typeof(WAVEFORMATEX));
                var supportedRates = new List<string>();

                foreach (var rate in commonAudioSampleRates)
                {
                    if (IsAudioFormatSupported(audioClient, mixFormatBytes, mixFormat.nBlockAlign, rate))
                    {
                        supportedRates.Add(rate.ToString());
                    }
                }

                if (supportedRates.Count == 0)
                {
                    supportedRates.Add(mixFormat.nSamplesPerSec.ToString());
                }

                return string.Join(", ", supportedRates.Distinct()) + " Hz";
            }
            catch (Exception ex)
            {
                return $"Unknown ({ex.Message})";
            }
            finally
            {
                if (mixFormatPointer != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(mixFormatPointer);
                }

                ReleaseComObject(audioClient);
            }
        }

        private static IAudioClient ActivateAudioClient(IMMDevice device)
        {
            object audioClientObject;
            Guid audioClientGuid = typeof(IAudioClient).GUID;
            device.Activate(ref audioClientGuid, (uint)ClsCtx.All, IntPtr.Zero, out audioClientObject);
            return (IAudioClient)audioClientObject;
        }

        private static bool IsAudioFormatSupported(IAudioClient audioClient, byte[] baseFormatBytes, ushort blockAlign, int sampleRate)
        {
            IntPtr formatPointer = IntPtr.Zero;
            IntPtr closestMatchPointer = IntPtr.Zero;

            try
            {
                byte[] candidateFormatBytes = (byte[])baseFormatBytes.Clone();
                WriteInt32(candidateFormatBytes, 4, sampleRate);
                WriteInt32(candidateFormatBytes, 8, sampleRate * blockAlign);

                formatPointer = Marshal.AllocCoTaskMem(candidateFormatBytes.Length);
                Marshal.Copy(candidateFormatBytes, 0, formatPointer, candidateFormatBytes.Length);

                audioClient.IsFormatSupported(AudioClientShareMode.Shared, formatPointer, out closestMatchPointer);
                return true;
            }
            catch (COMException ex) when ((uint)ex.ErrorCode == 0x88890008)
            {
                return false;
            }
            finally
            {
                if (closestMatchPointer != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(closestMatchPointer);
                }

                if (formatPointer != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(formatPointer);
                }
            }
        }

        private static byte[] GetWaveFormatBytes(IntPtr formatPointer)
        {
            var format = (WAVEFORMATEX)Marshal.PtrToStructure(formatPointer, typeof(WAVEFORMATEX));
            int size = Marshal.SizeOf(typeof(WAVEFORMATEX)) + format.cbSize;
            var bytes = new byte[size];
            Marshal.Copy(formatPointer, bytes, 0, size);
            return bytes;
        }

        private static void WriteInt32(byte[] bytes, int offset, int value)
        {
            byte[] valueBytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(valueBytes, 0, bytes, offset, valueBytes.Length);
        }

        private static string FormatWaveFormat(WAVEFORMATEX format)
        {
            return $"{format.nSamplesPerSec} Hz, {format.nChannels} ch, {format.wBitsPerSample}-bit";
        }

        private static string GetDeviceFriendlyName(IMMDevice device)
        {
            IPropertyStore propertyStore = null;
            PROPVARIANT value = new PROPVARIANT();

            try
            {
                device.OpenPropertyStore(StorageAccessMode.Read, out propertyStore);
                var propertyKey = PKEY_Device_FriendlyName;
                propertyStore.GetValue(ref propertyKey, out value);

                string friendlyName = value.GetValue();
                return string.IsNullOrWhiteSpace(friendlyName) ? "Unknown Audio Device" : friendlyName;
            }
            catch
            {
                return "Unknown Audio Device";
            }
            finally
            {
                PropVariantClear(ref value);
                ReleaseComObject(propertyStore);
            }
        }

        private static string GetDeviceId(IMMDevice device)
        {
            IntPtr idPointer = IntPtr.Zero;
            try
            {
                device.GetId(out idPointer);
                return Marshal.PtrToStringUni(idPointer);
            }
            finally
            {
                if (idPointer != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(idPointer);
                }
            }
        }

        private static string GetDeviceStateName(uint state)
        {
            if (state == (uint)DeviceStateMask.Active)
            {
                return "Active";
            }

            if (state == (uint)DeviceStateMask.Disabled)
            {
                return "Disabled";
            }

            if (state == (uint)DeviceStateMask.NotPresent)
            {
                return "Not Present";
            }

            if (state == (uint)DeviceStateMask.Unplugged)
            {
                return "Unplugged";
            }

            return $"State {state}";
        }

        private static bool ShouldShowAudioFormatDetails(uint state)
        {
            return state != (uint)DeviceStateMask.Unplugged && state != (uint)DeviceStateMask.NotPresent && state != (uint)DeviceStateMask.Disabled;
        }

        private static void ReleaseComObject(object comObject)
        {
            if (comObject != null && Marshal.IsComObject(comObject))
            {
                Marshal.FinalReleaseComObject(comObject);
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
                    if (userProfile.HasTwoExecutables)
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
                            if (config.FieldType == FieldType.Bool)
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

                            if (isFiltered)
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
            systemInfo.AppendLine("Audio Devices:");
            systemInfo.AppendLine(GetAudioDevices());
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

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PROPVARIANT pvar);

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject
        {
        }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            void EnumAudioEndpoints(EDataFlow dataFlow, DeviceStateMask stateMask, out IMMDeviceCollection devices);
            void GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
            void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        }

        [ComImport]
        [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceCollection
        {
            void GetCount(out uint count);
            void Item(uint index, out IMMDevice device);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            void Activate(ref Guid interfaceId, uint clsContext, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
            void OpenPropertyStore(StorageAccessMode accessMode, out IPropertyStore properties);
            void GetId(out IntPtr id);
            void GetState(out uint state);
        }

        [ComImport]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            void GetCount(out uint propertyCount);
            void GetAt(uint propertyIndex, out PROPERTYKEY key);
            void GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
        }

        [ComImport]
        [Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioClient
        {
            void Initialize(AudioClientShareMode shareMode, uint streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr format, IntPtr audioSessionGuid);
            void GetBufferSize(out uint bufferSize);
            void GetStreamLatency(out long latency);
            void GetCurrentPadding(out uint currentPadding);
            void IsFormatSupported(AudioClientShareMode shareMode, IntPtr format, out IntPtr closestMatchFormat);
            void GetMixFormat(out IntPtr deviceFormatPointer);
            void GetDevicePeriod(out long defaultDevicePeriod, out long minimumDevicePeriod);
            void Start();
            void Stop();
            void Reset();
            void SetEventHandle(IntPtr eventHandle);
            void GetService(ref Guid interfaceId, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
        }

        private enum EDataFlow
        {
            eRender = 0,
            eCapture = 1,
            eAll = 2
        }

        private enum ERole
        {
            eConsole = 0,
            eMultimedia = 1,
            eCommunications = 2
        }

        [Flags]
        private enum DeviceStateMask : uint
        {
            Active = 0x00000001,
            Disabled = 0x00000002,
            NotPresent = 0x00000004,
            Unplugged = 0x00000008,
            All = Active | Disabled | NotPresent | Unplugged
        }

        private enum StorageAccessMode : uint
        {
            Read = 0x00000000
        }

        private enum AudioClientShareMode
        {
            Shared = 0,
            Exclusive = 1
        }

        [Flags]
        private enum ClsCtx : uint
        {
            InprocServer = 0x1,
            InprocHandler = 0x2,
            LocalServer = 0x4,
            RemoteServer = 0x10,
            All = InprocServer | InprocHandler | LocalServer | RemoteServer
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct PROPVARIANT
        {
            [FieldOffset(0)]
            private ushort valueType;

            [FieldOffset(8)]
            private IntPtr pointerValue;

            public string GetValue()
            {
                const ushort VT_LPWSTR = 31;
                const ushort VT_BSTR = 8;

                if (valueType == VT_LPWSTR || valueType == VT_BSTR)
                {
                    return Marshal.PtrToStringUni(pointerValue);
                }

                return null;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }
    }
}
