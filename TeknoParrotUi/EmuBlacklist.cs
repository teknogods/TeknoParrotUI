using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace TeknoParrotUi
{
    public class EmuBlacklist
    {
        // Use .* as wildcard and escape normal . with \
        private static List<string> Blacklist = new List<string>
        {
            "typex_.*",
            "typexhook\\.dll",
            "detoured\\.dll",
            "jconfig.*\\.exe",
            "jvsemu.*\\.dll",
            "jvs_loader\\.exe",
            "ttx_.*\\.exe",
            "ttx_.*\\.dll",
            "rconfig\\.exe",
            "ring_io\\.dll",
            "ring_loader\\.exe",
            "es3_patch\\.dll",
        };

        // List of known patched files causing problems
        private static Dictionary<string, List<string>> DirtyList = new Dictionary<string, List<string>>
        {
            { "bnusio.dll", new List<string> {"7F53C048D6E9956A8335837BCABE3CAA", "667081B36612774B2528DFAF32D1A2EC"} },
            { "apm.dll", new List<string> {"D7EA6947D2875B341A21BD321EFDE088"} },
            { "apm_x86.dll", new List<string> {"99BFBF98A13DFE857394C3C3C655126E"} },
            { "iDmacDrv32.dll", new List<string> { "D292256FBD7C0ABC6D45D32F58357E98" } },
            { "iDmacDrv64.dll", new List<string> { "ADFF3E81ADC46D9507F0EA3FE8D28AEF", "77CDBC006119379ECDDBEEC4D9E6513E", "A7CB43283AAC04B9A71084B1FEC28496" } },
            { "libusb0.dll", new List<string> { "8C9131FFCC61FCB8B1A59C83F4E6CE5F", "1E326F62C2382986D380F92110FA91CB" } },
            { "nbamsavdat.dll", new List<string> { "CBB8682239BB3C709CE869C27DFF4C0E", "BB088B1CCCD9C9D48E21CABCE98041A9" } },
            { "usb_io.dll", new List<string> { "09682368F9B28F9FB9310FB17E5002E7" } },
        };

        public List<string> FilesToRemove = new List<string>();
        public List<string> FilesToClean = new List<string>();
        public bool FoundProblem = false;

        public EmuBlacklist(string gamePath)
        {
            if (string.IsNullOrEmpty(gamePath))
                return;

            string dir = Path.GetDirectoryName(gamePath);
            string[] files = Directory.GetFiles(dir);

            // Check files in game's folder
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);

                // Check filename blacklist
                foreach (var t in Blacklist)
                {
                    if (Regex.Match(fileName, t, RegexOptions.IgnoreCase).Success)
                    {
                        FilesToRemove.Add(fileName);
                        FoundProblem = true;
                    }
                }

                // Check for known bad hashes
                if (DirtyList.ContainsKey(fileName))
                {
                    string MD5 = GetFileMD5(file);

                    if (DirtyList[fileName].Contains(MD5))
                    {
                        FilesToClean.Add(fileName);
                        FoundProblem = true;
                    }
                }
            }

            // Paternscan game exe
            if (CheckFileForPattern(gamePath, new byte[] { 0x4A, 0x56, 0x53, 0x45, 0x6D, 0x75 }))
            {
                string fileName = Path.GetFileName(gamePath);
                FilesToClean.Add(fileName);
                FoundProblem = true;
            }

            if (CheckFileForPattern(gamePath, new byte[] { 0x4A, 0x00, 0x56, 0x00, 0x53, 0x00, 0x45, 0x00, 0x6D, 0x00, 0x75, 0x00 }))
            {
                string fileName = Path.GetFileName(gamePath);
                FilesToClean.Add(fileName);
                FoundProblem = true;
            }
        }

        private string GetFileMD5(string filename)
        {
            byte[] hash;

            using (Stream input = File.OpenRead(filename))
            {
                hash = MD5.Create().ComputeHash(input);
            }

            return BitConverter.ToString(hash).Replace("-", "");
        }

        private bool CheckFileForPattern(string path, byte[] pattern)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                uint patternPosition = 0;
                uint filePosition = 0;
                uint bufferSize = (uint)Math.Min(stream.Length, 100_000);

                byte[] buffer = new byte[bufferSize];
                int readCount = 0;

                while ((readCount = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < readCount; i++)
                    {
                        if (buffer[i] == pattern[patternPosition])
                        {
                            patternPosition++;

                            if (patternPosition == pattern.Length)
                            {
                                Debug.WriteLine("Found pattern at: {0:X8}", filePosition + 1 - pattern.Length);
                                return true;
                            }
                        }
                        else
                        {
                            patternPosition = 0;
                        }

                        filePosition++;
                    }
                }

                return false;
            }
        }
    }
}
