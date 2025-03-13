using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TeknoParrotUi.Common;
using TeknoParrotUi.Views;

namespace TeknoParrotUi
{
    /// <summary>
    /// Interaction logic for App.axaml
    /// </summary>
    /// 
    // Hello
    public partial class App : Application
    {
        private GameProfile _profile;
        private bool _emuOnly, _test, _tpOnline, _startMin;
        private bool _profileLaunch;
        private FluentTheme _fluentTheme;

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
                var b = Path.Combine("GameProfilesJSON\\", a);
                if (!File.Exists(b))
                    return false;
                if (File.Exists(Path.Combine("UserProfilesJSON\\", a)))
                {
                    _profile = JoystickHelper.DeSerializeGameProfile(Path.Combine("UserProfilesJSON\\", a), true);
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

        public static void LoadTheme(string colorName, bool darkMode, bool holiday)
        {
            // if user isn't patreon, use defaults
            if (!IsPatreon())
            {
                colorName = "lightblue";

                if (holiday)
                {
                    var now = DateTime.Now;

                    if (now.Month == 10 && now.Day == 31)
                    {
                        // halloween - orange title
                        colorName = "orange";
                    }

                    if (now.Month == 12 && now.Day == 25)
                    {
                        // christmas - red title
                        colorName = "red";
                    }
                }
            }

            Debug.WriteLine($"UI colour: {colorName} | Dark mode: {darkMode}");

            // Get the current app instance
            var app = Current as App;
            if (app != null)
            {
                // Apply theme changes
                app.UpdateTheme(colorName, darkMode);
            }
        }

        private void UpdateTheme(string colorName, bool darkMode)
        {
            if (_fluentTheme != null)
            {
                // Set theme variant (dark/light)
                Application.Current.RequestedThemeVariant = darkMode ? ThemeVariant.Dark : ThemeVariant.Light;

                // Apply accent color via resources
                var color = MaterialColorFromName(colorName);
                Resources["SystemAccentColor"] = color;
                Resources["SystemAccentColorDark1"] = AdjustBrightness(color, -0.1);
                Resources["SystemAccentColorDark2"] = AdjustBrightness(color, -0.2);
                Resources["SystemAccentColorDark3"] = AdjustBrightness(color, -0.3);
                Resources["SystemAccentColorLight1"] = AdjustBrightness(color, 0.1);
                Resources["SystemAccentColorLight2"] = AdjustBrightness(color, 0.2);
                Resources["SystemAccentColorLight3"] = AdjustBrightness(color, 0.3);
            }
        }

        private static Color AdjustBrightness(Color color, double factor)
        {
            var hsv = color.ToHsv();
            var v = Math.Clamp(hsv.V + factor, 0, 1);
            // Create a new HsvColor and convert to RGB Color
            var adjustedHsv = new HsvColor(hsv.A, hsv.H, hsv.S, v);
            return adjustedHsv.ToRgb();
        }

        private static Color MaterialColorFromName(string colorName)
        {
            // This is a simplified mapping of common colors
            return colorName.ToLowerInvariant() switch
            {
                "red" => Color.Parse("#F44336"),
                "pink" => Color.Parse("#E91E63"),
                "purple" => Color.Parse("#9C27B0"),
                "deeppurple" => Color.Parse("#673AB7"),
                "indigo" => Color.Parse("#3F51B5"),
                "blue" => Color.Parse("#2196F3"),
                "lightblue" => Color.Parse("#03A9F4"),
                "cyan" => Color.Parse("#00BCD4"),
                "teal" => Color.Parse("#009688"),
                "green" => Color.Parse("#4CAF50"),
                "lightgreen" => Color.Parse("#8BC34A"),
                "lime" => Color.Parse("#CDDC39"),
                "yellow" => Color.Parse("#FFEB3B"),
                "amber" => Color.Parse("#FFC107"),
                "orange" => Color.Parse("#FF9800"),
                "deeporange" => Color.Parse("#FF5722"),
                "brown" => Color.Parse("#795548"),
                "grey" => Color.Parse("#9E9E9E"),
                "bluegrey" => Color.Parse("#607D8B"),
                _ => Color.Parse("#03A9F4"), // Default to light blue
            };
        }

        public void InitializeTheme(string primaryColorName, string accentColorName, bool darkMode, bool holiday)
        {
            // Apply holiday overrides if necessary
            if (!IsPatreon() && holiday)
            {
                var now = DateTime.Now;
                if (now.Month == 10 && now.Day == 31)
                {
                    primaryColorName = "Orange";
                }
                else if (now.Month == 12 && now.Day == 25)
                {
                    primaryColorName = "Red";
                }
            }

            Debug.WriteLine($"UI colour: Primary={primaryColorName}, Accent={accentColorName} | Dark mode: {darkMode}");

            // Initialize FluentTheme
            _fluentTheme = Styles.OfType<FluentTheme>().FirstOrDefault();
            if (_fluentTheme == null)
            {
                _fluentTheme = new FluentTheme();
                Styles.Insert(0, _fluentTheme);
            }

            // Set theme mode using the newer API
            Application.Current.RequestedThemeVariant = darkMode ? ThemeVariant.Dark : ThemeVariant.Light;

            // Apply accent colors
            var primaryColor = MaterialColorFromName(primaryColorName);
            var accentColor = MaterialColorFromName(accentColorName);

            // Set accent colors in resources
            if (Resources == null)
            {
                Resources = new ResourceDictionary();
            }

            Resources["SystemAccentColor"] = primaryColor;
            Resources["SystemAccentColorDark1"] = AdjustBrightness(primaryColor, -0.1);
            Resources["SystemAccentColorDark2"] = AdjustBrightness(primaryColor, -0.2);
            Resources["SystemAccentColorDark3"] = AdjustBrightness(primaryColor, -0.3);
            Resources["SystemAccentColorLight1"] = AdjustBrightness(primaryColor, 0.1);
            Resources["SystemAccentColorLight2"] = AdjustBrightness(primaryColor, 0.2);
            Resources["SystemAccentColorLight3"] = AdjustBrightness(primaryColor, 0.3);

            // Additional accent color if needed
            Resources["SecondaryAccentColor"] = accentColor;
        }

        public static bool IsPatreon()
        {
            var tp = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot");
            return (tp != null && tp.GetValue("PatreonSerialKey") != null);
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            // Add the theme options array
            Resources["ThemeOptions"] = new string[]
            {
            "Default",
            "Whiteout",
            "Bluehat",
            "Obsidian",
            "Ember",
            "Frost",
            "Echo",
            "Void",
            "Cyber"
            };
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // This fixes the paths when the ui is started through the command line in a different folder
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler((sender, ex) =>
            {
                try
                {
                    // give us the exception in english
                    System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en");
                    var exceptionObject = ex.ExceptionObject as Exception;
                    var exceptiontext = exceptionObject?.ToString() ?? "Unknown exception";

                    // Log the exception to a file first in case showing message dialog fails
                    File.WriteAllText("exception.txt", exceptiontext);

                    // Show error using a simpler API that doesn't require async-await
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            var dialog = new MessageDialog
                            {
                                Message = $"TeknoParrotUI ran into an exception!\nPlease send exception.txt to the #teknoparrothelp channel on Discord or create a Github issue!\n{exceptiontext}",
                                Title = "Error",
                                ButtonDefs = MessageDialog.ButtonDefinitions.Ok
                            };
                            dialog.ShowDialog();
                        }
                        catch
                        {
                            // If dialog fails, at least we have the log file
                        }

                        Environment.Exit(1);
                    });
                }
                catch
                {
                    // Last resort
                    Environment.Exit(1);
                }
            });

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                string[] args = desktop.Args ?? Array.Empty<string>();

                // Call synchronous methods
                HandleArgs(args);

                // Run the async methods in a fire-and-forget way
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await JoystickHelper.DeSerialize();

                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            try
                            {
                                if (!Lazydata.ParrotData.HasReadPolicies)
                                {
                                    try
                                    {
                                        var policyWindow = new PoliciesWindow(2);
                                        policyWindow.Show();
                                        while (policyWindow.IsVisible)
                                        {
                                            await Task.Delay(100);
                                        }

                                        policyWindow.SetPolicyText(0);
                                        policyWindow.Show();
                                        while (policyWindow.IsVisible)
                                        {
                                            await Task.Delay(100);
                                        }
                                        // Wait a moment before showing the second window
                                        await Task.Delay(500);

                                        policyWindow.SetPolicyText(1);
                                        policyWindow.Show();
                                        while (policyWindow.IsVisible)
                                        {
                                            await Task.Delay(100);
                                        }

                                        Lazydata.ParrotData.HasReadPolicies = true;
                                        JoystickHelper.Serialize();
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Policy dialog error: {ex.Message}");
                                        // Continue with app initialization even if policy display fails
                                    }
                                }
                                // Continue with the rest of your initialization checks
                                if (!_tpOnline)
                                {
                                    // Check for other instances...
                                }

                                // Check for command line args to launch games or run the app
                                if (args.Length != 0)
                                {
                                    // Process command args
                                    if (HandleArgs(args))
                                    {
                                        var validationResult = await Views.Library.ValidateAndRun(_profile, _emuOnly, null, _test);
                                        if (validationResult.success)
                                        {
                                            // Launch the game...
                                        }
                                    }
                                    DiscordRPC.StartOrShutdown();

                                    // Start the main app
                                    await StartApp();
                                }
                                else
                                {
                                    // No args, start the main app normally
                                    await StartApp();
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"App initialization error: {ex.Message}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error during initialization: {ex.Message}");
                        Dispatcher.UIThread.Post(() =>
                        {
                            MessageBoxHelper.ErrorOK($"Error during initialization: {ex.Message}");
                        });
                    }
                });
            }

            base.OnFrameworkInitializationCompleted();
        }

        private async Task StartApp()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                try
                {
                    // Create the window using the correct thread context
                    Dispatcher.UIThread.Post(() =>
                    {
                        MainWindow wnd = new MainWindow();
                        desktop.MainWindow = wnd;
                        wnd.Show();
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating main window: {ex.Message}");
                    await MessageBoxHelper.ErrorOK($"Error creating main window: {ex.Message}");
                    // Show an error with simple MessageBox API that won't use the UI thread
                    Environment.Exit(1);
                }
            }
        }
    }
}