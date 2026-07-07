using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// Builds process start info and applies per-game pre-launch fixes for the
    /// native launch path. Verbatim port of the classic GameProcessManager logic
    /// for standard loader emulator types (OpenParrot/TeknoParrot/Lindbergh/N2/ElfLdr2/TeknoMacaw).
    /// </summary>
    public static class GameLaunchArguments
    {
        private static readonly EmulationProfile[] OpenSslFixProfiles =
        {
            EmulationProfile.ALLSSWDC, EmulationProfile.IDZ, EmulationProfile.ALLSSCHRONO,
            EmulationProfile.NxL2, EmulationProfile.RawThrillsFNF, EmulationProfile.ALLSHOTDSD,
            EmulationProfile.ALLSFGO, EmulationProfile.TimeCrisis5, EmulationProfile.JojoLastSurvivor,
            EmulationProfile.DenshaDeGo, EmulationProfile.ALLSIDTA, EmulationProfile.SegaOlympic2020
        };

        public static void ApplyOpenSslFix(GameProfile profile, ProcessStartInfo info)
        {
            if (!OpenSslFixProfiles.Contains(profile.EmulationProfile))
                return;
            try
            {
                info.UseShellExecute = false;
                info.EnvironmentVariables.Add("OPENSSL_ia32cap", ":~0x20000000");
            }
            catch
            {
                // already added
            }
        }

        public static string BuildGameArguments(GameProfile profile, string gameLocation, bool isTest)
        {
            var windowed = profile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "1") ||
                           profile.ConfigValues.Any(x => x.FieldName == "DisplayMode" && x.FieldValue == "Windowed");
            var fullscreen = profile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "0") ||
                             profile.ConfigValues.Any(x => x.FieldName == "DisplayMode" && x.FieldValue == "Fullscreen");
            var width = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "ResolutionWidth");
            var height = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "ResolutionHeight");

            var custom = string.IsNullOrEmpty(profile.CustomArguments) ? string.Empty : profile.CustomArguments;
            var extraXml = string.IsNullOrEmpty(profile.ExtraParameters) ? string.Empty : profile.ExtraParameters;

            var extra = string.Empty;
            switch (profile.EmulationProfile)
            {
                case EmulationProfile.AfterBurnerClimax:
                    extra = fullscreen ? "-full " : string.Empty;
                    break;
                case EmulationProfile.TaitoTypeXBattleGear:
                    extra = fullscreen ? "_MTS_FULL_SCREEN_ " : string.Empty;
                    break;
                case EmulationProfile.NamcoMachStorm:
                    extra = fullscreen ? "-fullscreen " : string.Empty;
                    break;
                case EmulationProfile.NamcoPokken:
                    if (width != null && short.TryParse(width.FieldValue, out var pw) &&
                        height != null && short.TryParse(height.FieldValue, out var ph))
                    {
                        extra = $"\"screen_width={pw}" + " " + $"screen_height={ph}\"";
                    }
                    break;
                case EmulationProfile.GuiltyGearRE2:
                    var englishHack = profile.ConfigValues.Any(x => x.FieldName == "EnglishHack" && x.FieldValue == "1");
                    extra = $"\"-SEEKFREELOADINGPCCONSOLE -LANGUAGE={(englishHack ? "ENG" : "JPN")} -NOHOMEDIR -NOSPLASH -NOWRITE -VSYNC -APM -PCTOC -AUTH\"";
                    if (width != null && short.TryParse(width.FieldValue, out var wGG) &&
                        height != null && short.TryParse(height.FieldValue, out var hGG))
                    {
                        extra += $"\"ResX={wGG} ResY={hGG}\"";
                    }
                    break;
                case EmulationProfile.GuiltyGearAPM3:
                    var englishHackApm3 = profile.ConfigValues.Any(x => x.FieldName == "EnglishHack" && x.FieldValue == "1");
                    extra = $"\"-SEEKFREELOADINGPCCONSOLE -LANGUAGE={(englishHackApm3 ? "ENG" : "JPN")} -NOHOMEDIR -NOSPLASH -NOWRITE -VSYNC -APM3 -PCTOC -AUTH -TMSDir=.\"";
                    if (width != null && short.TryParse(width.FieldValue, out var wApm) &&
                        height != null && short.TryParse(height.FieldValue, out var hApm))
                    {
                        extra += $"\"-ResX={wApm} -ResY={hApm}\"";
                    }
                    if (isTest)
                        extra += "\"-TESTMODE\"";
                    break;
                case EmulationProfile.SiN:
                    var name = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "Name");
                    extra = "\"+cl_stereo 1 +enablevr 0 +timelimitenable 0 +timelimit 0 +public 1 +deathmatch 0 +coop 1 +hostname \"TeknoParrotGang\" +set noudp 0 +map BANK1 +name " + name?.FieldValue + "\"";
                    break;
                case EmulationProfile.ALLSSWDC:
                    extra = "-launch=MiniCabinet";
                    break;
                case EmulationProfile.ALLSSCHRONO:
                    extra += windowed
                        ? "\" -screen-quality Fantastic -screen-width 1920 -screen-height 1080 -screen-fullscreen 0\""
                        : "\" -screen-quality Fantastic -screen-width 1920 -screen-height 1080 -screen-fullscreen 1\"";
                    break;
            }

            if (isTest)
            {
                return profile.TestMenuIsExecutable
                    ? $"\"{Path.Combine(Path.GetDirectoryName(gameLocation) ?? throw new InvalidOperationException(), profile.TestMenuParameter)}\" {profile.TestMenuExtraParameters}"
                    : $"\"{gameLocation}\" {profile.TestMenuParameter} {extra} {custom}";
            }

            switch (profile.EmulatorType)
            {
                case EmulatorType.Lindbergh:
                    if (profile.EmulationProfile == EmulationProfile.Vf5Lindbergh ||
                        profile.EmulationProfile == EmulationProfile.Vf5cLindbergh)
                    {
                        if (profile.ConfigValues.Any(x => x.FieldName == "VgaMode" && x.FieldValue == "1"))
                            extra += $"-vga {(fullscreen ? "-fs" : string.Empty)}";
                        else
                            extra += $"-wxga {(fullscreen ? "-fs" : string.Empty)}";
                    }
                    break;
                case EmulatorType.N2:
                    extra = "-heapsize 131072 +set developer 1 -game czero -devel -nodb -console -noms";
                    break;
            }

            return $"\"{gameLocation}\" {extra} {custom} {extraXml}";
        }

        public static ProcessStartInfo BuildProcessStartInfo(GameProfile profile, string gameLocation, bool isTest, string loaderExe, string loaderDll)
        {
            var windowed = profile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "1") ||
                           profile.ConfigValues.Any(x => x.FieldName == "DisplayMode" && x.FieldValue == "Windowed");
            var msaaLevel = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "MSAA Level");

            var gameArguments = BuildGameArguments(profile, gameLocation, isTest);

            // Per-game file fixes
            if (profile.ResetHint)
            {
                var hintPath = Path.Combine(Path.GetDirectoryName(profile.GamePath), "hints.dat");
                if (File.Exists(hintPath))
                    File.Delete(hintPath);
            }
            ToggleIniFullscreen(profile, gameLocation, "Magical Beat", "settings.ini", "FULLSCREEN=1", "FULLSCREEN=0", windowed);
            ToggleIniFullscreen(profile, gameLocation, "Operation G.H.O.S.T.", "gs2.ini", "FullScreen=1", "FullScreen=0", windowed);

            // DPI compatibility flags
            if (profile.EmulatorType != EmulatorType.ElfLdr2 && profile.EmulatorType != EmulatorType.Lindbergh)
                SetDpiAwareRegistryValue(gameLocation);
            SetDpiAwareRegistryValue(Path.GetFullPath(loaderExe));

            bool isElfldr2X64 = profile.EmulatorType == EmulatorType.ElfLdr2 &&
                (loaderExe.IndexOf("x64", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 loaderExe.IndexOf("_64", StringComparison.OrdinalIgnoreCase) >= 0);

            var exePath = isElfldr2X64 ? Path.GetFullPath(loaderExe) : loaderExe;
            var info = new ProcessStartInfo(exePath, $"{loaderDll} {gameArguments}");

            void SetEnv(string key, string value)
            {
                if (isElfldr2X64) Environment.SetEnvironmentVariable(key, value);
                else info.EnvironmentVariables.Add(key, value);
            }

            if (profile.EmulationProfile == EmulationProfile.APM3Direct && isTest)
                SetEnv("TP_DIRECTHOOK", "1");
            if (profile.UseRemoteThread)
                SetEnv("TP_REMOTETHREAD", "1");
            if (profile.msysType > 0)
                SetEnv("tp_msysType", profile.msysType.ToString());

            if (profile.EmulatorType == EmulatorType.N2 || profile.EmulatorType == EmulatorType.ElfLdr2)
            {
                info.WorkingDirectory = Path.GetDirectoryName(gameLocation) ?? throw new InvalidOperationException();
                info.UseShellExecute = false;
                SetEnv("tp_windowed", windowed ? "1" : "0");
                SetEnv("TP_LOGTOFILE", Lazydata.ParrotData.Elfldr2LogToFile ? "1" : "0");
                if (Lazydata.ParrotData.Elfldr2NetworkAdapterName != "")
                    SetEnv("TP_ETH", Lazydata.ParrotData.Elfldr2NetworkAdapterName);
                if (msaaLevel != null)
                    SetEnv("TP_MSAA", msaaLevel.FieldValue);
                if (profile.ProfileName == "TankTankTank")
                    SetEnv("TP_NUSOUND", "1");
                if (profile.EmulationProfile == EmulationProfile.Vt3Lindbergh)
                    SetEnv("TEA_DIR", Directory.GetParent(Path.GetDirectoryName(gameLocation)) + "\\");
                if (profile.EmulationProfile == EmulationProfile.SegaJvsLetsGoJungle)
                    SetEnv("TEA_DIR", Path.GetDirectoryName(gameLocation) + "\\");
            }
            else if (profile.EmulatorType == EmulatorType.Lindbergh)
            {
                if (windowed)
                    info.EnvironmentVariables.Add("tp_windowed", "1");

                if (profile.EmulationProfile == EmulationProfile.SegaInitialDLindbergh
                    || profile.EmulationProfile == EmulationProfile.Vf5Lindbergh
                    || profile.EmulationProfile == EmulationProfile.Vf5cLindbergh
                    || profile.EmulationProfile == EmulationProfile.SegaRtv
                    || profile.EmulationProfile == EmulationProfile.SegaJvsLetsGoJungle
                    || profile.EmulationProfile == EmulationProfile.Rambo
                    || profile.EmulationProfile == EmulationProfile.TooSpicy
                    || profile.EmulationProfile == EmulationProfile.SegaRTuned
                    || profile.EmulationProfile == EmulationProfile.GSEVO
                    || profile.EmulationProfile == EmulationProfile.HummerExtreme)
                {
                    info.EnvironmentVariables.Add("TEA_DIR", Path.GetDirectoryName(gameLocation) + "\\");
                }
                else if (profile.EmulationProfile == EmulationProfile.Vt3Lindbergh)
                {
                    info.EnvironmentVariables.Add("TEA_DIR", Directory.GetParent(Path.GetDirectoryName(gameLocation)) + "\\");
                }

                if (profile.ConfigValues.Any(x => x.FieldName == "EnableAmdFix" && x.FieldValue == "1"))
                {
                    info.EnvironmentVariables.Add("tp_AMDCGGL", "1");
                    if (profile.EmulationProfile == EmulationProfile.SegaInitialDLindbergh)
                        info.EnvironmentVariables.Add("tp_D4AMDFix", "1");
                }

                info.EnvironmentVariables.Add("REGAL_LOAD_GL", "opengl32.dll");
                info.WorkingDirectory = Path.GetDirectoryName(gameLocation) ?? throw new InvalidOperationException();
                info.UseShellExecute = false;
            }
            else
            {
                if (!isElfldr2X64)
                    info.UseShellExecute = false;
            }

            ApplyOpenSslFix(profile, info);
            return info;
        }

        /// <summary>
        /// Per-game pre-launch actions: zombie process cleanup, config file generation
        /// and helper daemons. Ported verbatim.
        /// </summary>
        public static void ApplyPerGamePreLaunch(GameProfile profile, string gameLocation, string loaderExe, string loaderDll,
            Action<string, string> runAndWait, Action<string> log)
        {
            var width = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "ResolutionWidth");
            var height = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "ResolutionHeight");
            var region = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "Region");

            if (InputCode.ButtonMode == EmulationProfile.NamcoMkdx)
                KillProcessesMatching(@"MK_AGP3_FINAL.*");

            if (InputCode.ButtonMode == EmulationProfile.EXVS2)
            {
                KillProcessesMatching(@"AMAuthd.*");
                KillProcessesMatching(@"exvs2_exe_Release.*");
            }

            if (InputCode.ButtonMode == EmulationProfile.EXVS2XB)
            {
                KillProcessesMatching(@"AMAuthd.*");
                KillProcessesMatching(@"vsac25_Release.*");
            }

            if (profile.GameNameInternal.StartsWith("Tekken 7"))
            {
                var tk7Lang = profile.ConfigValues.FirstOrDefault(t => t.FieldName == "Language");
                string lang = "us";
                if (tk7Lang != null && (tk7Lang.FieldValue == "us" || tk7Lang.FieldValue == "jp" || tk7Lang.FieldValue == "kr" ||
                                        tk7Lang.FieldValue == "as" || tk7Lang.FieldValue == "cn"))
                {
                    lang = tk7Lang.FieldValue;
                }
                File.WriteAllText(Path.GetDirectoryName(gameLocation) + "../../../Content/Config/tekken.ini",
                    "Ver=\"1.06\"\r\nLanguage=\"" + lang + "\"\r\nRegion=\"" + lang + "\"\r\nLoadVsyncOff=\"off\"\r\nNonWaitStageLoad=\"off\"\r\nINITIALIZE_SEQUENCE_ERR_CHECK=\"off\"\r\nauthtype=\"OFFLINE\"\r\n");
            }

            WriteTaitoGunIni(InputCode.ButtonMode == EmulationProfile.GaiaAttack4, gameLocation, "MINIGUN.INI", "GA4", width, height, region);
            WriteTaitoGunIni(InputCode.ButtonMode == EmulationProfile.HauntedMuseum, gameLocation, "MUSEUM.INI", "HM", width, height, region);
            WriteTaitoGunIni(InputCode.ButtonMode == EmulationProfile.HauntedMuseum2, gameLocation, "HAUNTED2.INI", "HM2", width, height, region);

            if (InputCode.ButtonMode == EmulationProfile.SegaInitialD)
            {
                var newCard = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "EnableNewCardCode");
                if (newCard == null || newCard.FieldValue == "0")
                {
                    runAndWait(loaderExe, $"{loaderDll} \"{Path.Combine(Path.GetDirectoryName(gameLocation), "picodaemon.exe")}");
                }
            }

            if (InputCode.ButtonMode == EmulationProfile.ALLSSWDC)
            {
                var isMainCab = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "Main Cabinet");
                var isOffline = profile.ConfigValues.FirstOrDefault(x => x.FieldName == "Offline Mode");
                if (isOffline != null && isOffline.FieldValue != "0" && isMainCab != null && isMainCab.FieldValue != "0")
                {
                    var tdrServer = Path.Combine(Path.GetDirectoryName(gameLocation), @"..\..\..\..\..\Tools", "tdrserver.exe");
                    if (File.Exists(tdrServer))
                        runAndWait(loaderExe, $"{loaderDll} \"{tdrServer}\"");
                }
            }
        }

        private static void ToggleIniFullscreen(GameProfile profile, string gameLocation, string gameName,
            string iniName, string fullscreenOn, string fullscreenOff, bool windowed)
        {
            if (profile.GameNameInternal != gameName)
                return;
            var iniPath = Path.Combine(Path.GetDirectoryName(gameLocation) ?? "", iniName);
            if (!File.Exists(iniPath))
                return;
            var settings = File.ReadAllText(iniPath);
            settings = windowed
                ? settings.Replace(fullscreenOn, fullscreenOff)
                : settings.Replace(fullscreenOff, fullscreenOn);
            File.WriteAllText(iniPath, settings);
        }

        private static void WriteTaitoGunIni(bool enabled, string gameLocation, string iniName, string cnfName,
            FieldInformation width, FieldInformation height, FieldInformation region)
        {
            if (!enabled)
                return;
            short w = 1280, h = 720;
            if (width != null) short.TryParse(width.FieldValue, out w);
            if (height != null) short.TryParse(height.FieldValue, out h);
            var regionValue = region?.FieldValue ?? "";
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(gameLocation), iniName),
                "REGION\t\t" + regionValue + "\r\n" + $"CNFNAME\t\t.\\OpenParrot\\{cnfName}\r\nRANKFILE\t.\\OpenParrot\\\r\nPRJENABLE   \t1\r\nSCREEN_WIDTH\t" + w +
                "\r\n" + "SCREEN_HEIGHT\t" + h + "\r\nRENDER_WIDTH\t" + w + "\r\n" + "RENDER_HEIGHT\t" + h +
                "\r\nRENDER_WIDTH3D\t" + w + "\r\n" + "RENDER_HEIGHT3D\t" + h + "\r\n");
        }

        private static void KillProcessesMatching(string pattern)
        {
            try
            {
                var regex = new Regex(pattern);
                foreach (var p in Process.GetProcesses("."))
                {
                    if (regex.Match(p.ProcessName).Success)
                        p.Kill();
                }
            }
            catch (Exception)
            {
                Debug.WriteLine("Attempted to kill a game process that wasn't running (this is fine)");
            }
        }

        private static void SetDpiAwareRegistryValue(string exePath)
        {
            try
            {
                const string registryKeyPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
                using var key = Registry.CurrentUser.OpenSubKey(registryKeyPath, true) ??
                                Registry.CurrentUser.CreateSubKey(registryKeyPath);
                var existingValue = key.GetValue(exePath) as string ?? string.Empty;
                var flags = new HashSet<string>(existingValue.Split(' '));
                foreach (var flag in new[] { "DPIUNAWARE", "GDIDPISCALING", "~DPIUNAWARE", "~GDIDPISCALING", "~", "HIGHDPIAWARE" })
                    flags.Remove(flag);
                flags.Add("~");
                flags.Add("HIGHDPIAWARE");
                key.SetValue(exePath, string.Join(" ", flags).Trim(), RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set DPI compatibility flag: {ex.Message}");
            }
        }
    }
}
