using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// SegaTools (Initial D Arcade Stage Zero) support — ported verbatim from
    /// the classic UI's GameProcessManager/SegaToolsHelper.
    /// </summary>
    public static class SegaToolsLauncher
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        public static extern bool SetConsoleTitle(string lpConsoleTitle);

        /// <summary>
        /// Copies the SegaTools dlls next to the game exe. Returns the loader
        /// (inject.exe) and hook dll, mirroring Library.ValidateAndRun.
        /// </summary>
        public static bool PrepareLoader(GameProfile profile, out string loaderExe, out string loaderDll, Action<string> log)
        {
            loaderExe = ".\\SegaTools\\inject.exe";
            loaderDll = "idzhook";
            try
            {
                var gameDir = Path.GetDirectoryName(profile.GamePath);
                File.Copy(".\\SegaTools\\aimeio.dll", gameDir + "\\aimeio.dll", true);
                File.Copy(".\\SegaTools\\idzhook.dll", gameDir + "\\idzhook.dll", true);
                File.Copy(".\\SegaTools\\idzio.dll", gameDir + "\\idzio.dll", true);
                File.Copy(".\\SegaTools\\inject.exe", gameDir + "\\inject.exe", true);
                return true;
            }
            catch (Exception ex)
            {
                log?.Invoke($"Error copying SegaTools files: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Generates segatools.ini, copies DEVICE certs and boots minime,
        /// amdaemon and ServerBox. Call right before starting the game process.
        /// </summary>
        public static void PrepareSession(GameProfile profile, Action<string> log)
        {
            SetConsoleTitle("TeknoParrot SegaTools Support");
            string gameDir = Path.GetDirectoryName(profile.GamePath);

            //check for DEVICE folder
            if (Directory.Exists(gameDir + "\\DEVICE"))
            {
                File.Copy(".\\SegaTools\\DEVICE\\billing.pub", gameDir + "\\DEVICE\\billing.pub", true);
                File.Copy(".\\SegaTools\\DEVICE\\ca.crt", gameDir + "\\DEVICE\\ca.crt", true);
            }
            else
            {
                Directory.CreateDirectory(gameDir + "\\DEVICE");
                File.Copy(".\\SegaTools\\DEVICE\\billing.pub", gameDir + "\\DEVICE\\billing.pub");
                File.Copy(".\\SegaTools\\DEVICE\\ca.crt", gameDir + "\\DEVICE\\ca.crt");
            }

            //gen segatools.ini — converts class data to segatools config file
            string fileOutput;
            string amfsDir;
            //idzv1 amfs dir is DIFFERENT TO v2 ergh
            if (profile.GameNameInternal.Contains("ver.2"))
            {
                amfsDir = Directory.GetParent(gameDir).FullName;
            }
            else
            {
                amfsDir = Directory.GetParent(Directory.GetParent(gameDir).FullName).FullName;
            }
            amfsDir += "\\amfs";
            fileOutput = "[vfs]\namfs=" + amfsDir + "\nappdata=" + (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\TeknoParrot\\IDZ\\") + "\n\n[dns]\ndefault=" +
                         profile.ConfigValues.Find(x => x.FieldName.Equals("NetworkAdapterIP")).FieldValue + "\n\n[ds]\nregion";
            if (profile.ConfigValues.Find(x => x.FieldName.Equals("ExportRegion")).FieldValue == "true" || profile.ConfigValues.Find(x => x.FieldName.Equals("ExportRegion")).FieldValue == "1")
            {
                fileOutput += "=4";
            }
            else
            {
                fileOutput += "=1";
            }

            if (profile.GameNameInternal.Contains("ver.2"))
            {
                fileOutput += "\n\n[aime]\naimeGen=1\nfelicaGen=0";
            }
            fileOutput += "\n\n[netenv]";
            if (profile.ConfigValues.Find(x => x.FieldName.Contains("EnableNetenv")).FieldValue == "true" || profile.ConfigValues.Find(x => x.FieldName.Contains("EnableNetenv")).FieldValue == "1")
            {
                fileOutput += "\nenable=1\n\n";
            }
            else
            {
                fileOutput += "\nenable=0\n\n";
            }
            IPAddress ip = IPAddress.Parse(profile.ConfigValues.Find(x => x.FieldName.Equals("NetworkAdapterIP")).FieldValue);
            fileOutput += "[keychip]\nsubnet=" + GetNetworkAddress(ip, IPAddress.Parse("255.255.255.0")) +
                          "\n\n[gpio]\ndipsw1=";
            if (profile.ConfigValues.Find(x => x.FieldName.Equals("EnableDistServ")).FieldValue == "true" || profile.ConfigValues.Find(x => x.FieldName.Equals("EnableDistServ")).FieldValue == "1")
            {
                fileOutput += "1\n\n";
            }
            else
            {
                fileOutput += "0\n\n";
            }

            fileOutput += "[io3]\nmode=";
            fileOutput += "tp\n";
            int shift = 0;
            if (profile.ConfigValues.Find(x => x.FieldName.Equals("EnableRealShifter")).FieldValue == "true" || profile.ConfigValues.Find(x => x.FieldName.Equals("EnableRealShifter")).FieldValue == "1")
            {
                shift = 1;
            }
            fileOutput += "pos_shifter=" + shift + "\nautoNeutral=1\nsingleStickSteering=1\nrestrict=" + profile.ConfigValues.Find(x => x.FieldName.Equals("WheelRestriction")).FieldValue + "\n\n[dinput]\ndeviceName=\nshifterName=\nbrakeAxis=RZ\naccelAxis=Y\nstart=3\nviewChg=10\nshiftDn=1\nshiftUp=2\ngear1=1\ngear2=2\ngear3=3\ngear4=4\ngear5=5\ngear6=6\nreverseAccelAxis=0\nreverseBrakeAxis=0\n";

            if (File.Exists(gameDir + "\\segatools.ini"))
            {
                File.Delete(gameDir + "\\segatools.ini");
            }
            File.WriteAllText(gameDir + "\\segatools.ini", fileOutput);

            new Thread(BootMinime) { IsBackground = true }.Start();
            new Thread(() => BootAmdaemon(gameDir)) { IsBackground = true }.Start();
            new Thread(() => BootServerbox(gameDir)) { IsBackground = true }.Start();
        }

        public static void BootMinime()
        {
            var psiNpmRunDist = new ProcessStartInfo
            {
                FileName = "cmd",
                RedirectStandardInput = true,
                WorkingDirectory = ".\\SegaTools\\minime",
                UseShellExecute = false
            };
            var pNpmRunDist = Process.Start(psiNpmRunDist);
            pNpmRunDist.StandardInput.WriteLine("start.bat");
            pNpmRunDist.WaitForExit();
        }

        public static void BootAmdaemon(string gameDir)
        {
            var psiNpmRunDist = new ProcessStartInfo
            {
                FileName = gameDir + "\\inject.exe",
                WorkingDirectory = gameDir,
                Arguments = "-d -k .\\idzhook.dll .\\amdaemon.exe -c configDHCP_Final_Common.json configDHCP_Final_JP.json configDHCP_Final_JP_ST1.json configDHCP_Final_JP_ST2.json configDHCP_Final_EX.json configDHCP_Final_EX_ST1.json configDHCP_Final_EX_ST2.json",
                UseShellExecute = false
            };
            var pNpmRunDist = Process.Start(psiNpmRunDist);
            pNpmRunDist.WaitForExit();
        }

        public static void BootServerbox(string gameDir)
        {
            var psiNpmRunDist = new ProcessStartInfo
            {
                FileName = gameDir + "\\inject.exe",
                WorkingDirectory = gameDir,
                Arguments = "-d -k .\\idzhook.dll .\\ServerBoxD8_Nu_x64.exe",
                UseShellExecute = false
            };
            var pNpmRunDist = Process.Start(psiNpmRunDist);
            pNpmRunDist.WaitForExit();
        }

        /// <summary>
        /// Will kill all processes related to IDZ with SegaTools (can probably be done better)
        /// </summary>
        public static void KillIDZ()
        {
            try
            {
                Regex regex = new Regex(@"amdaemon.*");

                foreach (Process p in Process.GetProcesses("."))
                {
                    if (regex.Match(p.ProcessName).Success)
                    {
                        p.Kill();
                        Console.WriteLine("killed amdaemon!");
                    }
                }

                FreeConsole();
            }
            catch (Exception)
            {
                Debug.WriteLine("Attempted to kill a game process that wasn't running (this is fine)");
            }
        }

        private static IPAddress GetNetworkAddress(IPAddress address, IPAddress subnetMask)
        {
            byte[] ipAdressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

            byte[] broadcastAddress = new byte[ipAdressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] & subnetMaskBytes[i]);
            }
            return new IPAddress(broadcastAddress);
        }
    }
}
