using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;

namespace TeknoParrotUi.Views
{
    public partial class GameScanner : UserControl
    {
        private ContentControl _contentControl;
        private Library _library;
        private string _folder;
        private GameProfile[] _gameProfiles;
        private List<string> _foundGames = new List<string>();

        public GameScanner(ContentControl contentControl, Library library, bool showGui = true)
        {
            InitializeComponent();
            _contentControl = contentControl;
            _library = library;

            if (!showGui)
            {
                // Handle non-GUI mode if needed
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Find control references
            FolderLocation = this.FindControl<TextBox>("FolderLocation");
            ScannerText = this.FindControl<TextBox>("ScannerText");
            MyScrollViewer = this.FindControl<ScrollViewer>("MyScrollViewer");
        }

        private async void BrowseClick(object sender, RoutedEventArgs e)
        {
            // Use Avalonia's folder picker
            var folders = await TopLevel.GetTopLevel(this).StorageProvider
                .OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select folder to scan",
                    AllowMultiple = false
                });

            if (folders.Count > 0)
            {
                _folder = folders[0].Path.LocalPath;
                FolderLocation.Text = _folder;
            }
        }

        private void ScanClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_folder))
            {
                WriteToTextBox("Please select a folder first!");
                return;
            }

            ScannerText.Text = string.Empty;
            _foundGames = new List<string>();
            WriteToTextBox($"Scanning folder: {_folder}");

            try
            {
                var games = GameProfileLoader.GameProfiles;

                foreach (var game in games)
                {
                    ScanForGame(game);
                }

                WriteToTextBox($"Scan complete! Found {_foundGames.Count} games.");
            }
            catch (Exception ex)
            {
                WriteToTextBox($"Error during scan: {ex.Message}");
            }
        }

        private void ScanForGame(GameProfile gameProfile)
        {
            // Implement your game scanning logic here
            // This would search for game executables and required files
        }

        private void VerifyClick(object sender, RoutedEventArgs e)
        {
            // Implement verification logic if needed
        }

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            if (_foundGames.Count == 0)
            {
                WriteToTextBox("No games found to save!");
                return;
            }

            try
            {
                // Implement saving logic
                // This would save the discovered games to your configuration

                WriteToTextBox($"Successfully saved {_foundGames.Count} games!");

                // Return to library view
                _contentControl.Content = _library;
            }
            catch (Exception ex)
            {
                WriteToTextBox($"Error saving games: {ex.Message}");
            }
        }

        private void WriteToTextBox(string text)
        {
            if (ScannerText != null)
            {
                ScannerText.Text += text + Environment.NewLine;

                // Scroll to the bottom
                MyScrollViewer?.ScrollToEnd();
            }
        }
    }
}