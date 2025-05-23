using System;
using System.Linq;
using System.Windows;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;
using TeknoParrotUi.Helpers;
using Linearstar.Windows.RawInput;
using TeknoParrotUi.Common.InputListening;
using TeknoParrotUi.Views.GameRunningCode.ControlHandlers;
using TeknoParrotUi.Views.GameRunningCode.ProcessManagement;
using TeknoParrotUi.Views.GameRunningCode.EmulatorHelpers;
using TeknoParrotUi.Views.GameRunningCode.Utilities;

namespace TeknoParrotUi.Views
{
    public partial class GameRunning
    {
        private readonly bool _isTest;
        private readonly string _gameLocation;
        private readonly string _gameLocation2;
        private readonly SerialPortHandler _serialPortHandler;
        private readonly GameProfile _gameProfile;
        private static bool _runEmuOnly;
        private static Thread _diThread;
        private static ControlSender _controlSender;
        private static readonly InputListener InputListener = new InputListener();
        private static bool _killGunListener;
        private bool _forceQuit;
        private readonly bool _cmdLaunch;
        private static ControlPipe _pipe;
        private Library _library;
        private string loaderExe;
        private string loaderDll;
        private HwndSource _source;
        private InputApi _inputApi = InputApi.DirectInput;
        private bool _twoExes;
        private bool _secondExeFirst;
        private string _secondExeArguments;
        public bool _launchMinimized;
        public bool _launchSecondExecutableMinimized;
        private bool _quitEarly = false;
#if DEBUG
        public DebugJVS jvsDebug;
#endif

        public GameRunning(GameProfile gameProfile, string loaderExe, string loaderDll, bool isTest, bool runEmuOnly = false, bool profileLaunch = false, Library library = null)
        {
            InitializeComponent();
            if (profileLaunch == false && !runEmuOnly)
            {
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = false;
            }

            string inputApiString = gameProfile.ConfigValues.Find(cv => cv.FieldName == "Input API")?.FieldValue;

            if (inputApiString != null)
                _inputApi = (InputApi)Enum.Parse(typeof(InputApi), inputApiString);

            textBoxConsole.Text = "";
            _runEmuOnly = runEmuOnly;
            _gameLocation = "";

            // In --emuonly dev mode path is never set, so we do same as below.
            try
            {
                _gameLocation = Path.GetFullPath(gameProfile.GamePath);
            }
            catch
            {
                _gameLocation = "";
            }

            // not all games have 2 locations, and an mempty string throws an exception in GetFullPath so
            // try-catch it is. <w<
            try
            {
                _gameLocation2 = Path.GetFullPath(gameProfile.GamePath2);
            }
            catch
            {
                _gameLocation2 = "";
            }
            _twoExes = gameProfile.HasTwoExecutables;
            _secondExeFirst = gameProfile.LaunchSecondExecutableFirst;
            _secondExeArguments = gameProfile.SecondExecutableArguments;
            _launchMinimized = gameProfile.LaunchMinimized;
            _launchSecondExecutableMinimized = gameProfile.LaunchSecondExecutableMinimized;
            InputCode.ButtonMode = gameProfile.EmulationProfile;
            _isTest = isTest;
            _gameProfile = gameProfile;
            _serialPortHandler = new SerialPortHandler();
            _cmdLaunch = profileLaunch;

            if (!_isTest)
            {
                if (_gameProfile.EmulationProfile == EmulationProfile.AfterBurnerClimax || _gameProfile.EmulationProfile == EmulationProfile.Outrun2SPX || _gameProfile.EmulationProfile == EmulationProfile.SegaInitialD || _gameProfile.EmulationProfile == EmulationProfile.SegaInitialDLindbergh ||
                    _gameProfile.EmulationProfile == EmulationProfile.SegaJvsLetsGoIsland || _gameProfile.EmulationProfile == EmulationProfile.SegaRTuned || _gameProfile.EmulationProfile == EmulationProfile.SegaRtv || _gameProfile.EmulationProfile == EmulationProfile.SegaSonicAllStarsRacing ||
                    _gameProfile.EmulationProfile == EmulationProfile.Hotd4 || _gameProfile.EmulationProfile == EmulationProfile.VirtuaTennis4 || _gameProfile.EmulationProfile == EmulationProfile.Vt3Lindbergh || _gameProfile.EmulationProfile == EmulationProfile.SegaJvsGoldenGun ||
                    _gameProfile.EmulationProfile == EmulationProfile.Rambo || _gameProfile.EmulationProfile == EmulationProfile.SegaToolsIDZ || _gameProfile.EmulationProfile == EmulationProfile.HummerExtreme || _gameProfile.EmulationProfile == EmulationProfile.GSEVO)
                {
                    if (_inputApi == InputApi.XInput)
                    {
                        if (!InputListenerXInput.DisableTestButton)
                        {
                            InputListenerXInput.DisableTestButton = true;
                        }
                    }
                    else if (_inputApi == InputApi.DirectInput)
                    {
                        if (!InputListenerDirectInput.DisableTestButton)
                        {
                            InputListenerDirectInput.DisableTestButton = true;
                        }
                    }
                    else if (_inputApi == InputApi.RawInput)
                    {
                        if (!InputListenerRawInput.DisableTestButton)
                        {
                            InputListenerRawInput.DisableTestButton = true;
                        }
                    }
                    else if (_inputApi == InputApi.RawInputTrackball)
                    {
                        if (!InputListenerRawInputTrackball.DisableTestButton)
                        {
                            InputListenerRawInputTrackball.DisableTestButton = true;
                        }
                    }
                }
            }
            else
            {
                if (_inputApi == InputApi.XInput)
                {
                    if (InputListenerXInput.DisableTestButton)
                    {
                        InputListenerXInput.DisableTestButton = false;
                    }
                }
                else if (_inputApi == InputApi.DirectInput)
                {
                    if (InputListenerDirectInput.DisableTestButton)
                    {
                        InputListenerDirectInput.DisableTestButton = false;
                    }
                }
                else if (_inputApi == InputApi.RawInput)
                {
                    if (InputListenerRawInput.DisableTestButton)
                    {
                        InputListenerRawInput.DisableTestButton = false;
                    }
                }
                else if (_inputApi == InputApi.RawInputTrackball)
                {
                    if (InputListenerRawInputTrackball.DisableTestButton)
                    {
                        InputListenerRawInputTrackball.DisableTestButton = false;
                    }
                }
            }

            if (runEmuOnly)
            {
                buttonForceQuit.Visibility = Visibility.Collapsed;
            }

            gameName.Text = _gameProfile.GameNameInternal;
            _library = library;
            this.loaderExe = loaderExe;
            this.loaderDll = loaderDll;
#if DEBUG
            jvsDebug = new DebugJVS();
            jvsDebug.Show();
#endif
        }

        private Thread CreateInputListenerThread()
        {
            var hWnd = new WindowInteropHelper(Application.Current.MainWindow ?? throw new InvalidOperationException()).EnsureHandle();
            var inputThread = new Thread(() => InputListener.Listen(Lazydata.ParrotData.UseSto0ZDrivingHack, Lazydata.ParrotData.StoozPercent, _gameProfile.JoystickButtons, _inputApi, _gameProfile));
            inputThread.Start();

            // Hook window proc messages
            if (_inputApi == InputApi.RawInput || _inputApi == InputApi.RawInputTrackball)
            {
                RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse, RawInputDeviceFlags.InputSink, hWnd);
                RawInputDevice.RegisterDevice(HidUsageAndPage.Keyboard, RawInputDeviceFlags.InputSink, hWnd);

                _source = HwndSource.FromHwnd(hWnd);
                _source.AddHook(WndProcHook);
            }

            return inputThread;
        }

        private static IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            InputListener?.WndProcReceived(hwnd, msg, wParam, lParam, ref handled);
            return IntPtr.Zero;
        }

        public void TerminateThreads()
        {
            _controlSender?.Stop();
            InputListener?.StopListening();

            if (_inputApi == InputApi.RawInput || _inputApi == InputApi.RawInputTrackball)
            {
                RawInputDevice.UnregisterDevice(HidUsageAndPage.Mouse);
                RawInputDevice.UnregisterDevice(HidUsageAndPage.Keyboard);
                _source?.RemoveHook(WndProcHook);
            }

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
            if (!_quitEarly)
            {
                if (Lazydata.ParrotData.UseDiscordRPC) DiscordRPC.UpdatePresence(null);
#if DEBUG
                jvsDebug?.Close();
#endif
                TerminateThreads();
                Thread.Sleep(100);
            }
            if (_runEmuOnly)
            {
                MainWindow.SafeExit();
            }
        }
        private void GameRunning_OnLoaded(object sender, RoutedEventArgs e)
        {

            if (_gameProfile.EmulatorType != EmulatorType.OpenParrot)
            {
                if (_gameProfile.EmulationProfile == EmulationProfile.APM3 || _gameProfile.EmulationProfile == EmulationProfile.APM3Direct)
                {
                    var userOnlineId = _gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "APM3ID");
                    if (userOnlineId.FieldValue == "" || userOnlineId.FieldValue.Length != 17)
                    {
                        MessageBoxHelper.ErrorOK(TeknoParrotUi.Properties.Resources.ErrorNoAPM3Id);
                        if (_runEmuOnly || _cmdLaunch)
                        {
                            Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
                        }
                        else if (_forceQuit == false)
                        {
                            textBoxConsole.Dispatcher.Invoke(delegate
                            {
                                gameRunning.Content = Properties.Resources.GameRunningGameStopped;
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
                            textBoxConsole.Dispatcher.Invoke(delegate
                            {
                                gameRunning.Content = Properties.Resources.GameRunningGameStopped;
                                progressBar.IsIndeterminate = false;
                                MessageBoxHelper.WarningOK(Properties.Resources.GameRunningCheckTaskMgr);
                                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                            });
                        }
                        _quitEarly = true;
                        return;
                    }
                }
                if (_gameProfile.EmulationProfile == EmulationProfile.ALLSSCHRONO)
                {
                    var userOnlineId = _gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "OnlineID");
                    if (userOnlineId.FieldValue == "" || userOnlineId.FieldValue.Length != 17)
                    {
                        MessageBoxHelper.ErrorOK(TeknoParrotUi.Properties.Resources.ErrorNoOnlineId);
                        if (_runEmuOnly || _cmdLaunch)
                        {
                            Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
                        }
                        else if (_forceQuit == false)
                        {
                            textBoxConsole.Dispatcher.Invoke(delegate
                            {
                                gameRunning.Content = Properties.Resources.GameRunningGameStopped;
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
                            textBoxConsole.Dispatcher.Invoke(delegate
                            {
                                gameRunning.Content = Properties.Resources.GameRunningGameStopped;
                                progressBar.IsIndeterminate = false;
                                MessageBoxHelper.WarningOK(Properties.Resources.GameRunningCheckTaskMgr);
                                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                            });
                        }
                        _quitEarly = true;
                        return;
                    }
                }
            }

            JvsPackageEmulator.Initialize(_gameProfile);
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
                case EmulationProfile.GunslingerStratos3:
                    if (_pipe == null)
                        _pipe = new FastIOPipe();
                    break;
                case EmulationProfile.ALLS:
                case EmulationProfile.ALLSHOTDSD:
                case EmulationProfile.ALLSFGO:
                    if (_pipe == null)
                        _pipe = new ALLSUsbIoPipe();
                    break;
                case EmulationProfile.ALLSSWDC:
                    if (_pipe == null)
                        _pipe = new SWDCALLSUsbIoPipe();
                    break;
                case EmulationProfile.ALLSSCHRONO:
                    if (_pipe == null)
                        _pipe = new ChronoRegaliaUsbIoPipe();
                    break;
                case EmulationProfile.Theatrhythm:
                    if (_pipe == null)
                        _pipe = new FastIOPipe();
                    break;
                case EmulationProfile.APM3:
                case EmulationProfile.APM3Direct:
                case EmulationProfile.GuiltyGearAPM3:
                    if (_pipe == null)
                        _pipe = new APM3Pipe();
                    break;
                case EmulationProfile.WonderlandWars:
                    if (_pipe == null)
                    {
                        _pipe = new amJvsPipe();
                    }
                    break;
                case EmulationProfile.ALLSIDTA:
                    if (_pipe == null)
                        _pipe = new SWDCALLSUsbIoPipe();
                    break;
                case EmulationProfile.SegaOlympic2020:
                    if (_pipe == null)
                        _pipe = new SWDCALLSUsbIoPipe();
                    break;
#if DEBUG
                case EmulationProfile.Outrun2SPX:
                    if (_pipe == null)
                        _pipe = new amJvsPipe();
                    break;
#endif
            }

            _pipe?.Start(_runEmuOnly);

            var invertButtons = _gameProfile.ConfigValues.Any(x => x.FieldName == "Invert Buttons" && x.FieldValue == "1");
            if (invertButtons)
            {
                JvsPackageEmulator.InvertMaiMaiButtons = true;
            }

            bool flag = InputCode.ButtonMode == EmulationProfile.SegaJvsLetsGoIsland || InputCode.ButtonMode == EmulationProfile.SegaJvsLetsGoJungle || InputCode.ButtonMode == EmulationProfile.LuigisMansion;
            //fills 0, 2, 4, 6
            for (int i = 0; i <= 6; i += 2)
            {
                InputCode.AnalogBytes[i] = flag ? (byte)127 : (byte)0;
            }

            if (InputCode.ButtonMode == EmulationProfile.GunslingerStratos3)
            {
                for (int i = 0; i <= 12; i += 2)
                {
                    InputCode.AnalogBytes[i] = (byte)127;
                }
            }

            bool RealGearShiftID = _gameProfile.ConfigValues.Any(x => x.FieldName == "RealGearshift" && x.FieldValue == "1");
            bool ProMode = _gameProfile.ConfigValues.Any(x => x.FieldName == "Professional Edition Enable" && x.FieldValue == "1");

            switch (InputCode.ButtonMode)
            {
                case EmulationProfile.DeadHeat:
                case EmulationProfile.Nirin:
                    _controlSender = new DeadHeatPipe();
                    break;
                case EmulationProfile.LGS:
                    _controlSender = new LGSPipe();
                    break;
                case EmulationProfile.NamcoPokken:
                    _controlSender = new Pokken();
                    break;
                case EmulationProfile.ExBoard:
                    _controlSender = new ExBoard();
                    break;
                case EmulationProfile.ALLSHOTDSD:
                    _controlSender = new HOTDSDPipe();
                    break;
                case EmulationProfile.ALLSSWDC:
                    _controlSender = new SWDCPipe();
                    break;
                case EmulationProfile.SegaJvsAime:
                case EmulationProfile.IDZ:
                    _controlSender = new AimeButton();
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
                case EmulationProfile.BlazingAngels:
                    _controlSender = new RawThrills(false);
                    break;
                case EmulationProfile.RawThrillsFNFH2O:
                    _controlSender = new RawThrills(true);
                    break;
                case EmulationProfile.LuigisMansion:
                    _controlSender = new LuigisMansion();
                    break;
                case EmulationProfile.LostLandAdventures:
                    _controlSender = new LostLandPipe();
                    break;
                case EmulationProfile.GHA:
                    _controlSender = new GHA();
                    break;
                case EmulationProfile.SegaToolsIDZ:
                    _controlSender = new SegaTools();
                    break;
                case EmulationProfile.TokyoCop:
                case EmulationProfile.RingRiders:
                case EmulationProfile.RadikalBikers:
                    _controlSender = new GaelcoPipe();
                    break;
                case EmulationProfile.StarTrekVoyager:
                    _controlSender = new StarTrekVoyagerPipe();
                    break;
                case EmulationProfile.SegaInitialD:
                case EmulationProfile.SegaInitialDLindbergh:
                    if (RealGearShiftID)
                        _controlSender = new SegaInitialDPipe();
                    break;
                case EmulationProfile.AliensExtermination:
                    _controlSender = new AliensExterminationPipe();
                    break;
                case EmulationProfile.Contra:
                    _controlSender = new ContraPipe();
                    break;
                case EmulationProfile.MarioBros:
                    _controlSender = new MarioBrosPipe();
                    break;
                case EmulationProfile.NamcoMkdx:
                    _controlSender = new BanapassButton();
                    break;
                case EmulationProfile.FarCry:
                    _controlSender = new FarCryPipe();
                    break;
                case EmulationProfile.SilentHill:
                    _controlSender = new SilentHillPipe();
                    break;
                case EmulationProfile.Taiko:
                    _controlSender = new TaikoPipe();
                    break;
                case EmulationProfile.WartranTroopers:
                    _controlSender = new WartranTroopersPipe();
                    break;
                case EmulationProfile.HotWheels:
                    _controlSender = new HotWheelsPipe();
                    break;
                case EmulationProfile.InfinityBlade:
                case EmulationProfile.TimeCrisis5:
                    _controlSender = new TC5Pipe();
                    break;
                case EmulationProfile.FrenzyExpress:
                    _controlSender = new FrenzyExpressPipe();
                    break;
                case EmulationProfile.AAA:
                    _controlSender = new AAAPipe();
                    break;
                case EmulationProfile.EuropaRSegaRally3:
                    _controlSender = new SegaRallyCoinPipe();
                    break;
                case EmulationProfile.RawThrillsGUN:
                    _controlSender = new RawThrillsGUN();
                    break;
                case EmulationProfile.DealorNoDeal:
                    _controlSender = new DealOrNoDealPipe();
                    break;
                case EmulationProfile.TMNT:
                    _controlSender = new TMNTPipe();
                    break;
                case EmulationProfile.EADP:
                    _controlSender = new EADPPipe();
                    break;
                case EmulationProfile.MusicGunGun2:
                case EmulationProfile.GaiaAttack4:
                case EmulationProfile.HauntedMuseum:
                case EmulationProfile.HauntedMuseum2:
                    _controlSender = new MusicGunGun2Pipe();
                    break;
                case EmulationProfile.PointBlankX:
                    _controlSender = new PointBlankPipe();
                    break;
                case EmulationProfile.TheAct:
                    _controlSender = new TheActPipe();
                    break;
                case EmulationProfile.SAO:
                case EmulationProfile.JojoLastSurvivor:
                case EmulationProfile.GundamKizuna2:
                    _controlSender = new BnusioPipe();
                    break;
                case EmulationProfile.EXVS2:
                case EmulationProfile.EXVS2XB:
                    _controlSender = new BanapassButtonEXVS2();
                    break;
                case EmulationProfile.WinningEleven:
                    _controlSender = new WinningElevenPipe();
                    break;
                case EmulationProfile.WonderlandWars:
                    _controlSender = new WonderlandWarsPipe();
                    break;
                case EmulationProfile.Friction:
                    _controlSender = new FrictionPipe();
                    break;
                case EmulationProfile.Castlevania:
                    _controlSender = new CastlevaniaPipe();
                    break;
                case EmulationProfile.SavageQuest:
                    _controlSender = new SavageQuestPipe();
                    break;
                case EmulationProfile.NxL2:
                    _controlSender = new NxL2Pipe();
                    break;
                case EmulationProfile.FastIo:
                case EmulationProfile.GunslingerStratos3:
                    _controlSender = new NesicaButton();
                    break;
                case EmulationProfile.BorderBreak:
                case EmulationProfile.ALLSSCHRONO:
                case EmulationProfile.ALLSIDTA:
                    _controlSender = new AimeButton();
                    break;
                case EmulationProfile.DenshaDeGo:
                    _controlSender = new NxL2Pipe();
                    break;
                case EmulationProfile.TransformersShadowsRising:
                    _controlSender = new TransformersShadowsRisingPipe();
                    break;
                case EmulationProfile.IncredibleTechnologies:
                    _controlSender = new IncredibleTechnologiesPipe();
                    break;
                case EmulationProfile.GenericTrackball:
                    _controlSender = new GenericTrackballPipe();
                    break;
                case EmulationProfile.NamcoWmmt6RR:
                    _controlSender = new BanapassButtonEXVS2();
                    break;
            }

            _controlSender?.Start();

            if (InputCode.ButtonMode == EmulationProfile.Rambo)
            {
                _killGunListener = false;
                new Thread(GameRunningCode.ControlHandlers.GunControlHandler.HandleRamboControls).Start();
            }

            if (InputCode.ButtonMode == EmulationProfile.GSEVO)
            {
                _killGunListener = false;
                new Thread(GameRunningCode.ControlHandlers.GunControlHandler.HandleGSEvoReload).Start();
            }

            if (InputCode.ButtonMode == EmulationProfile.SegaOlympic2016)
            {
                _killGunListener = false;
                new Thread(GameRunningCode.ControlHandlers.OlympicControlHandler.HandleOlympicControls).Start();
            }

            if (InputCode.ButtonMode == EmulationProfile.SegaOlympic2020)
            {
                _killGunListener = false;
                new Thread(GameRunningCode.ControlHandlers.OlympicControlHandler.Handle2020OlympicControls).Start();
            }

            if (!_runEmuOnly)
            {
                GameRunningCode.Utilities.ConfigurationWriter config =
                    new ConfigurationWriter(_gameProfile, _gameLocation, _gameLocation2, _twoExes);
                config.WriteConfigIni();
            }

            if (InputCode.ButtonMode != EmulationProfile.EuropaRFordRacing &&
                InputCode.ButtonMode != EmulationProfile.EuropaRSegaRally3 &&
                InputCode.ButtonMode != EmulationProfile.Theatrhythm &&
                InputCode.ButtonMode != EmulationProfile.FastIo &&
                InputCode.ButtonMode != EmulationProfile.GunslingerStratos3)
            {
                //bool DualJvsEmulation = _gameProfile.ConfigValues.Any(x => x.FieldName == "DualJvsEmulation" && x.FieldValue == "1");

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
                        if (ProMode)
                        {
                            // TODO: Can we remove ProMode and just use DualJvsEmulation to identify pro mode?
                            JvsPackageEmulator.DualJvsEmulation = true;
                            JvsPackageEmulator.ProMode = true;
                        }
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
                    case EmulationProfile.NamcoWmmt6RR:
                    case EmulationProfile.NamcoMkdx:
                    case EmulationProfile.NamcoMkdxUsa:
                    case EmulationProfile.DeadHeatRiders:
                    case EmulationProfile.NamcoGundamPod:
                    case EmulationProfile.EXVS2:
                    case EmulationProfile.EXVS2XB:
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
                        JvsPackageEmulator.DualJvsEmulation = true;
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
                    case EmulationProfile.Hotd4:
                        JvsPackageEmulator.Hotd4 = true;
                        break;
                    case EmulationProfile.Xiyangyang:
                        JvsPackageEmulator.JvsCommVersion = 0x10;
                        JvsPackageEmulator.JvsVersion = 0x30;
                        JvsPackageEmulator.JvsCommandRevision = 0x13;
                        JvsPackageEmulator.JvsIdentifier = JVSIdentifiers.SegaXiyangyang;
                        JvsPackageEmulator.Xiyangyang = true;
                        JvsPackageEmulator.JvsSwitchCount = 0x16;
                        break;
                }

                _serialPortHandler.StopListening();
                Thread.Sleep(1000);
                new Thread(() => _serialPortHandler.ListenPipe("TeknoParrot_JVS")).Start();
                new Thread(_serialPortHandler.ProcessQueue).Start();
            }

            _diThread?.Abort(0);
            _diThread = CreateInputListenerThread();

            if (Lazydata.ParrotData.UseDiscordRPC)
            {
                DiscordRPC.UpdatePresence(new DiscordRPC.RichPresence
                {
                    details = _gameProfile.GameNameInternal,
                    largeImageKey = _gameProfile.GameNameInternal.Replace(" ", "").ToLower(),
                    //https://stackoverflow.com/a/17632585
                    startTimestamp = (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds
                });
            }

            // Wait before launching second thread.
            if (!_runEmuOnly)
            {
                if (!Lazydata.ParrotData.DisableAnalytics)
                    Task.Run(() => Analytics.SendLaunchData(_gameProfile.ProfileName, _gameProfile.EmulatorType));
                Thread.Sleep(1000);
                // Send analytics
                var processManager = new GameProcessManager(this, _gameProfile, _gameLocation, _gameLocation2,
                    _twoExes, _secondExeFirst, _secondExeArguments, _isTest, ref _forceQuit, _library);
                processManager.CreateGameProcess(loaderExe, loaderDll, textBoxConsole, _runEmuOnly, _cmdLaunch);
                ;
            }
            else
            {
#if DEBUG
                if (jvsDebug != null)
                {
                    jvsDebug.StartDebugInputThread();
                }
#endif
            }
        }
    }
}
