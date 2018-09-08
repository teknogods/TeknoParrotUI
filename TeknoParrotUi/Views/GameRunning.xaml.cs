using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using SharpDX.DirectInput;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;
using TeknoParrotUi.Helpers;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for GameRunning.xaml
    /// </summary>
    public partial class GameRunning : MetroWindow
    {
        private readonly bool _isTest;
        private readonly string _gameLocation;
        private bool _gameRunning;
        private readonly SerialPortHandler _serialPortHandler;
        private readonly ParrotData _parrotData;
        private string _testMenuString;
        private bool _testMenuIsExe;
        private string _testMenuExe;
        private GameProfile _gameProfile;
        private static EuropaRPipeHandler _europa;
        private static bool _runEmuOnly;
        private static Thread _jvsThread;
        private static Thread _processQueueThread;
        private static Thread _diThread;
        private static PokkenControlSender _pokkenControlSender = new PokkenControlSender();
        private static GtiClub3ControlSender _gtiClub3ControlSender = new GtiClub3ControlSender();
        private static RawInputListener _rawInputListener = new RawInputListener();
        private static InputListener _inputListener = new InputListener();
        private static bool KillGunListener;
        private static Thread LgiThread;
        private static bool _endCheckBox = false;
        private bool _JvsOverride = false;
        private byte _player1GunMultiplier = 1;
        private byte _player2GunMultiplier = 1;
        private bool _enableForceFeedback = false;
        private static SpecialControlPipe _specialControl;

        public GameRunning(GameProfile gameProfile, bool isTest, ParrotData parrotData, string testMenuString, bool testMenuIsExe = false, string testMenuExe = "", bool runEmuOnly = false)
        {
            InitializeComponent();
            _runEmuOnly = runEmuOnly;
            _gameLocation = gameProfile.GamePath;
            InputCode.ButtonMode = gameProfile.EmulationProfile;
            _isTest = isTest;
            _gameProfile = gameProfile;
            _serialPortHandler = new SerialPortHandler();
            _parrotData = parrotData;
            _testMenuString = testMenuString;
            _testMenuIsExe = testMenuIsExe;
            _testMenuExe = testMenuExe;
            if (parrotData?.GunSensitivityPlayer1 > 10)
                _player1GunMultiplier = 10;
            if (parrotData?.GunSensitivityPlayer1 < 0)
                _player1GunMultiplier = 0;

            if (parrotData?.GunSensitivityPlayer2 > 10)
                _player2GunMultiplier = 10;
            if (parrotData?.GunSensitivityPlayer2 < 0)
                _player2GunMultiplier = 0;
        }

        /// <summary>
        /// Handles Lets Go Island controls.
        /// </summary>
        /// <param name="playerButtons"></param>
        /// <param name="playerNumber"></param>
        private void HandleLgiControls()
        {
            while (true)
            {
                if (KillGunListener)
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
                foreach (var fieldInformation in variables)
                {
                    lameFile += $"{fieldInformation.FieldName}={fieldInformation.FieldValue}{Environment.NewLine}";
                }
            }
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(_gameLocation), "teknoparrot.ini"), lameFile);
        }

        private void PrivateInitJvs()
        {
            JvsPackageEmulator.EnableNamco = false;
            JvsPackageEmulator.EnableTaito = false;
            JvsPackageEmulator.EnableTaitoStick = false;
            JvsPackageEmulator.EnableTaitoBattleGear = false;
            JvsPackageEmulator.EnableDualJvsEmulation = false;
        }

        private void GameRunning_OnLoaded(object sender, RoutedEventArgs e)
        {
            PrivateInitJvs();
            if (InputCode.ButtonMode == EmulationProfile.EuropaRFordRacing || InputCode.ButtonMode == EmulationProfile.EuropaRSegaRally3)
            {
                if(_europa == null)
                    _europa = new EuropaRPipeHandler();
                _europa.StartListening(InputCode.ButtonMode == EmulationProfile.EuropaRSegaRally3);
            }
            if (InputCode.ButtonMode == EmulationProfile.FastIo)
            {
                if (_specialControl == null)
                    _specialControl = new SpecialControlPipe();
                _specialControl.StartListening(SpecialControlPipe.PipeModes.FastIo);
            }
            if(_rawInputListener == null)
                _rawInputListener = new RawInputListener();

            if (InputCode.ButtonMode == EmulationProfile.SegaJvsLetsGoIsland)
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

            if(_parrotData.UseMouse && (InputCode.ButtonMode == EmulationProfile.SegaJvsLetsGoIsland || InputCode.ButtonMode == EmulationProfile.SegaJvsDreamRaiders || InputCode.ButtonMode == EmulationProfile.SegaJvsGoldenGun))
                _rawInputListener.ListenToDevice(InputCode.ButtonMode == EmulationProfile.SegaJvsGoldenGun);

            if (InputCode.ButtonMode == EmulationProfile.NamcoPokken)
            {
                _pokkenControlSender.StartListening();
            }

            if (InputCode.ButtonMode == EmulationProfile.GtiClub3)
            {
                _gtiClub3ControlSender.StartListening();
            }

            if (InputCode.ButtonMode == EmulationProfile.SegaJvsLetsGoIsland || InputCode.ButtonMode == EmulationProfile.SegaJvsDreamRaiders || InputCode.ButtonMode == EmulationProfile.SegaJvsGoldenGun)
            {
                KillGunListener = false;
                LgiThread = new Thread(HandleLgiControls);
                LgiThread.Start();
            }

            if(!_runEmuOnly)
                WriteConfigIni();

            if (InputCode.ButtonMode != EmulationProfile.EuropaRFordRacing && InputCode.ButtonMode != EmulationProfile.EuropaRSegaRally3 && InputCode.ButtonMode != EmulationProfile.FastIo)
            {
                // TODO: MAYBE MAKE THESE XML BASED?
                JvsPackageEmulator.JvsSwitchCount = 0x0E;
                switch (InputCode.ButtonMode)
                {
                    case EmulationProfile.VirtuaRLimit:
                    {
                        JvsPackageEmulator.JvsCommVersion = 0x10;
                        JvsPackageEmulator.JvsVersion = 0x20;
                        JvsPackageEmulator.JvsCommandRevision = 0x13;
                        JvsPackageEmulator.JvsIdentifier = JvsHelper.JVS_IDENTIFIER_Sega2005Jvs14572;
                        JvsPackageEmulator.EnableTaito = true;
                        JvsPackageEmulator.JvsSwitchCount = 0x18;
                        }
                        break;
                    case EmulationProfile.ChaseHq2:
                    case EmulationProfile.WackyRaces:
                    {
                        JvsPackageEmulator.JvsCommVersion = 0x10;
                        JvsPackageEmulator.JvsVersion = 0x20;
                        JvsPackageEmulator.JvsCommandRevision = 0x13;
                        JvsPackageEmulator.JvsIdentifier = JvsHelper.JVS_IDENTIFIER_Sega2005Jvs14572;
                        JvsPackageEmulator.EnableTaito = true;
                        JvsPackageEmulator.JvsSwitchCount = 0x18;
                        }
                        break;
                    case EmulationProfile.TaitoTypeXBattleGear:
                        {
                        JvsPackageEmulator.JvsCommVersion = 0x10;
                        JvsPackageEmulator.JvsVersion = 0x30;
                        JvsPackageEmulator.JvsCommandRevision = 0x13;
                        JvsPackageEmulator.JvsIdentifier = JvsHelper.JVS_IDENTIFIER_Sega2005Jvs14572;
                        JvsPackageEmulator.EnableTaitoStick = true;
                        JvsPackageEmulator.EnableTaitoBattleGear = true;
                            JvsPackageEmulator.JvsSwitchCount = 0x18;
                        }
                        break;
                    case EmulationProfile.TaitoTypeXGeneric:
                    {
                        JvsPackageEmulator.JvsCommVersion = 0x10;
                        JvsPackageEmulator.JvsVersion = 0x30;
                        JvsPackageEmulator.JvsCommandRevision = 0x13;
                        JvsPackageEmulator.JvsIdentifier = JvsHelper.JVS_IDENTIFIER_Sega2005Jvs14572;
                        JvsPackageEmulator.EnableTaitoStick = true;
                        JvsPackageEmulator.JvsSwitchCount = 0x18;
                        }
                        break;
                    case EmulationProfile.BorderBreak:
                    {
                        JvsPackageEmulator.JvsCommVersion = 0x10;
                        JvsPackageEmulator.JvsVersion = 0x20;
                        JvsPackageEmulator.JvsCommandRevision = 0x13;
                        JvsPackageEmulator.JvsIdentifier = JvsHelper.JVS_IDENTIFIER_Sega2005Jvs14572;
                        InputCode.AnalogBytes[0] = 0x7F; // Center analog
                        InputCode.AnalogBytes[2] = 0x7F; // Center analog
                    }
                        break;
                    case EmulationProfile.NamcoPokken:
                    {
                        JvsPackageEmulator.JvsVersion = 0x31;
                        JvsPackageEmulator.JvsCommVersion = 0x31;
                        JvsPackageEmulator.JvsCommandRevision = 0x31;
                        JvsPackageEmulator.JvsIdentifier = JvsHelper.JVS_IDENTIFIER_NBGI_Pokken;
                        JvsPackageEmulator.EnableNamco = true;
                    }
                        break;
                    case EmulationProfile.NamcoWmmt5:
                    case EmulationProfile.NamcoMkdx:
                    {
                        JvsPackageEmulator.JvsVersion = 0x31;
                        JvsPackageEmulator.JvsCommVersion = 0x31;
                        JvsPackageEmulator.JvsCommandRevision = 0x31;
                        JvsPackageEmulator.JvsIdentifier = JvsHelper.JVS_IDENTIFIER_NBGI_MarioKart3;
                        JvsPackageEmulator.EnableNamco = true;
                        JvsPackageEmulator.JvsSwitchCount = 0x18;
                    }
                        break;
                    case EmulationProfile.NamcoMachStorm:
                    {
                        JvsPackageEmulator.JvsVersion = 0x31;
                        JvsPackageEmulator.JvsCommVersion = 0x31;
                        JvsPackageEmulator.JvsCommandRevision = 0x31;
                        JvsPackageEmulator.JvsIdentifier = JvsHelper.JVS_IDENTIFIER_StarWars;
                        JvsPackageEmulator.EnableNamco = true;
                        JvsPackageEmulator.JvsSwitchCount = 0x18;
                        }
                        break;
                    case EmulationProfile.ShiningForceCrossRaid:
                    {
                        JvsPackageEmulator.JvsCommVersion = 0x10;
                        JvsPackageEmulator.JvsVersion = 0x20;
                        JvsPackageEmulator.JvsCommandRevision = 0x13;
                        JvsPackageEmulator.JvsIdentifier = JvsHelper.JVS_IDENTIFIER_Sega2005Jvs14572;
                    }
                        break;
                    case EmulationProfile.SegaJvsGoldenGun:
                    case EmulationProfile.AfterBurnerClimax:
                    {
                        JvsPackageEmulator.JvsCommVersion = 0x10;
                        JvsPackageEmulator.JvsVersion = 0x20;
                        JvsPackageEmulator.JvsCommandRevision = 0x13;
                        JvsPackageEmulator.JvsIdentifier = JvsHelper.JVS_IDENTIFIER_Sega2005Jvs14572;
                    }
                        break;
                    case EmulationProfile.SegaSonicAllStarsRacing:
                    {
                        JvsPackageEmulator.JvsCommVersion = 0x10;
                        JvsPackageEmulator.JvsVersion = 0x20;
                        JvsPackageEmulator.JvsCommandRevision = 0x13;
                        JvsPackageEmulator.JvsIdentifier = JvsHelper.JVS_IDENTIFIER_Sega2005Jvs14572;
                    }
                        break;
                    case EmulationProfile.DevThing1:
                    {
                        JvsPackageEmulator.JvsCommVersion = 0x10;
                        JvsPackageEmulator.JvsVersion = 0x30;
                        JvsPackageEmulator.JvsCommandRevision = 0x13;
                        JvsPackageEmulator.JvsIdentifier = JvsHelper.JVS_IDENTIFIER_Sega2005Jvs14572;
                        JvsPackageEmulator.EnableTaitoStick = true;
                        JvsPackageEmulator.EnableTaitoBattleGear = true;
                            JvsPackageEmulator.EnableDualJvsEmulation = true;
                        JvsPackageEmulator.JvsSwitchCount = 0x18;
                    }
                        break;
                    case EmulationProfile.VirtuaTennis4:
                    case EmulationProfile.ArcadeLove:
                    {
                        JvsPackageEmulator.JvsCommVersion = 0x10;
                        JvsPackageEmulator.JvsVersion = 0x20;
                        JvsPackageEmulator.JvsCommandRevision = 0x13;
                        JvsPackageEmulator.JvsIdentifier = JvsHelper.JVS_IDENTIFIER_Sega2005Jvs14572;
                        JvsPackageEmulator.EnableDualJvsEmulation = true;
                    }
                        break;
                    case EmulationProfile.LGS:
                    {
                        JvsPackageEmulator.JvsCommVersion = 0x30;
                        JvsPackageEmulator.JvsVersion = 0x30;
                        JvsPackageEmulator.JvsCommandRevision = 0x30;
                        JvsPackageEmulator.JvsIdentifier = JvsHelper.JVS_IDENTIFIER_SegaLetsGoSafari;
                    }
                        break;
                    case EmulationProfile.SegaJvs:
                    case EmulationProfile.SegaJvsLetsGoIsland:
                    case EmulationProfile.SegaJvsDreamRaiders:
                    case EmulationProfile.ProjectDivaNu:
                    case EmulationProfile.SegaInitialD:
                    case EmulationProfile.SegaInitialDLindbergh:
                    case EmulationProfile.SegaRacingClassic:
                    default:
                        {
                            JvsPackageEmulator.JvsCommVersion = 0x10;
                            JvsPackageEmulator.JvsVersion = 0x20;
                            JvsPackageEmulator.JvsCommandRevision = 0x13;
                            JvsPackageEmulator.JvsIdentifier = JvsHelper.JVS_IDENTIFIER_Sega2005Jvs14572;
                        }
                        break;
                }
                _serialPortHandler.StopListening();
                Thread.Sleep(1000);
                _jvsThread = new Thread(() => _serialPortHandler.ListenPipe("TeknoParrot_JVS"));
                _jvsThread.Start();
                _processQueueThread = new Thread(_serialPortHandler.ProcessQueue);
                _processQueueThread.Start();
            }

            if (_parrotData.UseMouse && (InputCode.ButtonMode == EmulationProfile.SegaJvsLetsGoIsland || InputCode.ButtonMode == EmulationProfile.SegaJvsDreamRaiders || InputCode.ButtonMode == EmulationProfile.SegaJvsGoldenGun))
            {
                _diThread?.Abort(0);
                _diThread = null;
            }
            else
            {
                _diThread?.Abort(0);
                _diThread = CreateInputListenerThread(_parrotData.XInputMode);
            }

            // Wait before launching second thread.
            if (!_runEmuOnly)
            {
                Thread.Sleep(1000);
                _gameRunning = true;
                CreateGameProcess();
            }
            else
            {
                if (_parrotData.UseHaptic)
                {
                    if (InputCode.ButtonMode == EmulationProfile.SegaRacingClassic
                        || InputCode.ButtonMode == EmulationProfile.EuropaRSegaRally3
                        || InputCode.ButtonMode == EmulationProfile.EuropaRFordRacing
                        || InputCode.ButtonMode == EmulationProfile.SegaInitialD
                        || InputCode.ButtonMode == EmulationProfile.WackyRaces
                        || InputCode.ButtonMode == EmulationProfile.ChaseHq2
                        || InputCode.ButtonMode == EmulationProfile.NamcoWmmt5
                        || InputCode.ButtonMode == EmulationProfile.Outrun2SPX)
                    {
                        // TODO: NOT TESTED BEFORE COMMIT
                        var t = new Thread(() => FfbHelper.UseForceFeedback(_parrotData, ref _endCheckBox));
                        t.Start();
                    }
                }
            }
        }

        private void CreateGameProcess()
        {
            // TODO: PUT ALL IN SEPARATE FUNCTIONS INSTEAD OF THIS DIHARREA THX
            var gameThread = new Thread(() =>
            {
                string loaderExe; 

                if (_gameProfile.IsOpenParrot)
                {
                    loaderExe = _gameProfile.Is64Bit ? "OpenParrot64.exe" : "OpenParrot.exe";
                }
                else
                {
                    loaderExe = _gameProfile.Is64Bit ? "ParrotLoader64.exe" : "ParrotLoader.exe";
                }

                if(_gameProfile.EmulationProfile == EmulationProfile.Outrun2SPX 
                   || _gameProfile.EmulationProfile == EmulationProfile.AfterBurnerClimax
                   || _gameProfile.EmulationProfile == EmulationProfile.SegaInitialDLindbergh
                   || _gameProfile.EmulationProfile == EmulationProfile.Vt3Lindbergh
                   || _gameProfile.EmulationProfile == EmulationProfile.SegaRtv)
                {
                    loaderExe = "BudgieLoader.exe";
                }
                ProcessStartInfo info;
                if (_isTest)
                {
                    if (_testMenuIsExe)
                    {
                        info = new ProcessStartInfo(loaderExe,
                            $"\"{Path.Combine(Path.GetDirectoryName(_gameLocation), _testMenuExe)}\" {_testMenuString}");
                    }
                    else
                    {
                        if (_gameProfile.EmulationProfile == EmulationProfile.AfterBurnerClimax &&
                            _gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "0"))
                        {
                            info = new ProcessStartInfo(loaderExe, $"\"{_gameLocation}\" {_testMenuString} -full");
                        }
                        else
                        {
                            info = new ProcessStartInfo(loaderExe, $"\"{_gameLocation}\" {_testMenuString}");
                        }
                    }
                }
                else
                {
                    // TODO: CLEAN THIS SHIT UP!
                    if (_gameProfile.EmulationProfile == EmulationProfile.TaitoTypeXBattleGear &&
                        _gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "0"))
                    {
                        info = new ProcessStartInfo(loaderExe, $"\"{_gameLocation}\" " + "_MTS_FULL_SCREEN_");
                    }
                    else if (_gameProfile.EmulationProfile == EmulationProfile.NamcoMachStorm &&
                        _gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "0"))
                    {
                        info = new ProcessStartInfo(loaderExe, $"\"{_gameLocation}\" " + "-fullscreen");
                    }
                    else if (_gameProfile.EmulationProfile == EmulationProfile.SegaInitialDLindbergh &&
                             _gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "1"))
                    {
                        info = new ProcessStartInfo(loaderExe, $"\"{_gameLocation}\"");
                        info.EnvironmentVariables.Add("tp_windowed", "1");
                    }
                    else if(_gameProfile.EmulationProfile == EmulationProfile.Outrun2SPX &&
                    _gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "1"))
                    {
                        info = new ProcessStartInfo(loaderExe, $"\"{_gameLocation}\"");
                        info.EnvironmentVariables.Add("tp_windowed", "1");
                    }
                    else if (_gameProfile.EmulationProfile == EmulationProfile.Vt3Lindbergh &&
                             _gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "1"))
                    {
                        info = new ProcessStartInfo(loaderExe, $"\"{_gameLocation}\"");
                        info.EnvironmentVariables.Add("tp_windowed", "1");
                    }
                    else if (_gameProfile.EmulationProfile == EmulationProfile.SegaRtv &&
                             _gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "1"))
                    {
                        info = new ProcessStartInfo(loaderExe, $"\"{_gameLocation}\"");
                        info.EnvironmentVariables.Add("tp_windowed", "1");
                    }
                    else if(_gameProfile.EmulationProfile == EmulationProfile.AfterBurnerClimax &&
                        _gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "1"))
                    {
                        info = new ProcessStartInfo(loaderExe, $"\"{_gameLocation}\"");
                        info.EnvironmentVariables.Add("tp_windowed", "1");
                    }
                    else if (_gameProfile.EmulationProfile == EmulationProfile.AfterBurnerClimax &&
                        _gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "0"))
                    {
                        info = new ProcessStartInfo(loaderExe, $"\"{_gameLocation}\" -full");
                    }
                    else
                    {
                        info = new ProcessStartInfo(loaderExe, $"\"{_gameLocation}\"");
                    }
                }
                if (_gameProfile.EmulationProfile == EmulationProfile.Outrun2SPX 
                    || _gameProfile.EmulationProfile == EmulationProfile.AfterBurnerClimax 
                    || _gameProfile.EmulationProfile == EmulationProfile.SegaInitialDLindbergh
                    || _gameProfile.EmulationProfile == EmulationProfile.Vt3Lindbergh
                    || _gameProfile.EmulationProfile == EmulationProfile.SegaRtv)
                {
                    if (_gameProfile.EmulationProfile == EmulationProfile.SegaInitialDLindbergh)
                    {
                        info.EnvironmentVariables.Add("TEA_DIR", Path.GetDirectoryName(_gameLocation) + "\\");
                    }
                    info.WorkingDirectory = Path.GetDirectoryName(_gameLocation);
                    info.UseShellExecute = false;
                }
                else
                {
                    info.UseShellExecute = false;
                }
                info.WindowStyle = ProcessWindowStyle.Normal;

                if (InputCode.ButtonMode == EmulationProfile.NamcoMkdx)
                {
                    // TODO: LOL CLEAN UP PLS
                    var isOriginalVersion = true;

                    if(!File.Exists(Path.Combine(Path.GetDirectoryName(_gameLocation), "AMCUS", "AMAuthd.exe")))
                    {
                        isOriginalVersion = false;
                    }

                    if (!File.Exists(Path.Combine(Path.GetDirectoryName(_gameLocation), "AMCUS", "iauthdll.dll")))
                    {
                        isOriginalVersion = false;
                    }

                    if (!isOriginalVersion)
                    {

                        // Write WritableConfig.ini
                        File.WriteAllText(
                            Path.Combine(Path.GetDirectoryName(_gameLocation), "AMCUS", "WritableConfig.ini"),
                            "[RuntimeConfig]\r\nmode=SERVER\r\nnetID=ABGN\r\nserialID=\r\n[MuchaChargeData]\r\ncamode-ch_token_consumed=0\r\ncamode-ch_token_charged=0\r\ncamode-ch_token_unit=0\r\ncamode-ch_token_lower=0\r\ncamode-ch_token_upper=0\r\ncamode-ch_token_month=0\r\n");

                        // Write AMConfig.ini
                        File.WriteAllText(Path.Combine(Path.GetDirectoryName(_gameLocation), "AMCUS", "AMConfig.ini"),
                            "[AMUpdaterConfig] \r\n;; AMUpdater\r\namucfg-title=COCO\r\namucfg-lang=JP\r\namucfg-countdown=5\r\namucfg-h_resol=1360\r\namucfg-v_resol=768\r\namucfg-logfile=amupdater.log\r\namucfg-game_rev=1\r\n\r\n[AMAuthdConfig]\r\n;; AMAuthd\r\namdcfg-authType=ALL.NET\r\namdcfg-sleepTime=50\r\namdcfg-resoNameTimeout=180\r\namdcfg-writableConfig=WritableConfig.ini\r\namdcfg-showConsole=ENABLE\r\namdcfg-logfile=- ;\r\namdcfg-export_log=AmAuthdLog.zip ;\r\n\r\n[AllnetConfig] \r\n;; ALL.Net\r\nallcfg-gameID=SBZB\r\nallcfg-gameVer=1.10\r\n;allcfg-tenpoAddr=;\r\n;allcfg-authServerAddr=;\r\n\r\n[AllnetOptionRevalTime]\r\n;; ALL.Net\r\nallopt-reval_hour=7\r\nallopt-reval_minute=0\r\nallopt-reval_second=0\r\n\r\n[AllnetOptionTimeout]\r\n;; ALL.Net\r\nallopt-timeout_connect=60000  \r\nallopt-timeout_send=60000\r\nallopt-timeout_recv=60000\r\n\r\n[MuchaAppConfig]\r\n;; mucha_app\r\nappcfg-logfile=muchaapp.log;\r\nappcfg-loglevel=INFO ;\r\n\r\n[MuchaSysConfig]\r\n;; MUCHA\r\nsyscfg-daemon_exe=.\\MuchaBin\\muchacd.exe\r\nsyscfg-daemon_pidfile=muchacd.pid ;\r\nsyscfg-daemon_logfile=muchacd.log ;\r\nsyscfg-daemon_loglevel=INFO ;\r\nsyscfg-daemon_listen=tcp:0.0.0.0:8765\r\nsyscfg-client_connect=tcp:127.0.0.1:8765\r\n\r\n[MuchaCAConfig]\r\n;; MUCHA\r\ncacfg-game_cd=MK31 ;\r\ncacfg-game_ver=10.22\r\ncacfg-game_board_type=0\r\ncacfg-game_board_id=PCB\r\ncacfg-auth_server_url=https://127.0.0.1:443/mucha_front/\r\ncacfg-auth_server_sslverify=1\r\ncacfg-auth_server_sslcafile=.\\MuchaBin\\cakey_mk3.pem\r\ncacfg-auth_server_timeout=0\r\ncacfg-interval_ainfo_renew=1800\r\ncacfg-interval_ainfo_retry=60\r\ncacfg-auth_place_id=JPN0128C ;\r\n;cacfg-auth_store_router_ip=\r\n\r\n[MuchaDtConfig]\r\n;; MUCHA\r\ndtcfg-dl_product_id=0x4d4b3331\r\ndtcfg-dl_chunk_size=65536\r\ndtcfg-dl_image_path=//./H:\r\ndtcfg-dl_image_size=0\r\ndtcfg-dl_image_type=RAW\r\ndtcfg-dl_image_crypt_key=0xfedcba9876543210\r\ndtcfg-dl_log_level=INFO ;\r\ndtcfg-dl_lan_crypt_key=0xfedcba9876543210\r\ndtcfg-dl_lan_broadcast_interval=1000\r\ndtcfg-dl_lan_udp_port=9026\r\ndtcfg-dl_lan_bandwidth_limit=0\r\ndtcfg-dl_lan_broadcast_address=0.0.0.0\r\ndtcfg-dl_wan_retry_limit=\r\ndtcfg-dl_wan_retry_interval=\r\ndtcfg-dl_wan_send_timeout=\r\ndtcfg-dl_wan_recv_timeout=\r\ndtcfg-dl_lan_retry_limit=\r\ndtcfg-dl_lan_retry_interval=\r\ndtcfg-dl_lan_send_timeout=\r\ndtcfg-dl_lan_recv_timeout=\r\n\r\n[MuchaDtModeConfig]\r\n;; MUCHA\r\ndtmode-io_dir=.\\ ;\r\ndtmode-io_file=MK3_JP_\r\ndtmode-io_conv=DECEXP\r\ndtmode-io_passphrase=ktinkynhgimbt\r\n");

                        // Register iauthd.dll
                        Register_Dlls(Path.Combine(Path.GetDirectoryName(_gameLocation), "AMCUS", "iauthdll.dll"));

                        // Start AMCUS
                        StartAmcus(loaderExe,
                            $"\"{Path.Combine(Path.GetDirectoryName(_gameLocation), "AMCUS", "AMAuthd.exe")}\"");
                    }
                }

                if(InputCode.ButtonMode == EmulationProfile.SegaInitialD)
                {
                    var newCard = _gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "EnableNewCardCode");
                    if(newCard == null)
                    {
                        StartPicodaemon(loaderExe, $"\"{Path.Combine(Path.GetDirectoryName(_gameLocation), "picodaemon.exe")}");
                    }
                    else if(newCard.FieldValue == "0")
                    {
                        StartPicodaemon(loaderExe, $"\"{Path.Combine(Path.GetDirectoryName(_gameLocation), "picodaemon.exe")}");
                    }
                }

                var process = Process.Start(info);
                if (_parrotData.UseHaptic)
                {
                    if (InputCode.ButtonMode == EmulationProfile.SegaRacingClassic
                    || InputCode.ButtonMode == EmulationProfile.EuropaRSegaRally3
                    || InputCode.ButtonMode == EmulationProfile.EuropaRFordRacing
                    || InputCode.ButtonMode == EmulationProfile.SegaInitialD
                    || InputCode.ButtonMode == EmulationProfile.WackyRaces
                    || InputCode.ButtonMode == EmulationProfile.ChaseHq2
                    || InputCode.ButtonMode == EmulationProfile.NamcoWmmt5
                    || InputCode.ButtonMode == EmulationProfile.Outrun2SPX)
                    {
                        // TODO: NOT TESTED BEFORE COMMIT
                        var t = new Thread(() => FfbHelper.UseForceFeedback(_parrotData, ref _endCheckBox));
                        t.Start();
                    }
                }
                while (!process.HasExited)
                {
                    if(_JvsOverride)
                        Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(this.DoCheckBoxesDude));

                    Thread.Sleep(500);
                }

                _gameRunning = false;
                TerminateThreads();

                Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(this.Close));
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

        private void StartAmcus(string loaderExe, string picodaemonPath)
        {
            ProcessStartInfo info2;
            info2 = new ProcessStartInfo(loaderExe, picodaemonPath);
            Process.Start(info2);
            Thread.Sleep(1000);
        }

        private void StartPicodaemon(string loaderExe, string picodaemonPath)
        {
            ProcessStartInfo info2;
            info2 = new ProcessStartInfo(loaderExe, picodaemonPath);
            Process.Start(info2);
            Thread.Sleep(1000);
        }

        private void DoCheckBoxesDude()
        {
            // TODO: ALWAYS ACTIVE ON DEBUG MODE
            //InputCode.PlayerDigitalButtons[0].Start = P1Start.IsChecked != null && P1Start.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].Start = P2Start.IsChecked != null && P2Start.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].Service = P1Service.IsChecked != null && P1Service.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].Service = P2Service.IsChecked != null && P2Service.IsChecked.Value;

            //InputCode.PlayerDigitalButtons[0].Up = P1Up.IsChecked != null && P1Up.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].Down = P1Down.IsChecked != null && P1Down.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].Left = P1Left.IsChecked != null && P1Left.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].Right = P1Right.IsChecked != null && P1Right.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].Button1 = P1Button1.IsChecked != null && P1Button1.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].Button2 = P1Button2.IsChecked != null && P1Button2.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].Button3 = P1Button3.IsChecked != null && P1Button3.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].Button4 = P1Button4.IsChecked != null && P1Button4.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].Button5 = P1Button5.IsChecked != null && P1Button5.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].Button6 = P1Button6.IsChecked != null && P1Button6.IsChecked.Value;

            //InputCode.PlayerDigitalButtons[1].Up = P2Up.IsChecked != null && P2Up.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].Down = P2Down.IsChecked != null && P2Down.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].Left = P2Left.IsChecked != null && P2Left.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].Right = P2Right.IsChecked != null && P2Right.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].Button1 = P2Button1.IsChecked != null && P2Button1.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].Button2 = P2Button2.IsChecked != null && P2Button2.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].Button3 = P2Button3.IsChecked != null && P2Button3.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].Button4 = P2Button4.IsChecked != null && P2Button4.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].Button5 = P2Button5.IsChecked != null && P2Button5.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].Button6 = P2Button6.IsChecked != null && P2Button6.IsChecked.Value;

            //InputCode.PlayerDigitalButtons[0].ExtensionButton1 = ExtOne1.IsChecked != null && ExtOne1.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton2 = ExtOne2.IsChecked != null && ExtOne2.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton3 = ExtOne3.IsChecked != null && ExtOne3.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton4 = ExtOne4.IsChecked != null && ExtOne4.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton1_1 = ExtOne11.IsChecked != null && ExtOne11.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton1_2 = ExtOne12.IsChecked != null && ExtOne12.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton1_3 = ExtOne13.IsChecked != null && ExtOne13.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton1_4 = ExtOne14.IsChecked != null && ExtOne14.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton1_5 = ExtOne15.IsChecked != null && ExtOne15.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton1_6 = ExtOne16.IsChecked != null && ExtOne16.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton1_7 = ExtOne17.IsChecked != null && ExtOne17.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton1_8 = ExtOne18.IsChecked != null && ExtOne18.IsChecked.Value;

            //InputCode.PlayerDigitalButtons[1].ExtensionButton1 = ExtTwo1.IsChecked != null && ExtTwo1.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton2 = ExtTwo2.IsChecked != null && ExtTwo2.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton3 = ExtTwo3.IsChecked != null && ExtTwo3.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton4 = ExtTwo4.IsChecked != null && ExtTwo4.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton1_1 = ExtTwo11.IsChecked != null && ExtTwo11.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton1_2 = ExtTwo12.IsChecked != null && ExtTwo12.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton1_3 = ExtTwo13.IsChecked != null && ExtTwo13.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton1_4 = ExtTwo14.IsChecked != null && ExtTwo14.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton1_5 = ExtTwo15.IsChecked != null && ExtTwo15.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton1_6 = ExtTwo16.IsChecked != null && ExtTwo16.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton1_7 = ExtTwo17.IsChecked != null && ExtTwo17.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton1_8 = ExtTwo18.IsChecked != null && ExtTwo18.IsChecked.Value;

            //InputCode.PlayerDigitalButtons[0].ExtensionButton2_1 = ExtOne21.IsChecked != null && ExtOne21.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton2_2 = ExtOne22.IsChecked != null && ExtOne22.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton2_3 = ExtOne23.IsChecked != null && ExtOne23.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton2_4 = ExtOne24.IsChecked != null && ExtOne24.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton2_5 = ExtOne25.IsChecked != null && ExtOne25.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton2_6 = ExtOne26.IsChecked != null && ExtOne26.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton2_7 = ExtOne27.IsChecked != null && ExtOne27.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[0].ExtensionButton2_8 = ExtOne28.IsChecked != null && ExtOne28.IsChecked.Value;

            //InputCode.PlayerDigitalButtons[1].ExtensionButton2_1 = ExtTwo21.IsChecked != null && ExtTwo21.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton2_2 = ExtTwo22.IsChecked != null && ExtTwo22.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton2_3 = ExtTwo23.IsChecked != null && ExtTwo23.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton2_4 = ExtTwo24.IsChecked != null && ExtTwo24.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton2_5 = ExtTwo25.IsChecked != null && ExtTwo25.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton2_6 = ExtTwo26.IsChecked != null && ExtTwo26.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton2_7 = ExtTwo27.IsChecked != null && ExtTwo27.IsChecked.Value;
            //InputCode.PlayerDigitalButtons[1].ExtensionButton2_8 = ExtTwo28.IsChecked != null && ExtTwo28.IsChecked.Value;

            //if (NumericAnalog0.Value.HasValue)
            //    InputCode.AnalogBytes[0] = (byte)NumericAnalog0.Value;
            //if (NumericAnalog1.Value.HasValue)
            //    InputCode.AnalogBytes[1] = (byte)NumericAnalog1.Value;
            //if (NumericAnalog2.Value.HasValue)
            //    InputCode.AnalogBytes[2] = (byte)NumericAnalog2.Value;
            //if (NumericAnalog3.Value.HasValue)
            //    InputCode.AnalogBytes[3] = (byte)NumericAnalog3.Value;
            //if (NumericAnalog4.Value.HasValue)
            //    InputCode.AnalogBytes[4] = (byte)NumericAnalog4.Value;
            //if (NumericAnalog5.Value.HasValue)
            //    InputCode.AnalogBytes[5] = (byte)NumericAnalog5.Value;
            //if (NumericAnalog6.Value.HasValue)
            //    InputCode.AnalogBytes[6] = (byte)NumericAnalog6.Value;
            //if (NumericAnalog7.Value.HasValue)
            //    InputCode.AnalogBytes[7] = (byte)NumericAnalog7.Value;
            //if (NumericAnalog8.Value.HasValue)
            //    InputCode.AnalogBytes[8] = (byte)NumericAnalog8.Value;
            //if (NumericAnalog9.Value.HasValue)
            //    InputCode.AnalogBytes[9] = (byte)NumericAnalog9.Value;
            //if (NumericAnalog10.Value.HasValue)
            //    InputCode.AnalogBytes[10] = (byte)NumericAnalog10.Value;
            //if (NumericAnalog11.Value.HasValue)
            //    InputCode.AnalogBytes[11] = (byte)NumericAnalog11.Value;
            //if (NumericAnalog12.Value.HasValue)
            //    InputCode.AnalogBytes[12] = (byte)NumericAnalog12.Value;
            //if (NumericAnalog13.Value.HasValue)
            //    InputCode.AnalogBytes[13] = (byte)NumericAnalog13.Value;
            //if (NumericAnalog14.Value.HasValue)
            //    InputCode.AnalogBytes[14] = (byte)NumericAnalog14.Value;
            //if (NumericAnalog15.Value.HasValue)
            //    InputCode.AnalogBytes[15] = (byte)NumericAnalog15.Value;

            //if (NumericAnalog16.Value.HasValue)
            //    InputCode.AnalogBytes[16] = (byte)NumericAnalog16.Value;
            //if (NumericAnalog17.Value.HasValue)
            //    InputCode.AnalogBytes[17] = (byte)NumericAnalog17.Value;
            //if (NumericAnalog18.Value.HasValue)
            //    InputCode.AnalogBytes[18] = (byte)NumericAnalog18.Value;
            //if (NumericAnalog19.Value.HasValue)
            //    InputCode.AnalogBytes[19] = (byte)NumericAnalog19.Value;
            //if (NumericAnalog20.Value.HasValue)
            //    InputCode.AnalogBytes[20] = (byte)NumericAnalog20.Value;
            
            //InputCode.PlayerDigitalButtons[0].Test = TEST.IsChecked != null && TEST.IsChecked.Value;
        }

        private Thread CreateInputListenerThread(bool useXinput)
        {
            IntPtr hWnd = new WindowInteropHelper(Application.Current.MainWindow).EnsureHandle();
            var inputThread = new Thread(() => _inputListener.Listen(_parrotData.UseSto0ZDrivingHack, _parrotData.StoozPercent, _gameProfile.JoystickButtons, useXinput, _gameProfile, hWnd));
            inputThread.Start();
            return inputThread;
        }

        private void TerminateThreads()
        {
            _rawInputListener?.StopListening();
            _specialControl?.StopListening();
            _pokkenControlSender.StopListening();
            _gtiClub3ControlSender.StopListening();
            _inputListener?.StopListening();
            _serialPortHandler?.StopListening();
            _europa?.StopListening();
            KillGunListener = true;
        }

        /// <summary>
        /// Prevent closing if game is running.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GameRunning_OnClosing(object sender, CancelEventArgs e)
        {
            if (_gameRunning)
                e.Cancel = true;
            _endCheckBox = true;
            TerminateThreads();
            Thread.Sleep(100);
            if (_runEmuOnly)
            {
                Application.Current.Shutdown(0);
            }
        }

        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            _JvsOverride = !_JvsOverride;
        }
    }
}
