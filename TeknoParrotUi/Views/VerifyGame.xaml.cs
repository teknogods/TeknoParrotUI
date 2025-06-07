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
using TeknoParrotUi.Properties;

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
                MessageBox.Show(TeknoParrotUi.Properties.Resources.VerifyInvalidGameExecutablePath, TeknoParrotUi.Properties.Resources.VerifyInvalidGameExecutablePathTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                verifyText.Text = TeknoParrotUi.Properties.Resources.VerifyCancelled;
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                CompleteVerification();
                return;
            }


            try
            {
                if (!File.Exists(Lazydata.ParrotData.DatXmlLocation))
                {
                    MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.VerifyDATFileNotFound, Lazydata.ParrotData.DatXmlLocation), TeknoParrotUi.Properties.Resources.VerifyDATFileMissing, MessageBoxButton.OK, MessageBoxImage.Error);
                    verifyText.Text = TeknoParrotUi.Properties.Resources.VerifyCancelled;
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
                    MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.VerifyGameProfileNotFound, _gameProfile.ProfileName), TeknoParrotUi.Properties.Resources.VerifyGameProfileNotFoundTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    verifyText.Text = TeknoParrotUi.Properties.Resources.VerifyCancelled;
                    Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                    CompleteVerification();
                    return;
                }

                // Check if there are any ROM entries to verify
                if (_gameData.Roms == null || _gameData.Roms.Count == 0)
                {
                    MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.VerifyNoROMEntries, _gameProfile.ProfileName), TeknoParrotUi.Properties.Resources.VerifyNoVerificationData, MessageBoxButton.OK, MessageBoxImage.Information);
                    verifyText.Text = TeknoParrotUi.Properties.Resources.VerifyCancelled;
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
                        var item = $"{TeknoParrotUi.Properties.Resources.VerifyInvalid} {filePath} ({TeknoParrotUi.Properties.Resources.VerifyFileNotFound})";
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
                        var item = $"{TeknoParrotUi.Properties.Resources.VerifyInvalid} {filePath} ({TeknoParrotUi.Properties.Resources.VerifyMD5Mismatch})";
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
                        listBoxAllFiles.Items.Add($"{TeknoParrotUi.Properties.Resources.VerifyValid} {filePath}");

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
                MessageBox.Show(string.Format(TeknoParrotUi.Properties.Resources.VerifyErrorVerifyingGame, ex.Message), TeknoParrotUi.Properties.Resources.VerifyVerificationError, MessageBoxButton.OK, MessageBoxImage.Error);
                verifyText.Text = TeknoParrotUi.Properties.Resources.VerifyCancelled;
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                CompleteVerification();
                return;
            }

            // Update UI based on verification results
            if (_cancel)
            {
                verifyText.Text = TeknoParrotUi.Properties.Resources.VerifyCancelled;
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
            }
            else if (invalidFiles.Count > 0)
            {
                verifyText.Text = TeknoParrotUi.Properties.Resources.VerifyFilesInvalid;
                tabResults.SelectedIndex = 1; // Switch to Invalid Files tab
                MessageBoxHelper.WarningOK(TeknoParrotUi.Properties.Resources.VerifyFilesInvalidExplain);
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
            }
            else
            {
                verifyText.Text = TeknoParrotUi.Properties.Resources.VerifyFilesValid;
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
            }

            CompleteVerification();
        }

        private void UpdateSummary(List<string> validFiles, List<string> invalidFiles)
        {
            var summaryText = new System.Text.StringBuilder();

            summaryText.AppendLine(string.Format(TeknoParrotUi.Properties.Resources.VerifyGameLabel, _gameProfile.GameNameInternal));
            summaryText.AppendLine(string.Format(TeknoParrotUi.Properties.Resources.VerifyProfileLabel, _gameProfile.ProfileName));
            summaryText.AppendLine();

            summaryText.AppendLine(string.Format(TeknoParrotUi.Properties.Resources.VerifyTotalFilesChecked, validFiles.Count + invalidFiles.Count));
            summaryText.AppendLine(string.Format(TeknoParrotUi.Properties.Resources.VerifyValidFilesCount, validFiles.Count));
            summaryText.AppendLine(string.Format(TeknoParrotUi.Properties.Resources.VerifyInvalidFilesCount, invalidFiles.Count));
            summaryText.AppendLine();

            if (invalidFiles.Count > 0)
            {
                summaryText.AppendLine(TeknoParrotUi.Properties.Resources.VerifyInvalidFilesList);
                foreach (var file in invalidFiles)
                {
                    summaryText.AppendLine($"- {file}");
                }
            }
            else
            {
                summaryText.AppendLine(TeknoParrotUi.Properties.Resources.VerifyAllFilesValid);
            }

            txtSummary.Text = summaryText.ToString();
        }

        private void CompleteVerification()
        {
            _verificationComplete = true;
            buttonCancel.Content = TeknoParrotUi.Properties.Resources.Back;

            if (verifyText.Text != TeknoParrotUi.Properties.Resources.VerifyFilesInvalid &&
                verifyText.Text != TeknoParrotUi.Properties.Resources.VerifyCancelled)
            {
                verifyText.Text = TeknoParrotUi.Properties.Resources.VerifyValid;
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
