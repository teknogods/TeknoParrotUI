using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace TeknoParrotUi.Views.GameRunningCode.EmulatorHelpers
{
    internal static class SegaToolsHelper
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        public static extern bool SetConsoleTitle(string lpConsoleTitle);

        public static void BootMinime()
        {
            var psiNpmRunDist = new ProcessStartInfo
            {
                FileName = "cmd",
                RedirectStandardInput = true,
                WorkingDirectory = ".\\SegaTools\\minime"
            };
            psiNpmRunDist.UseShellExecute = false;
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
                Arguments = "-d -k .\\idzhook.dll .\\amdaemon.exe -c configDHCP_Final_Common.json configDHCP_Final_JP.json configDHCP_Final_JP_ST1.json configDHCP_Final_JP_ST2.json configDHCP_Final_EX.json configDHCP_Final_EX_ST1.json configDHCP_Final_EX_ST2.json"
            };
            psiNpmRunDist.UseShellExecute = false;
            var pNpmRunDist = Process.Start(psiNpmRunDist);
            pNpmRunDist.WaitForExit();
        }

        public static void BootServerbox(string gameDir)
        {
            var psiNpmRunDist = new ProcessStartInfo
            {
                FileName = gameDir + "\\inject.exe",
                WorkingDirectory = gameDir,
                Arguments = "-d -k .\\idzhook.dll .\\ServerBoxD8_Nu_x64.exe"
            };
            psiNpmRunDist.UseShellExecute = false;
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
                var currentId = Process.GetCurrentProcess().Id;
                Regex regex = new Regex(@"amdaemon.*");

                foreach (Process p in Process.GetProcesses("."))
                {
                    if (regex.Match(p.ProcessName).Success)
                    {
                        p.Kill();
                        Console.WriteLine("killed amdaemon!");
                    }
                }

                // Add the rest of the process killing logic...
                // (abbreviated for space)
                
                FreeConsole();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Attempted to kill a game process that wasn't running (this is fine)");
            }
        }
    }
}