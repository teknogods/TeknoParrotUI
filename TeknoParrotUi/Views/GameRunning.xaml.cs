﻿using System;
using System.Linq;
using System.Windows;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Interop;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for GameRunningUC.xaml
    /// </summary>
    public partial class GameRunning
    {
        private readonly bool _isTest;
        private readonly string _gameLocation;
        private bool _gameRunning;
        private readonly SerialPortHandler _serialPortHandler;
        private readonly string _testMenuString;
        private readonly bool _testMenuIsExe;
        private readonly string _testMenuExe;
        private readonly GameProfile _gameProfile;
        private static bool _runEmuOnly;
        private static Thread _diThread;
        private static ControlSender _controlSender;
        private static RawInputListener _rawInputListener = new RawInputListener();
        private static readonly InputListener InputListener = new InputListener();
        private static bool _killGunListener;
        private bool _jvsOverride;
        private readonly byte _player1GunMultiplier = 1;
        private readonly byte _player2GunMultiplier = 1;
        private bool _forceQuit;
        private readonly bool _cmdLaunch;
        private static ControlPipe _pipe;
        private Library _library;
#if DEBUG
        DebugJVS jvsDebug;
#endif

        public GameRunning(GameProfile gameProfile, bool isTest, string testMenuString,
            bool testMenuIsExe = false, string testMenuExe = "", bool runEmuOnly = false, bool profileLaunch = false, Library library = null)
        {
            InitializeComponent();
            if (profileLaunch == false && !runEmuOnly)
            {
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = false;
            }

            textBoxConsole.Text = "";
            _runEmuOnly = runEmuOnly;
            _gameLocation = gameProfile.GamePath;
            InputCode.ButtonMode = gameProfile.EmulationProfile;
            _isTest = isTest;
            _gameProfile = gameProfile;
            _serialPortHandler = new SerialPortHandler();
            _testMenuString = testMenuString;
            _testMenuIsExe = testMenuIsExe;
            _testMenuExe = testMenuExe;
            _cmdLaunch = profileLaunch;
            if (Lazydata.ParrotData?.GunSensitivityPlayer1 > 10)
                _player1GunMultiplier = 10;
            if (Lazydata.ParrotData?.GunSensitivityPlayer1 < 0)
                _player1GunMultiplier = 0;

            if (Lazydata.ParrotData?.GunSensitivityPlayer2 > 10)
                _player2GunMultiplier = 10;
            if (Lazydata.ParrotData?.GunSensitivityPlayer2 < 0)
                _player2GunMultiplier = 0;
            if (runEmuOnly)
            {
                buttonForceQuit.Visibility = Visibility.Collapsed;
            }

            gameName.Text = _gameProfile.GameName;
            _library = library;

#if DEBUG
            jvsDebug = new DebugJVS();
            jvsDebug.Show();
#endif
        }

        /// <summary>
        /// Handles gun game controls.
        /// </summary>
        private void HandleGunControls()
        {
            while (true)
            {
                if (_killGunListener)
                    return;

                if (InputCode.PlayerDigitalButtons[0].UpPressed())
                {
                    if (InputCode.AnalogBytes[0] <= 0xE0)
                        InputCode.AnalogBytes[0] += _player1GunMultiplier;
                }

                if (InputCode.PlayerDigitalButtons[0].DownPressed())
                {
                    if (InputCode.AnalogBytes[0] >= 10)
                        InputCode.AnalogBytes[0] -= _player1GunMultiplier;
                }

                if (InputCode.PlayerDigitalButtons[0].RightPressed())
                {
                    if (InputCode.AnalogBytes[2] >= 10)
                        InputCode.AnalogBytes[2] -= _player1GunMultiplier;
                }

                if (InputCode.PlayerDigitalButtons[0].LeftPressed())
                {
                    if (InputCode.AnalogBytes[2] <= 0xE0)
                        InputCode.AnalogBytes[2] += _player1GunMultiplier;
                }

                if (InputCode.PlayerDigitalButtons[1].UpPressed())
                {
                    if (InputCode.AnalogBytes[4] <= 0xE0)
                        InputCode.AnalogBytes[4] += _player2GunMultiplier;
                }

                if (InputCode.PlayerDigitalButtons[1].DownPressed())
                {
                    if (InputCode.AnalogBytes[4] >= 10)
                        InputCode.AnalogBytes[4] -= _player2GunMultiplier;
                }

                if (InputCode.PlayerDigitalButtons[1].RightPressed())
                {
                    if (InputCode.AnalogBytes[6] >= 10)
                        InputCode.AnalogBytes[6] -= _player2GunMultiplier;
                }

                if (InputCode.PlayerDigitalButtons[1].LeftPressed())
                {
                    if (InputCode.AnalogBytes[6] <= 0xE0)
                        InputCode.AnalogBytes[6] += _player2GunMultiplier;
                }

                Thread.Sleep(10);
            }
        }

        private void WriteConfigIni()
        {
            if (InputCode.ButtonMode == EmulationProfile.EuropaRSegaRally3)
                return;
            var lameFile = "";
            var categories = _gameProfile.ConfigValues.Select(x => x.CategoryName).Distinct().ToList();

            for (var i = 0; i < categories.Count(); i++)
            {
                lameFile += $"[{categories[i]}]{Environment.NewLine}";
                var variables = _gameProfile.ConfigValues.Where(x => x.CategoryName == categories[i]);
                lameFile = variables.Aggregate(lameFile,
                    (current, fieldInformation) =>
                        current + $"{fieldInformation.FieldName}={fieldInformation.FieldValue}{Environment.NewLine}");
            }

            File.WriteAllText(
                Path.Combine(Path.GetDirectoryName(_gameLocation) ?? throw new InvalidOperationException(),
                    "teknoparrot.ini"), lameFile);
        }
        
        private void GameRunning_OnLoaded(object sender, RoutedEventArgs e)
        {
            JvsPackageEmulator.Initialize();
            switch (InputCode.ButtonMode)
            {
                case EmulationProfile.EuropaRFordRacing:
                    if (_pipe == null)
                        _pipe = new EuropaRPipe();
                    break;
                case EmulationProfile.EuropaRSegaRally3:
                    if (_pipe == null)
                        _pipe = new SegaRallyPipe();
                    break;
                case EmulationProfile.FastIo:
                    if (_pipe == null)
                        _pipe = new FastIOPipe();
                    break;
            }

            _pipe?.Start();

            var invertButtons =
                _gameProfile.ConfigValues.Any(x => x.FieldName == "Invert Buttons" && x.FieldValue == "1");
            if (invertButtons)
            {
                JvsPackageEmulator.InvertMaiMaiButtons = true;
            }

            if (_rawInputListener == null)
                _rawInputListener = new RawInputListener();

            if (InputCode.ButtonMode == EmulationProfile.SegaJvsLetsGoIsland ||
                InputCode.ButtonMode == EmulationProfile.SegaJvsLetsGoJungle)
            {
                InputCode.AnalogBytes[0] = 127;
                InputCode.AnalogBytes[2] = 127;
                InputCode.AnalogBytes[4] = 127;
                InputCode.AnalogBytes[6] = 127;
            }
            else
            {
                InputCode.AnalogBytes[0] = 0;
                InputCode.AnalogBytes[2] = 0;
                InputCode.AnalogBytes[4] = 0;
                InputCode.AnalogBytes[6] = 0;
            }

            bool useMouseForGun =
                _gameProfile.ConfigValues.Any(x => x.FieldName == "UseMouseForGun" && x.FieldValue == "1");

            if (useMouseForGun && _gameProfile.GunGame)
                _rawInputListener.ListenToDevice(InputCode.ButtonMode == EmulationProfile.SegaJvsGoldenGun ||
                                                 InputCode.ButtonMode == EmulationProfile.Hotd4);

            switch (InputCode.ButtonMode)
            {
                case EmulationProfile.NamcoPokken:
                    _controlSender = new Pokken();
                    break;
                case EmulationProfile.ExBoard:
                    _controlSender = new ExBoard();
                    break;
                case EmulationProfile.GtiClub3:
                    _controlSender = new GtiClub3();
                    break;
                case EmulationProfile.Daytona3:
                    _controlSender = new Daytona3();
                    break;
                case EmulationProfile.GRID:
                    _controlSender = new GRID();
                    break;
            }

            _controlSender?.Start();

            if (_gameProfile.GunGame)
            {
                _killGunListener = false;
                new Thread(HandleGunControls).Start();
            }

            if (!_runEmuOnly)
                WriteConfigIni();

            if (InputCode.ButtonMode != EmulationProfile.EuropaRFordRacing &&
                InputCode.ButtonMode != EmulationProfile.EuropaRSegaRally3 &&
                InputCode.ButtonMode != EmulationProfile.FastIo)
            {
                // TODO: MAYBE MAKE THESE XML BASED?
                switch (InputCode.ButtonMode)
                {
                    case EmulationProfile.VirtuaRLimit:
                    case EmulationProfile.ChaseHq2:
                    case EmulationProfile.WackyRaces:
                        JvsPackageEmulator.Taito = true;
                        JvsPackageEmulator.JvsSwitchCount = 0x18;
                        break;
                    case EmulationProfile.TaitoTypeXBattleGear:
                        JvsPackageEmulator.JvsVersion = 0x30;
                        JvsPackageEmulator.TaitoStick = true;
                        JvsPackageEmulator.TaitoBattleGear = true;
                        JvsPackageEmulator.JvsSwitchCount = 0x18;
                        break;
                    case EmulationProfile.TaitoTypeXGeneric:
                        JvsPackageEmulator.JvsVersion = 0x30;
                        JvsPackageEmulator.TaitoStick = true;
                        JvsPackageEmulator.JvsSwitchCount = 0x18;
                        break;
                    case EmulationProfile.BorderBreak:
                        InputCode.AnalogBytes[0] = 0x7F; // Center analog
                        InputCode.AnalogBytes[2] = 0x7F; // Center analog
                        break;
                    case EmulationProfile.NamcoPokken:
                        JvsPackageEmulator.JvsVersion = 0x31;
                        JvsPackageEmulator.JvsCommVersion = 0x31;
                        JvsPackageEmulator.JvsCommandRevision = 0x31;
                        JvsPackageEmulator.JvsIdentifier = JVSIdentifiers.NBGI_Pokken;
                        JvsPackageEmulator.Namco = true;
                        break;
                    case EmulationProfile.NamcoWmmt5:
                    case EmulationProfile.NamcoMkdx:
                        JvsPackageEmulator.JvsVersion = 0x31;
                        JvsPackageEmulator.JvsCommVersion = 0x31;
                        JvsPackageEmulator.JvsCommandRevision = 0x31;
                        JvsPackageEmulator.JvsIdentifier = JVSIdentifiers.NBGI_MarioKart3;
                        JvsPackageEmulator.Namco = true;
                        JvsPackageEmulator.JvsSwitchCount = 0x18;
                        break;
                    case EmulationProfile.NamcoMachStorm:
                        JvsPackageEmulator.JvsVersion = 0x31;
                        JvsPackageEmulator.JvsCommVersion = 0x31;
                        JvsPackageEmulator.JvsCommandRevision = 0x31;
                        JvsPackageEmulator.JvsIdentifier = JVSIdentifiers.NamcoMultipurpose;
                        JvsPackageEmulator.Namco = true;
                        JvsPackageEmulator.JvsSwitchCount = 0x18;
                        break;
                    case EmulationProfile.DevThing1:
                        JvsPackageEmulator.JvsVersion = 0x30;
                        JvsPackageEmulator.TaitoStick = true;
                        JvsPackageEmulator.TaitoBattleGear = true;
                        JvsPackageEmulator.DualJvsEmulation = true;
                        JvsPackageEmulator.JvsSwitchCount = 0x18;
                        break;
                    case EmulationProfile.VirtuaTennis4:
                    case EmulationProfile.ArcadeLove:
                        JvsPackageEmulator.DualJvsEmulation = true;
                        break;
                    case EmulationProfile.LGS:  
                        JvsPackageEmulator.JvsCommVersion = 0x30;
                        JvsPackageEmulator.JvsVersion = 0x30;
                        JvsPackageEmulator.JvsCommandRevision = 0x30;
                        JvsPackageEmulator.JvsIdentifier = JVSIdentifiers.SegaLetsGoSafari;
                        break;
                }

                _serialPortHandler.StopListening();
                Thread.Sleep(1000);
                new Thread(() => _serialPortHandler.ListenPipe("TeknoParrot_JVS")).Start();
                new Thread(_serialPortHandler.ProcessQueue).Start();
            }

            if (useMouseForGun && _gameProfile.GunGame)
            {
                _diThread?.Abort(0);
                _diThread = null;
            }
            else
            {
                _diThread?.Abort(0);
                _diThread = CreateInputListenerThread(
                    _gameProfile.ConfigValues.Any(x => x.FieldName == "XInput" && x.FieldValue == "1"));
            }

            if (Lazydata.ParrotData.UseDiscordRPC)
                DiscordRPC.UpdatePresence(new DiscordRPC.RichPresence
                {
                    details = _gameProfile.GameName,
                    largeImageKey = _gameProfile.GameName.Replace(" ", "").ToLower(),
                    //https://stackoverflow.com/a/17632585
                    startTimestamp = (long) DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds
                });

            // Wait before launching second thread.
            if (!_runEmuOnly)
            {
                Thread.Sleep(1000);
                _gameRunning = true;
                CreateGameProcess();
            }
            else
            {
#if DEBUG
                new Thread(() =>
                {
                    while (true)
                    {
                        if (jvsDebug.JvsOverride)
                            Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(jvsDebug.DoCheckBoxesDude));
                    }
                }).Start();
#endif
            }
        }

        private void CreateGameProcess()
        {
            var gameThread = new Thread(() =>
            {
                var loaderExe = _gameProfile.Is64Bit ? ".\\OpenParrotx64\\OpenParrotLoader64.exe" : ".\\OpenParrotWin32\\OpenParrotLoader.exe";
                var loaderDll = string.Empty;

                switch (_gameProfile.EmulatorType)
                {
                    case EmulatorType.Lindbergh:
                        loaderExe = ".\\TeknoParrot\\BudgieLoader.exe";
                        break;
                    case EmulatorType.N2:
                        loaderExe = ".\\N2\\BudgieLoader.exe";
                        break;
                    case EmulatorType.OpenParrot:
                        loaderDll = (_gameProfile.Is64Bit ? ".\\OpenParrotx64\\OpenParrot64" : ".\\OpenParrotWin32\\OpenParrot");
                        break;
                    default:
                        loaderDll = ".\\TeknoParrot\\TeknoParrot";
                        break;
                }

                var windowed = _gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "1");
                var fullscreen = _gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "0");
                var width = _gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "ResolutionWidth");
                var height = _gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "ResolutionHeight");

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
                }

                string gameArguments;

                if (_isTest)
                {
                    gameArguments = _testMenuIsExe
                        ? $"\"{Path.Combine(Path.GetDirectoryName(_gameLocation) ?? throw new InvalidOperationException(), _testMenuExe)}\" {_testMenuString}"
                        : $"\"{_gameLocation}\" {_testMenuString} {extra}";
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
                                    extra += "-vga";
                                else
                                    extra += "-wxga";
                            }

                            break;
                        case EmulatorType.N2:
                            extra = "-heapsize 131072 +set developer 1 -game czero -devel -nodb -console -noms";
                            break;
                    }

                    gameArguments = $"\"{_gameLocation}\" {extra}";
                }

                var info = new ProcessStartInfo(loaderExe, $"{loaderDll} {gameArguments}");
                var cmdProcess = new Process();

                if (_gameProfile.EmulatorType == EmulatorType.N2)
                {
                    info.WorkingDirectory =
                        Path.GetDirectoryName(_gameLocation) ?? throw new InvalidOperationException();
                    info.UseShellExecute = false;
                    info.EnvironmentVariables.Add("tp_msysType", "3");
                    info.EnvironmentVariables.Add("tp_windowed", windowed ? "1" : "0");
                }

                if (_gameProfile.EmulatorType == EmulatorType.Lindbergh)
                {
                    if (windowed)
                        info.EnvironmentVariables.Add("tp_windowed", "1");

                    if (_gameProfile.EmulationProfile == EmulationProfile.Vt3Lindbergh
                        || _gameProfile.EmulationProfile == EmulationProfile.Vf5Lindbergh
                        || _gameProfile.EmulationProfile == EmulationProfile.Vf5cLindbergh)
                        info.EnvironmentVariables.Add("tp_msysType", "2");

                    if (_gameProfile.EmulationProfile == EmulationProfile.SegaRtv
                        || _gameProfile.EmulationProfile == EmulationProfile.SegaJvsLetsGoJungle)
                        info.EnvironmentVariables.Add("tp_msysType", "3");

                    if (_gameProfile.EmulationProfile == EmulationProfile.SegaInitialDLindbergh
                        || _gameProfile.EmulationProfile == EmulationProfile.Vf5Lindbergh
                        || _gameProfile.EmulationProfile == EmulationProfile.Vf5cLindbergh
                        || _gameProfile.EmulationProfile == EmulationProfile.SegaRtv
                        || _gameProfile.EmulationProfile == EmulationProfile.SegaJvsLetsGoJungle)
                    {
                        info.EnvironmentVariables.Add("TEA_DIR", Path.GetDirectoryName(_gameLocation) + "\\");
                    }
                    else if (_gameProfile.EmulationProfile == EmulationProfile.Vt3Lindbergh)
                    {
                        info.EnvironmentVariables.Add("TEA_DIR",
                            Directory.GetParent(Path.GetDirectoryName(_gameLocation)) + "\\");
                    }

                    info.WorkingDirectory =
                        Path.GetDirectoryName(_gameLocation) ?? throw new InvalidOperationException();
                    info.UseShellExecute = false;
                }
                else
                {
                    info.UseShellExecute = false;
                }

                if (Lazydata.ParrotData.SilentMode && _gameProfile.EmulatorType != EmulatorType.Lindbergh &&
                    _gameProfile.EmulatorType != EmulatorType.N2)
                {
                    info.WindowStyle = ProcessWindowStyle.Hidden;
                    info.RedirectStandardError = true;
                    info.RedirectStandardOutput = true;
                    info.CreateNoWindow = true;
                }
                else
                {
                    info.WindowStyle = ProcessWindowStyle.Normal;
                }

                if (InputCode.ButtonMode == EmulationProfile.NamcoMkdx)
                {
                    var amcus = Path.Combine(Path.GetDirectoryName(_gameLocation), "AMCUS");

                    //If these files exist, this isn't a "original version"
                    if (File.Exists(Path.Combine(amcus, "AMAuthd.exe")) &&
                        File.Exists(Path.Combine(amcus, "iauthdll.dll")))
                    {
                        // Write WritableConfig.ini
                        File.WriteAllText(
                            Path.Combine(Path.GetDirectoryName(_gameLocation), "AMCUS", "WritableConfig.ini"),
                            "[RuntimeConfig]\r\nmode=SERVER\r\nnetID=ABGN\r\nserialID=\r\n[MuchaChargeData]\r\ncamode-ch_token_consumed=0\r\ncamode-ch_token_charged=0\r\ncamode-ch_token_unit=0\r\ncamode-ch_token_lower=0\r\ncamode-ch_token_upper=0\r\ncamode-ch_token_month=0\r\n");

                        // Write AMConfig.ini
                        File.WriteAllText(Path.Combine(Path.GetDirectoryName(_gameLocation), "AMCUS", "AMConfig.ini"),
                            "[AMUpdaterConfig] \r\n;; AMUpdater\r\namucfg-title=COCO\r\namucfg-lang=JP\r\namucfg-countdown=5\r\namucfg-h_resol=1360\r\namucfg-v_resol=768\r\namucfg-logfile=amupdater.log\r\namucfg-game_rev=1\r\n\r\n[AMAuthdConfig]\r\n;; AMAuthd\r\namdcfg-authType=ALL.NET\r\namdcfg-sleepTime=50\r\namdcfg-resoNameTimeout=180\r\namdcfg-writableConfig=WritableConfig.ini\r\namdcfg-showConsole=ENABLE\r\namdcfg-logfile=- ;\r\namdcfg-export_log=AmAuthdLog.zip ;\r\n\r\n[AllnetConfig] \r\n;; ALL.Net\r\nallcfg-gameID=SBZB\r\nallcfg-gameVer=1.10\r\n;allcfg-tenpoAddr=;\r\n;allcfg-authServerAddr=;\r\n\r\n[AllnetOptionRevalTime]\r\n;; ALL.Net\r\nallopt-reval_hour=7\r\nallopt-reval_minute=0\r\nallopt-reval_second=0\r\n\r\n[AllnetOptionTimeout]\r\n;; ALL.Net\r\nallopt-timeout_connect=60000  \r\nallopt-timeout_send=60000\r\nallopt-timeout_recv=60000\r\n\r\n[MuchaAppConfig]\r\n;; mucha_app\r\nappcfg-logfile=muchaapp.log;\r\nappcfg-loglevel=INFO ;\r\n\r\n[MuchaSysConfig]\r\n;; MUCHA\r\nsyscfg-daemon_exe=.\\MuchaBin\\muchacd.exe\r\nsyscfg-daemon_pidfile=muchacd.pid ;\r\nsyscfg-daemon_logfile=muchacd.log ;\r\nsyscfg-daemon_loglevel=INFO ;\r\nsyscfg-daemon_listen=tcp:0.0.0.0:8765\r\nsyscfg-client_connect=tcp:127.0.0.1:8765\r\n\r\n[MuchaCAConfig]\r\n;; MUCHA\r\ncacfg-game_cd=MK31 ;\r\ncacfg-game_ver=10.22\r\ncacfg-game_board_type=0\r\ncacfg-game_board_id=PCB\r\ncacfg-auth_server_url=https://127.0.0.1:443/mucha_front/\r\ncacfg-auth_server_sslverify=1\r\ncacfg-auth_server_sslcafile=.\\MuchaBin\\cakey_mk3.pem\r\ncacfg-auth_server_timeout=0\r\ncacfg-interval_ainfo_renew=1800\r\ncacfg-interval_ainfo_retry=60\r\ncacfg-auth_place_id=JPN0128C ;\r\n;cacfg-auth_store_router_ip=\r\n\r\n[MuchaDtConfig]\r\n;; MUCHA\r\ndtcfg-dl_product_id=0x4d4b3331\r\ndtcfg-dl_chunk_size=65536\r\ndtcfg-dl_image_path=chunk.img\r\ndtcfg-dl_image_size=0\r\ndtcfg-dl_image_type=FILE\r\ndtcfg-dl_image_crypt_key=0xfedcba9876543210\r\ndtcfg-dl_log_level=INFO ;\r\ndtcfg-dl_lan_crypt_key=0xfedcba9876543210\r\ndtcfg-dl_lan_broadcast_interval=1000\r\ndtcfg-dl_lan_udp_port=9026\r\ndtcfg-dl_lan_bandwidth_limit=0\r\ndtcfg-dl_lan_broadcast_address=0.0.0.0\r\ndtcfg-dl_wan_retry_limit=\r\ndtcfg-dl_wan_retry_interval=\r\ndtcfg-dl_wan_send_timeout=\r\ndtcfg-dl_wan_recv_timeout=\r\ndtcfg-dl_lan_retry_limit=\r\ndtcfg-dl_lan_retry_interval=\r\ndtcfg-dl_lan_send_timeout=\r\ndtcfg-dl_lan_recv_timeout=\r\n\r\n[MuchaDtModeConfig]\r\n;; MUCHA\r\ndtmode-io_dir=.\\ ;\r\ndtmode-io_file=MK3_JP_\r\ndtmode-io_conv=DECEXP\r\ndtmode-io_passphrase=ktinkynhgimbt\r\n");

                        // Register iauthd.dll
                        Register_Dlls(Path.Combine(Path.GetDirectoryName(_gameLocation), "AMCUS", "iauthdll.dll"));

                        // Start AMCUS
                        RunAndWait(loaderExe,
                            $"{loaderDll} \"{Path.Combine(Path.GetDirectoryName(_gameLocation), "AMCUS", "AMAuthd.exe")}\"");
                    }
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

                //this starts the game
                cmdProcess.StartInfo = info;

                cmdProcess.OutputDataReceived += (sender, e) =>
                {
                    // Prepend line numbers to each line of the output.
                    if (string.IsNullOrEmpty(e.Data)) return;
                    textBoxConsole.Dispatcher.Invoke(() => textBoxConsole.Text += "\n" + e.Data,
                        DispatcherPriority.Background);
                    Console.WriteLine(e.Data);
                };

                cmdProcess.EnableRaisingEvents = true;

                cmdProcess.Start();
                if (Lazydata.ParrotData.SilentMode && _gameProfile.EmulatorType != EmulatorType.Lindbergh &&
                    _gameProfile.EmulatorType != EmulatorType.N2)
                {
                    cmdProcess.BeginOutputReadLine();
                }

                //cmdProcess.WaitForExit();

                while (!cmdProcess.HasExited)
                {
#if DEBUG
                    if (jvsDebug.JvsOverride)
                        Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(jvsDebug.DoCheckBoxesDude));
#endif
                    if (_forceQuit)
                    {
                        cmdProcess.Kill();
                    }

                    Thread.Sleep(500);
                }

                _gameRunning = false;
                TerminateThreads();
                if (_runEmuOnly || _cmdLaunch)
                {
                    Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
                }
                else if (_forceQuit == false)
                {
                    textBoxConsole.Invoke(delegate
                    {
                        gameRunning.Text = "Game Stopped";
                        progressBar.IsIndeterminate = false;
                        Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                    });
                    Thread.Sleep(5000);
                    Application.Current.Dispatcher.Invoke(delegate
                        {
                            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = _library;
                        });
                }
                else
                {
                    textBoxConsole.Invoke(delegate
                    {
                        gameRunning.Text = "Game Stopped";
                        progressBar.IsIndeterminate = false;
                        MessageBox.Show(
                            "Since you force closed the emulator, you should check Task Manager for any processes still running that are related to the emulator or your game.");
                        Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                    });
                }
            });
            gameThread.Start();
        }


        private static void Register_Dlls(string filePath)
        {
            try
            {
                //'/s' : Specifies regsvr32 to run silently and to not display any message boxes.
                string argFileinfo = "/s" + " " + "\"" + filePath + "\"";
                Process reg = new Process();
                //This file registers .dll files as command components in the registry.
                reg.StartInfo.FileName = "regsvr32.exe";
                reg.StartInfo.Arguments = argFileinfo;
                reg.StartInfo.UseShellExecute = false;
                reg.StartInfo.CreateNoWindow = true;
                reg.StartInfo.RedirectStandardOutput = true;
                reg.Start();
                reg.WaitForExit();
                reg.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void RunAndWait(string loaderExe, string daemonPath)
        {
            Process.Start(new ProcessStartInfo(loaderExe, daemonPath));
            Thread.Sleep(1000);
        }

        private Thread CreateInputListenerThread(bool useXinput)
        {
            var hWnd = new WindowInteropHelper(Application.Current.MainWindow ?? throw new InvalidOperationException())
                .EnsureHandle();
            var inputThread = new Thread(() => InputListener.Listen(Lazydata.ParrotData.UseSto0ZDrivingHack,
                Lazydata.ParrotData.StoozPercent, _gameProfile.JoystickButtons, useXinput, _gameProfile));
            inputThread.Start();
            return inputThread;
        }

        private void TerminateThreads()
        {
            _rawInputListener?.StopListening();
            _controlSender?.Stop();
            InputListener?.StopListening();
            _serialPortHandler?.StopListening();
            _pipe?.Stop();
            _killGunListener = true;
        }

        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            _jvsOverride = !_jvsOverride;
        }

        private void ButtonForceQuit_Click(object sender, RoutedEventArgs e)
        {
            _forceQuit = true;
        }

        private void GameRunning_OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (Lazydata.ParrotData.UseDiscordRPC) DiscordRPC.ClearPresence();
#if DEBUG
            jvsDebug?.Close();
#endif
            TerminateThreads();
            Thread.Sleep(100);
            if (_runEmuOnly)
            {
                MainWindow.SafeExit();
            }
        }
    }
}