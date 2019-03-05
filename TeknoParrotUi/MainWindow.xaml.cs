using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using MahApps.Metro.Controls;
using TeknoParrotUi.Common;

namespace TeknoParrotUi
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public static ParrotData _parrotData;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadParrotData();
            //CreateConfigValue();

            foreach (var gameProfile in GameProfileLoader.GameProfiles)
            {
                ComboBoxItem item = new ComboBoxItem
                {
                    Content = gameProfile.GameName + (gameProfile.Patreon ? " (Patreon Only)" : string.Empty),
                    Tag = gameProfile
                };

                GameListComboBox.Items.Add(item);

                if (_parrotData.SaveLastPlayed && gameProfile.GameName == _parrotData.LastPlayed)
                {
                    GameListComboBox.SelectedItem = item;
                }
            }

            new Thread(() =>
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                Thread.CurrentThread.IsBackground = true;
                try
                {
                    string contents;
                    using (var wc = new WebClient())
                        contents = wc.DownloadString("https://teknoparrot.com/api/version");
                    if (UpdateChecker.CheckForUpdate(GameVersion.CurrentVersion, contents))
                    {
                        if (MessageBox.Show(
                                $"There is a new version available: {contents} (currently using {GameVersion.CurrentVersion}). Would you like to download it?",
                                "New update!", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        {
                            Thread.CurrentThread.IsBackground = false;
                            //Process.Start("https://teknoparrot.com");
                           
                            Application.Current.Dispatcher.Invoke((Action)delegate {
                                Views.DownloadWindow update = new Views.DownloadWindow(contents);
                                update.ShowDialog();
                            });
                        }
                    }
                }
                catch (Exception)
                {
                    // Ignored
                }
            }).Start();

            if (_parrotData.UseDiscordRPC)
                DiscordRPC.UpdatePresence(new DiscordRPC.RichPresence
                {
                    details = "Main Menu",
                    largeImageKey = "teknoparrot",
                });

            Title = "TeknoParrot UI " + GameVersion.CurrentVersion;
        }

        private void CreateConfigValue()
        {
            var game = new GameProfile();
            var f1 = new FieldInformation
            {
                CategoryName = "Network",
                FieldName = "Dhcp",
                FieldType = FieldType.Bool,
                FieldValue = "1"
            };
            var f2 = new FieldInformation
            {
                CategoryName = "Network",
                FieldName = "Ip",
                FieldType = FieldType.Text,
                FieldValue = "192.168.1.2"
            };
            var f3 = new FieldInformation
            {
                CategoryName = "Network",
                FieldName = "Mask",
                FieldType = FieldType.Text,
                FieldValue = "255.255.255.0"
            };
            var f4 = new FieldInformation
            {
                CategoryName = "Network",
                FieldName = "Gateway",
                FieldType = FieldType.Text,
                FieldValue = "192.168.1.1"
            };
            var f5 = new FieldInformation
            {
                CategoryName = "Network",
                FieldName = "Dns1",
                FieldType = FieldType.Text,
                FieldValue = "192.168.1.1"
            };
            var f6 = new FieldInformation
            {
                CategoryName = "Network",
                FieldName = "Dns2",
                FieldType = FieldType.Text,
                FieldValue = "0.0.0.0"
            };
            var f7 = new FieldInformation
            {
                CategoryName = "Network",
                FieldName = "BroadcastIP",
                FieldType = FieldType.Text,
                FieldValue = "192.168.1.255"
            };
            var f8 = new FieldInformation
            {
                CategoryName = "Network",
                FieldName = "Cab1IP",
                FieldType = FieldType.Text,
                FieldValue = "192.168.1.2"
            };
            var f9 = new FieldInformation
            {
                CategoryName = "Network",
                FieldName = "Cab2IP",
                FieldType = FieldType.Text,
                FieldValue = "192.168.1.3"
            };
            var x1 = new FieldInformation
            {
                CategoryName = "General",
                FieldName = "DongleRegion",
                FieldType = FieldType.Text,
                FieldValue = "JAPAN"
            };
            var x2 = new FieldInformation
            {
                CategoryName = "General",
                FieldName = "PcbRegion",
                FieldType = FieldType.Text,
                FieldValue = "JAPAN"
            };
            var x3 = new FieldInformation
            {
                CategoryName = "General",
                FieldName = "FreePlay",
                FieldType = FieldType.Bool,
                FieldValue = "1"
            };
            var x4 = new FieldInformation
            {
                CategoryName = "General",
                FieldName = "Windowed",
                FieldType = FieldType.Bool,
                FieldValue = "1"
            };
            game.ConfigValues = new List<FieldInformation> {x1, x2, x3, x4, f1, f2, f3, f4, f5, f6, f7, f8, f9};
            game.FileName = "test.xml";
            JoystickHelper.SerializeGameProfile(game);
        }

        /// <summary>
        /// Loads the settings data file.
        /// </summary>
        private void LoadParrotData()
        {
            try
            {
                if (!File.Exists("ParrotData.xml"))
                {
                    MessageBox.Show("Seems this is first time you are running me, please set emulation settings.",
                        "Hello World", MessageBoxButton.OK, MessageBoxImage.Information);
                    _parrotData = new ParrotData();
                    Lazydata.ParrotData = _parrotData;
                    JoystickHelper.Serialize(_parrotData);
                    return;
                }
                _parrotData = JoystickHelper.DeSerialize();
                if (_parrotData == null)
                {
                    _parrotData = new ParrotData();
                    Lazydata.ParrotData = _parrotData;
                    JoystickHelper.Serialize(_parrotData);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(
                    $"Exception happened during loading ParrotData.xml! Generate new one by saving!{Environment.NewLine}{Environment.NewLine}{e}",
                    "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnStartGame(object sender, RoutedEventArgs e)
        {
            if (GameListComboBox.Items.Count == 0)
                return;

            var gameProfile = (GameProfile) ((ComboBoxItem) GameListComboBox.SelectedItem).Tag;

            if (_parrotData.SaveLastPlayed)
            {
                _parrotData.LastPlayed = gameProfile.GameName;
                JoystickHelper.Serialize(_parrotData);
            }

            var testMenuExe = gameProfile.TestMenuIsExecutable ? gameProfile.TestMenuParameter : "";

            var testStr = gameProfile.TestMenuIsExecutable ? gameProfile.TestMenuExtraParameters : gameProfile.TestMenuParameter;
            
            ValidateAndRun(gameProfile, testStr, testMenuExe);
        }

        /// <summary>
        /// Validates that the game exists and then runs it with the emulator.
        /// </summary>
        /// <param name="gameLocation">Game executable location.</param>
        /// <param name="gameProfile">Input profile.</param>
        /// <param name="testMenuString">Command to run test menu.</param>
        /// <param name="isSinglePlayer">Init only first player controller.</param>
        /// <param name="testMenuIsExe">If uses separate exe.</param>
        /// <param name="exeName">Test menu exe name.</param>
        private void ValidateAndRun(GameProfile gameProfile, string testMenuString ,string exeName = "")
        {
            if (!ValidateGameRun(gameProfile))
                return;

            var testMenu = ChkTestMenu.IsChecked != null && ChkTestMenu.IsChecked.Value;

            var gameRunning = new TeknoParrotUi.Views.GameRunning(gameProfile, testMenu, _parrotData, testMenuString, gameProfile.TestMenuIsExecutable, exeName);
            gameRunning.ShowDialog();
            gameRunning.Close();
        }
        
        static List<string> RequiredFiles = new List<string>
        {
            "OpenParrot.dll",
            "OpenParrot64.dll",
            "TeknoParrot.dll",
            "TeknoParrot64.dll",
            "OpenParrotLoader.exe",
            "OpenParrotLoader64.exe",
            "ParrotLoader.exe",
            "ParrotLoader64.exe",
            "BudgieLoader.exe",
            ".\\N2\\BudgieLoader.exe",
            "Opensegaapi.dll"
        };

        private bool ValidateGameRun(GameProfile gameProfile)
        {
            if (!File.Exists(gameProfile.GamePath))
            {
                MessageBox.Show($"Cannot find game exe at: {gameProfile.GamePath}", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            foreach (var file in RequiredFiles)
            {
                if (!File.Exists(file))
                {
                    MessageBox.Show($"Cannot find {file}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }

            if (EmuBlacklist.CheckForBlacklist(Directory.GetFiles(Path.GetDirectoryName(gameProfile.GamePath))))
            {
                var errorMsg =
                    "Hold it right there!" + Environment.NewLine + "it seems you have other emulator already in use." + Environment.NewLine + "Please remove the following files from the game directory:" + Environment.NewLine;
                foreach (var fileName in EmuBlacklist.BlacklistedList)
                {
                    errorMsg += fileName + Environment.NewLine;
                }
                MessageBox.Show(errorMsg, "Validation error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (File.Exists(Path.Combine(gameProfile.GamePath, "iDmacDrv32.dll")))
            {
                var description = FileVersionInfo.GetVersionInfo("iDmacDrv32.dll");
                if (description.FileDescription != "PCI-Express iDMAC Driver Library (DLL)")
                {
                    return (MessageBox.Show("You seem to be using an unofficial iDmacDrv32.dll file! This game may crash or be unstable. Continue?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes);
                }
            }

            return true;
        }

        private void BtnSettings(object sender, RoutedEventArgs e)
        {
            //_settingsWindow.ShowDialog();
            LoadParrotData();
            SettingsControl.LoadStuff(_parrotData);
            EmulatorSettings.IsOpen = true;
        }

        public static void SafeExit()
        {
            if (_parrotData.UseDiscordRPC && File.Exists("discord-rpc.dll"))
                DiscordRPC.Shutdown();

            Application.Current.Shutdown(0);
        }

        private void BtnQuit(object sender, RoutedEventArgs e)
        {
            JoystickControl.StopListening();
            SafeExit();
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            JoystickControl.StopListening();
            SafeExit();
        }

        private void BtnAbout(object sender, RoutedEventArgs e)
        {
            new Views.About().ShowDialog();
        }

        private void Image_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start("https://www.patreon.com/Teknogods");
        }

        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start("https://teknogods.com");
        }

        private void BtnHelp(object sender, RoutedEventArgs e)
        {
            Process.Start("https://teknogods.com/phpbb/viewforum.php?f=83");
        }

        private void GameListComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var modifyItem = (ComboBoxItem) ((ComboBox) sender).SelectedItem;
            var profile = (GameProfile) ((ComboBoxItem) ((ComboBox) sender).SelectedItem).Tag;
            var icon = profile.IconName;
            BitmapImage imageBitmap = new BitmapImage(File.Exists(icon) ? new Uri(icon, UriKind.Relative) : new Uri("Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));
            MainLogo.Source = imageBitmap;
            GameSettingsControl.LoadNewSettings(profile, modifyItem);
            JoystickControl.LoadNewSettings(profile, modifyItem, _parrotData);
            if (!profile.HasSeparateTestMode)
            {
                ChkTestMenu.IsChecked = false;
                ChkTestMenu.IsEnabled = false;
            }
            else
            {
                ChkTestMenu.IsEnabled = true;
                ChkTestMenu.ToolTip = "Enable or disable test mode.";
            }
        }

        private void BtnGameSettings(object sender, RoutedEventArgs e)
        {
            FlyoutSettings.IsOpen = true;
        }

        private void FlyoutSettings_OnIsOpenChanged(object sender, RoutedEventArgs e)
        {
            if (FlyoutSettings.IsOpen)
            {
                JoystickControl.Listen();
            }
            else
            {
                JoystickControl.StopListening();
            }
        }
        }
    }
