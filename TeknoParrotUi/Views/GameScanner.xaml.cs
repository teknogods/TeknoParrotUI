using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TeknoParrotUi.Common;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using Path = System.IO.Path;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for GameScanner.xaml
    /// </summary>
    public partial class GameScanner : UserControl
    {
        public GameScanner(Library library, ContentControl contentControl)
        {
            InitializeComponent();
            _library = library;
            _contentControl = contentControl;
        }

        private readonly List<string> _foundGameIds = new List<string>();
        private List<GameSetupContainer> _gameSetupContainers = new List<GameSetupContainer>();
        private string romDir = string.Empty;
        private readonly Library _library;
        private ContentControl _contentControl;

        private void LogTextBox(string log, bool initialize = false)
        {
            if (initialize)
                ScannerText.Text = "";
            ScannerText.Text += log + Environment.NewLine;
            ScannerText.Select(ScannerText.Text.Length, 1);
            MyScrollViewer.ScrollToBottom();
        }

        static async Task<string> CalculateMd5Async(string filename)
        {
            if (!System.IO.File.Exists(filename))
            {
                Trace.WriteLine("Couldn't find: " + filename);
                return null;
            }

            if (filename.Contains("teknoparrot.ini"))
            {
                return null;
            }
            using (var md5 = MD5.Create())
            {
                using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true)
                      ) // true means use IO async operations
                {
                    // Let's use a big buffer size to speed up checking on games like IDAC where some files are HUGE
                    byte[] buffer = new byte[81920];
                    int bytesRead;
                    do
                    {
                        bytesRead = await stream.ReadAsync(buffer, 0, 81920);
                        if (bytesRead > 0)
                        {
                            md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                        }
                    } while (bytesRead > 0);

                    md5.TransformFinalBlock(buffer, 0, 0);
                    return BitConverter.ToString(md5.Hash).Replace("-", "").ToLowerInvariant();

                }
            }
        }

        private void BrowseClick(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    if (Directory.Exists(dialog.SelectedPath))
                    {
                        FolderLocation.Text = dialog.SelectedPath;
                    }
                }
            }
        }

        private void ScanDir(string scanDir)
        {
            if (Directory.Exists(scanDir))
            {
                _gameSetupContainers.Clear();
                _foundGameIds.Clear();
                LogTextBox("Scanning romset", true);
                // Scan roms
                var gameSetupFiles = Directory.GetFiles("GameSetup\\", "*.xml");
                foreach (var gameSetupFile in gameSetupFiles)
                {
                    var gameSetup = JoystickHelper.DeSerializeGameSetup(gameSetupFile);
                    bool foundExe = false;
                    bool foundTest = false;
                    bool foundExe2 = false;
                    var gameId = gameSetupFile.Replace("GameSetup\\", "").Replace(".xml", "");
                    if (!string.IsNullOrWhiteSpace(gameSetup.GameExecutableLocation))
                    {
                        if (File.Exists(Path.Combine(scanDir, gameId, gameSetup.GameExecutableLocation)))
                        {
                            foundExe = true;
                        }
                    }
                    else
                    {
                        foundExe = true;
                    }

                    if (!string.IsNullOrWhiteSpace(gameSetup.GameExecutableLocation2))
                    {
                        if (File.Exists(Path.Combine(scanDir, gameId, gameSetup.GameExecutableLocation2)))
                        {
                            foundExe2 = true;
                        }
                    }
                    else
                    {
                        foundExe2 = true;
                    }

                    if (!string.IsNullOrWhiteSpace(gameSetup.GameTestExecutableLocation))
                    {
                        if (File.Exists(Path.Combine(scanDir, gameId, gameSetup.GameTestExecutableLocation)))
                        {
                            foundTest = true;
                        }
                    }
                    else
                    {
                        foundTest = true;
                    }

                    if (foundExe && foundTest && foundExe2)
                    {
                        // Get game proper name from description
                        GameSetupContainer setup = new GameSetupContainer();
                        setup.GameId = gameId;
                        setup.GameSetupData = gameSetup;
                        _gameSetupContainers.Add(setup);
                        var metaData = JoystickHelper.DeSerializeMetadata(gameId);
                        LogTextBox($"Found: {metaData.game_name} ({metaData.platform})");
                        _foundGameIds.Add(gameId);
                    }
                }

                romDir = scanDir;
                LogTextBox("Scan complete, click save to premake the user profiles.");
            }
        }

        private void ScanClick(object sender, RoutedEventArgs e)
        {
            ScanDir(FolderLocation.Text);
        }

        private async void VerifyClick(object sender, RoutedEventArgs e)
        {
            var invalidFiles = new List<string>();

            foreach (var foundGameId in _foundGameIds)
            {
                var _md5S = File.ReadAllLines(Path.Combine("MD5\\", foundGameId + ".md5")).Where(l => !l.Trim().StartsWith(";")).ToList();
                var _total = _md5S.Count;
                var gamePath = Path.Combine(romDir, foundGameId);
                var _current = 0;
                foreach (var t in _md5S)
                {
                    //if (_cancel)
                    //{
                    //    break;
                    //}
                    Trace.WriteLine("Scanning " + t);
                    var temp = t.Split(new[] { ' ' }, 2);
                    var fileToCheck = temp[1].Replace("*", "");
                    var tempMd5 =
                        await CalculateMd5Async(Path.Combine(gamePath ?? throw new InvalidOperationException(),
                            fileToCheck)).ConfigureAwait(true);
                    if (tempMd5 != temp[0])
                    {
                        invalidFiles.Add(fileToCheck);
                        LogTextBox($"{Properties.Resources.VerifyInvalid}: {fileToCheck}");
                        //listBoxFiles.Items.Add($"{Properties.Resources.VerifyInvalid}: {fileToCheck}");
                        //listBoxFiles.SelectedIndex = listBoxFiles.Items.Count - 1;
                        //listBoxFiles.ScrollIntoView(listBoxFiles.SelectedItem);
                        var first = _current / _total;
                        var calc = first * 100;
                        //progressBar1.Dispatcher.Invoke(() => progressBar1.Value = calc,
                        //    System.Windows.Threading.DispatcherPriority.Background);
                    }
                    else
                    {
                        LogTextBox($"{Properties.Resources.VerifyValid}: {fileToCheck}");
                        //listBoxFiles.Items.Add($"{Properties.Resources.VerifyValid}: {fileToCheck}");
                        //listBoxFiles.SelectedIndex = listBoxFiles.Items.Count - 1;
                        //listBoxFiles.ScrollIntoView(listBoxFiles.SelectedItem);
                        var first = _current / _total;
                        var calc = first * 100;
                        //progressBar1.Dispatcher.Invoke(() => progressBar1.Value = calc,
                        //    System.Windows.Threading.DispatcherPriority.Background);
                    }

                    _current++;
                }
            }
        }

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            var userProfiles = Directory.GetFiles("UserProfiles\\", "*.xml");
            for (int i = 0; i < _foundGameIds.Count; i++)
            {
                var foundIt = userProfiles.FirstOrDefault(x =>
                    x.Replace("UserProfiles\\", "").Replace(".xml", "") == _foundGameIds[i]);
                if (foundIt != null)
                {
                    //Trace.WriteLine($"Won't add game: {_foundGameIds[i]}, already configured!");
                    LogTextBox($"Won't add game: {_foundGameIds[i]}, already configured!");
                }
                else
                {
                    var gameSetup = _gameSetupContainers.FirstOrDefault(x => x.GameId == _foundGameIds[i]);
                    if (gameSetup == null)
                    {
                        LogTextBox($"Won't add game: {_foundGameIds[i]}, setup missing?!");
                    }
                    else
                    {
                        var deSerializeIt = JoystickHelper.DeSerializeGameProfile($"GameProfiles\\{_foundGameIds[i]}.xml", false);
                        if(!string.IsNullOrWhiteSpace(gameSetup.GameSetupData.GameExecutableLocation))
                            deSerializeIt.GamePath = Path.Combine(romDir, _foundGameIds[i], gameSetup.GameSetupData.GameExecutableLocation);
                        if (!string.IsNullOrWhiteSpace(gameSetup.GameSetupData.GameExecutableLocation2))
                            deSerializeIt.GamePath2 = Path.Combine(romDir, _foundGameIds[i], gameSetup.GameSetupData.GameExecutableLocation2);
                        JoystickHelper.SerializeGameProfile(deSerializeIt);
                        LogTextBox($"Configured game: {_foundGameIds[i]} succesfully!");
                    }
                }
            }

            MessageBox.Show("Complete!");

            _library.ListUpdate();

            _contentControl.Content = _library;
        }
    }
}
