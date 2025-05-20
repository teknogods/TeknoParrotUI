using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Security.Cryptography;
using TeknoParrotUi.Helpers;
using System.Diagnostics;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for VerifyGame.xaml
    /// </summary>
    public partial class VerifyGame
    {
        private readonly GameProfile _gameProfile;
        private Library _library;
        private bool _cancel;
        private double _total;
        private double _current;
        private bool _verificationComplete = false;
        private DatXmlParser.DatGame _gameData;

        public VerifyGame(GameProfile gameProfile, Library library)
        {
            InitializeComponent();
            Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = false;
            _gameProfile = gameProfile;
            _library = library;
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

        /// <summary>
        /// When the control is loaded, it starts checking every file using the DatXML format
        /// </summary>
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            var invalidFiles = new List<string>();
            var validFiles = new List<string>();
            totalCount.Text = "0";
            invalidCount.Text = "0";
            validCount.Text = "0";

            // Try to get the game directory
            string gamePath;
            try
            {
                gamePath = Path.GetDirectoryName(_gameProfile.GamePath);
                if (string.IsNullOrEmpty(gamePath))
                {
                    throw new InvalidOperationException("Game path is empty");
                }
            }
            catch
            {
                MessageBox.Show("You don't have a valid game executable path configured.", "Invalid game executable path", MessageBoxButton.OK, MessageBoxImage.Warning);
                verifyText.Text = Properties.Resources.VerifyCancelled;
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                CompleteVerification();
                return;
            }


            try
            {
                if (!File.Exists(Lazydata.ParrotData.DatXmlLocation))
                {
                    MessageBox.Show($"DAT file not found: {Lazydata.ParrotData.DatXmlLocation}", "DAT File Missing", MessageBoxButton.OK, MessageBoxImage.Error);
                    verifyText.Text = Properties.Resources.VerifyCancelled;
                    Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                    CompleteVerification();
                    return;
                }

                // Use streaming to find the matching game profile
                bool gameFound = false;
                DatXmlParser.ProcessDatFileStreaming(
                    Lazydata.ParrotData.DatXmlLocation,
                    header => { /* We don't need the header information */ },
                    game =>
                    {
                        // Check if this is the game we're looking for
                        if (game.GameProfile == _gameProfile.ProfileName)
                        {
                            _gameData = game;
                            gameFound = true;
                        }
                    }
                );

                if (!gameFound || _gameData == null)
                {
                    MessageBox.Show($"Game profile '{_gameProfile}' not found in the DAT file.", "Game Profile Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    verifyText.Text = Properties.Resources.VerifyCancelled;
                    Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                    CompleteVerification();
                    return;
                }

                // Check if there are any ROM entries to verify
                if (_gameData.Roms == null || _gameData.Roms.Count == 0)
                {
                    MessageBox.Show($"No ROM entries found for game profile '{_gameProfile}'.", "No Verification Data", MessageBoxButton.OK, MessageBoxImage.Information);
                    verifyText.Text = Properties.Resources.VerifyCancelled;
                    Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                    CompleteVerification();
                    return;
                }

                // Update the total count and setup progress tracking
                _total = _gameData.Roms.Count;
                totalCount.Text = _total.ToString();

                // Verify each ROM in the game entry
                foreach (var rom in _gameData.Roms)
                {
                    if (_cancel)
                    {
                        break;
                    }

                    // Skip directories
                    if (string.IsNullOrEmpty(rom.Name) || rom.Name.EndsWith("/"))
                    {
                        _current++;
                        continue;
                    }

                    // Normalize path and get expected MD5
                    string filePath = rom.Name.Replace('/', '\\');

                    // Check if the file exists
                    string fullPath = Path.Combine(gamePath, filePath);

                    // Calculate the actual MD5 of the file
                    string actualMd5 = await CalculateMd5Async(fullPath);

                    // If the file doesn't exist or MD5 doesn't match
                    if (actualMd5 == null)
                    {
                        invalidFiles.Add(filePath);
                        var item = $"{Properties.Resources.VerifyInvalid}: {filePath} (File not found)";
                        listBoxAllFiles.Items.Add(item);
                        listBoxInvalidFiles.Items.Add(item);

                        Dispatcher.Invoke(() =>
                        {
                            invalidCount.Text = invalidFiles.Count.ToString();
                        });
                    }
                    else if (!string.IsNullOrEmpty(rom.Md5) && actualMd5 != rom.Md5.ToLowerInvariant())
                    {
                        invalidFiles.Add(filePath);
                        var item = $"{Properties.Resources.VerifyInvalid}: {filePath} (MD5 mismatch)";
                        listBoxAllFiles.Items.Add(item);
                        listBoxInvalidFiles.Items.Add(item);

                        Dispatcher.Invoke(() =>
                        {
                            invalidCount.Text = invalidFiles.Count.ToString();
                        });
                    }
                    else
                    {
                        validFiles.Add(filePath);
                        listBoxAllFiles.Items.Add($"{Properties.Resources.VerifyValid}: {filePath}");

                        Dispatcher.Invoke(() =>
                        {
                            validCount.Text = validFiles.Count.ToString();
                        });
                    }

                    // Update progress bar
                    _current++;
                    var percentComplete = (_current / _total) * 100;
                    progressBar1.Dispatcher.Invoke(() => progressBar1.Value = percentComplete,
                        System.Windows.Threading.DispatcherPriority.Background);
                }

                // Update the summary tab
                UpdateSummary(validFiles, invalidFiles);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error verifying game: {ex.Message}", "Verification Error", MessageBoxButton.OK, MessageBoxImage.Error);
                verifyText.Text = Properties.Resources.VerifyCancelled;
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                CompleteVerification();
                return;
            }

            // Update UI based on verification results
            if (_cancel)
            {
                verifyText.Text = Properties.Resources.VerifyCancelled;
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
            }
            else if (invalidFiles.Count > 0)
            {
                verifyText.Text = Properties.Resources.VerifyFilesInvalid;
                tabResults.SelectedIndex = 1; // Switch to Invalid Files tab
                MessageBoxHelper.WarningOK(Properties.Resources.VerifyFilesInvalidExplain);
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
            }
            else
            {
                verifyText.Text = Properties.Resources.VerifyFilesValid;
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
            }

            CompleteVerification();
        }

        private void UpdateSummary(List<string> validFiles, List<string> invalidFiles)
        {
            var summaryText = new System.Text.StringBuilder();

            summaryText.AppendLine($"Game: {_gameProfile.GameNameInternal}");
            summaryText.AppendLine($"Profile: {_gameProfile.ProfileName}");
            summaryText.AppendLine();

            summaryText.AppendLine($"Total files checked: {validFiles.Count + invalidFiles.Count}");
            summaryText.AppendLine($"Valid files: {validFiles.Count}");
            summaryText.AppendLine($"Invalid files: {invalidFiles.Count}");
            summaryText.AppendLine();

            if (invalidFiles.Count > 0)
            {
                summaryText.AppendLine("Invalid files list:");
                foreach (var file in invalidFiles)
                {
                    summaryText.AppendLine($"- {file}");
                }
            }
            else
            {
                summaryText.AppendLine("All files are valid!");
            }

            txtSummary.Text = summaryText.ToString();
        }

        private void CompleteVerification()
        {
            _verificationComplete = true;
            buttonCancel.Content = Properties.Resources.Back;

            if (verifyText.Text != Properties.Resources.VerifyFilesInvalid &&
                verifyText.Text != Properties.Resources.VerifyCancelled)
            {
                verifyText.Text = Properties.Resources.VerifyValid;
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_verificationComplete)
            {
                // If verification is complete, close/return to previous screen
                var parent = Parent as ContentControl;
                if (parent != null)
                    parent.Content = _library; // Assuming _library is the previous screen
                else
                    Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = _library;
            }
            else
            {
                // If verification is still in progress, cancel it
                _cancel = true;
            }
        }
    }
}
