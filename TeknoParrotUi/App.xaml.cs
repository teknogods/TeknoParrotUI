using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CefSharp;
using CefSharp.Wpf;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using TeknoParrotUi.Views;
using System.IO.MemoryMappedFiles;
using TeknoParrotUi.Properties;
using System.Globalization;

namespace TeknoParrotUi
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    /// 
    // Hello
    public partial class App
    {
        private GameProfile _profile;
        private bool _emuOnly, _test, _tpOnline, _startMin;
        private bool _profileLaunch;
        private Mutex _mutex;
        private const string MutexName = "TeknoParrotUiSingleInstanceMutex";

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
        public static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
        public static extern uint TimeEndPeriod(uint uMilliseconds);

        public static bool Is64Bit()
        {
            // for testing
            //return false;
            return Environment.Is64BitOperatingSystem;
        }

        private void TerminateProcesses()
        {
            var currentId = Process.GetCurrentProcess().Id;
            foreach (var process in Process.GetProcessesByName("TeknoParrotUi"))
            {
                if (process.Id != currentId)
                {
                    process.Kill();
                }
            }
        }

        private bool HandleArgs(string[] args)
        {
            _test = args.Any(x => x == "--test");
            if (args.Contains("--tponline"))
            {
                _tpOnline = true;
            }

            if (args.Contains("--startMinimized"))
            {
                _startMin = true;
            }
            if (args.Any(x => x.StartsWith("--profile=")) && args.All(x => x != "--emuonly"))
            {
                // Run game + emu
                if (!FetchProfile(args.FirstOrDefault(x => x.StartsWith("--profile="))))
                    return false;
                _emuOnly = false;
                _profileLaunch = true;
                if (string.IsNullOrWhiteSpace(_profile.GamePath))
                {
                    MessageBoxHelper.ErrorOK(TeknoParrotUi.Properties.Resources.ErrorGamePathNotSet);
                    return false;
                }

                return true;
            }

            if (args.Any(x => x.StartsWith("--profile=")) && args.Any(x => x == "--emuonly"))
            {
                // Run emu only
                if (!FetchProfile(args.FirstOrDefault(x => x.StartsWith("--profile="))))
                    return false;
                _emuOnly = true;
                return true;
            }

            return false;
        }

        private bool FetchProfile(string profile)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(profile))
                    return false;
                var a = profile.Substring(10, profile.Length - 10);
                if (string.IsNullOrWhiteSpace(a))
                    return false;
                var b = Path.Combine("GameProfiles\\", a);
                if (!File.Exists(b))
                    return false;
                if (File.Exists(Path.Combine("UserProfiles\\", a)))
                {
                    _profile = JoystickHelper.DeSerializeGameProfile(Path.Combine("UserProfiles\\", a), true);
                }
                else
                {
                    _profile = JoystickHelper.DeSerializeGameProfile(b, false);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static PaletteHelper ph = new PaletteHelper();
        static SwatchesProvider sp = new SwatchesProvider();

        public static void LoadTheme(string colourname, bool darkmode, bool holiday)
        {
            // if user isn't patreon, use defaults
            if (!IsPatreon())
            {
                colourname = "lightblue";

                if (holiday)
                {
                    var now = DateTime.Now;

                    if (now.Month == 10 && now.Day == 31)
                    {
                        // halloween - orange title
                        colourname = "orange";
                    }

                    if (now.Month == 12 && now.Day == 25)
                    {
                        // christmas - red title
                        colourname = "red";
                    }
                }
            }

            Debug.WriteLine($"UI colour: {colourname} | Dark mode: {darkmode}");

            var theme = ph.GetTheme();
            theme.SetBaseTheme(darkmode ? BaseTheme.Dark : BaseTheme.Light);

            try
            {
                var allSwatches = sp.Swatches.ToList();
                var colorNames = allSwatches.Select(s => s.Name).ToList();
                //Debug.WriteLine($"Available colors: {string.Join(", ", colorNames)}");

                var colour = allSwatches.FirstOrDefault(a => string.Equals(a.Name, colourname, StringComparison.OrdinalIgnoreCase));

                if (colour != null)
                {
                    theme.SetPrimaryColor(colour.ExemplarHue.Color);
                    // bluegrey and brown do not have a secondary exemplar hue...
                    if (colour.SecondaryExemplarHue != null)
                    {
                        theme.SetSecondaryColor(colour.SecondaryExemplarHue.Color);
                    }
                    else
                    {
                        Debug.WriteLine($"SecondaryExemplarHue is null for {colourname}, using primary color instead");
                        theme.SetSecondaryColor(colour.ExemplarHue.Color);
                    }
                }
                else
                {
                    Debug.WriteLine($"Color '{colourname}' not found, using default");
                    var defaultColour = allSwatches.FirstOrDefault(a => a.Name == "lightblue") ?? allSwatches.First();
                    theme.SetPrimaryColor(defaultColour.ExemplarHue.Color);
                    if (defaultColour.SecondaryExemplarHue != null)
                    {
                        theme.SetSecondaryColor(defaultColour.SecondaryExemplarHue.Color);
                    }
                    else
                    {
                        theme.SetSecondaryColor(defaultColour.ExemplarHue.Color);
                    }
                }

                ph.SetTheme(theme);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading theme: {ex.Message}");
                MessageBoxHelper.ErrorOK(TeknoParrotUi.Properties.Resources.AppErrorLoadingTheme);
            }
        }

        public static bool IsPatreon()
        {
            var tp = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot");
            return (tp != null && tp.GetValue("PatreonSerialKey") != null);
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                // Second instance, send message to existing instance
                if (e.Args.Length > 0 && e.Args[0].StartsWith("teknoparrot://"))
                {
                    SendMessageToExistingInstance(e.Args[0]);
                }
                else
                {
                    // When using TPO we need to allow a second instance, as that's what launches the game
                    if (e.Args.Contains("--tponline"))
                    {
                        _tpOnline = true;
                    }
                    if (!_tpOnline)
                    {
                        // but if it's not TPO, we want the old single instance behaviour back.
                        if (MessageBoxHelper.ErrorYesNo(TeknoParrotUi.Properties.Resources.ErrorAlreadyRunning))
                        {
                            TerminateProcesses();
                        }
                        else
                        {
                            Current.Shutdown(0);
                            return;
                        }
                    }
                }

                if (!createdNew && e.Args.Length > 0 && e.Args[0].StartsWith("teknoparrot://"))
                {
                    Current.Shutdown(0);
                    return;
                }
            }

            // This fixes the paths when the ui is started through the command line in a different folder
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler((_, ex) =>
            {
                // give us the exception in english
                System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en");
                var exceptiontext = (ex.ExceptionObject as Exception).ToString();
                MessageBoxHelper.ErrorOK(string.Format(TeknoParrotUi.Properties.Resources.AppUnhandledException, exceptiontext));
                File.WriteAllText("exception.txt", exceptiontext);
                Environment.Exit(1);
            });
            // Localization testing without changing system language.
            // Language code list: https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-lcid/70feba9f-294e-491e-b6eb-56532684c37f
            //System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("fr-FR");

            HandleArgs(e.Args);
            if (!Lazydata.ParrotData.HasReadPolicies)
            {
                MessageBox.Show(
                    TeknoParrotUi.Properties.Resources.AppPrivacyNoticeMessage,
                    TeknoParrotUi.Properties.Resources.AppPrivacyNoticeTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                var policyWindow = new PoliciesWindow(0, Current);
                policyWindow.ShowDialog();

                policyWindow.SetPolicyText(1);
                policyWindow.ShowDialog();

                Lazydata.ParrotData.HasReadPolicies = true;
                JoystickHelper.Serialize();
            }
            if (!_tpOnline)
            {
                if (!Lazydata.ParrotData.HideVanguardWarning)
                {
                    if (Process.GetProcessesByName("vgc").Where((p) => p.Id != Process.GetCurrentProcess().Id).Count() > 0 || Process.GetProcessesByName("vgtray").Where((p) => p.Id != Process.GetCurrentProcess().Id).Count() > 0)
                    {
                        MessageBoxHelper.WarningOK(TeknoParrotUi.Properties.Resources.VanguardDetected);
                    }
                }
            }

            if (File.Exists("DumbJVSManager.exe"))
            {
                MessageBoxHelper.ErrorOK(TeknoParrotUi.Properties.Resources.ErrorOldTeknoParrotDirectory);
                Current.Shutdown(0);
                return;
            }

            // updater cleanup
            try
            {
                var bakfiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.bak", SearchOption.TopDirectoryOnly);
                foreach (var file in bakfiles)
                {
                    try
                    {
                        Debug.WriteLine($"Deleting old updater file {file}");
                        File.Delete(file);
                    }
                    catch
                    {
                        // ignore..
                    }
                }
            }
            catch
            {
                // do nothing honestly
            }

            // old description file cleanup
            try
            {
                var olddescriptions = Directory.GetFiles("Descriptions", "*.xml");
                foreach (var file in olddescriptions)
                {
                    try
                    {
                        Debug.WriteLine($"Deleting old description file {file}");
                        File.Delete(file);
                    }
                    catch
                    {
                        // ignore..
                    }
                }
            }
            catch
            {
                // ignore
            }

            LoadTheme(Lazydata.ParrotData.UiColour, Lazydata.ParrotData.UiDarkMode, Lazydata.ParrotData.UiHolidayThemes);
            if (Lazydata.ParrotData.UiDisableHardwareAcceleration)
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            using (Process p = Process.GetCurrentProcess())
                p.PriorityClass = ProcessPriorityClass.AboveNormal;

            TimeBeginPeriod(1);

            if (e.Args.Length != 0)
            {
                // Process command args
                if (HandleArgs(e.Args) && Views.Library.ValidateAndRun(_profile, out var loader, out var dll, _emuOnly, null, _test))
                {
                    var gamerunning = new Views.GameRunning(_profile, loader, dll, _test, _emuOnly, _profileLaunch);
                    // Args ok, let's do stuff
                    var window = new Window
                    {
                        //fuck you nezarn no more resizing smh /s
                        Title = TeknoParrotUi.Properties.Resources.AppGameRunningTitle,
                        Content = gamerunning,
                        MaxWidth = 800,
                        MinWidth = 800,
                        MaxHeight = 800,
                        MinHeight = 800,
                    };
                    if (_startMin)
                    {
                        window.WindowState = WindowState.Minimized;
                    }

                    //             d:DesignHeight="800" d:DesignWidth="800" Loaded="GameRunning_OnLoaded" Unloaded="GameRunning_OnUnloaded">
                    window.Dispatcher.ShutdownStarted += (x, x2) => gamerunning.GameRunning_OnUnloaded(null, null);

                    window.Show();

                    return;
                }
            }
            DiscordRPC.StartOrShutdown();

            StartApp();
        }

        private void ApplyLanguageSetting()
        {
            try
            {
                string language = Lazydata.ParrotData?.Language ?? "en-US";
                
                Debug.WriteLine($"Applying language setting: {language}");
                
                var culture = new CultureInfo(language);
                
                // Set the UI culture for the current thread
                Thread.CurrentThread.CurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;
                
                // Set for the entire application domain
                CultureInfo.DefaultThreadCurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentCulture = culture;
                
                // Force resource manager to reload
                TeknoParrotUi.Properties.Resources.Culture = culture;
                
                Debug.WriteLine($"Culture applied successfully. Current UI Culture: {Thread.CurrentThread.CurrentUICulture.Name}");
            }
            catch (CultureNotFoundException ex)
            {
                Debug.WriteLine($"Culture not found: {ex.Message}. Falling back to English.");
                // Fall back to English if the culture is not found
                var englishCulture = new CultureInfo("en-US");
                Thread.CurrentThread.CurrentUICulture = englishCulture;
                Thread.CurrentThread.CurrentCulture = englishCulture;
                CultureInfo.DefaultThreadCurrentUICulture = englishCulture;
                CultureInfo.DefaultThreadCurrentCulture = englishCulture;
                TeknoParrotUi.Properties.Resources.Culture = englishCulture;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying language setting: {ex.Message}");
            }
        }

        private void SendMessageToExistingInstance(string arg)
        {
            try
            {
                var processes = Process.GetProcessesByName("TeknoParrotUi");
                var mainProcess = processes.FirstOrDefault(p => p.Id != Process.GetCurrentProcess().Id);

                if (mainProcess != null)
                {
                    IntPtr hWnd = mainProcess.MainWindowHandle;
                    if (hWnd != IntPtr.Zero)
                    {
                        uint WM_PROTOCOLACTIVATION = NativeMethods.RegisterWindowMessage("TeknoParrotUi_ProtocolActivation");


                        using (var mmf = MemoryMappedFile.CreateNew("TeknoParrotUi_Protocol_Data", 4096))
                        {
                            using (var accessor = mmf.CreateViewAccessor())
                            {
                                byte[] bytes = System.Text.Encoding.Unicode.GetBytes(arg);
                                accessor.Write(0, bytes.Length);
                                accessor.WriteArray(4, bytes, 0, bytes.Length);
                            }

                            NativeMethods.SendMessage(hWnd, WM_PROTOCOLACTIVATION, IntPtr.Zero, IntPtr.Zero);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error but continue
                Debug.WriteLine($"Error sending message to existing instance: {ex.Message}");
            }
        }

        public OAuthHelper OAuthHelper { get; private set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Load ParrotData and apply language BEFORE base.OnStartup
            try
            {
                Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
                JoystickHelper.DeSerialize();
                ApplyLanguageSetting();
            }
            catch
            {
                // If loading fails, continue with default language
            }
            base.OnStartup(e);

            OAuthHelper = new OAuthHelper();

            if (await OAuthHelper.EnsureAuthenticatedAsync(false))
            {
                Trace.WriteLine("User is logged in");
            }
            else
            {
                Trace.WriteLine("User is not logged in or has no internet connection");
            }
        }

        private void StartApp()
        {
            MainWindow wnd = new MainWindow();
            wnd.Show();
        }
    }
}
