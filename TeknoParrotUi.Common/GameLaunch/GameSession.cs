using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using TeknoParrotUi.Common.InputListening;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;
using TeknoParrotUi.Views.GameRunningCode.ControlHandlers;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// UI-agnostic game launch orchestrator: JVS emulation, pipes, input listening,
    /// configuration writing and process lifetime. Ported from the classic WPF
    /// GameRunning/GameProcessManager pipeline.
    ///
    /// Emulator types with heavy external-emulator configuration (Dolphin, Play,
    /// RPCS3, cxbxr, pcsx2x6, SegaTools) are not yet supported natively —
    /// see <see cref="SupportsNativeLaunch"/>.
    /// </summary>
    public sealed class GameSession : IDisposable
    {
        private readonly GameProfile _profile;
        private readonly bool _isTest;
        private readonly string _gameLocation;
        private readonly string _gameLocation2;
        private readonly bool _twoExes;
        private readonly bool _secondExeFirst;
        private readonly string _secondExeArguments;

        private readonly SerialPortHandler _serialPortHandler = new SerialPortHandler();
        private readonly InputListener _inputListener = new InputListener();
        private ControlPipe _pipe;
        private ControlSender _controlSender;
        private RawInputForwardWindow _rawInputWindow;
        private Thread _inputThread;
        private Process _process;
        private volatile bool _forceQuit;
        private InputApi _inputApi = InputApi.DirectInput;

        public event Action<string> OutputReceived;
        public event Action<string> StateChanged;
        public event Action<int> Exited;

        public GameSession(GameProfile profile, bool isTest = false)
        {
            _profile = profile;
            _isTest = isTest;
            _gameLocation = SafeFullPath(profile.GamePath);
            _gameLocation2 = SafeFullPath(profile.GamePath2);
            _twoExes = profile.HasTwoExecutables;
            _secondExeFirst = profile.LaunchSecondExecutableFirst;
            _secondExeArguments = profile.SecondExecutableArguments;
        }

        private static string SafeFullPath(string path)
        {
            try { return Path.GetFullPath(path); }
            catch { return ""; }
        }

        public static bool SupportsNativeLaunch(GameProfile profile)
        {
            switch (profile.EmulatorType)
            {
                case EmulatorType.Dolphin:
                case EmulatorType.Play:
                case EmulatorType.RPCS3:
                case EmulatorType.cxbxr:
                case EmulatorType.pcsx2x6:
                case EmulatorType.SegaTools:
                    return false;
                default:
                    return profile.EmulationProfile != EmulationProfile.SegaToolsIDZ;
            }
        }

        public bool Start()
        {
            if (!SupportsNativeLaunch(_profile))
            {
                StateChanged?.Invoke($"{_profile.EmulatorType} games are not supported by the native launcher yet.");
                return false;
            }

            if (!ResolveLoader(out var loaderExe, out var loaderDll))
                return false;

            var inputApiString = _profile.ConfigValues.Find(cv => cv.FieldName == "Input API")?.FieldValue;
            if (inputApiString != null && Enum.TryParse<InputApi>(inputApiString, out var parsedApi))
                _inputApi = parsedApi;

            InputCode.ButtonMode = _profile.EmulationProfile;
            InputCode.GameProfile = _profile;

            // --- JVS package + pipes (same order as the classic view) ---
            JvsPackageEmulator.Initialize(_profile);

            _pipe = PipeFactory.CreateControlPipe(_profile.EmulationProfile);
            _pipe?.Start(false);

            if (_profile.ConfigValues.Any(x => x.FieldName == "Invert Buttons" && x.FieldValue == "1"))
                JvsPackageEmulator.InvertMaiMaiButtons = true;

            JvsSetup.InitializeAnalogBytes(_profile.EmulationProfile);

            _controlSender = PipeFactory.CreateControlSender(_profile.EmulationProfile, _profile);
            _controlSender?.Start();

            StartControlHandlerThreads();

            if (!_isTest)
                TeknoParrotIniWriter.WriteConfigIni(_profile, _gameLocation, _gameLocation2, _twoExes);

            if (JvsSetup.UsesJvsPipe(_profile))
            {
                JvsSetup.ConfigureJvsPackage(_profile);
                _serialPortHandler.StopListening();
                new Thread(() => _serialPortHandler.ListenPipe("TeknoParrot_JVS")) { IsBackground = true }.Start();
                new Thread(_serialPortHandler.ProcessQueue) { IsBackground = true }.Start();
            }

            // --- input listening ---
            _inputThread = new Thread(() => _inputListener.Listen(
                Lazydata.ParrotData.UseSto0ZDrivingHack, Lazydata.ParrotData.StoozPercent,
                _profile.JoystickButtons, _inputApi, _profile)) { IsBackground = true };
            _inputThread.Start();

            if (_inputApi == InputApi.RawInput || _inputApi == InputApi.RawInputTrackball || _inputApi == InputApi.MergedInput)
            {
                _rawInputWindow = new RawInputForwardWindow(_inputListener);
                _rawInputWindow.Start();
            }

            // --- process ---
            var monitorThread = new Thread(() => RunGameProcess(loaderExe, loaderDll)) { IsBackground = true };
            monitorThread.Start();
            StateChanged?.Invoke("Game starting...");
            return true;
        }

        public void ForceQuit() => _forceQuit = true;

        private bool ResolveLoader(out string loaderExe, out string loaderDll)
        {
            bool is64Bit = _isTest ? _profile.TestExecIs64Bit : _profile.Is64Bit;
            loaderExe = is64Bit ? ".\\OpenParrotx64\\OpenParrotLoader64.exe" : ".\\OpenParrotWin32\\OpenParrotLoader.exe";
            loaderDll = string.Empty;

            switch (_profile.EmulatorType)
            {
                case EmulatorType.Lindbergh:
                    loaderExe = ".\\TeknoParrot\\BudgieLoader.exe";
                    break;
                case EmulatorType.N2:
                    loaderExe = ".\\N2\\BudgieLoader.exe";
                    break;
                case EmulatorType.ElfLdr2:
                    loaderExe = is64Bit ? ".\\ElfLdr2\\x64\\BudgieLoader_x64.exe" : ".\\ElfLdr2\\BudgieLoader.exe";
                    break;
                case EmulatorType.TeknoMacaw:
                    loaderExe = is64Bit ? ".\\TeknoParrot\\TeknoMacaw64.exe" : ".\\TeknoParrot\\TeknoMacaw.exe";
                    break;
                case EmulatorType.OpenParrot:
                    loaderDll = is64Bit ? ".\\OpenParrotx64\\OpenParrot64" : ".\\OpenParrotWin32\\OpenParrot";
                    break;
                case EmulatorType.OpenParrotKonami:
                    loaderExe = ".\\OpenParrotWin32\\OpenParrotKonamiLoader.exe";
                    break;
                default:
                    loaderDll = is64Bit ? ".\\TeknoParrot\\TeknoParrot64" : ".\\TeknoParrot\\TeknoParrot";
                    break;
            }

            if (!File.Exists(loaderExe))
            {
                StateChanged?.Invoke($"Cannot find loader: {loaderExe}");
                return false;
            }
            if (loaderDll != string.Empty && !File.Exists(loaderDll + ".dll"))
            {
                StateChanged?.Invoke($"Cannot find loader dll: {loaderDll}.dll");
                return false;
            }
            if (string.IsNullOrEmpty(_gameLocation) || !File.Exists(_gameLocation))
            {
                StateChanged?.Invoke("Game executable not found — set the game path first.");
                return false;
            }
            return true;
        }

        private void StartControlHandlerThreads()
        {
            if (InputCode.ButtonMode == EmulationProfile.Rambo)
            {
                GunControlHandler.SetKillFlag(false);
                new Thread(GunControlHandler.HandleRamboControls) { IsBackground = true }.Start();
            }
            if (InputCode.ButtonMode == EmulationProfile.GSEVO)
            {
                GunControlHandler.SetKillFlag(false);
                new Thread(GunControlHandler.HandleGSEvoReload) { IsBackground = true }.Start();
            }
            if (InputCode.ButtonMode == EmulationProfile.SegaOlympic2016)
            {
                OlympicControlHandler.SetKillFlag(false);
                new Thread(OlympicControlHandler.HandleOlympicControls) { IsBackground = true }.Start();
            }
            if (InputCode.ButtonMode == EmulationProfile.SegaOlympic2020)
            {
                OlympicControlHandler.SetKillFlag(false);
                new Thread(OlympicControlHandler.Handle2020OlympicControls) { IsBackground = true }.Start();
            }
        }

        private void RunGameProcess(string loaderExe, string loaderDll)
        {
            try
            {
                var info = GameLaunchArguments.BuildProcessStartInfo(_profile, _gameLocation, _isTest, loaderExe, loaderDll);
                GameLaunchArguments.ApplyPerGamePreLaunch(_profile, _gameLocation, loaderExe, loaderDll, RunAndWait, OutputReceived);

                if (_twoExes && _secondExeFirst)
                    RunAndWait(loaderExe, $"{loaderDll} \"{_gameLocation2}\" {_secondExeArguments}");

                info.WindowStyle = _profile.LaunchMinimized ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal;

                bool silent = Lazydata.ParrotData.SilentMode &&
                              _profile.EmulatorType != EmulatorType.Lindbergh &&
                              _profile.EmulatorType != EmulatorType.N2 &&
                              _profile.EmulatorType != EmulatorType.ElfLdr2;
                if (silent)
                {
                    info.WindowStyle = ProcessWindowStyle.Hidden;
                    info.RedirectStandardError = true;
                    info.RedirectStandardOutput = true;
                    info.CreateNoWindow = true;
                }

                _process = new Process { StartInfo = info, EnableRaisingEvents = true };
                _process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        OutputReceived?.Invoke(e.Data);
                };

                _process.Start();
                if (silent)
                    _process.BeginOutputReadLine();

                StateChanged?.Invoke("Game running");

                if (_twoExes && !_secondExeFirst)
                    RunAndWait(loaderExe, $"{loaderDll} \"{_gameLocation2}\" {_secondExeArguments}");

                while (!_process.HasExited)
                {
                    if (_forceQuit)
                    {
                        try { _process.Kill(); } catch { }
                    }
                    Thread.Sleep(500);
                }

                var exitCode = _process.ExitCode;
                Cleanup();
                StateChanged?.Invoke("Game stopped");
                Exited?.Invoke(exitCode);
            }
            catch (Exception ex)
            {
                OutputReceived?.Invoke($"Launch error: {ex.Message}");
                Cleanup();
                StateChanged?.Invoke("Launch failed");
                Exited?.Invoke(-1);
            }
        }

        private void RunAndWait(string loaderExe, string daemonArgs)
        {
            var info = new ProcessStartInfo(loaderExe, daemonArgs);
            GameLaunchArguments.ApplyOpenSslFix(_profile, info);
            info.WindowStyle = _profile.LaunchSecondExecutableMinimized ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal;
            Process.Start(info);
            Thread.Sleep(1000);
        }

        private void Cleanup()
        {
            _controlSender?.Stop();
            _inputListener?.StopListening();
            _rawInputWindow?.Stop();
            _serialPortHandler?.StopListening();
            _pipe?.Stop();
            GunControlHandler.SetKillFlag(true);
            OlympicControlHandler.SetKillFlag(true);
        }

        public void Dispose()
        {
            _forceQuit = true;
            Cleanup();
        }
    }
}
