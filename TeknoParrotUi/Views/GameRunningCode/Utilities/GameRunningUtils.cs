using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
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
                catch (Exception e)
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