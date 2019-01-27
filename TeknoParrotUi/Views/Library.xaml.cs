using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for Library.xaml
    /// </summary>
    public partial class Library : UserControl
    {

        public UserControls.JoystickControl joystick = new UserControls.JoystickControl();
        public Library()
        {
            InitializeComponent();
            BitmapImage imageBitmap = new BitmapImage(new Uri("pack://application:,,,/TeknoParrotUi;component/Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Absolute));
            
                image1.Source = imageBitmap;
            
        }
        
        
        List<GameProfile> gameNames = new List<GameProfile>();
        UserControls.GameSettingsControl gameSettings = new UserControls.GameSettingsControl();

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            
            var modifyItem = (ListBoxItem)((ListBox)sender).SelectedItem;
            var profile = gameNames[gameList.SelectedIndex];
            var icon = profile.IconName;
            BitmapImage imageBitmap = new BitmapImage(File.Exists(icon) ? new Uri("..\\" + icon, UriKind.Relative) : new Uri("../Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));
            image1.Source = imageBitmap;
            
            gameSettings.LoadNewSettings(profile, modifyItem);
            joystick.LoadNewSettings(profile, modifyItem, MainWindow._parrotData);
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
        private void listUpdate()
        {
            gameList.Items.Clear();
            foreach (var gameProfile in GameProfileLoader.UserProfiles)
            {
                ListBoxItem item = new ListBoxItem
                {
                    Content = gameProfile.GameName,
                    Tag = gameProfile
                };
                gameNames.Add(gameProfile);
                gameList.Items.Add(item);

                if (MainWindow._parrotData.SaveLastPlayed && gameProfile.GameName == MainWindow._parrotData.LastPlayed)
                {
                    gameList.SelectedItem = item;
                }
                else
                {
                    gameList.SelectedIndex = 0;
                }
            }
            if (gameList.Items.Count == 0)
            {
                if (MessageBox.Show("Looks like you have no games set up. Do you want to add one now?", "No games found", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    Application.Current.Windows.OfType<MainWindow>().SingleOrDefault(x => x.IsActive).contentControl.Content = new AddGame();
                }
            }

        }
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Application.Current.Windows.OfType<MainWindow>().SingleOrDefault(x => x.IsActive).LoadParrotData();
            //CreateConfigValue();

            /*foreach (var gameProfile in GameProfileLoader.UserProfiles)
            {
                ListBoxItem item = new ListBoxItem
                {
                    Content = gameProfile.GameName,
                    Tag = gameProfile
                };
                gameNames.Add(gameProfile);
                gameList.Items.Add(item);

                if (MainWindow._parrotData.SaveLastPlayed && gameProfile.GameName == MainWindow._parrotData.LastPlayed)
                {
                    gameList.SelectedItem = item;
                }
                else
                {
                    gameList.SelectedIndex = 0;
                }
            }*/
            listUpdate();

           

            
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
            game.ConfigValues = new List<FieldInformation> { x1, x2, x3, x4, f1, f2, f3, f4, f5, f6, f7, f8, f9 };
            game.FileName = "test.xml";
            JoystickHelper.SerializeGameProfile(game);
        }

        

        private void BtnStartGame(object sender, RoutedEventArgs e)
        {
            if (gameList.Items.Count == 0)
                return;

            var gameProfile = (GameProfile)((ListBoxItem)gameList.SelectedItem).Tag;

            if (MainWindow._parrotData.SaveLastPlayed)
            {
                MainWindow._parrotData.LastPlayed = gameProfile.GameName;
                JoystickHelper.Serialize(MainWindow._parrotData);
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
        private void ValidateAndRun(GameProfile gameProfile, string testMenuString, string exeName = "")
        {
            if (!ValidateGameRun(gameProfile))
                return;

            var testMenu = ChkTestMenu.IsChecked;

            var gameRunning = new TeknoParrotUi.Views.GameRunningUC(gameProfile, testMenu, MainWindow._parrotData, testMenuString, gameProfile.TestMenuIsExecutable, exeName);
            Application.Current.Windows.OfType<MainWindow>().SingleOrDefault(x => x.IsActive).contentControl.Content = gameRunning;
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
            "BudgieLoader.exe"
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

            if (File.Exists(System.IO.Path.Combine(gameProfile.GamePath, "iDmacDrv32.dll")))
            {
                var description = FileVersionInfo.GetVersionInfo("iDmacDrv32.dll");
                if (description.FileDescription != "PCI-Express iDMAC Driver Library (DLL)")
                {
                    return (MessageBox.Show("You seem to be using an unofficial iDmacDrv32.dll file! This game may crash or be unstable. Continue?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes);
                }
            }

            return true;
        }



        /*private void GameListListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var modifyItem = (ListBoxItem)((ListBox)sender).SelectedItem;
            var profile = (GameProfile)((ListBoxItem)((ListBox)sender).SelectedItem).Tag;
            var icon = profile.IconName;
            BitmapImage imageBitmap = new BitmapImage(File.Exists(icon) ? new Uri(icon, UriKind.Relative) : new Uri("Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));
            MainLogo.Source = imageBitmap;
            GameSettingsControl.LoadNewSettings(profile, modifyItem);
            JoystickControl.LoadNewSettings(profile, modifyItem, MainWindow._parrotData);
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
        }*/

        private void BtnGameSettings(object sender, RoutedEventArgs e)
        {
            
        }
        

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            
            Application.Current.Windows.OfType<MainWindow>().SingleOrDefault(x => x.IsActive).contentControl.Content = gameSettings;

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            joystick.Listen();
            Application.Current.Windows.OfType<MainWindow>().SingleOrDefault(x => x.IsActive).contentControl.Content = joystick;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (gameList.Items.Count == 0)
                return;

            var gameProfile = (GameProfile)((ListBoxItem)gameList.SelectedItem).Tag;

            if (MainWindow._parrotData.SaveLastPlayed)
            {
                MainWindow._parrotData.LastPlayed = gameProfile.GameName;
                JoystickHelper.Serialize(MainWindow._parrotData);
            }

            var testMenuExe = gameProfile.TestMenuIsExecutable ? gameProfile.TestMenuParameter : "";

            var testStr = gameProfile.TestMenuIsExecutable ? gameProfile.TestMenuExtraParameters : gameProfile.TestMenuParameter;

            ValidateAndRun(gameProfile, testStr, testMenuExe);
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            Application.Current.Windows.OfType<MainWindow>().SingleOrDefault(x => x.IsActive).contentControl.Content = new VerifyGame(gameNames[gameList.SelectedIndex].GamePath, gameNames[gameList.SelectedIndex].ValidMd5);
        }
        /*private void FlyoutSettings_OnIsOpenChanged(object sender, RoutedEventArgs e)
{
if (FlyoutSettings.IsOpen)
{

joystick.Listen();
}
else
{
joystick.StopListening();
}
}*/


    }
}
