using System;
using System.Net;
using System.Windows;
using Microsoft.Win32;

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
                using (var key = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"))
                {
                    key?.SetValue(exePath, "HIGHDPIAWARE", RegistryValueKind.String);
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
                MessageBox.Show("Your AMD driver is unsupported for this game. \nIf the game crashes or has new graphical issues, please downgrade to the AMD driver version 22.5.1 or older", "Teknoparrot UI");
            }
        }
    }
}