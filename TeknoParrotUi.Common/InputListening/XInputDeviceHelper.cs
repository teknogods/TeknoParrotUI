using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using SharpDX.DirectInput;

namespace TeknoParrotUi.Common.InputListening
{
    /// <summary>
    /// Detects which DirectInput devices are XInput controllers using the
    /// Microsoft-documented registry approach. XInput devices are registered
    /// under device enum keys with "IG_" (XInput Game interface) in their
    /// device instance path or HardwareID values.
    /// </summary>
    public static class XInputDeviceHelper
    {
        /// <summary>
        /// Returns the set of DirectInput instance GUIDs that correspond to XInput devices.
        /// Used to exclude them from DirectInput enumeration in MergedInput mode.
        /// </summary>
        public static HashSet<Guid> GetXInputDeviceGuids()
        {
            var xinputVidPids = GetXInputVidPids();
            var xinputGuids = new HashSet<Guid>();

            using (var di = new DirectInput())
            {
                foreach (var device in di.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly))
                {
                    // DirectInput product GUID format: {PPPPVVVV-0000-0000-0000-504944564944}
                    // Data1 = (PID << 16) | VID, stored little-endian in ToByteArray()
                    // So bytes[0-1] = VID, bytes[2-3] = PID
                    var bytes = device.ProductGuid.ToByteArray();
                    uint vid = (uint)(bytes[0] | (bytes[1] << 8));
                    uint pid = (uint)(bytes[2] | (bytes[3] << 8));
                    uint vidpid = (vid << 16) | pid;

                    if (xinputVidPids.Contains(vidpid))
                    {
                        Trace.WriteLine($"[XInputDeviceHelper] Excluding XInput device: {device.InstanceName} (VID={vid:X4} PID={pid:X4})");
                        xinputGuids.Add(device.InstanceGuid);
                    }
                }
            }

            if (xinputGuids.Count == 0)
                Trace.WriteLine("[XInputDeviceHelper] No XInput devices detected for exclusion");

            return xinputGuids;
        }

        /// <summary>
        /// Scans registry device enum keys for entries containing "IG_"
        /// (XInput Game interface) and extracts their VID/PID pairs.
        /// Checks all bus types (USB, HID, BTHENUM, etc.) and also
        /// inspects HardwareID values for Bluetooth-connected controllers.
        /// </summary>
        private static HashSet<uint> GetXInputVidPids()
        {
            var vidpids = new HashSet<uint>();

            try
            {
                using (var enumKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum"))
                {
                    if (enumKey == null) return vidpids;

                    // Scan each bus type (USB, HID, BTHENUM, etc.)
                    foreach (var busName in enumKey.GetSubKeyNames())
                    {
                        try
                        {
                            using (var busKey = enumKey.OpenSubKey(busName))
                            {
                                if (busKey == null) continue;

                                foreach (var deviceKeyName in busKey.GetSubKeyNames())
                                {
                                    // Check if the device key name itself contains "IG_"
                                    if (deviceKeyName.IndexOf("IG_", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        ExtractVidPid(deviceKeyName, vidpids);
                                        continue;
                                    }

                                    // For devices without "IG_" in key name (e.g. Bluetooth),
                                    // check HardwareID values in instance subkeys
                                    try
                                    {
                                        using (var deviceKey = busKey.OpenSubKey(deviceKeyName))
                                        {
                                            if (deviceKey == null) continue;

                                            foreach (var instanceName in deviceKey.GetSubKeyNames())
                                            {
                                                try
                                                {
                                                    using (var instanceKey = deviceKey.OpenSubKey(instanceName))
                                                    {
                                                        var hardwareIds = instanceKey?.GetValue("HardwareID") as string[];
                                                        if (hardwareIds == null) continue;

                                                        foreach (var hwId in hardwareIds)
                                                        {
                                                            if (hwId.IndexOf("IG_", StringComparison.OrdinalIgnoreCase) >= 0)
                                                            {
                                                                ExtractVidPid(hwId, vidpids);
                                                                ExtractVidPid(deviceKeyName, vidpids);
                                                            }
                                                        }
                                                    }
                                                }
                                                catch { }
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            Trace.WriteLine($"[XInputDeviceHelper] Found {vidpids.Count} XInput VID/PID pairs in registry");
            return vidpids;
        }

        /// <summary>
        /// Extracts VID/PID from a device path string.
        /// Handles both standard format (VID_XXXX&amp;PID_XXXX) and
        /// Bluetooth format (VID&amp;0002XXXX_PID&amp;XXXX).
        /// </summary>
        private static void ExtractVidPid(string text, HashSet<uint> vidpids)
        {
            // Standard format: VID_XXXX
            var vidMatch = Regex.Match(text, @"VID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase);
            var pidMatch = Regex.Match(text, @"PID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase);

            if (!vidMatch.Success || !pidMatch.Success)
            {
                // Bluetooth format: VID&0002XXXX (last 4 hex chars are VID)
                vidMatch = Regex.Match(text, @"VID&[0-9A-Fa-f]{4}([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase);
                pidMatch = Regex.Match(text, @"PID&([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase);
            }

            if (vidMatch.Success && pidMatch.Success)
            {
                uint vid = Convert.ToUInt32(vidMatch.Groups[1].Value, 16);
                uint pid = Convert.ToUInt32(pidMatch.Groups[1].Value, 16);
                vidpids.Add((vid << 16) | pid);
            }
        }
    }
}
