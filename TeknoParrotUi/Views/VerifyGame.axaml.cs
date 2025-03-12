using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using TeknoParrotUi.Helpers;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for VerifyGame.axaml
    /// </summary>
    public partial class VerifyGame : UserControl
    {
        private readonly string _gameExe;
        private readonly string _validMd5;
        private List<string> _md5S = new List<string>();
        private bool _cancel;
        private double _total;
        private double _current;

        // UI controls
        private TextBlock _verifyText;
        private ListBox _listBoxFiles;
        private Button _buttonCancel;
        private ProgressBar _progressBar1;

        // Observable collection for ListBox items
        private ObservableCollection<string> _fileVerificationResults = new ObservableCollection<string>();

        public VerifyGame()
        {
            InitializeComponent();
        }

        public VerifyGame(string gameExe, string validMd5)
        {
            InitializeComponent();
            _validMd5 = validMd5;
            _gameExe = gameExe;

            // Disable menu button in main window if needed
            var mainWindow = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (mainWindow != null)
            {
                var menuButton = mainWindow.FindControl<Button>("menuButton");
                if (menuButton != null)
                    menuButton.IsEnabled = false;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Get control references
            _verifyText = this.FindControl<TextBlock>("verifyText");
            _listBoxFiles = this.FindControl<ListBox>("listBoxFiles");
            _buttonCancel = this.FindControl<Button>("buttonCancel");
            _progressBar1 = this.FindControl<ProgressBar>("progressBar1");

            // Set the ListBox's ItemsSource to our observable collection
            if (_listBoxFiles != null)
                _listBoxFiles.ItemsSource = _fileVerificationResults;
        }

        static async Task<string> CalculateMd5Async(string filename)
        {
            if (!File.Exists(filename))
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
                using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                {
                    // Use a large buffer for performance
                    byte[] buffer = new byte[81920];
                    int bytesRead;
                    do
                    {
                        bytesRead = await stream.ReadAsync(buffer);
                        if (bytesRead > 0)
                        {
                            md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                        }
                    }
                    while (bytesRead > 0);

                    md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    return BitConverter.ToString(md5.Hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        /// <summary>
        /// When the control is loaded, it starts checking every file
        /// </summary>
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Read MD5 file
                _md5S = File.ReadAllLines(_validMd5).Where(l => !l.Trim().StartsWith(";")).ToList();
                _total = _md5S.Count;
                _current = 0;

                progressBar1.Minimum = 0;
                progressBar1.Maximum = _total;
                progressBar1.Value = 0;

                // Clear existing results
                _fileVerificationResults.Clear();

                // Get game directory
                string gameDir = Path.GetDirectoryName(_gameExe);

                foreach (var md5Line in _md5S)
                {
                    if (_cancel)
                        break;

                    var temp = md5Line.Split(new[] { ' ' }, 2);
                    var expectedMd5 = temp[0];
                    var file = temp[1];

                    var filePath = Path.Combine(gameDir, file);

                    // Update the UI
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _fileVerificationResults.Clear();
                        _fileVerificationResults.Add($"Checking: {file}");
                        _current++;
                        progressBar1.Value = _current;
                    });

                    // Calculate MD5 for the file
                    var actualMd5 = await CalculateMd5Async(filePath);

                    // Add to the list
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (actualMd5 == null)
                        {
                            _fileVerificationResults.Add($"Missing: {file}");
                        }
                        else if (expectedMd5.Equals(actualMd5, StringComparison.OrdinalIgnoreCase))
                        {
                            _fileVerificationResults.Add($"OK: {file}");
                        }
                        else
                        {
                            _fileVerificationResults.Add($"INVALID: {file} (Expected: {expectedMd5}, Got: {actualMd5})");
                        }
                    });
                }

                // Update the status when done
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    verifyText.Text = "Verification Complete";

                    // Enable menu button again if needed
                    var mainWindow = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                    if (mainWindow != null)
                    {
                        var menuButton = mainWindow.FindControl<Button>("menuButton");
                        if (menuButton != null)
                            menuButton.IsEnabled = true;
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    verifyText.Text = "Error: " + ex.Message;
                });
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            _cancel = true;

            // Return to previous screen
            var mainWindow = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (mainWindow != null)
            {
                var menuButton = mainWindow.FindControl<Button>("menuButton");
                if (menuButton != null)
                    menuButton.IsEnabled = true;

                var contentControl = mainWindow.FindControl<ContentControl>("contentControl");
                if (contentControl != null)
                {
                    contentControl.Content = new Library(contentControl);
                }
            }
        }
    }
}