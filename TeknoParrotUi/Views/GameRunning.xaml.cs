using MahApps.Metro.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;
using TeknoParrotUi.Helpers;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for GameRunningUC.xaml
    /// </summary>
    public partial class GameRunning
    {
        private readonly bool _isTest;
        private string _gameLocation;
        private readonly SerialPortHandler _serialPortHandler;
        private readonly GameProfile _gameProfile;
        private static bool _runEmuOnly;
        private static Thread _diThread;
        private static ControlSender _controlSender;
        private static RawInputListener _rawInputListener = new RawInputListener();
        private static readonly InputListener InputListener = new InputListener();
        private static bool _killGunListener;
        private readonly byte _player1GunMultiplier = 1;
        private readonly byte _player2GunMultiplier = 1;
        private bool _forceQuit;
        private readonly bool _cmdLaunch;
        private static ControlPipe _pipe;
        private Library _library;
        private string loaderExe;
        private string loaderDll;
#if DEBUG
        DebugJVS jvsDebug;
#endif

        public GameRunning(GameProfile gameProfile, string loaderExe, string loaderDll, bool isTest, bool runEmuOnly = false, bool profileLaunch = false, Library library = null)
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
            _cmdLaunch = profileLaunch;
            if (Lazydata.ParrotData?.GunSensitivityPlayer1 > 10)
                _player1GunMultiplier = 10;
            else if (Lazydata.ParrotData?.GunSensitivityPlayer1 <= 0)
                _player1GunMultiplier = 1;
            else
            {
                if (Lazydata.ParrotData?.GunSensitivityPlayer1 != null)
                    _player1GunMultiplier = (byte)Lazydata.ParrotData?.GunSensitivityPlayer1;
            }

            if (Lazydata.ParrotData?.GunSensitivityPlayer2 > 10)
                _player2GunMultiplier = 10;
            else if (Lazydata.ParrotData?.GunSensitivityPlayer2 <= 0)
                _player2GunMultiplier = 1;
            else
            {
                if (Lazydata.ParrotData?.GunSensitivityPlayer2 != null)
                    _player2GunMultiplier = (byte)Lazydata.ParrotData?.GunSensitivityPlayer2;
            }

            if (runEmuOnly)
            {
                buttonForceQuit.Visibility = Visibility.Collapsed;
            }

            gameName.Text = _gameProfile.GameName;
            _library = library;
            this.loaderExe = loaderExe;
            this.loaderDll = loaderDll;
#if DEBUG
            jvsDebug = new DebugJVS();
            jvsDebug.Show();
#endif
        }

        /// <summary>
        /// Handles gun game controls.
        /// </summary>
        private void HandleGunControls2Spicy()
        {
            bool useMouseForGun =
                _gameProfile.ConfigValues.Any(x => x.FieldName == "UseMouseForGun" && x.FieldValue == "1");
            while (true)
            {
                if (_killGunListener)
                    return;

                if (!useMouseForGun)
                {
                    if (InputCode.PlayerDigitalButtons[1].UpPressed())
                    {
                        if (InputCode.AnalogBytes[0] <= 0xE0)
                            InputCode.AnalogBytes[0] += _player1GunMultiplier;
                    }

                    if (InputCode.PlayerDigitalButtons[1].DownPressed())
                    {
                        if (InputCode.AnalogBytes[0] >= 10)
                            InputCode.AnalogBytes[0] -= _player1GunMultiplier;
                    }

                    if (InputCode.PlayerDigitalButtons[1].RightPressed())
                    {
                        if (InputCode.AnalogBytes[2] >= 10)
                            InputCode.AnalogBytes[2] -= _player1GunMultiplier;
                    }

                    if (InputCode.PlayerDigitalButtons[1].LeftPressed())
                    {
                        if (InputCode.AnalogBytes[2] <= 0xE0)
                            InputCode.AnalogBytes[2] += _player1GunMultiplier;
                    }
                }

                Thread.Sleep(10);
            }
        }

        // TODO: These should be moved to own class
        private void HandleLuigiMansion(bool useMouseForGun)
        {
            if (!useMouseForGun)
            {
                if (InputCode.PlayerDigitalButtons[0].DownPressed())
                {
                    if (InputCode.AnalogBytes[0] < 0xFE)
                        InputCode.AnalogBytes[0] += _player1GunMultiplier;
                }

                if (InputCode.PlayerDigitalButtons[0].UpPressed())
                {
                    if (InputCode.AnalogBytes[0] > 1)
                        InputCode.AnalogBytes[0] -= _player1GunMultiplier;
                }

                if (InputCode.PlayerDigitalButtons[0].LeftPressed())
                {
                    if (InputCode.AnalogBytes[2] > 1)
                        InputCode.AnalogBytes[2] -= _player1GunMultiplier;
                }

                if (InputCode.PlayerDigitalButtons[0].RightPressed())
                {
                    if (InputCode.AnalogBytes[2] < 0xFE)
                        InputCode.AnalogBytes[2] += _player1GunMultiplier;
                }
            }

            if (InputCode.PlayerDigitalButtons[1].DownPressed())
            {
                if (InputCode.AnalogBytes[4] < 0xFE)
                    InputCode.AnalogBytes[4] += _player2GunMultiplier;
            }

            if (InputCode.PlayerDigitalButtons[1].UpPressed())
            {
                if (InputCode.AnalogBytes[4] > 1)
                    InputCode.AnalogBytes[4] -= _player2GunMultiplier;
            }

            if (InputCode.PlayerDigitalButtons[1].LeftPressed())
            {
                if (InputCode.AnalogBytes[6] > 1)
                    InputCode.AnalogBytes[6] -= _player2GunMultiplier;
            }

            if (InputCode.PlayerDigitalButtons[1].RightPressed())
            {
                if (InputCode.AnalogBytes[6] < 0xFE)
                    InputCode.AnalogBytes[6] += _player2GunMultiplier;
            }
            Thread.Sleep(10);
        }

        /// <summary>
        /// Handles gun game controls.
        /// </summary>
        private void HandleGunControls()
        {
            bool useMouseForGun =
                _gameProfile.ConfigValues.Any(x => x.FieldName == "UseMouseForGun" && x.FieldValue == "1");
            while (true)
            {
                if (_killGunListener)
                    return;

                if (_gameProfile.EmulationProfile == EmulationProfile.Rambo)
                {
                    HandleRamboGuns(useMouseForGun);
                    continue;
                }

                if (_gameProfile.EmulationProfile == EmulationProfile.GSEVO)
                {
                    HandleGSEvoGuns(useMouseForGun);
                    continue;
                }

                if (_gameProfile.EmulationProfile == EmulationProfile.LuigisMansion || _gameProfile.EmulationProfile == EmulationProfile.LostLandAdventuresPAL)
                {
                    HandleLuigiMansion(useMouseForGun);
                    continue;
                }

                if (!_gameProfile.InvertedMouseAxis)
                {
                    if (!useMouseForGun)
                    {
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
                }
                else
                {
                    if (!useMouseForGun)
                    {
                        if (InputCode.PlayerDigitalButtons[0].DownPressed())
                        {
                            if (InputCode.AnalogBytes[0] <= 0xE0)
                                InputCode.AnalogBytes[0] += _player1GunMultiplier;
                        }

                        if (InputCode.PlayerDigitalButtons[0].UpPressed())
                        {
                            if (InputCode.AnalogBytes[0] >= 10)
                                InputCode.AnalogBytes[0] -= _player1GunMultiplier;
                        }

                        if (InputCode.PlayerDigitalButtons[0].LeftPressed())
                        {
                            if (InputCode.AnalogBytes[2] >= 10)
                                InputCode.AnalogBytes[2] -= _player1GunMultiplier;
                        }

                        if (InputCode.PlayerDigitalButtons[0].RightPressed())
                        {
                            if (InputCode.AnalogBytes[2] <= 0xE0)
                                InputCode.AnalogBytes[2] += _player1GunMultiplier;
                        }
                    }

                    if (InputCode.PlayerDigitalButtons[1].DownPressed())
                    {
                        if (InputCode.AnalogBytes[4] <= 0xE0)
                            InputCode.AnalogBytes[4] += _player2GunMultiplier;
                    }

                    if (InputCode.PlayerDigitalButtons[1].UpPressed())
                    {
                        if (InputCode.AnalogBytes[4] >= 10)
                            InputCode.AnalogBytes[4] -= _player2GunMultiplier;
                    }

                    if (InputCode.PlayerDigitalButtons[1].LeftPressed())
                    {
                        if (InputCode.AnalogBytes[6] >= 10)
                            InputCode.AnalogBytes[6] -= _player2GunMultiplier;
                    }

                    if (InputCode.PlayerDigitalButtons[1].RightPressed())
                    {
                        if (InputCode.AnalogBytes[6] <= 0xE0)
                            InputCode.AnalogBytes[6] += _player2GunMultiplier;
                    }
                }

                Thread.Sleep(10);
            }
        }

        private bool reloaded1 = false;
        private bool reloaded2 = false;

        private void HandleRamboGuns(bool useMouseForGun)
        {
            if (!useMouseForGun)
            {
                if (InputCode.PlayerDigitalButtons[0].Button2.HasValue &&
                    InputCode.PlayerDigitalButtons[0].Button2.Value)
                {
                    // Reload
                    InputCode.AnalogBytes[0] = 0x80;
                    if (!reloaded1)
                        InputCode.AnalogBytes[2] = 0xFF;
                    else
                        InputCode.AnalogBytes[2] = 0xF0;
                    reloaded1 = !reloaded1;
                }
                else
                {
                    if (InputCode.PlayerDigitalButtons[0].UpPressed())
                    {
                        if (InputCode.AnalogBytes[0] <= 0xF0)
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
                        if (InputCode.AnalogBytes[2] <= 0xF0)
                            InputCode.AnalogBytes[2] += _player1GunMultiplier;
                    }
                }
            }

            if (InputCode.PlayerDigitalButtons[1].Button2.HasValue &&
                InputCode.PlayerDigitalButtons[1].Button2.Value)
            {
                InputCode.AnalogBytes[4] = 0x80;
                if (!reloaded2)
                    InputCode.AnalogBytes[6] = 0xFF;
                else
                    InputCode.AnalogBytes[6] = 0xF0;
                reloaded2 = !reloaded2;
            }
            else
            {
                // Reload
                if (InputCode.PlayerDigitalButtons[1].UpPressed())
                {
                    if (InputCode.AnalogBytes[4] <= 0xF0)
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
                    if (InputCode.AnalogBytes[6] <= 0xF0)
                        InputCode.AnalogBytes[6] += _player2GunMultiplier;
                }
            }

            Thread.Sleep(10);
        }

        private void HandleGSEvoGuns(bool useMouseForGun)
        {
            if (!useMouseForGun)
            {

                if (InputCode.PlayerDigitalButtons[0].UpPressed())
                {
                    if (InputCode.AnalogBytes[0] <= 0xF0)
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
                    if (InputCode.AnalogBytes[2] <= 0xF0)
                        InputCode.AnalogBytes[2] += _player1GunMultiplier;
                }
            }

            // Reload
            if (InputCode.PlayerDigitalButtons[1].UpPressed())
            {
                if (InputCode.AnalogBytes[4] <= 0xF0)
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
                if (InputCode.AnalogBytes[6] <= 0xF0)
                    InputCode.AnalogBytes[6] += _player2GunMultiplier;
            }

            Thread.Sleep(10);
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
                case EmulationProfile.Theatrhythm:
                    if (_pipe == null)
                        _pipe = new FastIOPipe();
                    break;
            }

            _pipe?.Start(_runEmuOnly);

            var invertButtons =
                _gameProfile.ConfigValues.Any(x => x.FieldName == "Invert Buttons" && x.FieldValue == "1");
            if (invertButtons)
            {
                JvsPackageEmulator.InvertMaiMaiButtons = true;
            }

            if (_rawInputListener == null)
                _rawInputListener = new RawInputListener();

            bool flag = InputCode.ButtonMode == EmulationProfile.SegaJvsLetsGoIsland || InputCode.ButtonMode == EmulationProfile.SegaJvsLetsGoJungle || InputCode.ButtonMode == EmulationProfile.LuigisMansion;
            //fills 0, 2, 4, 6
            for (int i = 0; i <= 6; i += 2)
            {
                InputCode.AnalogBytes[i] = flag ? (byte)127 : (byte)0;
            }

            bool useMouseForGun =
                _gameProfile.ConfigValues.Any(x => x.FieldName == "UseMouseForGun" && x.FieldValue == "1");

            if (useMouseForGun && _gameProfile.GunGame)
                _rawInputListener.ListenToDevice(_gameProfile.InvertedMouseAxis, _gameProfile);

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
                case EmulationProfile.RawThrillsFNF:
                    _controlSender = new RawThrills(false);
                    break;
                case EmulationProfile.RawThrillsFNFH2O:
                    _controlSender = new RawThrills(true);
                    break;
                case EmulationProfile.LuigisMansion:
                    _controlSender = new LuigisMansion();
                    break;
                case EmulationProfile.LostLandAdventuresPAL:
                    _controlSender = new LostLandPipe();
                    break;
                case EmulationProfile.GHA:
                    _controlSender = new GHA();
                    break;
                case EmulationProfile.SpiceToolsKonami:
                    _controlSender = new SpiceTools();
                    break;
            }

            _controlSender?.Start();

            if (_gameProfile.GunGame)
            {
                _killGunListener = false;
                if (_gameProfile.EmulationProfile == EmulationProfile.TooSpicy)
                {
                    new Thread(HandleGunControls2Spicy).Start();
                }
                else
                {
                    new Thread(HandleGunControls).Start();
                }
            }

            if (!_runEmuOnly)
                WriteConfigIni();

            if (InputCode.ButtonMode != EmulationProfile.EuropaRFordRacing &&
                InputCode.ButtonMode != EmulationProfile.EuropaRSegaRally3 &&
                InputCode.ButtonMode != EmulationProfile.Theatrhythm &&
                InputCode.ButtonMode != EmulationProfile.FastIo)
            {
                bool DualJvsEmulation = _gameProfile.ConfigValues.Any(x => x.FieldName == "DualJvsEmulation" && x.FieldValue == "1");

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
                        JvsPackageEmulator.DualJvsEmulation = DualJvsEmulation;
                        break;
                    case EmulationProfile.ArcadeLove:
                        JvsPackageEmulator.DualJvsEmulation = true;
                        break;
                    case EmulationProfile.LGS:
                        JvsPackageEmulator.JvsCommVersion = 0x30;
                        JvsPackageEmulator.JvsVersion = 0x30;
                        JvsPackageEmulator.JvsCommandRevision = 0x30;
                        JvsPackageEmulator.JvsIdentifier = JVSIdentifiers.SegaLetsGoSafari;
                        JvsPackageEmulator.LetsGoSafari = true;
                        JvsPackageEmulator.JvsSwitchCount = 0x16;
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
                    startTimestamp = (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds
                });

            // Wait before launching second thread.
            if (!_runEmuOnly)
            {
                Thread.Sleep(1000);
                CreateGameProcess();
            }
            else
            {
#if DEBUG
                if (jvsDebug != null)
                {
                    new Thread(() =>
                    {
                        while (true)
                        {
                            if (jvsDebug.JvsOverride)
                                Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(jvsDebug.DoCheckBoxesDude));
                        }
                    }).Start();
                }
#endif
            }
        }

        private void CreateGameProcess()
        {
            var gameThread = new Thread(() =>
            {
                var windowed = _gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "1");
                var fullscreen = _gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "0");
                var width = _gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "ResolutionWidth");
                var height = _gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "ResolutionHeight");

                var extra = string.Empty;

                var custom = string.Empty;
                if (!string.IsNullOrEmpty(_gameProfile.CustomArguments))
                {
                    custom = _gameProfile.CustomArguments;
                }

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
                        extra = $"\"-SEEKFREELOADINGPCCONSOLE -LANGUAGE={(englishHack ? "ENG" : "JPN")} -NOHOMEDIR -NOSPLASH -NOWRITE -VSYNC -APM -PCTOC -AUTH \"";
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
                        case EmulatorType.SpiceTools:
                            // Copy SpiceTools to game folder
                            var spice_path = Path.Combine(Path.GetDirectoryName(_gameProfile.GamePath), Path.GetFileName(loaderExe));
                            if (File.Exists(spice_path))
                                File.Delete(spice_path);

                            File.Copy(loaderExe, spice_path);

                            loaderDll += ".dll";
                            // Copy OpenParrot to game folder
                            var openparrot_path = Path.Combine(Path.GetDirectoryName(_gameProfile.GamePath), Path.GetFileName(loaderDll));
                            if (File.Exists(openparrot_path))
                                File.Delete(openparrot_path);

                            File.Copy(loaderDll, openparrot_path);

                            // TODO: many command line options, such as -ea.
                            extra = $"-k {(Path.GetFileName(loaderDll))} -cfgpath spicetools.xml -overlaydisable -nolegacy {(fullscreen ? "-w" : string.Empty)}";

                            loaderExe = spice_path;
                            // let SpiceTools detect game.
                            _gameLocation = string.Empty; // Path.GetFileName(_gameProfile.GamePath);
                            loaderDll = string.Empty;
                            break;
                    }

                    gameArguments = $"\"{_gameLocation}\" {extra} {custom}";
                }

                if (_gameProfile.ResetHint)
                {
                    var hintPath = Path.Combine(Path.GetDirectoryName(_gameProfile.GamePath), "hints.dat");
                    if (File.Exists(hintPath))
                    {
                        File.Delete(hintPath);
                    }
                }

                var info = new ProcessStartInfo(loaderExe, $"{loaderDll} {gameArguments}")
                {
                    UseShellExecute = false
                };

                if (_gameProfile.EmulatorType == EmulatorType.SpiceTools)
                {
                    // info.UseShellExecute = true;
                    info.WorkingDirectory = Path.GetDirectoryName(_gameProfile.GamePath);
                }

                if (_gameProfile.msysType > 0)
                {
                    info.EnvironmentVariables.Add("tp_msysType", _gameProfile.msysType.ToString());
                }

                if (_gameProfile.EmulatorType == EmulatorType.N2)
                {
                    info.WorkingDirectory =
                        Path.GetDirectoryName(_gameLocation) ?? throw new InvalidOperationException();
                    info.EnvironmentVariables.Add("tp_windowed", windowed ? "1" : "0");
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
                        || _gameProfile.EmulationProfile == EmulationProfile.GSEVO)
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

                    info.WorkingDirectory =
                        Path.GetDirectoryName(_gameLocation) ?? throw new InvalidOperationException();
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

                if (_gameProfile.GameName.StartsWith("Tekken 7"))
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

                if (InputCode.ButtonMode == EmulationProfile.SegaInitialD)
                {
                    var newCard = _gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "EnableNewCardCode");
                    if (newCard == null || newCard.FieldValue == "0")
                    {
                        RunAndWait(loaderExe,
                            $"{loaderDll} \"{Path.Combine(Path.GetDirectoryName(_gameLocation), "picodaemon.exe")}");
                    }
                }

                try
                {
                    var cmdProcess = new Process
                    {
                        StartInfo = info
                    };

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
                        if (jvsDebug != null)
                        {
                            if (jvsDebug.JvsOverride)
                                Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(jvsDebug.DoCheckBoxesDude));
                        }
#endif
                        if (_forceQuit)
                        {
                            cmdProcess.Kill();
                        }

                        Thread.Sleep(500);
                    }
                }
                catch (Exception e)
                {
                    textBoxConsole.Invoke(delegate
                    {
                        gameRunning.Text = Properties.Resources.GameRunningGameStopped;
                        progressBar.IsIndeterminate = false;
                        MessageBoxHelper.ErrorOK($"Filename: {info.FileName}\nArguments: {info.Arguments}\nWorking directory: {info.WorkingDirectory}\nError: {e.Message}");
                        Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                    });
                }

                TerminateThreads();
                if (_runEmuOnly || _cmdLaunch)
                {
                    Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
                }
                else if (_forceQuit == false)
                {
                    textBoxConsole.Invoke(delegate
                    {
                        gameRunning.Text = Properties.Resources.GameRunningGameStopped;
                        progressBar.IsIndeterminate = false;
                        Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                    });
                    Application.Current.Dispatcher.Invoke(delegate
                        {
                            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = _library;
                        });
                }
                else
                {
                    textBoxConsole.Invoke(delegate
                    {
                        gameRunning.Text = Properties.Resources.GameRunningGameStopped;
                        progressBar.IsIndeterminate = false;
                        MessageBoxHelper.WarningOK(Properties.Resources.GameRunningCheckTaskMgr);
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
                MessageBoxHelper.ErrorOK(ex.ToString());
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

        private void ButtonForceQuit_Click(object sender, RoutedEventArgs e)
        {
            _forceQuit = true;
        }

        public void GameRunning_OnUnloaded(object sender, RoutedEventArgs e)
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
