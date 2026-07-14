using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        private readonly bool _emuOnly;
        private readonly string _gameLocation;
        private readonly string _gameLocation2;
        private readonly bool _twoExes;
        private readonly bool _secondExeFirst;
        private readonly string _secondExeArguments;

        private readonly SerialPortHandler _serialPortHandler = new SerialPortHandler();
        private readonly InputListenersManager _inputListeners = new InputListenersManager();
        private readonly IProcessStarter _processStarter = new RealProcessStarter();
        private ControlPipe _pipe;
        private ControlSender _controlSender;
        private RawInputForwardWindow _rawInputWindow;
        private Process _process;
        private volatile bool _forceQuit;
        private InputApi _inputApi = InputApi.DirectInput;
        // Created at the very beginning of StartInner (before ANY pipe/helper/
        // prefix preparation) and preserved for the whole session - see
        // GameLaunchSessionIdentity.
        private GameLaunchSessionIdentity _sessionIdentity;
        private int _cleanupStarted;

        public event Action<string> OutputReceived;
        public event Action<string> StateChanged;
        public event Action<int> Exited;

        public GameSession(GameProfile profile, bool isTest = false, bool emuOnly = false)
        {
            _profile = profile;
            _isTest = isTest;
            _emuOnly = emuOnly;
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
            // All emulator types are launched natively now that the classic
            // launcher is gone: loader-based games, external emulators
            // (Dolphin/Play/RPCS3/PCSX2/Cxbx-Reloaded) and SegaTools.
            return true;
        }

        public bool Start()
        {
            try
            {
                return StartInner();
            }
            catch (Exception ex)
            {
                // A launch-time crash here used to take the whole app down
                // (unhandled exception on the UI thread) — e.g. the game folder
                // being unwritable by the current user (permission denied writing
                // teknoparrot.ini). Report it and unwind any partially-started
                // pipes/listeners instead of throwing.
                var friendly = DescribeLaunchException(ex);
                OutputReceived?.Invoke("ERROR: " + friendly);
                StateChanged?.Invoke(friendly);
                try { Cleanup(); } catch { /* best effort */ }
                return false;
            }
        }

        /// <summary>Turns common launch-time exceptions into an actionable message instead of a raw stack trace.</summary>
        private static string DescribeLaunchException(Exception ex)
        {
            if (ex is PlatformNotSupportedException)
            {
                // Already a clear, complete user-facing message (see
                // ProtonPackageManager.UnsupportedHostMessage) - no need for
                // the generic "Launch failed:" prefix below.
                return ex.Message;
            }
            if (ex is UnauthorizedAccessException || ex is IOException)
            {
                // File.WriteAllText wraps the path in its message on most runtimes.
                return $"Cannot write game files ({ex.GetType().Name}: {ex.Message}). " +
                       "The game folder is not writable by the current user — check its " +
                       "ownership/permissions (e.g. a folder shared via FTP/Samba owned by another account).";
            }
            return $"Launch failed: {ex.Message}";
        }

        private bool StartInner()
        {
            // Hard gate, before ANY part of game-session preparation runs:
            // resolving loaders, writing config, creating pipes/JVS state,
            // starting input listeners, or preparing/launching a Wine/Proton
            // environment. TeknoParrot and the Windows games it wraps are
            // x86/x86_64 - an ARM64 (or any other non-x86_64) Linux host can't
            // run them at all yet (no x86_64 translation layer implemented),
            // regardless of what wine/Proton happens to be installed. See
            // Proton.ProtonPackageManager.IsSupportedHost/UnsupportedHostMessage.
            //
            // Policy: Linux ARM64 is unsupported for every TeknoParrot
            // game-session launch mode, including emulation-only launch
            // (--emuonly), until an x86/x86_64 translation backend is
            // implemented - this must stay the very first statement, before
            // the _emuOnly branch below.
            //
            // Extracted into GameLaunchPlatformGuard (rather than inlined
            // here) so this exact gate is directly unit-testable without
            // constructing a full GameSession (heavy real dependencies -
            // serial port handler, input listeners, actual process launch).
            GameLaunchPlatformGuard.ThrowIfUnsupported(OperatingSystem.IsLinux(), RuntimeInformation.OSArchitecture);

            // Unique per-launch session token (TP_LAUNCH_SESSION_ID) - created
            // BEFORE any pipe, pipehelper, shared-memory or prefix preparation
            // below, so every pipehelper process launched for this session
            // (including the eager-start helpers created while pipes come up)
            // explicitly carries the token and can always be discovered and
            // terminated session-scoped, even after Wine reparents it.
            _sessionIdentity = GameLaunchSessionIdentity.Create();
            Proton.ProtonRuntime.CurrentSessionToken = _sessionIdentity.EnvironmentVariableValue;
            OutputReceived?.Invoke($"[GameSessionLifecycle] SessionId: {_sessionIdentity.EnvironmentVariableValue}");

            // Second-launch diagnostics: report any resource of a PREVIOUS
            // session of this process that is still alive right now.
            if (OperatingSystem.IsLinux())
            {
                var previous = Proton.PipeHelperRegistry.LiveRegisteredHelpers();
                if (previous.Count > 0)
                {
                    foreach (var helper in previous)
                        OutputReceived?.Invoke("[PipeSession] WARNING: previous session resource still alive: " +
                            $"pipehelper pid {helper.Pid} (session {helper.SessionToken ?? "?"}, bridge {helper.BridgeId ?? "?"})");
                }
                else
                {
                    OutputReceived?.Invoke("[PipeSession] No previous-session pipe resources alive.");
                }
            }

            // --emuonly developer mode: run only the emulation layer (JVS, pipes,
            // input listeners) without resolving loaders or starting the game
            // process — the developer attaches/starts the game themselves.
            var loaderExe = string.Empty;
            var loaderDll = string.Empty;
            if (!_emuOnly && !ResolveLoader(out loaderExe, out loaderDll))
                return false;

            var inputApiString = _profile.ConfigValues.Find(cv => cv.FieldName == "Input API")?.FieldValue;
            if (inputApiString != null && Enum.TryParse<InputApi>(inputApiString, out var parsedApi))
                _inputApi = parsedApi;

            InputCode.ButtonMode = _profile.EmulationProfile;
            InputCode.GameProfile = _profile;

            // Linux: mark the Proton session active BEFORE any pipes are created,
            // otherwise the pipe factories build local .NET pipes the Wine game
            // can never see (the game then runs with dead controls). PrepareSession
            // also resolves wine + prefix so bridges can create their in-prefix
            // pipes BEFORE the game boots (JVS is probed immediately, no retry).
            if (Proton.ProtonLauncher.ShouldUseProton)
            {
                Proton.ProtonLauncher.PrepareSession(_profile);
                // Crash recovery ONLY: terminate orphaned helpers whose owning
                // TeknoParrotUI process verifiably died (token + dead-owner +
                // same-prefix checks; see PipeHelperRegistry). Never a global
                // name-wide pipehelper sweep - unrelated and newly-starting
                // helpers are left alone.
                Proton.PipeHelperRegistry.CleanupOrphanedHelpers(
                    Proton.ProtonRuntime.WinePrefix, line => OutputReceived?.Invoke(line));
                // Bridge diagnostics go to the game-running console.
                Proton.ProtonLog.LineWritten -= OnProtonLogLine;
                Proton.ProtonLog.LineWritten += OnProtonLogLine;
                EnsureExitCleanupHook();
            }

            // --- JVS package + pipes (same order as the classic view) ---
            JvsPackageEmulator.Initialize(_profile);

            _pipe = PipeFactory.CreateControlPipe(_profile.EmulationProfile);
            _pipe?.Start(_emuOnly);

            if (_profile.ConfigValues.Any(x => x.FieldName == "Invert Buttons" && x.FieldValue == "1"))
                JvsPackageEmulator.InvertMaiMaiButtons = true;

            JvsSetup.InitializeAnalogBytes(_profile.EmulationProfile);

            _controlSender = PipeFactory.CreateControlSender(_profile.EmulationProfile, _profile);
            _controlSender?.Start();

            StartControlHandlerThreads();

            if (!_isTest && !_emuOnly)
                TeknoParrotIniWriter.WriteConfigIni(_profile, _gameLocation, _gameLocation2, _twoExes);

            if (JvsSetup.UsesJvsPipe(_profile))
            {
                JvsSetup.ConfigureJvsPackage(_profile);
                // The handler owns BOTH threads (listener + queue) so Cleanup
                // can join them - anonymous unjoinable threads let a previous
                // session's listener keep running into the next session.
                _serialPortHandler.StartPipe("TeknoParrot_JVS");
            }

            // --- input listening ---
            // Platform-aware: legacy Windows listeners for DirectInput/XInput/RawInput,
            // SDL2 gamepad everywhere else (and when SDL2 is selected explicitly).
            // Gun games get a mouse listener alongside SDL2 (RawInput on Windows, evdev on Linux).
            _inputListeners.Start(_profile, _profile.JoystickButtons, _inputApi);

            LogInputSetup();

            if (OperatingSystem.IsWindows() && _inputListeners.NeedsWndProcRouting)
            {
                _rawInputWindow = new RawInputForwardWindow(_inputListeners);
                _rawInputWindow.Start();
            }

            // --- process ---
            if (_emuOnly)
            {
                // No game process: the developer starts the game themselves —
                // register the expected executables so the RawInput listeners
                // still find its window.
                GameWindowTracker.Reset();
                GameWindowTracker.AddExecutable(_gameLocation);
                if (_twoExes)
                    GameWindowTracker.AddExecutable(_gameLocation2);

                // Keep the emulation layer alive until force quit.
                var emuOnlyThread = new Thread(() =>
                {
                    StateChanged?.Invoke("Emulator running (emu only) — start the game process yourself.");
                    while (!_forceQuit)
                        Thread.Sleep(500);
                    Cleanup();
                    StateChanged?.Invoke("Emulator stopped");
                    Exited?.Invoke(0);
                }) { IsBackground = true };
                emuOnlyThread.Start();
                return true;
            }

            var monitorThread = new Thread(() => RunGameProcess(loaderExe, loaderDll)) { IsBackground = true };
            monitorThread.Start();
            StateChanged?.Invoke("Game starting...");
            return true;
        }

        public void ForceQuit() => _forceQuit = true;

        /// <summary>
        /// Logs the input setup and warns when the game has no bindings the
        /// active listeners can read — the #1 cause of "controls don't work".
        /// Input is always merged: gamepads via SDL2 (XInputButton bindings),
        /// keyboard/mouse/guns via RawInput; the saved Input API only selects
        /// the gun flavour (RawInput vs Trackball).
        /// </summary>
        private void LogInputSetup()
        {
            bool trackball = _inputApi == InputApi.RawInputTrackball;
            OutputReceived?.Invoke($"Input: SDL2 gamepads + RawInput keyboard/mouse{(trackball ? " + trackball" : "")} (merged)");

            // Linux: /dev/input readability is per-device — vendor udev ACLs can
            // make mice work while keyboards silently don't. Say so loudly,
            // state which fallback took over, and give the exact fix.
            if (OperatingSystem.IsLinux())
            {
                bool mouseOk = InputListening.Mouse.EvdevInterop.AnyReadableMouse();
                bool keyboardOk = InputListening.Mouse.EvdevInterop.AnyReadableKeyboard();
                if (mouseOk && keyboardOk)
                {
                    OutputReceived?.Invoke("Input devices: direct evdev access OK (full support).");
                }
                else
                {
                    bool x11 = InputListening.Mouse.X11Interop.IsAvailable();
                    OutputReceived?.Invoke("==================== INPUT PERMISSION WARNING ====================");
                    foreach (var warning in InputListening.Mouse.EvdevInterop.GetAccessWarnings())
                        OutputReceived?.Invoke("WARNING: " + warning);
                    if (x11)
                    {
                        string covered = !mouseOk && !keyboardOk ? "mouse aim/buttons and keyboard keys"
                            : !mouseOk ? "mouse aim/buttons" : "keyboard keys";
                        OutputReceived?.Invoke($"X11 fallback active: {covered} will work without any setup (no root needed).");
                        OutputReceived?.Invoke("Limits: dedicated light-gun hardware and per-device bindings need direct access.");
                    }
                    else
                    {
                        OutputReceived?.Invoke("No X display found — affected devices will NOT work until access is fixed.");
                    }
                    OutputReceived?.Invoke("Fix (one-time, from the TeknoParrot folder):  sudo ./setup/install-udev-rules.sh");
                    OutputReceived?.Invoke("  (grants your desktop session read access via logind ACLs; nothing runs as root afterwards.");
                    OutputReceived?.Invoke("   Alternative: sudo usermod -aG input $USER  then log out and back in.)");
                    OutputReceived?.Invoke("==================================================================");
                }
            }

            bool CountsFor(JoystickButtons b) =>
                b.XInputButton != null ||
                (b.RawInputButton != null && b.RawInputButton.DeviceType != RawDeviceType.None);

            // Wheel games: steering/pedal rows bound to keyboard/mouse only work
            // when the keyboard-axis engine is on — a classic silent-failure trap.
            bool axisEngineOn = _profile.ConfigValues?.Any(cv =>
                cv.FieldName == "Use Keyboard/Button For Axis" && cv.FieldValue == "1") == true;
            bool hasKbAxisRows = _profile.JoystickButtons.Any(b =>
                b?.RawInputButton != null && b.RawInputButton.DeviceType != RawDeviceType.None &&
                (b.AnalogType == AnalogType.Wheel || b.AnalogType == AnalogType.Gas || b.AnalogType == AnalogType.Brake));
            if (hasKbAxisRows && !axisEngineOn)
            {
                OutputReceived?.Invoke("WARNING: wheel/gas/brake are bound to keyboard or mouse buttons but " +
                                       "'Use Keyboard/Button For Axis' is OFF in Game Settings — steering and pedals will NOT respond. Enable it.");
            }

            int usable = _profile.JoystickButtons.Count(CountsFor);
            if (usable == 0 && _profile.JoystickButtons.Count > 0)
            {
                OutputReceived?.Invoke("WARNING: this game has NO bindings the input system can read — controls will not work.");
                if (_profile.JoystickButtons.Any(b => b.DirectInputButton != null))
                    OutputReceived?.Invoke("This game only has old DirectInput bindings; DirectInput was removed. Rebind your controls in Controller Setup (controllers, keyboard and mouse all work there).");
                else
                    OutputReceived?.Invoke("Bind controls in Controller Setup (or Multi-Game Button Config and press Save).");
            }
            else
            {
                OutputReceived?.Invoke($"{usable} binding(s) active.");
            }
        }

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
                case EmulatorType.SegaTools:
                    if (!SegaToolsLauncher.PrepareLoader(_profile, out loaderExe, out loaderDll, OutputReceived))
                        return false;
                    break;
                default:
                    loaderDll = is64Bit ? ".\\TeknoParrot\\TeknoParrot64" : ".\\TeknoParrot\\TeknoParrot";
                    break;
            }

            // Loader paths use Windows separators; on Linux normalize them so
            // File.Exists and process start work ("./OpenParrotWin32/...").
            // Wine and the loaders accept forward slashes fine.
            if (!OperatingSystem.IsWindows())
            {
                loaderExe = loaderExe.Replace('\\', '/');
                if (loaderDll != string.Empty)
                    loaderDll = loaderDll.Replace('\\', '/');
            }

            // External emulators launch their own exe — the loader is not used.
            if (ExternalEmulatorLauncher.IsExternalEmulator(_profile))
            {
                if (string.IsNullOrEmpty(_gameLocation) || !File.Exists(_gameLocation))
                {
                    StateChanged?.Invoke("Game executable not found — set the game path first.");
                    return false;
                }
                return true;
            }

            if (!File.Exists(loaderExe))
            {
                StateChanged?.Invoke($"Cannot find loader: {loaderExe}");
                return false;
            }
            if (loaderDll != string.Empty && !File.Exists(loaderDll + ".dll") &&
                _profile.EmulationProfile != EmulationProfile.SegaToolsIDZ)
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
                // Let the RawInput listeners recognise the game window even when
                // its title is not in HookedWindows.txt (merged input runs
                // RawInput for every game now)
                GameWindowTracker.Reset();
                GameWindowTracker.AddExecutable(_gameLocation);
                if (_twoExes)
                    GameWindowTracker.AddExecutable(_gameLocation2);

                ProcessStartInfo info;
                if (ExternalEmulatorLauncher.IsExternalEmulator(_profile))
                {
                    info = ExternalEmulatorLauncher.Build(_profile, _gameLocation, line => OutputReceived?.Invoke(line));
                }
                else if (_profile.EmulationProfile == EmulationProfile.SegaToolsIDZ)
                {
                    info = new ProcessStartInfo(loaderExe, $" -d -k {loaderDll}.dll {Path.GetFileName(_profile.GamePath)}")
                    {
                        UseShellExecute = false,
                        WorkingDirectory = Path.GetDirectoryName(_gameLocation) ?? throw new InvalidOperationException()
                    };
                }
                else
                {
                    info = GameLaunchArguments.BuildProcessStartInfo(_profile, _gameLocation, _isTest, loaderExe, loaderDll);
                }

                // Linux: run the game (and its loader) under Wine/Proton. The
                // pipe/COM/shared-memory factories detect the active Proton
                // session and create bridges instead of local endpoints.
                if (Proton.ProtonLauncher.ShouldUseProton)
                {
                    info = Proton.ProtonLauncher.WrapWithProton(info, _profile);
                    OutputReceived?.Invoke($"Launching via Wine/Proton: {info.FileName}");
                }

                GameLaunchArguments.ApplyPerGamePreLaunch(_profile, _gameLocation, loaderExe, loaderDll, RunAndWait, OutputReceived);

                if (_profile.EmulationProfile == EmulationProfile.SegaToolsIDZ)
                    SegaToolsLauncher.PrepareSession(_profile, line => OutputReceived?.Invoke(line));

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

                GameWindowTracker.AddExecutable(info.FileName);

                // Unique per-launch session token (TP_LAUNCH_SESSION_ID) -
                // created ONCE at the very beginning of StartInner (before any
                // pipes/helpers existed) and injected into the ORIGINAL
                // Wine/Proton ProcessStartInfo BEFORE Gamescope wrapping so the
                // wrapper and every inherited child (gamescopereaper, wine,
                // loader, game, helpers) carry the same token - the reliable
                // discovery signal for session-scoped lifecycle/termination
                // (see GameLaunchSessionIdentity/ProcSessionProcessLocator).
                // The direct (non-wrapped) path receives it too, which does
                // not otherwise alter direct-launch behavior.
                var sessionIdentity = _sessionIdentity ?? GameLaunchSessionIdentity.Create();
                sessionIdentity.TryApplyTo(info);

                // Linux Gamescope fullscreen-scaling decision: built AFTER
                // WindowStyle/silent-mode redirection are finalized above, so
                // both the direct AND (if used) Gamescope-wrapped
                // ProcessStartInfo carry the same settings (GamescopeCommandBuilder.Wrap
                // clones whatever `info` looks like at this point). Deliberately
                // separate from the actual Process.Start below - see
                // GamescopeLauncher.BuildLaunchPlan / GameProcessLauncher for
                // why (safe pre-start AND actual-Process.Start-level fallback,
                // never falling back once Gamescope has successfully started).
                var launchPlan = Proton.ProtonLauncher.ShouldUseProton
                    ? Proton.GamescopeLauncher.BuildLaunchPlan(info, _profile, line => OutputReceived?.Invoke(line))
                    : new GameProcessLaunchPlan { DirectStartInfo = info };
                if (launchPlan.GamescopeStartInfo != null)
                    GameWindowTracker.AddExecutable(launchPlan.GamescopeStartInfo.FileName);

                // Profile-driven Gamescope window-mode compatibility: when
                // AutomaticFit is effective (a wrapped start info exists) and
                // the profile is marked RequireWindowed, write a launch-time
                // windowed override into this launch's teknoparrot.ini. The
                // user's SAVED profile is never modified - `-S fit -f` still
                // presents the windowed game fullscreen. When AutomaticFit is
                // disabled/unavailable no override is applied and the original
                // config written above stays exactly as saved.
                if (launchPlan.GamescopeStartInfo != null && !_isTest)
                {
                    var compat = Proton.GamescopeWindowCompatibilityPolicy.Plan(_profile, automaticFitEffective: true);
                    if (compat.OverrideApplied)
                    {
                        TeknoParrotIniWriter.WriteConfigIni(compat.EffectiveProfile, _gameLocation, _gameLocation2, _twoExes);
                        OutputReceived?.Invoke(compat.ToLogBlock());
                        // Also to the terminal log for diagnosability.
                        Proton.ProtonLog.Write(compat.ToLogBlock());
                    }
                }

                var launchResult = GameProcessLauncher.LaunchWithResult(launchPlan, _processStarter, line => OutputReceived?.Invoke(line));
                _process = launchResult.Process;
                _process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        OutputReceived?.Invoke(e.Data);
                };

                GameWindowTracker.GameProcessId = _process.Id;
                if (silent)
                    _process.BeginOutputReadLine();

                StateChanged?.Invoke("Game running");

                if (_twoExes && !_secondExeFirst)
                    RunAndWait(loaderExe, $"{loaderDll} \"{_gameLocation2}\" {_secondExeArguments}");

                Proton.WrappedGameLifecycleResult wrappedLifecycle = null;
                if (launchResult.UsedGamescopeWrapper)
                {
                    // Gamescope wraps the ACTUAL game process - _process is the
                    // wrapper, not necessarily the Wine/Proton game. The wrapped
                    // lifecycle (state machine + session-token process discovery
                    // + centralized session terminator) tracks the REAL game
                    // session: temporary launchers, replacement executables,
                    // reparented Wine processes, a lingering wrapper after game
                    // exit, and a dead wrapper with the game still alive are all
                    // handled session-scoped - never touching unrelated processes.
                    wrappedLifecycle = RunWrapperLifecycleLoop(sessionIdentity, loaderExe);
                }
                else
                {
                    // Direct (Disabled / non-wrapped) launch: unchanged original
                    // tracking. The Kill() below targets ONLY the specific game
                    // process object this session started - no wrapper session
                    // exists here, so the session terminator does not apply.
                    while (!_process.HasExited)
                    {
                        if (_forceQuit)
                        {
                            try { _process.Kill(); } catch { }
                        }
                        Thread.Sleep(500);
                    }
                }

                // Wine/Proton NON-WRAPPED launches: the initial wine process
                // (hosting the loader) can exit while the actual game keeps
                // running under wineserver. Keep the session alive as long as
                // the game process exists. Wrapped sessions already track the
                // real game via the session token - the system-wide scan below
                // must NOT run for them (it could latch onto a same-named
                // process from an unrelated launch).
                if (!launchResult.UsedGamescopeWrapper &&
                    Proton.ProtonRuntime.IsActive && !_forceQuit &&
                    ProcessExitSafety.TryGetExitCode(_process, out var loaderExitCode) && loaderExitCode == 0)
                {
                    var gameExe = Path.GetFileName(_gameLocation);
                    // Give the detached game a moment to appear, then track it.
                    Proton.ProtonGameInfo game = null;
                    for (var i = 0; i < 20 && game == null && !_forceQuit; i++)
                    {
                        game = Proton.ProtonProcessDetector.FindRunningProtonGame(gameExe);
                        if (game == null)
                            Thread.Sleep(250);
                    }

                    if (game != null)
                    {
                        OutputReceived?.Invoke($"Loader exited; tracking game process (pid {game.Pid}).");
                        GameWindowTracker.GameProcessId = game.Pid;
                        while (!_forceQuit && Directory.Exists($"/proc/{game.Pid}"))
                            Thread.Sleep(500);

                        if (_forceQuit)
                        {
                            // Direct-launch force quit of the specific tracked game
                            // PID (pre-existing behavior, unchanged - wrapped
                            // sessions never reach this branch).
                            try { Process.GetProcessById(game.Pid).Kill(); } catch { /* already gone */ }
                        }
                    }
                }

                // cxbxr re-launches itself - monitor the child process until it's truly gone
                if (_profile.EmulatorType == EmulatorType.cxbxr)
                    ExternalEmulatorLauncher.WaitForCxbxrChildren(() => _forceQuit);

                if (_profile.EmulationProfile == EmulationProfile.SegaToolsIDZ)
                    SegaToolsLauncher.KillIDZ();

                // ExitCode is only ever read AFTER process exit is confirmed
                // (ProcessExitSafety guards HasExited + disposal races). For a
                // wrapped session whose wrapper exit could NOT be verified,
                // ExitCode is honestly reported unavailable instead of racing.
                int exitCode;
                if (wrappedLifecycle != null)
                {
                    if (!string.IsNullOrEmpty(wrappedLifecycle.ErrorMessage))
                        OutputReceived?.Invoke("ERROR: " + wrappedLifecycle.ErrorMessage);

                    if (wrappedLifecycle.PlannedCleanupTermination)
                    {
                        // The game exited normally on its own; we then
                        // deliberately terminated the lingering wrapper/loader
                        // as planned cleanup. The wrapper's kill-signal exit
                        // code (SIGTERM=143 etc.) is NOT a game error - report
                        // a normal exit so the UI returns to the menu.
                        exitCode = 0;
                    }
                    else if (wrappedLifecycle.WrapperExitConfirmed && ProcessExitSafety.TryGetExitCode(_process, out var wrapperExitCode))
                    {
                        exitCode = wrapperExitCode;
                    }
                    else
                    {
                        var remaining = string.Join(",", wrappedLifecycle.RemainingProcesses.Select(p => p.Pid));
                        OutputReceived?.Invoke("[GameSessionLifecycle]\n" +
                            $"WrapperExited: {(wrappedLifecycle.WrapperExitConfirmed ? "true" : "false")}\n" +
                            "ExitCodeAvailable: false\n" +
                            $"RemainingSessionPids: {(remaining.Length == 0 ? "none" : remaining)}");
                        exitCode = wrappedLifecycle.FinalState == Proton.WrappedGameLifecycleState.Completed ? 0 : -1;
                    }
                }
                else
                {
                    exitCode = ProcessExitSafety.TryGetExitCode(_process, out var directExitCode) ? directExitCode : -1;
                }

                if (wrappedLifecycle != null)
                {
                    // Also to ProtonLog (stdout) - the UI console is gone once
                    // the window closes, but the terminal keeps this record.
                    Proton.ProtonLog.Write("[GameSessionLifecycle] Final: " +
                        $"state={wrappedLifecycle.FinalState}, exitCode={exitCode}, " +
                        $"plannedCleanup={wrappedLifecycle.PlannedCleanupTermination}, " +
                        $"terminationReason={wrappedLifecycle.TerminationReason?.ToString() ?? "none"}, " +
                        $"wrapperExitConfirmed={wrappedLifecycle.WrapperExitConfirmed}");
                }

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

        /// <summary>
        /// Drives a Gamescope-wrapped session to completion. <see cref="_process"/>
        /// is the Gamescope wrapper here, NOT necessarily the actual Wine/
        /// Proton game. Composes the production lifecycle stack: session-token
        /// process discovery (<see cref="Proton.ProcSessionProcessLocator"/> over
        /// /proc/&lt;pid&gt;/environ - survives forking/reparenting/detaching),
        /// main-game confidence classification, the deterministic state machine
        /// (<see cref="Proton.WrappedGameLifecycleStateMachine"/>: candidate
        /// stabilization, replacement executables, wrapper linger, wrapper crash
        /// with the game alive), and the ONE centralized session terminator
        /// (<see cref="Proton.GameSessionTerminator"/>: identity-verified,
        /// deepest-first, wrapper-last, verified-not-assumed). Force Quit goes
        /// through the same terminator - never a bare wrapper-only Kill().
        /// </summary>
        private Proton.WrappedGameLifecycleResult RunWrapperLifecycleLoop(GameLaunchSessionIdentity sessionIdentity, string loaderExe)
        {
            var proc = new Proton.LinuxProcReader();
            var wrapperPid = _process.Id;
            var wrapperStat = proc.ReadStat(wrapperPid);
            if (wrapperStat == null)
                OutputReceived?.Invoke("[GameSessionLifecycle] WARNING: wrapper start time unreadable - PID-only wrapper identity (reduced confidence).");

            var wrapperDescriptor = new Proton.SessionProcessInfo
            {
                Pid = wrapperPid,
                ParentPid = wrapperStat?.ParentPid ?? 0,
                ProcessName = wrapperStat?.Comm ?? "gamescope",
                StartTimeTicks = wrapperStat?.StartTimeTicks,
                IsWrapper = true,
                IsInfrastructureProcess = true,
                Confidence = Proton.GameProcessConfidence.Infrastructure
            };

            var locator = new Proton.ProcSessionProcessLocator(
                proc,
                new Proton.SessionWrapperDescriptor { WrapperPid = wrapperPid, WrapperStartTimeTicks = wrapperStat?.StartTimeTicks },
                line => OutputReceived?.Invoke(line));

            var options = new Proton.WrappedGameLifecycleOptions();
            var terminator = new Proton.GameSessionTerminator(
                locator, new Proton.LinuxProcessSignaler(), options, Environment.ProcessId,
                log: line => OutputReceived?.Invoke(line));

            var expectations = new Proton.GameExecutableExpectations
            {
                PrimaryExecutable = Path.GetFileName(_gameLocation),
                SecondaryExecutable = _twoExes ? Path.GetFileName(_gameLocation2) : null,
                LauncherExecutables = BuildKnownLauncherExecutables(loaderExe)
            };

            var diagnostics = new Proton.GamescopeLaunchDiagnostics
            {
                WrapperPid = wrapperPid,
                WrapperStarted = true
            };
            var knownGamePids = new System.Collections.Generic.HashSet<int>();

            var runner = new Proton.WrappedGameLifecycleRunner(
                options, locator, terminator, sessionIdentity, expectations, wrapperDescriptor,
                forceQuitRequested: () => _forceQuit,
                wrapperAlive: () =>
                {
                    try { return !_process.HasExited; }
                    catch { return false; }
                },
                log: line => OutputReceived?.Invoke(line),
                onConfirmedGameChanged: game =>
                {
                    knownGamePids.Add(game.Pid);
                    GameWindowTracker.GameProcessId = game.Pid;
                    diagnostics.PrimaryGamePid = game.Pid;
                    OutputReceived?.Invoke($"Gamescope wrapper: confirmed game process (pid {game.Pid}, {game.ProcessName}).");
                });

            Proton.WrappedGameLifecycleResult result;
            try
            {
                result = runner.RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                OutputReceived?.Invoke($"Wrapped session lifecycle error: {ex.Message}");
                result = new Proton.WrappedGameLifecycleResult
                {
                    FinalState = Proton.WrappedGameLifecycleState.Failed,
                    WrapperStarted = true,
                    ErrorMessage = ex.Message
                };
            }

            diagnostics.GameChildObserved = result.ConfirmedGameObserved;
            diagnostics.GameChildExited = result.ConfirmedGameExited;
            diagnostics.WrapperExitedNaturally = result.WrapperExitedNaturally;
            diagnostics.WrapperTerminationRequested = result.WrapperTerminationAttempted;
            diagnostics.WrapperTerminationSucceeded = result.TerminationResult?.CompletedSuccessfully ?? false;
            diagnostics.DirectFallbackUsed = false; // never true here - a wrapper was already successfully running
            var sessionPids = new System.Collections.Generic.List<int>(knownGamePids) { wrapperPid };
            diagnostics.KnownSessionPids = sessionPids;
            OutputReceived?.Invoke(diagnostics.ToLogBlock());
            return result;
        }

        /// <summary>Loader/launcher executables that may appear in a session but are never instantly the confirmed game.</summary>
        private static System.Collections.Generic.IReadOnlyList<string> BuildKnownLauncherExecutables(string loaderExe)
        {
            var launchers = new System.Collections.Generic.List<string>
            {
                "BudgieLoader.exe", "BudgieLoader_x64.exe",
                "OpenParrotLoader.exe", "OpenParrotLoader64.exe", "OpenParrotKonamiLoader.exe",
                "TeknoMacaw.exe", "TeknoMacaw64.exe"
            };
            var loaderName = Path.GetFileName(loaderExe ?? string.Empty);
            if (!string.IsNullOrEmpty(loaderName) && !launchers.Contains(loaderName))
                launchers.Add(loaderName);
            return launchers;
        }

        private void RunAndWait(string loaderExe, string daemonArgs)
        {
            var info = new ProcessStartInfo(loaderExe, daemonArgs);
            GameLaunchArguments.ApplyOpenSslFix(_profile, info);
            info.WindowStyle = _profile.LaunchSecondExecutableMinimized ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal;
            Process.Start(info);
            Thread.Sleep(1000);
        }

        private void OnProtonLogLine(string line) => OutputReceived?.Invoke(line);

        private static bool _exitHookRegistered;

        /// <summary>
        /// Ensures pipehelper processes STARTED BY THIS PROCESS are terminated
        /// even when the UI is closed without stopping the game session first.
        /// Only registered, identity-verified helpers are touched (PID + start
        /// time re-checked immediately before the signal) - never a
        /// process-name-wide sweep that could hit another TeknoParrotUI
        /// instance's helpers.
        /// </summary>
        private static void EnsureExitCleanupHook()
        {
            if (_exitHookRegistered)
                return;
            _exitHookRegistered = true;
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try { Proton.PipeHelperRegistry.TerminateRegisteredHelpers(); } catch { /* ignored */ }
            };
        }

        /// <summary>
        /// Idempotent, synchronous session cleanup. Reached from normal game
        /// exit, launch exceptions, Force Quit, Dispose, the emulator-only
        /// thread and application shutdown - only the FIRST caller runs it;
        /// everyone else returns immediately. Stops and JOINS every owned
        /// thread and waits for the session's pipehelpers before the caller
        /// publishes "Game stopped"/Exited, so the UI can never return to a
        /// launch-ready menu while old pipe/helper threads still shut down.
        /// </summary>
        private void Cleanup()
        {
            if (Interlocked.Exchange(ref _cleanupStarted, 1) != 0)
                return;

            // 1. stop control sender
            _controlSender?.Stop();
            // 2. stop input listeners
            _inputListeners?.Stop();
            // 3. stop raw-input window
            _rawInputWindow?.Stop();

            // 4. stop and JOIN the SerialPortHandler listener + queue threads
            //    (includes waiting for its Proton bridge pipehelper, if any).
            var serialResult = _serialPortHandler?.StopAndWait(TimeSpan.FromSeconds(5));
            if (serialResult != null)
                OutputReceived?.Invoke("[PipeSession] JVS pipe shutdown: " +
                    $"ListenerThreadExited: {serialResult.ListenerThreadExited}, " +
                    $"QueueThreadExited: {serialResult.QueueThreadExited}, " +
                    $"HelperExited: {serialResult.HelperExited}, " +
                    $"RemainingHelperPids: {(serialResult.RemainingHelperPids.Count == 0 ? "none" : string.Join(",", serialResult.RemainingHelperPids))}");

            // 5. stop and JOIN the ControlPipe worker thread.
            var pipeResult = _pipe?.StopAndWait(TimeSpan.FromSeconds(5));
            if (pipeResult != null && !pipeResult.Completed)
                OutputReceived?.Invoke($"[PipeSession] WARNING: control pipe shutdown incomplete: {pipeResult.Detail}");

            // 6-7. verify no pipehelper of THIS session remains; terminate
            //      verified session helpers only (never name-wide).
            if (OperatingSystem.IsLinux() && _sessionIdentity != null && Proton.ProtonRuntime.IsActive)
            {
                var token = _sessionIdentity.EnvironmentVariableValue;
                var leftovers = Proton.PipeHelperRegistry.FindSessionHelperProcesses(token);
                if (leftovers.Count > 0)
                {
                    var proc = new Proton.LinuxProcReader();
                    var signaler = new Proton.LinuxProcessSignaler();
                    foreach (var (pid, startTicks) in leftovers)
                    {
                        var stat = proc.ReadStat(pid);
                        if (stat == null || stat.StartTimeTicks != startTicks)
                            continue; // gone or PID reused - never signal
                        OutputReceived?.Invoke($"[PipeSession] terminating leftover session helper pid {pid}");
                        signaler.SignalForce(pid);
                        Proton.PipeHelperRegistry.Unregister(pid);
                    }
                }
                var remaining = Proton.PipeHelperRegistry.FindSessionHelperProcesses(token);
                OutputReceived?.Invoke("[PipeSession] Session helper check: " +
                    (remaining.Count == 0 ? "none remaining" : $"STILL ALIVE: {string.Join(",", remaining.Select(p => p.Pid))}"));
            }

            // 8. shared-memory claims are released by each bridge's
            //    Close/CloseAndWait (after its helper shutdown is confirmed).
            // 9. detach Proton logging
            Proton.ProtonLog.LineWritten -= OnProtonLogLine;
            // 10. end the Proton session (also clears the session token).
            Proton.ProtonLauncher.EndSession();
            // 11. control-handler kill flags
            GunControlHandler.SetKillFlag(true);
            OlympicControlHandler.SetKillFlag(true);
            // 12. "Game stopped"/Exited are published by the CALLER after this
            //     method returns - i.e. only after cleanup completed.
        }

        public void Dispose()
        {
            _forceQuit = true;
            Cleanup();
        }
    }
}
