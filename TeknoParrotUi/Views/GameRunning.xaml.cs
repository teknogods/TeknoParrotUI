using System;
using System.Linq;
using System.Windows;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;
using TeknoParrotUi.Helpers;
using Linearstar.Windows.RawInput;
using TeknoParrotUi.Common.InputListening;
using System.Management;
using Microsoft.Win32;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for GameRunningUC.xaml
    /// </summary>
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
        const int killIDZ_ID = 1;
        private HwndSource _source;
        private InputApi _inputApi = InputApi.DirectInput;
        private bool _twoExes;
        private bool _secondExeFirst;
        private string _secondExeArguments;
        private bool _quitEarly = false;
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

            string inputApiString = gameProfile.ConfigValues.Find(cv => cv.FieldName == "Input API")?.FieldValue;

            if (inputApiString != null)
                _inputApi = (InputApi)Enum.Parse(typeof(InputApi), inputApiString);

            textBoxConsole.Text = "";
            _runEmuOnly = runEmuOnly;
            _gameLocation = gameProfile.GamePath;
            _gameLocation2 = gameProfile.GamePath2;
            _twoExes = gameProfile.HasTwoExecutables;
            _secondExeFirst = gameProfile.LaunchSecondExecutableFirst;
            _secondExeArguments = gameProfile.SecondExecutableArguments;
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
                    _gameProfile.EmulationProfile == EmulationProfile.Rambo || _gameProfile.EmulationProfile == EmulationProfile.SegaToolsIDZ || _gameProfile.EmulationProfile == EmulationProfile.HummerExtreme)
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

        private void SetDPIAwareRegistryValue(string exePath)
        {
            // Set DPI aware compatibility flag via registry to avoid awkward scaling on 4k displays or laptops etc
            string registryKeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
            string appendString = "HIGHDPIAWARE";

            if (Registry.GetValue(registryKeyPath, exePath, null) == null)
            {
                Registry.SetValue(registryKeyPath, exePath, appendString);
            }
            else
            {
                string existingValue = Registry.GetValue(registryKeyPath, exePath, "").ToString();
                if (!existingValue.Contains(appendString))
                {
                    existingValue += " " + appendString;
                    Registry.SetValue(registryKeyPath, exePath, existingValue);
                }
            }
        }

        private bool reloaded1 = false;
        private bool reloaded2 = false;

        /// <summary>
        /// Handles gun game controls.
        /// </summary>
        private void HandleRamboControls()
        {
            while (true)
            {
                if (_killGunListener)
                    return;

                if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                {
                    // Reload
                    InputCode.AnalogBytes[0] = 0x80;
                    if (!reloaded1)
                        InputCode.AnalogBytes[2] = 0xFF;
                    else
                        InputCode.AnalogBytes[2] = 0xF0;
                    reloaded1 = !reloaded1;
                }

                if (InputCode.PlayerDigitalButtons[1].Button2.HasValue && InputCode.PlayerDigitalButtons[1].Button2.Value)
                {
                    InputCode.AnalogBytes[4] = 0x80;
                    if (!reloaded2)
                        InputCode.AnalogBytes[6] = 0xFF;
                    else
                        InputCode.AnalogBytes[6] = 0xF0;
                    reloaded2 = !reloaded2;
                }

                Thread.Sleep(10);
            }
        }

        /// <summary>
        /// Handles gun game controls.
        /// </summary>
        private void HandleOlympicControls()
        {
            while (true)
            {
                if (_killGunListener)
                    return;

                // Handle jump sensors
                if (InputCode.PlayerDigitalButtons[0].Button6.HasValue && InputCode.PlayerDigitalButtons[0].Button6.Value)
                {
                    InputCode.PlayerDigitalButtons[1].Button6 = true;
                    InputCode.PlayerDigitalButtons[0].ExtensionButton3 = true;
                    InputCode.PlayerDigitalButtons[0].ExtensionButton4 = true;
                    InputCode.PlayerDigitalButtons[1].ExtensionButton3 = true;
                    InputCode.PlayerDigitalButtons[1].ExtensionButton4 = true;
                }
                else
                {
                    InputCode.PlayerDigitalButtons[1].Button6 = false;
                    InputCode.PlayerDigitalButtons[0].ExtensionButton3 = false;
                    InputCode.PlayerDigitalButtons[0].ExtensionButton4 = false;
                    InputCode.PlayerDigitalButtons[1].ExtensionButton3 = false;
                    InputCode.PlayerDigitalButtons[1].ExtensionButton4 = false;
                }

                // Joy1 Right Up
                if (InputCode.PlayerDigitalButtons[0].Up.HasValue && InputCode.PlayerDigitalButtons[0].Up.Value
                                                                  && InputCode.PlayerDigitalButtons[0].Right.HasValue &&
                                                                  InputCode.PlayerDigitalButtons[0].Right.Value)
                {
                    InputCode.PlayerDigitalButtons[0].Left = true;
                }
                else
                {
                    InputCode.PlayerDigitalButtons[0].Left = false;
                }

                // Joy1 Right Down
                if (InputCode.PlayerDigitalButtons[0].Up.HasValue && InputCode.PlayerDigitalButtons[0].Up.Value
                                                                  && InputCode.PlayerDigitalButtons[0].Button1.HasValue &&
                                                                  InputCode.PlayerDigitalButtons[0].Button1.Value)
                {
                    InputCode.PlayerDigitalButtons[0].Down = true;
                }
                else
                {
                    InputCode.PlayerDigitalButtons[0].Down = false;
                }

                // Joy1 Left Down
                if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value
                                                                       && InputCode.PlayerDigitalButtons[0].Button1.HasValue &&
                                                                       InputCode.PlayerDigitalButtons[0].Button1.Value)
                {
                    InputCode.PlayerDigitalButtons[0].Button2 = true;
                }
                else
                {
                    InputCode.PlayerDigitalButtons[0].Button2 = false;
                }

                // Joy1 Left Up
                if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value
                                                                       && InputCode.PlayerDigitalButtons[0].Right.HasValue &&
                                                                       InputCode.PlayerDigitalButtons[0].Right.Value)
                {
                    InputCode.PlayerDigitalButtons[0].Button4 = true;
                }
                else
                {
                    InputCode.PlayerDigitalButtons[0].Button4 = false;
                }

                // Joy2 Right Up
                if (InputCode.PlayerDigitalButtons[1].Up.HasValue && InputCode.PlayerDigitalButtons[1].Up.Value
                                                                  && InputCode.PlayerDigitalButtons[1].Right.HasValue &&
                                                                  InputCode.PlayerDigitalButtons[1].Right.Value)
                {
                    InputCode.PlayerDigitalButtons[1].Left = true;
                }
                else
                {
                    InputCode.PlayerDigitalButtons[1].Left = false;
                }

                // Joy2 Right Down
                if (InputCode.PlayerDigitalButtons[1].Up.HasValue && InputCode.PlayerDigitalButtons[1].Up.Value
                                                                  && InputCode.PlayerDigitalButtons[1].Button1.HasValue &&
                                                                  InputCode.PlayerDigitalButtons[1].Button1.Value)
                {
                    InputCode.PlayerDigitalButtons[1].Down = true;
                }
                else
                {
                    InputCode.PlayerDigitalButtons[1].Down = false;
                }

                // Joy2 Left Down
                if (InputCode.PlayerDigitalButtons[1].Button3.HasValue && InputCode.PlayerDigitalButtons[1].Button3.Value
                                                                       && InputCode.PlayerDigitalButtons[1].Button1.HasValue &&
                                                                       InputCode.PlayerDigitalButtons[1].Button1.Value)
                {
                    InputCode.PlayerDigitalButtons[1].Button2 = true;
                }
                else
                {
                    InputCode.PlayerDigitalButtons[1].Button2 = false;
                }

                // Joy2 Left Up
                if (InputCode.PlayerDigitalButtons[1].Button3.HasValue && InputCode.PlayerDigitalButtons[1].Button3.Value
                                                                       && InputCode.PlayerDigitalButtons[1].Right.HasValue &&
                                                                       InputCode.PlayerDigitalButtons[1].Right.Value)
                {
                    InputCode.PlayerDigitalButtons[1].Button4 = true;
                }
                else
                {
                    InputCode.PlayerDigitalButtons[1].Button4 = false;
                }

                Thread.Sleep(10);
            }
        }

        private void WriteConfigIni()
        {
            var lameFile = "";
            var categories = _gameProfile.ConfigValues.Select(x => x.CategoryName).Distinct().ToList();
            lameFile += "[GlobalHotkeys]\n";
            lameFile += "ExitKey=" + Lazydata.ParrotData.ExitGameKey + "\n";
            lameFile += "PauseKey=" + Lazydata.ParrotData.PauseGameKey + "\n";

            bool ScoreEnabled = _gameProfile.ConfigValues.Any(x => x.FieldName == "Enable Submission" && x.FieldValue == "1");
            if (ScoreEnabled)
            {
                lameFile += "[GlobalScore]\n";
                lameFile += "Submission ID=" + Lazydata.ParrotData.ScoreSubmissionID + "\n";
                lameFile += "CollapseGUIKey=" + Lazydata.ParrotData.ScoreCollapseGUIKey + "\n";
            }

            for (var i = 0; i < categories.Count(); i++)
            {
                lameFile += $"[{categories[i]}]{Environment.NewLine}";
                var variables = _gameProfile.ConfigValues.Where(x => x.CategoryName == categories[i]);
                lameFile = variables.Aggregate(lameFile,
                    (current, fieldInformation) =>
                        current + $"{fieldInformation.FieldName}={fieldInformation.FieldValue}{Environment.NewLine}");
            }

            File.WriteAllText(Path.Combine(Path.GetDirectoryName(_gameLocation) ?? throw new InvalidOperationException(), "teknoparrot.ini"), lameFile);

            if (_twoExes && !string.IsNullOrEmpty(_gameLocation2))
            {
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(_gameLocation2) ?? throw new InvalidOperationException(), "teknoparrot.ini"), lameFile);
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
                        _quitEarly = true;
                        return;
                    }
                }
            }

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
                case EmulationProfile.Harley:
                    if (_pipe == null)
                        _pipe = new amJvsPipe();
                    break;
                case EmulationProfile.ALLSIDTA:
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

            bool RealGearShiftID = _gameProfile.ConfigValues.Any(x => x.FieldName == "RealGearshift" && x.FieldValue == "1");
            bool ProMode = _gameProfile.ConfigValues.Any(x => x.FieldName == "Professional Edition Enable" && x.FieldValue == "1");

            switch (InputCode.ButtonMode)
            {
                case EmulationProfile.DeadHeat:
                case EmulationProfile.Nirin:
                    _controlSender = new DeadHeatPipe();
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
                case EmulationProfile.TaitoTypeXBattleGear:
                    if (ProMode)
                        _controlSender = new BG4ProPipe();
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
                case EmulationProfile.NxL2:
                    _controlSender = new NxL2Pipe();
                    break;
                case EmulationProfile.FastIo:
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
            }

            _controlSender?.Start();

            if (InputCode.ButtonMode == EmulationProfile.Rambo)
            {
                _killGunListener = false;
                new Thread(HandleRamboControls).Start();
            }

            if (InputCode.ButtonMode == EmulationProfile.SegaOlympic2016)
            {
                _killGunListener = false;
                new Thread(HandleOlympicControls).Start();
            }

            if (!_runEmuOnly)
                WriteConfigIni();

            if (InputCode.ButtonMode != EmulationProfile.EuropaRFordRacing &&
                InputCode.ButtonMode != EmulationProfile.EuropaRSegaRally3 &&
                InputCode.ButtonMode != EmulationProfile.Theatrhythm &&
                InputCode.ButtonMode != EmulationProfile.FastIo)
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
                        JvsPackageEmulator.TaitoStick = true;
                        if (ProMode)
                        {
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
                Thread.Sleep(1000);
                CreateGameProcess();
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

        public static IPAddress GetNetworkAddress(IPAddress address, IPAddress subnetMask)
        {
            byte[] ipAdressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

            byte[] broadcastAddress = new byte[ipAdressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] & (subnetMaskBytes[i]));
            }
            return new IPAddress(broadcastAddress);
        }

        // IDZ specific stuff, should probably be replaced
        // It's ZeroLauncher code that I give full permission to be used here, now people can't have a cry "reeee stole code" - nzgamer
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool FreeConsole();
        [DllImport("kernel32.dll")]
        static extern bool SetConsoleTitle(string lpConsoleTitle);
        private void bootMinime()
        {
            var psiNpmRunDist = new ProcessStartInfo
            {
                FileName = "cmd",
                RedirectStandardInput = true,
                WorkingDirectory = ".\\SegaTools\\minime"

            };
            //psiNpmRunDist.CreateNoWindow = true;
            psiNpmRunDist.UseShellExecute = false;
            var pNpmRunDist = Process.Start(psiNpmRunDist);
            pNpmRunDist.StandardInput.WriteLine("start.bat");
            pNpmRunDist.WaitForExit();
        }

        private void bootAmdaemon(string gameDir)
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

        private void bootServerbox(string gameDir)
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

        // End ZeroLauncher Code

        private void CreateGameProcess()
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
                    SetDPIAwareRegistryValue(_gameLocation);
                }
                SetDPIAwareRegistryValue(Path.GetFullPath(loaderExe));

                ProcessStartInfo info;

                if (_gameProfile.EmulationProfile == EmulationProfile.SegaToolsIDZ)
                {
                    info = new ProcessStartInfo(loaderExe, $" -d -k {loaderDll}.dll {Path.GetFileName(_gameProfile.GamePath)}");
                    info.UseShellExecute = false;
                    info.WorkingDirectory = Path.GetDirectoryName(_gameLocation) ?? throw new InvalidOperationException();
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
                    if (Lazydata.ParrotData.Elfldr2NetworkAdapterName != "")
                    {
                        info.EnvironmentVariables.Add("TP_ETH", Lazydata.ParrotData.Elfldr2NetworkAdapterName);
                    }

                    if (_gameProfile.EmulationProfile == EmulationProfile.Vt3Lindbergh)
                    {
                        info.EnvironmentVariables.Add("TEA_DIR",
                            Directory.GetParent(Path.GetDirectoryName(_gameLocation)) + "\\");
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
                    fileOutput += "[keychip]\nsubnet=" + GetNetworkAddress(ip, IPAddress.Parse("255.255.255.0")) +
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
                    ths = new ThreadStart(() => bootMinime());
                    th = new Thread(ths);
                    th.Start();

                    ThreadStart ths2 = null;
                    Thread th2 = null;
                    ths2 = new ThreadStart(() => bootAmdaemon(Path.GetDirectoryName(_gameProfile.GamePath)));
                    th2 = new Thread(ths2);
                    th2.Start();

                    ThreadStart ths3 = null;
                    Thread th3 = null;
                    ths3 = new ThreadStart(() => bootServerbox(Path.GetDirectoryName(_gameProfile.GamePath)));
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
                    CheckAMDDriver();
                }

                if (_twoExes && _secondExeFirst)
                    RunAndWait(loaderExe, $"{loaderDll} \"{_gameLocation2}\" {_secondExeArguments}");

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
                _gameProfile.EmulationProfile == EmulationProfile.ALLSIDTA
                )
                {
                    try
                    {
                        info.UseShellExecute = false;
                        info.EnvironmentVariables.Add("OPENSSL_ia32cap", ":~0x20000000");
                        Trace.WriteLine("openssl fix applied");
                    }
                    catch
                    {
                        Trace.WriteLine("woops, openssl fix already applied by user");
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
                        textBoxConsole.Dispatcher.Invoke(() => textBoxConsole.Text += "\n" + e.Data,
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
                    if (jvsDebug != null)
                    {
                        jvsDebug.StartDebugInputThread();
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
                                killIDZ();

                                FreeConsole();
                                idzRun = true;
                            }
                        });

                    }

                    Thread.Sleep(500);
                }

                Debug.WriteLine("Exit code: " + cmdProcess.ExitCode.ToString());

                switch (cmdProcess.ExitCode)
                {
                    case 1337:
                        MessageBox.Show("Unsupported CRC, please use a supported version of the game.");
                        break;
                    case 76501:
                        MessageBox.Show("This version of EXVS2 Xboost cannot be played. Please use Version 27 aka Final");
                        break;
                    case 76502:
                        MessageBox.Show("SFV 3.53 requires the games Patch folder to exist, either next to the \"game\" folder if you kept the original folder structure\n, or next to the Exe in WindowsNoEditor\\StreetFighterV\\Binaries\\Win64.\nIt should contain a bunch of patch pak files.");
                        break;
                    case 76503:
                        MessageBox.Show("Your ServerBoxD8_Nu_x64.exe is still encrypted. Please use a fully decrypted dump as the game won't work correctly without it.");
                        break;
                    case 3820:
                        MessageBox.Show("Score Submission - You are banned from making submissions!");
                        break;
                    case 3821:
                        MessageBox.Show("Score Submission - Detected old version, please update to latest version!");
                        break;
                    case 3822:
                        MessageBox.Show("Score Submission - Serial is invalid, please add a valid serial!");
                        break;
                    case 3823:
                        MessageBox.Show("Score Submission - Check ScoreSubmissionLog.txt in game folder for Audio Devices!");
                        break;
                    case 0xB0B0001:
                        MessageBox.Show("This game need these files in game root:\n./bin\n./bin/bms_GDK.exe\n......\n\nNow closing...");
                        break;
                    case 0xB0B0002:
                        MessageBox.Show("GAME REVISION not supported!!!\n\nNow closing.");
                        break;
                    case 0xB0B0003:
                        MessageBox.Show("This game need these files in game root:\ndk2win32.dll\n......\n\nNow closing...");
                        break;
                    case 0xB0B0004:
                        MessageBox.Show("This game need these files in game root:\ninpout32.dll\n......\n\nNow closing...");
                        break;
                    case 0xB0B0005:
                        MessageBox.Show("This game need these files in game root:\n./bin\n./bin/bms_IG2.exe\n......\n\nNow closing...");
                        break;
                    case 0xB0B0006:
                        MessageBox.Show("The screen used is not compatible with this setting.\n\nPlease run the game in windowed mode.\n\nNow closing...");
                        break;
                    case 0xB0B0007:
                        MessageBox.Show("This game need these files in game root:\nd3dx8.dll\nPlease copy the file or disable custom crosshairs.\n\nNow closing...");
                        break;
                    case 0xB0B0008:
                        MessageBox.Show("This game need these files in game root:\n./bin\n./bin/bms_IMS.exe\n......\n\nNow closing...");
                        break;
                    case 0xB0B0009:
                        MessageBox.Show("Main game executable file need to be patched with 4GB PATCHER on x64 OS, check: \n\n- fixes-channel on TeknoParrot Discord\n or\n- https:////ntcore.com//?page_id=371 \n\n Now closing...");
                        break;
                    case 0xB0B000A:
                        MessageBox.Show("This game need to be run in XP compatibility mode to avoid freezes/crashes:\nPlease change \"game.exe\" Compatibility Mode setting to \"Windows XP\" and relaunch game.\n......\n\nNow closing...");
                        break;
                    case 0xB0B000B:
                        MessageBox.Show("This game need to be patched to remove all trash:\nPlease patch \"game.exe\" with TrashCleaner for BlockKing (download from fixes-channel on TeknoParrot Discord).\n Once done relaunch game.\n......\n\nNow closing...");
                        break;
                    case 0xB0B000C:
                        MessageBox.Show("This game need these files in game root:\nd3dx11_43.dll (64-bit)\nPlease copy the file or disable custom bezel.\n\nNow closing...");
                        break;
                    case 0xB0B000D:
                        MessageBox.Show("This game need these files in game root:\nd3dx8.dll\nPlease copy the file or disable Landscape screen orientation.\n\nNow closing...");
                        break;
                    case 0xB0B000E:
                        MessageBox.Show("This game need this file in game root:\nglide3x.dll\nAvailable from #Fixes channel on TP-Discord or from nGlide v2.10\n......\n\nNow closing...");
                        break;
                    case 0xB0B0010:
                        MessageBox.Show("GAME PATH CHECK FAILED!...\nThis game need all his files to be in a \"pm\" directory.\n\nPlease move every files inside your game/dump folder to the newly created \"pm\" directory inside it.\nThen please re-set the path to the game elf in TeknoparrotUI settings before restarting the game.\n\nNow closing...");
                        break;
                    case 0xB0B0011:
                        MessageBox.Show("Missing Files detected. Please extract and place the \"programs_dec\" folder next to the game elf otherwise the game will not function properly. Now closing...");
                        break;
                    case 0xB0B0012:
                        MessageBox.Show("Missing Files detected. Please extract and place the \"hasp\" folder next to the game elf otherwise the game will not function properly. Now closing...");
                        break;
                    case 0xB0B0013:
                        MessageBox.Show("Missing Files detected. Please extract and place the \"TPVirtualCards.dll\" file next to the game exe to enable Virtual Cards interface.\nAvailable from #Fixes channel on TP-Discord.\n Now closing...");
                        break;
                    case 0xB0B0020:
                        MessageBox.Show("This game need these file in game root:\nSDL2.dll\n\nPlease come to #Fixes channel on TP-Discord.\n......\n\nNow closing...");
                        break;
                    case 0xB0B0021:
                        MessageBox.Show("This game need these file in game root:\nzlib1.dll (v1.2.3)\nlibeay32.dll (v1.0.0.e)\nssleay32.dll (v1.0.0.e)\n\nPlease come to #Fixes channel on TP-Discord.\n......\n\nNow closing...");
                        break;
                    case 0xB0B0022:
                        MessageBox.Show("This game need these file in game root:\nalleg40.dll (Allegro API v4.0.X)\n\nPlease come to #Fixes channel on TP-Discord.\n......\n\nNow closing...");
                        break;
                    case 0xAAA0000:
                        MessageBox.Show("Could not connect to TPO2 lobby server. Quitting game...");
                        break;
                    case 0xAAA0001:
                        MessageBox.Show("You're using a version of the game that hasn't been whitelisted for TPO.\nTo ensure people don't experience crashes or glitches because of mismatchd, only the latest public version will work.");
                        break;
                }

                TerminateThreads();
                if (!idzRun && _gameProfile.EmulationProfile == EmulationProfile.SegaToolsIDZ)
                {
                    //just in case it's been stopped some other way
                    killIDZ();
                    FreeConsole();
                }
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

        /// <summary>
        /// Will kill all processes related to IDZ with SegaTools (can probably be done better)
        /// </summary>
        private void killIDZ()
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

                regex = new Regex(@"InitialD0.*");

                foreach (Process p in Process.GetProcesses("."))
                {
                    if (regex.Match(p.ProcessName).Success)
                    {
                        p.Kill();
                        Console.WriteLine("killed game process!");
                    }
                }

                regex = new Regex(@"ServerBoxD8.*");

                foreach (Process p in Process.GetProcesses("."))
                {
                    if (regex.Match(p.ProcessName).Success)
                    {
                        p.Kill();
                        Console.WriteLine("killed serverbox!");
                    }
                }

                regex = new Regex(@"inject.*");

                foreach (Process p in Process.GetProcesses("."))
                {
                    if (regex.Match(p.ProcessName).Success)
                    {
                        p.Kill();
                        Console.WriteLine("killed inject.exe!");
                    }
                }

                regex = new Regex(@"node.*");

                foreach (Process p in Process.GetProcesses("."))
                {
                    if (regex.Match(p.ProcessName).Success)
                    {
                        p.Kill();
                        Console.WriteLine("killed nodeJS! (if you were running node, you may want to restart it)");
                    }
                }

                FreeConsole();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Attempted to kill a game process that wasn't running (this is fine)");
            }

            return;
        }

        // Let people know why IDAS won't work if they're on newer AMD drivers
        private void CheckAMDDriver()
        {
            bool nvidiaFound = false;
            bool badDriver = false;
            using (var searcher = new ManagementObjectSearcher("select * from Win32_VideoController"))
            {
                try
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string driverVersionString = obj["DriverVersion"].ToString();
                        long driverVersion = Int64.Parse(driverVersionString.Replace(".", string.Empty));

                        if (obj["Name"].ToString().Contains("AMD"))
                        {
                            if (driverVersion > 3002101710000)
                            {
                                badDriver = true;
                            }
                        }
                        else if (obj["Name"].ToString().Contains("NVIDIA"))
                        {
                            nvidiaFound = true;
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("AMD driver check failed, probably because WMI is not working on the users system (IE: borked windows installed)");
                }
            }

            // Making sure there is no nvidia gpu before we throw this MSG to not confuse people with Ryzen Laptops + NVIDIA DGPU
            if (badDriver && !nvidiaFound)
            {
                MessageBox.Show("Your AMD driver is unsupported for this game. \nIf the game crashes or has new graphical issues, please downgrade to the AMD driver version 22.5.1 or older", "Teknoparrot UI");
            }
        }

        private void RunAndWait(string loaderExe, string daemonPath)
        {
            ProcessStartInfo info = new ProcessStartInfo(loaderExe, daemonPath);
            if (_gameProfile.EmulationProfile == EmulationProfile.ALLSSWDC ||
            _gameProfile.EmulationProfile == EmulationProfile.IDZ ||
            _gameProfile.EmulationProfile == EmulationProfile.ALLSSCHRONO ||
            _gameProfile.EmulationProfile == EmulationProfile.NxL2 ||
            _gameProfile.EmulationProfile == EmulationProfile.RawThrillsFNF ||
            _gameProfile.EmulationProfile == EmulationProfile.ALLSHOTDSD ||
            _gameProfile.EmulationProfile == EmulationProfile.ALLSFGO ||
            _gameProfile.EmulationProfile == EmulationProfile.TimeCrisis5 ||
            _gameProfile.EmulationProfile == EmulationProfile.JojoLastSurvivor ||
            _gameProfile.EmulationProfile == EmulationProfile.ALLSIDTA
            )
            {
                try
                {
                    info.UseShellExecute = false;
                    info.EnvironmentVariables.Add("OPENSSL_ia32cap", ":~0x20000000");
                    Trace.WriteLine("openssl fix applied");
                }
                catch
                {
                    Trace.WriteLine("woops, openssl fix already applied by user");
                }

            }
            Process.Start(info);
            Thread.Sleep(1000);
        }

        private Thread CreateInputListenerThread()
        {
            var hWnd = new WindowInteropHelper(Application.Current.MainWindow ?? throw new InvalidOperationException()).EnsureHandle();
            var inputThread = new Thread(() => InputListener.Listen(Lazydata.ParrotData.UseSto0ZDrivingHack, Lazydata.ParrotData.StoozPercent, _gameProfile.JoystickButtons, _inputApi, _gameProfile));
            inputThread.Start();

            // Hook window proc messages
            if (_inputApi == InputApi.RawInput)
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

        private void TerminateThreads()
        {
            _controlSender?.Stop();
            InputListener?.StopListening();

            if (_inputApi == InputApi.RawInput)
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
    }
}
