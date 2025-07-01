using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Properties;
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
        private DatXmlParser.DatFile _datFile;
        private Dictionary<string, string> _gameDirectories = new Dictionary<string, string>();

        private void LogTextBox(string log, bool initialize = false)
        {
            // Use Dispatcher to ensure we're on the UI thread
            Application.Current.Dispatcher.Invoke(() => 
            {
                if (initialize)
                    ScannerText.Text = "";
                
                ScannerText.Text += log + Environment.NewLine;
                ScannerText.Select(ScannerText.Text.Length, 1);
                MyScrollViewer.ScrollToBottom();
                
                // Force the UI to update immediately
                ScannerText.UpdateLayout();
                MyScrollViewer.UpdateLayout();
                
                // Process all pending UI events
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                    new Action(delegate { }));
            });
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
                LogTextBox(TeknoParrotUi.Properties.Resources.GameScannerScanningRomset, true);
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
                LogTextBox(TeknoParrotUi.Properties.Resources.GameScannerScanComplete);
            }
        }

        private async void VerifyClick(object sender, RoutedEventArgs e)
        {
            if (_datFile != null && _foundGameIds.Count > 0)
            {
                LogTextBox(TeknoParrotUi.Properties.Resources.GameScannerVerifyingROMs, true);
                
                foreach (var gameId in _foundGameIds)
                {
                    var game = _datFile.Games.Find(g => g.GameProfile == gameId);
                    if (game == null) continue;
                    
                    string gameDir = FindGameDirectory(romDir, game);
                    if (gameDir == null) continue;
                    
                    LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerVerifyingGame, game.Name, gameId));
                    await Task.Run(() => VerifyRomsFromDat(gameDir, game.Roms));
                }
            }
            else
            {
                // Fall back to original verification method
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
                            LogTextBox($"{TeknoParrotUi.Properties.Resources.VerifyInvalid}: {fileToCheck}");
                            //listBoxFiles.Items.Add($"{TeknoParrotUi.Properties.Resources.VerifyInvalid}: {fileToCheck}");
                            //listBoxFiles.SelectedIndex = listBoxFiles.Items.Count - 1;
                            //listBoxFiles.ScrollIntoView(listBoxFiles.SelectedItem);
                            var first = _current / _total;
                            var calc = first * 100;
                            //progressBar1.Dispatcher.Invoke(() => progressBar1.Value = calc,
                            //    System.Windows.Threading.DispatcherPriority.Background);
                        }
                        else
                        {
                            LogTextBox($"{TeknoParrotUi.Properties.Resources.VerifyValid}: {fileToCheck}");
                            //listBoxFiles.Items.Add($"{TeknoParrotUi.Properties.Resources.VerifyValid}: {fileToCheck}");
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
                    LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerWontAddAlreadyConfigured, _foundGameIds[i]));
                }
                else
                {
                    var gameSetup = _gameSetupContainers.FirstOrDefault(x => x.GameId == _foundGameIds[i]);
                    if (gameSetup == null)
                    {
                        LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerWontAddSetupMissing, _foundGameIds[i]));
                    }
                    else
                    {
                        var deSerializeIt = JoystickHelper.DeSerializeGameProfile($"GameProfiles\\{_foundGameIds[i]}.xml", false);
                        
                        // Find the actual game directory - use FindGameDirectory if it's available
                        string gameDir = null;
                        
                        if (_datFile != null)
                        {
                            var game = _datFile.Games.Find(g => g.GameProfile == _foundGameIds[i]);
                            if (game != null)
                            {
                                gameDir = FindGameDirectory(romDir, game);
                            }
                        }
                        
                        // If we couldn't find from DAT, default back to traditional folder structure
                        if (string.IsNullOrEmpty(gameDir))
                        {
                            gameDir = Path.Combine(romDir, _foundGameIds[i]);
                        }
                        
                        if (!Directory.Exists(gameDir))
                        {
                            LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerWarningDirectoryNotFound, _foundGameIds[i]));
                            continue;
                        }
                        
                        if (!string.IsNullOrWhiteSpace(gameSetup.GameSetupData.GameExecutableLocation))
                        {
                            string exePath = Path.Combine(gameDir, gameSetup.GameSetupData.GameExecutableLocation);
                            if (File.Exists(exePath))
                                deSerializeIt.GamePath = exePath;
                            else
                                LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerWarningExecutableNotFound, exePath));
                        }
                        
                        if (!string.IsNullOrWhiteSpace(gameSetup.GameSetupData.GameExecutableLocation2))
                        {
                            string exe2Path = Path.Combine(gameDir, gameSetup.GameSetupData.GameExecutableLocation2);
                            if (File.Exists(exe2Path))
                                deSerializeIt.GamePath2 = exe2Path;
                            else
                                LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerWarningExecutable2NotFound, exe2Path));
                        }
                        
                        JoystickHelper.SerializeGameProfile(deSerializeIt);
                        LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerConfiguredSuccessfully, _foundGameIds[i]));
                    }
                }
            }

            MessageBox.Show(TeknoParrotUi.Properties.Resources.GameScannerComplete);
            _library.ListUpdate();
            _contentControl.Content = _library;
        }

        // Update ScanWithDatClick method:
        private void ScanWithDatClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Lazydata.ParrotData.DatXmlLocation) || !File.Exists(Lazydata.ParrotData.DatXmlLocation))
            {
                LogTextBox(TeknoParrotUi.Properties.Resources.GameScannerNoDATFileConfigured, true);
                return;
            }

            if (string.IsNullOrWhiteSpace(FolderLocation.Text) || !Directory.Exists(FolderLocation.Text))
            {
                LogTextBox(TeknoParrotUi.Properties.Resources.GameScannerSelectValidROMFolder, true);
                return;
            }

            LogTextBox(TeknoParrotUi.Properties.Resources.GameScannerStartingScanWithDAT, true);
            romDir = FolderLocation.Text;
            _foundGameIds.Clear();
            _gameSetupContainers.Clear();

            // Get file size to determine scanning approach
            FileInfo fileInfo = new FileInfo(Lazydata.ParrotData.DatXmlLocation);
            if (fileInfo.Length > 100 * 1024 * 1024) // If larger than 100 MB
            {
                // For large files, use the streaming parser with callbacks
                DatXmlParser.ProcessDatFileStreaming(
                    Lazydata.ParrotData.DatXmlLocation,
                    header => { /* We already have the header */ },
                    game => ProcessGameFromDat(game)
                );
            }
            else if (_datFile != null && _datFile.Games.Count > 0)
            {
                // For smaller files, iterate through the games list
                foreach (var game in _datFile.Games)
                {
                    ProcessGameFromDat(game);
                }
            }
            else
            {
                // Load the DAT file if _datFile is null
                try
                {
                    _datFile = DatXmlParser.ParseDatFile(Lazydata.ParrotData.DatXmlLocation);
                    string fileName = _datFile.Header?.Name ?? Path.GetFileName(Lazydata.ParrotData.DatXmlLocation);
                    LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerLoadedDATFile, fileName));
                    
                    // Process the games
                    foreach (var game in _datFile.Games)
                    {
                        ProcessGameFromDat(game);
                    }
                }
                catch (Exception ex)
                {
                    LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerErrorLoadingDAT, ex.Message), true);
                    return;
                }
            }
            
            LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerScanCompleteFoundGames, _foundGameIds.Count));
        }

        // Update ScanDirByRomName method to use Lazydata.ParrotData.DatXmlLocation
        private void ScanDirByRomName(string scanDir)
        {
            if (!Directory.Exists(scanDir))
            {
                LogTextBox(TeknoParrotUi.Properties.Resources.GameScannerDirectoryNotExist, true);
                return;
            }

            _gameSetupContainers.Clear();
            _foundGameIds.Clear();
            _gameDirectories.Clear();
            LogTextBox(TeknoParrotUi.Properties.Resources.GameScannerScanningForROMFiles, true);
            
            // Get all game setups
            var gameSetupFiles = Directory.GetFiles("GameSetup\\", "*.xml");
            var gameSetups = new Dictionary<string, Tuple<string, GameSetup>>();
            
            foreach (var gameSetupFile in gameSetupFiles)
            {
                var gameId = gameSetupFile.Replace("GameSetup\\", "").Replace(".xml", "");
                var gameSetup = JoystickHelper.DeSerializeGameSetup(gameSetupFile);
                if (gameSetup != null)
                {
                    gameSetups.Add(gameId, new Tuple<string, GameSetup>(gameSetupFile, gameSetup));
                }
            }
            
            // Create mappings for both ROM filenames and game names
            Dictionary<string, HashSet<string>> romNameToGameIds = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> gameNameToGameId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> gameIdToOriginalName = new Dictionary<string, string>();
            Dictionary<string, string> sanitizedGameNameToGameId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            // Load directly from XML file instead of relying on _datFile which might not be fully populated
            if (File.Exists(Lazydata.ParrotData.DatXmlLocation))
            {
                LogTextBox(TeknoParrotUi.Properties.Resources.GameScannerLoadingGameNames);
                
                // Get file size to determine parsing approach
                FileInfo fileInfo = new FileInfo(Lazydata.ParrotData.DatXmlLocation);
                if (fileInfo.Length > 100 * 1024 * 1024) // If larger than 100 MB
                {
                    // For large files, use streaming parser with callbacks to build our dictionaries
                    int gameCount = 0;
                    DatXmlParser.ProcessDatFileStreaming(
                        Lazydata.ParrotData.DatXmlLocation,
                        header => { /* Ignore header */ },
                        game => 
                        {
                            gameCount++;
                            
                            // Process each game to build our dictionaries
                            if (!string.IsNullOrWhiteSpace(game.GameProfile) && gameSetups.ContainsKey(game.GameProfile))
                            {
                                // Store original game name to GameID mapping
                                if (!string.IsNullOrEmpty(game.Name))
                                {
                                    // Store the original unmodified game name
                                    gameNameToGameId[game.Name] = game.GameProfile;
                                    gameIdToOriginalName[game.GameProfile] = game.Name;
                                    
                                    // Also create a sanitized version for matching
                                    string sanitized = SanitizeDirectoryName(game.Name).ToLowerInvariant();
                                    if (!string.IsNullOrEmpty(sanitized))
                                    {
                                        sanitizedGameNameToGameId[sanitized] = game.GameProfile;
                                    }
                                }
                                    
                                // Map ROM files to game IDs
                                foreach (var rom in game.Roms)
                                {
                                    if (string.IsNullOrEmpty(rom.Name) || rom.Name.EndsWith("/"))
                                        continue; // Skip directories
                                    
                                    string romFileName = rom.Name;
                                    if (romFileName.Contains("/"))
                                    {
                                        romFileName = romFileName.Substring(romFileName.LastIndexOf('/') + 1);
                                    }
                                    
                                    if (!string.IsNullOrEmpty(romFileName))
                                    {
                                        if (!romNameToGameIds.ContainsKey(romFileName))
                                        {
                                            romNameToGameIds[romFileName] = new HashSet<string>();
                                        }
                                        romNameToGameIds[romFileName].Add(game.GameProfile);
                                    }
                                }
                            }
                            
                            // Show progress for large files
                            if (gameCount % 500 == 0)
                            {
                                LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerProcessedGames, gameCount));
                            }
                        }
                    );
                }
                else if (_datFile != null && _datFile.Games.Count > 0)
                {
                    // For smaller files, use the already loaded _datFile
                    foreach (var game in _datFile.Games)
                    {
                        if (string.IsNullOrWhiteSpace(game.GameProfile))
                            continue;
                            
                        // Avoid duplicate game profiles that don't exist in our setup
                        if (!gameSetups.ContainsKey(game.GameProfile))
                            continue;
                            
                        // Store original game name to GameID mapping
                        if (!string.IsNullOrEmpty(game.Name))
                        {
                            // Store the original game name
                            gameNameToGameId[game.Name] = game.GameProfile;
                            gameIdToOriginalName[game.GameProfile] = game.Name;
                            
                            // Also create a sanitized version for matching
                            string sanitized = SanitizeDirectoryName(game.Name).ToLowerInvariant();
                            if (!string.IsNullOrEmpty(sanitized))
                            {
                                sanitizedGameNameToGameId[sanitized] = game.GameProfile;
                            }
                        }
                            
                        // Map ROM files to game IDs
                        foreach (var rom in game.Roms)
                        {
                            if (string.IsNullOrEmpty(rom.Name) || rom.Name.EndsWith("/"))
                                continue; // Skip directories
                            
                            string romFileName = rom.Name;
                            if (romFileName.Contains("/"))
                            {
                                romFileName = romFileName.Substring(romFileName.LastIndexOf('/') + 1);
                            }
                            
                            if (!string.IsNullOrEmpty(romFileName))
                            {
                                if (!romNameToGameIds.ContainsKey(romFileName))
                                {
                                    romNameToGameIds[romFileName] = new HashSet<string>();
                                }
                                romNameToGameIds[romFileName].Add(game.GameProfile);
                            }
                        }
                    }
                }
            }
            
            // Dictionary to keep track of found locations for each GameID
            Dictionary<string, string> gameIdToFolderPath = new Dictionary<string, string>();
            Dictionary<string, HashSet<string>> gameIdToRomsFound = new Dictionary<string, HashSet<string>>();
            
            LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerPreparedForMatching, gameNameToGameId.Count, romNameToGameIds.Count));
            
            // STEP 1: First check for direct folder name matches with game names
            foreach (var directory in Directory.GetDirectories(scanDir, "*", SearchOption.TopDirectoryOnly))
            {
                string folderName = Path.GetFileName(directory);
                
                // Check for exact match with full game name (highest priority)
                if (gameNameToGameId.TryGetValue(folderName, out string gameId))
                {
                    LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerFoundExactMatch, folderName, gameId));
                    gameIdToFolderPath[gameId] = directory;
                    if (!gameIdToRomsFound.ContainsKey(gameId))
                        gameIdToRomsFound[gameId] = new HashSet<string>();
                    continue; // Skip other checks if we found an exact match
                }
                
                // Try with sanitized folder name (lower priority)
                string sanitizedFolderName = SanitizeDirectoryName(folderName).ToLowerInvariant();
                if (sanitizedGameNameToGameId.TryGetValue(sanitizedFolderName, out gameId))
                {
                    LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerFoundSanitizedMatch, folderName, gameId, gameIdToOriginalName[gameId]));
                    gameIdToFolderPath[gameId] = directory;
                    if (!gameIdToRomsFound.ContainsKey(gameId))
                        gameIdToRomsFound[gameId] = new HashSet<string>();
                    continue; // Skip other checks if we found a sanitized match
                }
                
                // Try partial matches if no exact match (lowest priority)
                if (!string.IsNullOrEmpty(sanitizedFolderName))
                {
                    bool foundMatch = false;
                    
                    // First try exact game name partial matches
                    foreach (var kvp in gameNameToGameId)
                    {
                        if (kvp.Key.IndexOf(folderName, StringComparison.OrdinalIgnoreCase) >= 0 || 
                            folderName.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerFoundPartialMatch, folderName, kvp.Value, kvp.Key));
                            gameIdToFolderPath[kvp.Value] = directory;
                            if (!gameIdToRomsFound.ContainsKey(kvp.Value))
                                gameIdToRomsFound[kvp.Value] = new HashSet<string>();
                            foundMatch = true;
                            break;
                        }
                    }
                    
                    // If no match yet, try sanitized name partial matches
                    if (!foundMatch)
                    {
                        foreach (var kvp in sanitizedGameNameToGameId)
                        {
                            // Check if sanitized folder name contains or is contained by sanitized game name
                            if (sanitizedFolderName.Contains(kvp.Key) || kvp.Key.Contains(sanitizedFolderName))
                            {
                                string originalName = gameIdToOriginalName.ContainsKey(kvp.Value) 
                                    ? gameIdToOriginalName[kvp.Value] 
                                    : kvp.Value;
                                    
                                LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerFoundPartialSanitizedMatch, folderName, kvp.Value, originalName));
                                gameIdToFolderPath[kvp.Value] = directory;
                                if (!gameIdToRomsFound.ContainsKey(kvp.Value))
                                    gameIdToRomsFound[kvp.Value] = new HashSet<string>();
                                break; // Take first match to avoid duplicates
                            }
                        }
                    }
                }
            }
            
            // STEP 2: Now look for ROM files to confirm or find additional matches
            foreach (var directory in Directory.EnumerateDirectories(scanDir, "*", SearchOption.AllDirectories))
            {
                // Skip directories that are too deep (to prevent excessive scanning)
                if (directory.Split(Path.DirectorySeparatorChar).Length > 10)
                {
                    continue;
                }
                
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    string fileName = Path.GetFileName(file);
                    
                    // Check if this file is a known ROM
                    if (romNameToGameIds.ContainsKey(fileName))
                    {
                        foreach (var romGameId in romNameToGameIds[fileName])
                        {
                            if (!gameIdToFolderPath.ContainsKey(romGameId))
                            {
                                gameIdToFolderPath[romGameId] = directory;
                                gameIdToRomsFound[romGameId] = new HashSet<string>();
                            }
                            
                            gameIdToRomsFound[romGameId].Add(fileName);
                        }
                    }
                    
                    // Also check executable names
                    foreach (var kvp in gameSetups)
                    {
                        string setupGameId = kvp.Key;
                        var gameSetup = kvp.Value.Item2;
                        
                        bool isExecutable = false;
                        
                        if (!string.IsNullOrWhiteSpace(gameSetup.GameExecutableLocation) && 
                            Path.GetFileName(gameSetup.GameExecutableLocation).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            isExecutable = true;
                        }
                        else if (!string.IsNullOrWhiteSpace(gameSetup.GameExecutableLocation2) && 
                            Path.GetFileName(gameSetup.GameExecutableLocation2).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            isExecutable = true;
                        }
                        else if (!string.IsNullOrWhiteSpace(gameSetup.GameTestExecutableLocation) && 
                            Path.GetFileName(gameSetup.GameTestExecutableLocation).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            isExecutable = true;
                        }
                        
                        if (isExecutable)
                        {
                            if (!gameIdToFolderPath.ContainsKey(setupGameId))
                            {
                                gameIdToFolderPath[setupGameId] = directory;
                                gameIdToRomsFound[setupGameId] = new HashSet<string>();
                            }
                            
                            gameIdToRomsFound[setupGameId].Add(fileName);
                        }
                    }
                }
            }
            
            // Now process all the found games
            int foundGames = 0;
            foreach (var gameId in gameIdToFolderPath.Keys)
            {
                // Skip if we don't have a corresponding game setup
                if (!gameSetups.ContainsKey(gameId))
                    continue;
                    
                var gameSetupTuple = gameSetups[gameId];
                var gameSetup = gameSetupTuple.Item2;
                
                // Store the located directory in our global dictionary
                _gameDirectories[gameId] = gameIdToFolderPath[gameId];
                
                // Add to our found games
                GameSetupContainer setup = new GameSetupContainer
                {
                    GameId = gameId,
                    GameSetupData = gameSetup
                };
                _gameSetupContainers.Add(setup);
                _foundGameIds.Add(gameId);
                foundGames++;
                
                var metaData = JoystickHelper.DeSerializeMetadata(gameId);
                
                // Display the original game name from DAT if available
                string displayName = gameIdToOriginalName.ContainsKey(gameId) ? 
                    gameIdToOriginalName[gameId] : 
                    (metaData?.game_name ?? gameId);
                    
                LogTextBox($"Found: {displayName} ({metaData?.platform ?? "Unknown"}) in {gameIdToFolderPath[gameId]}");
            }

            romDir = scanDir;
            LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerScanCompleteFoundGames, foundGames));
        }

        // Add this helper method to process each game entry
        private void ProcessGameFromDat(DatXmlParser.DatGame game)
        {
            // Skip entries that don't have a GameProfile
            if (string.IsNullOrWhiteSpace(game.GameProfile))
                return;

            // Try to find the game directory
            string gameDir = FindGameDirectory(romDir, game);
            
            if (gameDir == null)
            {
                LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerCouldNotFindDirectory, game.Name));
                return;
            }

            // Try to get the game profile
            try
            {
                var gameId = game.GameProfile;
                var gameSetupPath = $"GameSetup\\{gameId}.xml";
                
                // Check if game setup exists
                if (!File.Exists(gameSetupPath))
                {
                    LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerGameSetupNotFound, gameSetupPath, game.Name));
                    return;
                }
                
                var gameSetup = JoystickHelper.DeSerializeGameSetup(gameSetupPath);
                if (gameSetup == null)
                {
                    LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerFailedDeserialize, game.Name));
                    return;
                }
                
                GameSetupContainer setup = new GameSetupContainer
                {
                    GameId = gameId,
                    GameSetupData = gameSetup
                };
                
                _gameSetupContainers.Add(setup);
                _foundGameIds.Add(gameId);
                
                var metaData = JoystickHelper.DeSerializeMetadata(gameId);
                string gameName = metaData?.game_name ?? game.Name;
                string platform = metaData?.platform ?? "Unknown";
                
                LogTextBox($"Found: {gameName} ({platform})");
            }
            catch (Exception ex)
            {
                LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerErrorProcessingGame, game.Name, ex.Message));
            }
        }

        // Method to find the game directory based on profile or ROM name
        private string FindGameDirectory(string baseDir, DatXmlParser.DatGame game)
        {
            // 1. First try exact match with GameProfile name
            string profileDir = Path.Combine(baseDir, game.GameProfile);
            if (Directory.Exists(profileDir))
                return profileDir;
            
            // 2. Try a sanitized version of the game name
            string sanitizedName = SanitizeDirectoryName(game.Name);
            string nameDir = Path.Combine(baseDir, sanitizedName);
            if (Directory.Exists(nameDir))
                return nameDir;
            
            //// 3. Check if there's a directory containing the executable path
            //if (!string.IsNullOrEmpty(game.Executable))
            //{
            //    string exePath = game.Executable.TrimStart('\\', '/');
            //    foreach (var dir in Directory.GetDirectories(baseDir))
            //    {
            //        if (File.Exists(Path.Combine(dir, exePath)))
            //            return dir;
            //    }
            //}
            
            //// 4. Look for a directory with a similar name to GameProfile
            //foreach (var dir in Directory.GetDirectories(baseDir))
            //{
            //    string dirName = Path.GetFileName(dir).ToLowerInvariant();
            //    if (dirName.Contains(game.GameProfile.ToLowerInvariant()))
            //        return dir;
            //}
            
            //// 5. Look for the first ROM file in any directory as a fallback
            //if (game.Roms.Count > 0)
            //{
            //    foreach (var dir in Directory.GetDirectories(baseDir))
            //    {
            //        foreach (var rom in game.Roms)
            //        {
            //            if (string.IsNullOrEmpty(rom.Name) || rom.Name.EndsWith("/"))
            //                continue;
                            
            //            string romPath = rom.Name;
            //            if (romPath.Contains("/"))
            //                romPath = romPath.Substring(romPath.LastIndexOf('/') + 1);
                            
            //            if (File.Exists(Path.Combine(dir, romPath)))
            //                return dir;
            //        }
            //    }
            //}
            
            return null;
        }

        private string SanitizeDirectoryName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
                
            // Remove anything in parentheses or brackets
            name = Regex.Replace(name, @"\(.*?\)", "");
            name = Regex.Replace(name, @"\[.*?\]", "");
            
            // Remove illegal characters
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            
            return name.Trim();
        }

        private async void VerifyRomsFromDat(string gameDir, List<DatXmlParser.DatRom> roms)
        {
            LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerVerifyingFiles, gameDir));
            int verified = 0;
            int mismatched = 0;
            int missing = 0;
            
            foreach (var rom in roms)
            {
                if (string.IsNullOrEmpty(rom.Name) || rom.Name.EndsWith("/"))
                    continue; // Skip directories
                    
                string filePath = Path.Combine(gameDir, rom.Name);
                
                if (!File.Exists(filePath))
                {
                    LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerMissing, rom.Name));
                    missing++;
                    continue;
                }
                
                // Calculate MD5 if provided in DAT
                if (!string.IsNullOrEmpty(rom.Md5))
                {
                    string calculatedMd5 = await CalculateMd5Async(filePath);
                    if (calculatedMd5 != null && calculatedMd5.Equals(rom.Md5, StringComparison.OrdinalIgnoreCase))
                    {
                        LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerVerified, rom.Name));
                        verified++;
                    }
                    else
                    {
                        LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerChecksumMismatch, rom.Name, rom.Md5, calculatedMd5));
                        mismatched++;
                    }
                }
                // Use CRC if no MD5
                else if (!string.IsNullOrEmpty(rom.Crc))
                {
                    // You could implement CRC calculation here
                    // For now, just count as verified
                    LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerAssumedOK, rom.Name));
                    verified++;
                }
                else
                {
                    // No checksum to verify
                    LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerPresentNotVerified, rom.Name));
                    verified++;
                }
            }
            
            LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerVerificationComplete, verified, mismatched, missing));
        }

        /// <summary>
        /// Scans based on GameProfile folder structure
        /// </summary>
        private void GameProfileScanClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FolderLocation.Text) || !Directory.Exists(FolderLocation.Text))
            {
                LogTextBox(TeknoParrotUi.Properties.Resources.GameScannerSelectValidROMFolder, true);
                return;
            }

            ScanDirByGameProfile(FolderLocation.Text);
        }

        /// <summary>
        /// Scans based on ROM file names in any folder structure
        /// </summary>
        private void RomNameScanClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FolderLocation.Text) || !Directory.Exists(FolderLocation.Text))
            {
                LogTextBox(TeknoParrotUi.Properties.Resources.GameScannerSelectValidROMFolder, true);
                return;
            }

            ScanDirByRomName(FolderLocation.Text);
        }

        /// <summary>
        /// Scan directory structure based on GameProfile names
        /// </summary>
        private void ScanDirByGameProfile(string scanDir)
        {
            if (!Directory.Exists(scanDir))
            {
                LogTextBox(TeknoParrotUi.Properties.Resources.GameScannerDirectoryNotExist, true);
                return;
            }

            _gameSetupContainers.Clear();
            _foundGameIds.Clear();
            LogTextBox(TeknoParrotUi.Properties.Resources.GameScannerScanningForFolders, true);
            
            // Get all GameSetup profiles
            var gameSetupFiles = Directory.GetFiles("GameSetup\\", "*.xml");
            int foundGames = 0;
            
            foreach (var gameSetupFile in gameSetupFiles)
            {
                var gameId = gameSetupFile.Replace("GameSetup\\", "").Replace(".xml", "");
                
                // Try to find an exact match directory
                string profileDir = Path.Combine(scanDir, gameId);
                if (!Directory.Exists(profileDir))
                    continue;
                    
                var gameSetup = JoystickHelper.DeSerializeGameSetup(gameSetupFile);
                if (gameSetup == null)
                    continue;
                    
                bool foundExe = false;
                bool foundTest = false;
                bool foundExe2 = false;
                
                // Check for main executable
                if (!string.IsNullOrWhiteSpace(gameSetup.GameExecutableLocation))
                {
                    if (File.Exists(Path.Combine(profileDir, gameSetup.GameExecutableLocation)))
                    {
                        foundExe = true;
                    }
                }
                else
                {
                    foundExe = true;
                }

                // Check for second executable
                if (!string.IsNullOrWhiteSpace(gameSetup.GameExecutableLocation2))
                {
                    if (File.Exists(Path.Combine(profileDir, gameSetup.GameExecutableLocation2)))
                    {
                        foundExe2 = true;
                    }
                }
                else
                {
                    foundExe2 = true;
                }

                // Check for test executable
                if (!string.IsNullOrWhiteSpace(gameSetup.GameTestExecutableLocation))
                {
                    if (File.Exists(Path.Combine(profileDir, gameSetup.GameTestExecutableLocation)))
                    {
                        foundTest = true;
                    }
                }
                else
                {
                    foundTest = true;
                }

                // If all required files are found, add to the list
                if (foundExe && foundTest && foundExe2)
                {
                    // Get game proper name from metadata
                    GameSetupContainer setup = new GameSetupContainer
                    {
                        GameId = gameId,
                        GameSetupData = gameSetup
                    };
                    _gameSetupContainers.Add(setup);
                    
                    var metaData = JoystickHelper.DeSerializeMetadata(gameId);
                    LogTextBox($"Found: {metaData?.game_name ?? gameId} ({metaData?.platform ?? "Unknown"})");
                    _foundGameIds.Add(gameId);
                    foundGames++;
                }
            }

            romDir = scanDir;
            LogTextBox(string.Format(TeknoParrotUi.Properties.Resources.GameScannerScanCompleteFoundGames, foundGames));
        }
    }
}
