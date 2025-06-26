using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;

namespace TeknoParrotUi.Views.GameRunningCode.ProcessManagement
{
    internal class GameProcessManager
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]

        static extern bool AllocConsole();
        [DllImport("kernel32.dll")]
        static extern bool SetConsoleTitle(string lpConsoleTitle);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeConsole();
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AttachConsole(uint dwProcessId);
        private const int SW_SHOWMINIMIZED = 2;
        private const int SW_MINIMIZE = 6;

        private readonly GameRunning _gameRunning;
        private readonly GameProfile _gameProfile;
        private readonly string _gameLocation;
        private readonly string _gameLocation2;
        private readonly bool _twoExes;
        private readonly bool _secondExeFirst;
        private readonly string _secondExeArguments;
        private readonly bool _isTest;
        private readonly bool _forceQuit;
        private readonly Library _library;


        public GameProcessManager(GameRunning gameRunning, GameProfile gameProfile, string gameLocation, string gameLocation2,
            bool twoExes, bool secondExeFirst, string secondExeArguments, bool isTest, ref bool forceQuit, Library library)
        {
            _gameRunning = gameRunning;
            _gameProfile = gameProfile;
            _gameLocation = gameLocation;
            _gameLocation2 = gameLocation2;
            _twoExes = twoExes;
            _secondExeFirst = secondExeFirst;
            _secondExeArguments = secondExeArguments;
            _isTest = isTest;
            _forceQuit = forceQuit;
            _library = library;
        }

        public void RunAndWait(string loaderExe, string daemonPath)
        {
            ProcessStartInfo info = new ProcessStartInfo(loaderExe, daemonPath);
            bool needsShowWindow = false;
            if (_gameProfile.EmulationProfile == EmulationProfile.ALLSSWDC ||
            _gameProfile.EmulationProfile == EmulationProfile.IDZ ||
            _gameProfile.EmulationProfile == EmulationProfile.ALLSSCHRONO ||
            _gameProfile.EmulationProfile == EmulationProfile.NxL2 ||
            _gameProfile.EmulationProfile == EmulationProfile.RawThrillsFNF ||
            _gameProfile.EmulationProfile == EmulationProfile.ALLSHOTDSD ||
            _gameProfile.EmulationProfile == EmulationProfile.ALLSFGO ||
            _gameProfile.EmulationProfile == EmulationProfile.TimeCrisis5 ||
            _gameProfile.EmulationProfile == EmulationProfile.JojoLastSurvivor ||
            _gameProfile.EmulationProfile == EmulationProfile.ALLSIDTA ||
            _gameProfile.EmulationProfile == EmulationProfile.SegaOlympic2020)
            {
                try
                {
                    info.UseShellExecute = false;
                    info.EnvironmentVariables.Add("OPENSSL_ia32cap", ":~0x20000000");
                    needsShowWindow = true;
                }
                catch
                {
                    // Already added
                }
            }

            // This will not work for exes (like amdaemon) that need UseShellExecute = false... Thanks MS!
            info.WindowStyle = _gameRunning._launchSecondExecutableMinimized ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal;
            Process proc = Process.Start(info);
            Thread.Sleep(1000);
        }

        public void CreateGameProcess(string loaderExe, string loaderDll, TextBox textBoxConsole, bool runEmuOnly, bool cmdLaunch)
        {
            if (_gameProfile.EmulationProfile == EmulationProfile.SegaToolsIDZ)
            {
                AllocConsole();
            }
            var gameThread = new Thread(() =>
            {
                var windowed = _gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "1") || _gameProfile.ConfigValues.Any(x => x.FieldName == "DisplayMode" && x.FieldValue == "Windowed");
                var fullscreen = _gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "0") || _gameProfile.ConfigValues.Any(x => x.FieldName == "DisplayMode" && x.FieldValue == "Fullscreen");
                var width = _gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "ResolutionWidth");
                var height = _gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "ResolutionHeight");
                var region = _gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "Region");
                var msaaLevel = _gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "MSAA Level");

                var custom = string.Empty;
                if (!string.IsNullOrEmpty(_gameProfile.CustomArguments))
                {
                    custom = _gameProfile.CustomArguments;
                }

                var extra_xml = string.Empty;
                if (!string.IsNullOrEmpty(_gameProfile.ExtraParameters))
                {
                    extra_xml = _gameProfile.ExtraParameters;
                }

                // TODO: move to XML
                var extra = string.Empty;
                switch (_gameProfile.EmulationProfile)
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
                        if (width != null && short.TryParse(width.FieldValue, out var _width) &&
                            height != null && short.TryParse(height.FieldValue, out var _height))
                        {
                            extra = $"\"screen_width={_width}" + " " +
                                           $"screen_height={_height}\"";
                        }
                        break;
                    case EmulationProfile.GuiltyGearRE2:
                        var englishHack = (_gameProfile.ConfigValues.Any(x => x.FieldName == "EnglishHack" && x.FieldValue == "1"));
                        extra = $"\"-SEEKFREELOADINGPCCONSOLE -LANGUAGE={(englishHack ? "ENG" : "JPN")} -NOHOMEDIR -NOSPLASH -NOWRITE -VSYNC -APM -PCTOC -AUTH\"";
                        if (width != null && short.TryParse(width.FieldValue, out var _widthGG) &&
                            height != null && short.TryParse(height.FieldValue, out var _heightGG))
                        {
                            extra += $"\"ResX={_widthGG} ResY={_heightGG}\"";
                        }
                        break;
                    case EmulationProfile.GuiltyGearAPM3:
                        var englishHackAPM3 = (_gameProfile.ConfigValues.Any(x => x.FieldName == "EnglishHack" && x.FieldValue == "1"));
                        extra = $"\"-SEEKFREELOADINGPCCONSOLE -LANGUAGE={(englishHackAPM3 ? "ENG" : "JPN")} -NOHOMEDIR -NOSPLASH -NOWRITE -VSYNC -APM3 -PCTOC -AUTH -TMSDir=.\"";
                        if (width != null && short.TryParse(width.FieldValue, out var _widthGGAPM3) &&
                            height != null && short.TryParse(height.FieldValue, out var _heightGGAPM3))
                        {
                            extra += $"\"-ResX={_widthGGAPM3} -ResY={_heightGGAPM3}\"";
                        }
                        if (_isTest)
                        {
                            extra += $"\"-TESTMODE\"";
                        }
                        break;
                    case EmulationProfile.SiN:
                        {
                            var name = _gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "Name");

                            extra = "\"+cl_stereo 1 +enablevr 0 +timelimitenable 0 +timelimit 0 +public 1 +deathmatch 0 +coop 1 +hostname \"TeknoParrotGang\" +set noudp 0 +map BANK1 +name " + name.FieldValue + "\"";
                        }
                        break;
                    case EmulationProfile.ALLSSWDC:
                        {
                            extra = "-launch=MiniCabinet";
                        }
                        break;
                    case EmulationProfile.ALLSSCHRONO:
                        {
                            if (windowed)
                            {
                                extra += "\" -screen-quality Fantastic -screen-width 1920 -screen-height 1080 -screen-fullscreen 0\"";
                            }
                            else
                            {
                                extra += "\" -screen-quality Fantastic -screen-width 1920 -screen-height 1080 -screen-fullscreen 1\"";
                            }
                        }
                        break;
                }

                string gameArguments;

                if (_isTest)
                {
                    gameArguments = _gameProfile.TestMenuIsExecutable
                        ? $"\"{Path.Combine(Path.GetDirectoryName(_gameLocation) ?? throw new InvalidOperationException(), _gameProfile.TestMenuParameter)}\" {_gameProfile.TestMenuExtraParameters}"
                        : $"\"{_gameLocation}\" {_gameProfile.TestMenuParameter} {extra} {custom}";
                }
                else
                {
                    switch (_gameProfile.EmulatorType)
                    {
                        case EmulatorType.Lindbergh:
                            if (_gameProfile.EmulationProfile == EmulationProfile.Vf5Lindbergh
                                || _gameProfile.EmulationProfile == EmulationProfile.Vf5cLindbergh)
                            {
                                if (_gameProfile.ConfigValues.Any(x => x.FieldName == "VgaMode" && x.FieldValue == "1"))
                                    extra += $"-vga {(fullscreen ? "-fs" : string.Empty)}";
                                else
                                    extra += $"-wxga {(fullscreen ? "-fs" : string.Empty)}";
                            }

                            break;
                        //NOTE: heapsize, +set, game, and console are GoldSrc engine options, so they'll probably only work on CS:NEO.
                        case EmulatorType.N2:
                            extra = "-heapsize 131072 +set developer 1 -game czero -devel -nodb -console -noms";
                            break;
                    }

                    gameArguments = $"\"{_gameLocation}\" {extra} {custom} {extra_xml}";
                }

                if (_gameProfile.ResetHint)
                {
                    var hintPath = Path.Combine(Path.GetDirectoryName(_gameProfile.GamePath), "hints.dat");
                    if (File.Exists(hintPath))
                    {
                        File.Delete(hintPath);
                    }
                }

                if (_gameProfile.GameNameInternal == "Magical Beat")
                {
                    if (File.Exists(Path.GetDirectoryName(_gameLocation) + "\\settings.ini"))
                    {
                        if (windowed)
                        {
                            string settings = File.ReadAllText(Path.GetDirectoryName(_gameLocation) + "\\settings.ini");
                            settings = settings.Replace("FULLSCREEN=1", "FULLSCREEN=0");
                            File.WriteAllText(Path.GetDirectoryName(_gameLocation) + "\\settings.ini", settings);
                        }
                        else
                        {
                            string settings = File.ReadAllText(Path.GetDirectoryName(_gameLocation) + "\\settings.ini");
                            settings = settings.Replace("FULLSCREEN=0", "FULLSCREEN=1");
                            File.WriteAllText(Path.GetDirectoryName(_gameLocation) + "\\settings.ini", settings);
                        }
                    }
                }

                if (_gameProfile.GameNameInternal == "Operation G.H.O.S.T.")
                {
                    if (File.Exists(Path.GetDirectoryName(_gameLocation) + "\\gs2.ini"))
                    {
                        if (windowed)
                        {
                            string settings = File.ReadAllText(Path.GetDirectoryName(_gameLocation) + "\\gs2.ini");
                            settings = settings.Replace("FullScreen=1", "FullScreen=0");
                            File.WriteAllText(Path.GetDirectoryName(_gameLocation) + "\\gs2.ini", settings);
                        }
                        else
                        {
                            string settings = File.ReadAllText(Path.GetDirectoryName(_gameLocation) + "\\gs2.ini");
                            settings = settings.Replace("FullScreen=0", "FullScreen=1");
                            File.WriteAllText(Path.GetDirectoryName(_gameLocation) + "\\gs2.ini", settings);
                        }
                    }
                }

                // Set DPI aware compatibility flag via registry to avoid awkward scaling on 4k displays or laptops etc
                // also, check so we don't add elf files to the registry as thats kinda pointless
                if (_gameProfile.EmulatorType != EmulatorType.ElfLdr2 && _gameProfile.EmulatorType != EmulatorType.Lindbergh)
                {
                    GameRunningCode.Utilities.GameRunningUtils.SetDPIAwareRegistryValue(_gameLocation);
                }
                GameRunningCode.Utilities.GameRunningUtils.SetDPIAwareRegistryValue(Path.GetFullPath(loaderExe));

                ProcessStartInfo info;

                if (_gameProfile.EmulationProfile == EmulationProfile.SegaToolsIDZ)
                {
                    info = new ProcessStartInfo(loaderExe, $" -d -k {loaderDll}.dll {Path.GetFileName(_gameProfile.GamePath)}");
                    info.UseShellExecute = false;
                    info.WorkingDirectory = Path.GetDirectoryName(_gameLocation) ?? throw new InvalidOperationException();
                }
                else if (_gameProfile.EmulatorType == EmulatorType.Dolphin)
                {
                    var parameters = new List<string>();

                    if (_gameProfile.ProfileName == "tatsuvscap")
                    {
                        // Dolphin.exe -b -n 0000000100000002
                        parameters.Add("-b");
                        parameters.Add("-n 0000000100000002");

                        ConfigureDolphinIni(true);
                    }
                    else
                    {
                        ConfigureDolphinIni(false);

                        if (Lazydata.ParrotData.HideDolphinGUI)
                        {
                            // -b (batch) to hide ui, which in turn requires -e to specify the game
                            parameters.Add("-b");
                            parameters.Add("-e");
                        }

                        // Important, game path needs to be after -e (executable)
                        parameters.Add($"\"{_gameProfile.GamePath}\"");
                    }

                    if (!windowed)
                    {
                        parameters.Add("--config");
                        parameters.Add("\"Dolphin.Display.Fullscreen=True\"");
                    }

                    var dolphinParameters = string.Join(" ", parameters);

                    info = new ProcessStartInfo(@".\CrediarDolphin\Dolphin.exe", dolphinParameters);
                    info.UseShellExecute = false;
                    info.WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "CrediarDolphin") ?? throw new InvalidOperationException();
                }
                else if (_gameProfile.EmulatorType == EmulatorType.Play)
                {
                    // Get the game directory path
                    string gamePath = Path.GetDirectoryName(_gameLocation);

                    // Path to the Play config file
                    string configPath = Path.Combine(".", "Play", "TeknoParrot", "Documents", "Play Data Files", "config.xml");
                    var configDirectory = Path.GetDirectoryName(configPath);
                    if (!Directory.Exists(configDirectory))
                    {
                        Directory.CreateDirectory(configDirectory);
                    }


                    string sys256CabId = "1";
                    if (_gameProfile.ConfigValues.Any(x => x.FieldName == "Cabinet Id" && x.FieldValue == "2"))
                    {
                        sys256CabId = "2";
                    }

                    try
                    {
                        var configValues = new Dictionary<string, (string type, string value)>
                        {
                            ["ps2.arcaderoms.directory"] = ("path", gamePath),
                            ["video.gshandler"] = ("integer", GetGraphicsBackendValue()),
                            ["renderer.opengl.resfactor"] = ("integer", GetResolutionFactorValue()),
                            ["sys256.cabinet.linkid"] = ("integer", sys256CabId)
                        };

                        CreateOrUpdatePlayConfig(configPath, configValues);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Error updating Play config: {ex.Message}\n");
                        textBoxConsole.Dispatcher.Invoke(() => textBoxConsole.AppendText($"Error updating Play config: {ex.Message}\n"),
                            DispatcherPriority.Background);
                    }
                    var parameters = new List<string>();

                    // Important, game path needs to be after -e (executable)
                    parameters.Add($"--arcade {_gameProfile.ProfileName}");

                    if (!windowed)
                    {
                        parameters.Add("--fullscreen");
                    }

                    var playParameters = string.Join(" ", parameters);

                    info = new ProcessStartInfo(@".\Play\Play.exe", playParameters);
                    info.UseShellExecute = false;
                    info.WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Play") ?? throw new InvalidOperationException();
                }
                else
                {
                    info = new ProcessStartInfo(loaderExe, $"{loaderDll} {gameArguments}");
                }

                if (_gameProfile.EmulationProfile == EmulationProfile.APM3Direct && _isTest)
                {
                    info.EnvironmentVariables.Add("TP_DIRECTHOOK", "1");
                }

                if (_gameProfile.msysType > 0)
                {
                    info.EnvironmentVariables.Add("tp_msysType", _gameProfile.msysType.ToString());
                }

                if (_gameProfile.EmulatorType == EmulatorType.N2 || _gameProfile.EmulatorType == EmulatorType.ElfLdr2)
                {
                    info.WorkingDirectory =
                        Path.GetDirectoryName(_gameLocation) ?? throw new InvalidOperationException();
                    info.UseShellExecute = false;
                    info.EnvironmentVariables.Add("tp_windowed", windowed ? "1" : "0");
                    info.EnvironmentVariables.Add("TP_LOGTOFILE", Lazydata.ParrotData.Elfldr2LogToFile ? "1" : "0");
                    if (Lazydata.ParrotData.Elfldr2NetworkAdapterName != "")
                    {
                        info.EnvironmentVariables.Add("TP_ETH", Lazydata.ParrotData.Elfldr2NetworkAdapterName);
                    }

                    if (msaaLevel != null)
                    {
                        info.EnvironmentVariables.Add("TP_MSAA", msaaLevel.FieldValue);
                    }

                    if (_gameProfile.ProfileName == "TankTankTank")
                    {
                        info.EnvironmentVariables.Add("TP_NUSOUND", "1");
                    }

                    if (_gameProfile.EmulationProfile == EmulationProfile.Vt3Lindbergh)
                    {
                        info.EnvironmentVariables.Add("TEA_DIR",
                            Directory.GetParent(Path.GetDirectoryName(_gameLocation)) + "\\");
                    }

                    if (_gameProfile.EmulationProfile == EmulationProfile.SegaJvsLetsGoJungle)
                    {
                        info.EnvironmentVariables.Add("TEA_DIR", Path.GetDirectoryName(_gameLocation) + "\\");
                    }
                }

                if (_gameProfile.EmulatorType == EmulatorType.Lindbergh)
                {
                    if (windowed)
                        info.EnvironmentVariables.Add("tp_windowed", "1");

                    if (_gameProfile.EmulationProfile == EmulationProfile.SegaInitialDLindbergh
                        || _gameProfile.EmulationProfile == EmulationProfile.Vf5Lindbergh
                        || _gameProfile.EmulationProfile == EmulationProfile.Vf5cLindbergh
                        || _gameProfile.EmulationProfile == EmulationProfile.SegaRtv
                        || _gameProfile.EmulationProfile == EmulationProfile.SegaJvsLetsGoJungle
                        || _gameProfile.EmulationProfile == EmulationProfile.Rambo
                        || _gameProfile.EmulationProfile == EmulationProfile.TooSpicy
                        || _gameProfile.EmulationProfile == EmulationProfile.SegaRTuned
                        || _gameProfile.EmulationProfile == EmulationProfile.GSEVO
                        || _gameProfile.EmulationProfile == EmulationProfile.HummerExtreme)
                    {
                        info.EnvironmentVariables.Add("TEA_DIR", Path.GetDirectoryName(_gameLocation) + "\\");
                    }
                    else if (_gameProfile.EmulationProfile == EmulationProfile.Vt3Lindbergh)
                    {
                        info.EnvironmentVariables.Add("TEA_DIR",
                            Directory.GetParent(Path.GetDirectoryName(_gameLocation)) + "\\");
                    }

                    if (_gameProfile.ConfigValues.Any(x => x.FieldName == "EnableAmdFix" && x.FieldValue == "1"))
                    {
                        info.EnvironmentVariables.Add("tp_AMDCGGL", "1");

                        if (_gameProfile.EmulationProfile == EmulationProfile.SegaInitialDLindbergh)
                        {
                            info.EnvironmentVariables.Add("tp_D4AMDFix", "1");
                        }
                    }

                    info.EnvironmentVariables.Add("REGAL_LOAD_GL", "opengl32.dll");

                    info.WorkingDirectory =
                        Path.GetDirectoryName(_gameLocation) ?? throw new InvalidOperationException();
                    info.UseShellExecute = false;
                }
                else
                {
                    info.UseShellExecute = false;
                }

                if (_gameProfile.EmulationProfile == EmulationProfile.SegaToolsIDZ)
                {
                    //aaaaa

                    SetConsoleTitle("TeknoParrot SegaTools Support");
                    string gameDir = Path.GetDirectoryName(_gameProfile.GamePath);
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

                    //gen segatools.ini

                    //converts class data to segatools config file
                    string fileOutput;
                    string amfsDir;
                    //idzv1 amfs dir is DIFFERENT TO v2 ergh

                    if (_gameProfile.GameNameInternal.Contains("ver.2"))
                    {
                        amfsDir = Directory.GetParent(gameDir).FullName;
                    }
                    else
                    {
                        amfsDir = Directory.GetParent(Directory.GetParent(gameDir).FullName).FullName;
                    }
                    amfsDir += "\\amfs";
                    fileOutput = "[vfs]\namfs=" + amfsDir + "\nappdata=" + (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\TeknoParrot\\IDZ\\") + "\n\n[dns]\ndefault=" +
                                 _gameProfile.ConfigValues.Find(x => x.FieldName.Equals("NetworkAdapterIP")).FieldValue + "\n\n[ds]\nregion";
                    if (_gameProfile.ConfigValues.Find(x => x.FieldName.Equals("ExportRegion")).FieldValue == "true" || _gameProfile.ConfigValues.Find(x => x.FieldName.Equals("ExportRegion")).FieldValue == "1")
                    {
                        fileOutput += "=4";
                    }
                    else
                    {
                        fileOutput += "=1";
                    }

                    if (_gameProfile.GameNameInternal.Contains("ver.2"))
                    {
                        fileOutput += "\n\n[aime]\naimeGen=1\nfelicaGen=0";
                    }
                    fileOutput += "\n\n[netenv]";
                    if (_gameProfile.ConfigValues.Find(x => x.FieldName.Contains("EnableNetenv")).FieldValue == "true" || _gameProfile.ConfigValues.Find(x => x.FieldName.Contains("EnableNetenv")).FieldValue == "1")
                    {
                        fileOutput += "\nenable=1\n\n";
                    }
                    else
                    {
                        fileOutput += "\nenable=0\n\n";
                    }
                    IPAddress ip = IPAddress.Parse(_gameProfile.ConfigValues.Find(x => x.FieldName.Equals("NetworkAdapterIP")).FieldValue);
                    fileOutput += "[keychip]\nsubnet=" + GameRunningCode.Utilities.GameRunningUtils.GetNetworkAddress(ip, IPAddress.Parse("255.255.255.0")) +
                                  "\n\n[gpio]\ndipsw1=";
                    if (_gameProfile.ConfigValues.Find(x => x.FieldName.Equals("EnableDistServ")).FieldValue == "true" || _gameProfile.ConfigValues.Find(x => x.FieldName.Equals("EnableDistServ")).FieldValue == "1")
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
                    if (_gameProfile.ConfigValues.Find(x => x.FieldName.Equals("EnableRealShifter")).FieldValue == "true" || _gameProfile.ConfigValues.Find(x => x.FieldName.Equals("EnableRealShifter")).FieldValue == "1")
                    {
                        shift = 1;
                    }
                    fileOutput += "pos_shifter=" + shift + "\nautoNeutral=1\nsingleStickSteering=1\nrestrict=" + _gameProfile.ConfigValues.Find(x => x.FieldName.Equals("WheelRestriction")).FieldValue + "\n\n[dinput]\ndeviceName=\nshifterName=\nbrakeAxis=RZ\naccelAxis=Y\nstart=3\nviewChg=10\nshiftDn=1\nshiftUp=2\ngear1=1\ngear2=2\ngear3=3\ngear4=4\ngear5=5\ngear6=6\nreverseAccelAxis=0\nreverseBrakeAxis=0\n";

                    if (File.Exists(Path.GetDirectoryName(_gameProfile.GamePath) + "\\segatools.ini"))
                    {
                        File.Delete(Path.GetDirectoryName(_gameProfile.GamePath) + "\\segatools.ini");
                    }
                    File.WriteAllText((Path.GetDirectoryName(_gameProfile.GamePath) + "\\segatools.ini"), fileOutput);
                    //RunAndWait(Path.GetDirectoryName(_gameProfile.GamePath) + "\\inject.exe",$" -d -k {loaderDll}.dll " + gameDir + "\\amdaemon.exe -c configDHCP_Final_Common.json configDHCP_Final_JP.json configDHCP_Final_JP_ST1.json configDHCP_Final_JP_ST2.json configDHCP_Final_EX.json configDHCP_Final_EX_ST1.json configDHCP_Final_EX_ST2.json");

                    ThreadStart ths = null;
                    Thread th = null;
                    ths = new ThreadStart(() => GameRunningCode.EmulatorHelpers.SegaToolsHelper.BootMinime());
                    th = new Thread(ths);
                    th.Start();

                    ThreadStart ths2 = null;
                    Thread th2 = null;
                    ths2 = new ThreadStart(() => GameRunningCode.EmulatorHelpers.SegaToolsHelper.BootAmdaemon(Path.GetDirectoryName(_gameProfile.GamePath)));
                    th2 = new Thread(ths2);
                    th2.Start();

                    ThreadStart ths3 = null;
                    Thread th3 = null;
                    ths3 = new ThreadStart(() => GameRunningCode.EmulatorHelpers.SegaToolsHelper.BootServerbox(Path.GetDirectoryName(_gameProfile.GamePath)));
                    th3 = new Thread(ths3);
                    th3.Start();

                }

                if (Lazydata.ParrotData.SilentMode && _gameProfile.EmulatorType != EmulatorType.Lindbergh &&
                    _gameProfile.EmulatorType != EmulatorType.N2 && _gameProfile.EmulatorType != EmulatorType.ElfLdr2)
                {
                    info.WindowStyle = ProcessWindowStyle.Hidden;
                    info.RedirectStandardError = true;
                    info.RedirectStandardOutput = true;
                    info.CreateNoWindow = true;
                }
                else if (Lazydata.ParrotData.SilentMode && (_gameProfile.EmulatorType == EmulatorType.ElfLdr2 || _gameProfile.EmulatorType == EmulatorType.Lindbergh))
                {
                    info.WindowStyle = ProcessWindowStyle.Hidden;
                    info.CreateNoWindow = true;
                }
                else
                {
                    info.WindowStyle = ProcessWindowStyle.Normal;
                }

                if (InputCode.ButtonMode == EmulationProfile.NamcoMkdx)
                {
                    // make sure the game isn't already running still
                    try
                    {
                        Regex regex = new Regex(@"MK_AGP3_FINAL.*");

                        foreach (Process p in Process.GetProcesses("."))
                        {
                            if (regex.Match(p.ProcessName).Success)
                            {
                                p.Kill();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Attempted to kill a game process that wasn't running (this is fine)");
                    }
                }

                if (InputCode.ButtonMode == EmulationProfile.EXVS2)
                {
                    // make sure the game isn't already running still
                    try
                    {
                        Regex regex = new Regex(@"AMAuthd.*");

                        foreach (Process p in Process.GetProcesses("."))
                        {
                            if (regex.Match(p.ProcessName).Success)
                            {
                                p.Kill();
                                Console.WriteLine("killed amauth!");
                            }
                        }

                        regex = new Regex(@"exvs2_exe_Release.*");

                        foreach (Process p in Process.GetProcesses("."))
                        {
                            if (regex.Match(p.ProcessName).Success)
                            {
                                p.Kill();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Attempted to kill a game process that wasn't running (this is fine)");
                    }
                }

                if (InputCode.ButtonMode == EmulationProfile.EXVS2XB)
                {
                    // make sure the game isn't already running still
                    try
                    {
                        Regex regex = new Regex(@"AMAuthd.*");

                        foreach (Process p in Process.GetProcesses("."))
                        {
                            if (regex.Match(p.ProcessName).Success)
                            {
                                p.Kill();
                                Console.WriteLine("killed amauth!");
                            }
                        }

                        regex = new Regex(@"vsac25_Release.*");

                        foreach (Process p in Process.GetProcesses("."))
                        {
                            if (regex.Match(p.ProcessName).Success)
                            {
                                p.Kill();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Attempted to kill a game process that wasn't running (this is fine)");
                    }
                }

                if (_gameProfile.GameNameInternal.StartsWith("Tekken 7"))
                {
                    FieldInformation tk7lang = new FieldInformation();
                    foreach (var t in _gameProfile.ConfigValues)
                    {
                        if (t.FieldName == "Language")
                        {
                            tk7lang = t;
                        }
                    }

                    string lang = "us";
                    if (tk7lang.FieldValue == "us" || tk7lang.FieldValue == "jp" || tk7lang.FieldValue == "kr" ||
                        tk7lang.FieldValue == "as" || tk7lang.FieldValue == "cn")
                    {
                        lang = tk7lang.FieldValue;
                    }
                    File.WriteAllText(Path.GetDirectoryName(_gameLocation) + "../../../Content/Config/tekken.ini",
                        "Ver=\"1.06\"\r\nLanguage=\"" + lang + "\"\r\nRegion=\"" + lang + "\"\r\nLoadVsyncOff=\"off\"\r\nNonWaitStageLoad=\"off\"\r\nINITIALIZE_SEQUENCE_ERR_CHECK=\"off\"\r\nauthtype=\"OFFLINE\"\r\n");
                }

                if (InputCode.ButtonMode == EmulationProfile.GaiaAttack4)
                {
                    short _widthGA4 = 1280;
                    short _heightGA4 = 720;
                    short.TryParse(width.FieldValue, out _widthGA4);
                    short.TryParse(height.FieldValue, out _heightGA4);
                    string _region = region.FieldValue;
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(_gameLocation), "MINIGUN.INI"), "REGION\t\t" + _region + "\r\n" + "CNFNAME\t\t.\\OpenParrot\\GA4\r\nRANKFILE\t.\\OpenParrot\\\r\nPRJENABLE   \t1\r\nSCREEN_WIDTH\t" + _widthGA4 + "\r\n" + "SCREEN_HEIGHT\t" + _heightGA4 + "\r\nRENDER_WIDTH\t" + _widthGA4 + "\r\n" + "RENDER_HEIGHT\t" + _heightGA4 + "\r\nRENDER_WIDTH3D\t" + _widthGA4 + "\r\n" + "RENDER_HEIGHT3D\t" + _heightGA4 + "\r\n");
                }

                if (InputCode.ButtonMode == EmulationProfile.HauntedMuseum)
                {
                    short _widthHM = 1280;
                    short _heightHM = 720;
                    short.TryParse(width.FieldValue, out _widthHM);
                    short.TryParse(height.FieldValue, out _heightHM);
                    string _region = region.FieldValue;
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(_gameLocation), "MUSEUM.INI"), "REGION\t\t" + _region + "\r\n" + "CNFNAME\t\t.\\OpenParrot\\HM\r\nRANKFILE\t.\\OpenParrot\\\r\nPRJENABLE   \t1\r\nSCREEN_WIDTH\t" + _widthHM + "\r\n" + "SCREEN_HEIGHT\t" + _heightHM + "\r\nRENDER_WIDTH\t" + _widthHM + "\r\n" + "RENDER_HEIGHT\t" + _heightHM + "\r\nRENDER_WIDTH3D\t" + _widthHM + "\r\n" + "RENDER_HEIGHT3D\t" + _heightHM + "\r\n");
                }

                if (InputCode.ButtonMode == EmulationProfile.HauntedMuseum2)
                {
                    short _widthHM2 = 1280;
                    short _heightHM2 = 720;
                    short.TryParse(width.FieldValue, out _widthHM2);
                    short.TryParse(height.FieldValue, out _heightHM2);
                    string _region = region.FieldValue;
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(_gameLocation), "HAUNTED2.INI"), "REGION\t\t" + _region + "\r\n" + "CNFNAME\t\t.\\OpenParrot\\HM2\r\nRANKFILE\t.\\OpenParrot\\\r\nPRJENABLE   \t1\r\nSCREEN_WIDTH\t" + _widthHM2 + "\r\n" + "SCREEN_HEIGHT\t" + _heightHM2 + "\r\nRENDER_WIDTH\t" + _widthHM2 + "\r\n" + "RENDER_HEIGHT\t" + _heightHM2 + "\r\nRENDER_WIDTH3D\t" + _widthHM2 + "\r\n" + "RENDER_HEIGHT3D\t" + _heightHM2 + "\r\n");
                }

                if (InputCode.ButtonMode == EmulationProfile.SegaInitialD)
                {
                    var newCard = _gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "EnableNewCardCode");
                    if (newCard == null || newCard.FieldValue == "0")
                    {
                        RunAndWait(loaderExe,
                            $"{loaderDll} \"{Path.Combine(Path.GetDirectoryName(_gameLocation), "picodaemon.exe")}");
                    }
                }

                if (InputCode.ButtonMode == EmulationProfile.ALLSSWDC)
                {
                    // boot tdrserver.exe if its the main cab
                    var isSwdcMainCab = _gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "Main Cabinet");
                    var isOfflineMode = _gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "Offline Mode");

                    if (isOfflineMode != null && isOfflineMode.FieldValue != "0")
                    {
                        if (isSwdcMainCab != null && isSwdcMainCab.FieldValue != "0")
                        {
                            string tdrserverPath = Path.Combine(Path.GetDirectoryName(_gameLocation), @"..\..\..\..\..\Tools", "tdrserver.exe");
                            if (File.Exists(tdrserverPath))
                            {
                                RunAndWait(loaderExe, $"{loaderDll} \"{tdrserverPath}\"");
                            }
                        }
                    }
                }

                if (_gameProfile.EmulationProfile == EmulationProfile.SegaInitialDLindbergh || _gameProfile.EmulationProfile == EmulationProfile.SegaInitialD
                    || _gameProfile.EmulationProfile == EmulationProfile.Rambo
                    || _gameProfile.EmulationProfile == EmulationProfile.Vf5Lindbergh || _gameProfile.EmulationProfile == EmulationProfile.Vf5cLindbergh)
                {
                    GameRunningCode.Utilities.GameRunningUtils.CheckAMDDriver();
                }

                if (_twoExes && _secondExeFirst)
                    RunAndWait(loaderExe, $"{loaderDll} \"{_gameLocation2}\" {_secondExeArguments}");

                // minimize if requested
                info.WindowStyle = _gameRunning._launchMinimized ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal;

                Debug.WriteLine("Main App minimized: " + _gameRunning._launchMinimized);
                // intel openssl workaround
                if (_gameProfile.EmulationProfile == EmulationProfile.ALLSSWDC ||
                _gameProfile.EmulationProfile == EmulationProfile.IDZ ||
                _gameProfile.EmulationProfile == EmulationProfile.ALLSSCHRONO ||
                _gameProfile.EmulationProfile == EmulationProfile.NxL2 ||
                _gameProfile.EmulationProfile == EmulationProfile.RawThrillsFNF ||
                _gameProfile.EmulationProfile == EmulationProfile.ALLSHOTDSD ||
                _gameProfile.EmulationProfile == EmulationProfile.ALLSFGO ||
                _gameProfile.EmulationProfile == EmulationProfile.TimeCrisis5 ||
                _gameProfile.EmulationProfile == EmulationProfile.JojoLastSurvivor ||
                _gameProfile.EmulationProfile == EmulationProfile.DenshaDeGo ||
                _gameProfile.EmulationProfile == EmulationProfile.ALLSIDTA ||
                _gameProfile.EmulationProfile == EmulationProfile.SegaOlympic2020
                )
                {
                    try
                    {
                        info.UseShellExecute = false;
                        info.EnvironmentVariables.Add("OPENSSL_ia32cap", ":~0x20000000");
                        //Trace.WriteLine("openssl fix applied");
                    }
                    catch
                    {
                        //Trace.WriteLine("woops, openssl fix already applied by user");
                    }

                }

                var cmdProcess = new Process
                {
                    StartInfo = info
                };

                cmdProcess.OutputDataReceived += (sender, e) =>
                {
                    // Prepend line numbers to each line of the output.
                    if (string.IsNullOrEmpty(e.Data)) return;
                    try
                    {
                        textBoxConsole.Dispatcher.Invoke(() => textBoxConsole.AppendText("\n" + e.Data),
                        DispatcherPriority.Background);
                    }
                    catch
                    {
                        // swallow exception so exiting from something like launchbox doesnt cause an error message
                        Console.WriteLine("Ignoring textBoxConsoleDispatcher exception.");
                    }

                    Console.WriteLine(e.Data);
                };

                cmdProcess.EnableRaisingEvents = true;

                cmdProcess.Start();
                if (Lazydata.ParrotData.SilentMode &&
                    _gameProfile.EmulatorType != EmulatorType.Lindbergh &&
                    _gameProfile.EmulatorType != EmulatorType.N2 &&
                    _gameProfile.EmulatorType != EmulatorType.ElfLdr2)
                {
                    cmdProcess.BeginOutputReadLine();
                }

                if (_twoExes && !_secondExeFirst)
                    RunAndWait(loaderExe, $"{loaderDll} \"{_gameLocation2}\" {_secondExeArguments}");

                //cmdProcess.WaitForExit();
                bool idzRun = false;
                while (!cmdProcess.HasExited)
                {
#if DEBUG
                    if (_gameRunning.jvsDebug != null)
                    {
                        _gameRunning.jvsDebug.StartDebugInputThread();
                    }
#endif
                    if (_forceQuit)
                    {
                        cmdProcess.Kill();
                    }

                    if (_gameProfile.EmulationProfile == EmulationProfile.SegaToolsIDZ)
                    {
                        Application.Current.Dispatcher.Invoke((Action)delegate
                        {
                            if (System.Windows.Input.Keyboard.IsKeyDown(Key.Escape))
                            {
                                GameRunningCode.EmulatorHelpers.SegaToolsHelper.KillIDZ();

                                FreeConsole();
                                idzRun = true;
                            }
                        });

                    }

                    Thread.Sleep(500);
                }

                Analytics.DisableSending();
                GameErrorMessage.ShowGameError(cmdProcess.ExitCode);

                _gameRunning.TerminateThreads();
                if (!idzRun && _gameProfile.EmulationProfile == EmulationProfile.SegaToolsIDZ)
                {
                    //just in case it's been stopped some other way
                    GameRunningCode.EmulatorHelpers.SegaToolsHelper.KillIDZ();
                    FreeConsole();
                }
                if (runEmuOnly || cmdLaunch)
                {
                    Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
                }
                else if (_forceQuit == false)
                {
                    textBoxConsole.Dispatcher.Invoke(delegate
                    {
                        _gameRunning.gameRunning.Content = Properties.Resources.GameRunningGameStopped;
                        _gameRunning.progressBar.IsIndeterminate = false;
                        Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                    });
                    Application.Current.Dispatcher.Invoke(delegate
                        {
                            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = _library;
                        });
                }
                else
                {
                    textBoxConsole.Dispatcher.Invoke(delegate
                    {
                        _gameRunning.gameRunning.Content = Properties.Resources.GameRunningGameStopped;
                        _gameRunning.progressBar.IsIndeterminate = false;
                        MessageBoxHelper.WarningOK(Properties.Resources.GameRunningCheckTaskMgr);
                        Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                    });
                }
            });
            gameThread.Start();
        }
        private void ConfigureDolphinIni(bool isTatsuvscap)
        {
            string configPath = Path.Combine(".", "CrediarDolphin", "User", "Config", "Dolphin.ini");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));

                var lines = new List<string>();
                bool coreSection = false;
                bool foundCore = false;

                var coreSettings = new Dictionary<string, string>
        {
            {"SIDevice0", "11"},
            {"SIDevice1", "6"},
            {"SIDevice2", "0"},
            {"SIDevice3", "0"},
            {"SerialPort1", isTatsuvscap ? "255" : "6"},
            {"SlotA", "14"},
            {"SlotB", "14"},
            {"MEM1Size", "0x04000000"},
            {"MEM2Size", "0x08000000"},
            {"RAMOverrideEnable", isTatsuvscap ? "True" : "False"}
        };

                var processedSettings = new HashSet<string>();
                bool foundAnalytics = false;
                bool inAnalyticsSection = false;
                bool analyticsEnabledSet = false;
                bool permissionAskedSet = false;

                if (File.Exists(configPath))
                {
                    var existingLines = File.ReadAllLines(configPath);

                    for (int i = 0; i < existingLines.Length; i++)
                    {
                        string line = existingLines[i];
                        string trimmedLine = line.Trim();

                        if (trimmedLine == "[Core]")
                        {
                            coreSection = true;
                            foundCore = true;
                            lines.Add(line);
                            continue;
                        }

                        if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                        {
                            if (coreSection)
                            {
                                foreach (var setting in coreSettings)
                                {
                                    if (!processedSettings.Contains(setting.Key))
                                        lines.Add($"{setting.Key} = {setting.Value}");
                                }
                            }

                            coreSection = false;

                            inAnalyticsSection = trimmedLine == "[Analytics]";
                            if (inAnalyticsSection)
                                foundAnalytics = true;

                            lines.Add(line);
                            continue;
                        }

                        if (coreSection && trimmedLine.Contains("="))
                        {
                            string key = trimmedLine.Split('=')[0].Trim();
                            if (coreSettings.ContainsKey(key))
                            {
                                lines.Add($"{key} = {coreSettings[key]}");
                                processedSettings.Add(key);
                                continue;
                            }
                        }

                        if (inAnalyticsSection)
                        {
                            if (trimmedLine.StartsWith("ID"))
                                continue; // Skip the ID line

                            if (trimmedLine.StartsWith("Enabled"))
                            {
                                lines.Add("Enabled = False");
                                analyticsEnabledSet = true;
                                continue;
                            }

                            if (trimmedLine.StartsWith("PermissionAsked"))
                            {
                                lines.Add("PermissionAsked = True");
                                permissionAskedSet = true;
                                continue;
                            }
                        }

                        lines.Add(line);
                    }

                    if (coreSection)
                    {
                        foreach (var setting in coreSettings)
                        {
                            if (!processedSettings.Contains(setting.Key))
                                lines.Add($"{setting.Key} = {setting.Value}");
                        }
                    }
                }

                if (!foundCore)
                {
                    lines.Add("[Core]");
                    foreach (var setting in coreSettings)
                        lines.Add($"{setting.Key} = {setting.Value}");
                }

                if (!foundAnalytics)
                {
                    lines.Add("");
                    lines.Add("[Analytics]");
                    lines.Add("Enabled = False");
                    lines.Add("PermissionAsked = True");
                }
                else
                {
                    // Add missing keys to existing [Analytics] section
                    int insertIndex = lines.FindIndex(l => l.Trim() == "[Analytics]") + 1;

                    if (!analyticsEnabledSet)
                        lines.Insert(insertIndex++, "Enabled = False");

                    if (!permissionAskedSet)
                        lines.Insert(insertIndex, "PermissionAsked = True");
                }

                File.WriteAllLines(configPath, lines);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating Dolphin config: {ex.Message}");
            }
        }

        private void CreateOrUpdatePlayConfig(string configPath, Dictionary<string, (string type, string value)> configValues)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));

            var xmlDoc = new System.Xml.XmlDocument();

            if (File.Exists(configPath))
            {
                xmlDoc.Load(configPath);
            }
            else
            {
                xmlDoc.LoadXml("<Config></Config>");
            }

            var rootNode = xmlDoc.DocumentElement;

            foreach (var config in configValues)
            {
                var existingNode = xmlDoc.SelectSingleNode($"//Preference[@Name='{config.Key}']");

                if (existingNode != null)
                {
                    existingNode.Attributes["Value"].Value = config.Value.value;
                }
                else
                {
                    var newNode = xmlDoc.CreateElement("Preference");
                    newNode.SetAttribute("Name", config.Key);
                    newNode.SetAttribute("Type", config.Value.type);
                    newNode.SetAttribute("Value", config.Value.value);
                    rootNode.AppendChild(newNode);
                }
            }

            xmlDoc.Save(configPath);
        }

        private string GetGraphicsBackendValue()
        {
            if (_gameProfile.ConfigValues.Any(x => x.FieldName == "Graphics Backend" && x.FieldValue == "Vulkan"))
                return "1";

            return "0";
        }

        private string GetResolutionFactorValue()
        {
            var resolutionConfig = _gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "Resolution");

            if (resolutionConfig?.FieldValue == "960p") return "2";
            if (resolutionConfig?.FieldValue == "1920p") return "4";
            if (resolutionConfig?.FieldValue == "4320p") return "8";
            if (resolutionConfig?.FieldValue == "7680p") return "16";

            return "1";
        }
    }
}