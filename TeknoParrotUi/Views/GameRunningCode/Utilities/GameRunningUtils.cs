using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Windows;
using Microsoft.Win32;
using TeknoParrotUi.Properties;

namespace TeknoParrotUi.Views.GameRunningCode.Utilities
{
    internal static class GameRunningUtils
    {
        public static IPAddress GetNetworkAddress(IPAddress address, IPAddress subnetMask)
        {
            byte[] ipAdressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

            byte[] broadcastAddress = new byte[ipAdressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] & (subnetMaskBytes[i]));
            }
            return new IPAddress(broadcastAddress);
        }

        public static void SetDPIAwareRegistryValue(string exePath)
        {
            try
            {
                string registryKeyPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
                using (var key = Registry.CurrentUser.OpenSubKey(registryKeyPath, true) ??
                                 Registry.CurrentUser.CreateSubKey(registryKeyPath))
                {
                    string existingValue = key.GetValue(exePath) as string ?? string.Empty;

                    // I am unsure what happens if there's multiple of these set at ones, as in if windows is clever enough to
                    // only use the last one supplied (which should be ours, normally) so, let's make sure we remove all existing ones first.
                    // We want to keep stuff like WinXP compatibility though as that fixes some games I think?
                    // We also remove ours if it's there so we can add it back in without adding it multiple times
                    // Not sure if there's more flags, these are what are on my system somehow. Google doesn't give me a good list...
                    string[] dpiFlags = new[] { "DPIUNAWARE", "GDIDPISCALING","~DPIUNAWARE", "~GDIDPISCALING", "~" };
                    var flags = new HashSet<string>(existingValue.Split(' '));
                    foreach (string flag in dpiFlags)
                    {
                        flags.Remove(flag);
                    }

                    if (!flags.Contains("HIGHDPIAWARE"))
                    {
                        flags.Add("HIGHDPIAWARE");
                    }

                    string newValue = string.Join(" ", flags);
                    key.SetValue(exePath, newValue, RegistryValueKind.String);
                }
            }
            catch
            {
                // Ignore registry errors
            }
        }

        // Nezarn's old "Nirin flicker fix" (Mesa 22.1.7) told people to copy these into ElfLdr2.
        // Thanks to BudgieLoader.exe.local, every ElfLdr2 game then renders through Mesa instead of the GPU driver,
        // which breaks a bunch of them (LGJ black screen, ABC 3D, Race TV...). The flicker is fixed in TeknoParrot.dll now.
        private const string NirinMesaFolderName = "disabled_nirin_mesa";
        private static readonly Dictionary<string, string> NirinMesaFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "opengl32.dll", "82bbb5b0b82122a3f765794e5a29c8d4" },
            { "opengl32sw.dll", "82bbb5b0b82122a3f765794e5a29c8d4" },
            { "libgallium_wgl.dll", "5f9c254d7ea028910d56abad0068f86d" },
            { "libglapi.dll", "2de98d4694377e5c4f5613de2ddc8a66" },
            { "dxil.dll", "160949bd77b331b4442126a7a22e662c" },
            { "BudgieLoader.exe.local", "e35317955041885d80e3a7850e30874e" },
        };

        private static bool FileMd5Matches(string path, string md5)
        {
            using (var hasher = MD5.Create())
            using (var stream = File.OpenRead(path))
            {
                var hash = BitConverter.ToString(hasher.ComputeHash(stream)).Replace("-", string.Empty);
                return string.Equals(hash, md5, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Moves the obsolete Nirin Mesa files out of the loader folder so the real GPU driver gets used.
        /// Only acts if opengl32.dll is the Mesa one, never throws.
        /// </summary>
        /// <returns>The folder the files were moved to if opengl32.dll was disabled, otherwise null.</returns>
        public static string DisableObsoleteNirinMesaFiles(string loaderFolder)
        {
            try
            {
                var openGlPath = Path.Combine(loaderFolder, "opengl32.dll");
                if (!File.Exists(openGlPath) || !FileMd5Matches(openGlPath, NirinMesaFiles["opengl32.dll"]))
                {
                    return null;
                }

                var disabledFolder = Path.Combine(loaderFolder, NirinMesaFolderName);
                Directory.CreateDirectory(disabledFolder);

                bool openGlMoved = false;
                foreach (var file in NirinMesaFiles)
                {
                    var source = Path.Combine(loaderFolder, file.Key);
                    try
                    {
                        if (!File.Exists(source) || !FileMd5Matches(source, file.Value))
                        {
                            continue;
                        }

                        var destination = Path.Combine(disabledFolder, file.Key);
                        if (File.Exists(destination))
                        {
                            File.Delete(destination);
                        }
                        File.Move(source, destination);

                        if (file.Key == "opengl32.dll")
                        {
                            openGlMoved = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not disable old Nirin Mesa file {source}: {ex.Message}");
                    }
                }

                try
                {
                    File.WriteAllText(Path.Combine(disabledFolder, "README.txt"),
                        "These files were the old Nirin flicker fix (Mesa 22.1.7).\r\n" +
                        "TeknoParrot fixes the flicker itself now, and these files broke the graphics of other games, so they were moved here.\r\n" +
                        "They are not needed any more.\r\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not write Nirin Mesa README: {ex.Message}");
                }

                return openGlMoved ? disabledFolder : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Old Nirin Mesa file check failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Let people know why IDAS won't work if they're on newer AMD drivers
        /// </summary>
        public static void CheckAMDDriver()
        {
            bool nvidiaFound = false;
            bool badDriver = false;
            using (var searcher = new System.Management.ManagementObjectSearcher("select * from Win32_VideoController"))
            {
                try
                {
                    foreach (System.Management.ManagementObject obj in searcher.Get())
                    {
                        string driverVersionString = obj["DriverVersion"].ToString();
                        long driverVersion = Int64.Parse(driverVersionString.Replace(".", string.Empty));

                        if (obj["Name"].ToString().Contains("AMD"))
                        {
                            if (driverVersion > 3002101710000)
                            {
                                badDriver = true;
                            }
                        }
                        else if (obj["Name"].ToString().Contains("NVIDIA"))
                        {
                            nvidiaFound = true;
                        }
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("AMD driver check failed, probably because WMI is not working on the users system (IE: borked windows installed)");
                }
            }

            // Making sure there is no nvidia gpu before we throw this MSG to not confuse people with Ryzen Laptops + NVIDIA DGPU
            if (badDriver && !nvidiaFound)
            {
                MessageBox.Show(Resources.GameRunningUtilsAMDDriverUnsupported, Resources.GameRunningUtilsTeknoParrotUI);
            }
        }
    }
}