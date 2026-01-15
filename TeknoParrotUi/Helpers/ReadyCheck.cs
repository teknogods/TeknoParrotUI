using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeknoParrotUi.Helpers
{
    public class ReadyCheck
    {
        public static bool IsVCRuntimeNewEnough(
            string arch,        // "x86" or "x64"
            Version minVersion)
        {
            var view = arch == "x64"
                ? RegistryView.Registry64
                : RegistryView.Registry32;

            using (var baseKey = RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine, view))
            {
                using (var key = baseKey.OpenSubKey(
                    $@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\{arch}"))
                {
                    if (key == null)
                        return false;

                    if ((int?)key.GetValue("Installed") != 1)
                        return false;

                    var major = (int?)key.GetValue("Major") ?? 0;
                    var minor = (int?)key.GetValue("Minor") ?? 0;
                    var bld = (int?)key.GetValue("Bld") ?? 0;

                    var installed = new Version(major, minor, bld);

                    return installed >= minVersion;
                }
            }
        }

        public static bool IsVCRuntimeInstalled(string arch)
        {
            var view = arch == "x64"
                ? RegistryView.Registry64
                : RegistryView.Registry32;

            using (var baseKey = RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine, view))
            {
                using (var key = baseKey.OpenSubKey(
                    $@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\{arch}"))
                {
                    if (key == null)
                        return false;

                    if ((int?)key.GetValue("Installed") != 1)
                        return false;

                    return true;
                }
            }
        }

        public static string GetVCRuntimeVersion(string arch)
        {
            var view = arch == "x64"
                ? RegistryView.Registry64
                : RegistryView.Registry32;

            using (var baseKey = RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine, view))
            {
                using (var key = baseKey.OpenSubKey(
                    $@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\{arch}"))
                {
                    if (key == null)
                        return "Unknown";

                    if ((int?)key.GetValue("Installed") != 1)
                        return "Not Installed";

                    var major = (int?)key.GetValue("Major") ?? 0;
                    var minor = (int?)key.GetValue("Minor") ?? 0;
                    var bld = (int?)key.GetValue("Bld") ?? 0;

                    return $"{major}.{minor}.{bld}";
                }
            }
        }

    }
}
