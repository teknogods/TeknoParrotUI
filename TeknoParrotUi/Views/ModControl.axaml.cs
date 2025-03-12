using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for ModControl.axaml
    /// </summary>
    public partial class ModControl : UserControl
    {
        private string _modName;
        private string _gameName;
        private string _description;
        private string _zipUrl;
        private string _creator;
        private GameProfile _thisGame;
        bool isDone = false;
        private ModMenu _modMenu;

        public ModControl()
        {
            InitializeComponent();
        }

        public ModControl(string modName, string gameName, string description, string zipUrl, string creator, GameProfile thisGame, ModMenu modmenu)
        {
            InitializeComponent();

            _modName = modName;
            _gameName = gameName;
            _description = description;
            _zipUrl = zipUrl;
            _creator = creator;
            _thisGame = thisGame;
            _modMenu = modmenu;

            labelModName.Text = _modName;
            labelGameName.Text = "Game:\n\n" + _gameName;
            buttonDescription.Text = _description;
            tbCreator.Text = _creator;

            // Set background color based on dark mode
            bool darkMode = false; // Get this from your settings
            if (darkMode)
            {
                uiGrid.Background = new SolidColorBrush(Color.FromRgb(0x36, 0x36, 0x36));
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Get references to controls
            uiGrid = this.FindControl<Grid>("uiGrid");
            labelModName = this.FindControl<TextBlock>("labelModName");
            labelGameName = this.FindControl<TextBlock>("labelGameName");
            buttonDescription = this.FindControl<TextBlock>("buttonDescription");
            buttonDl = this.FindControl<Button>("buttonDl");
            tbCreator = this.FindControl<TextBlock>("tbCreator");
        }

        private async void buttonDl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                buttonDl.IsEnabled = false;
                buttonDl.Content = "Downloading...";

                using (var httpClient = new HttpClient())
                {
                    var modData = await httpClient.GetByteArrayAsync(_zipUrl);
                    string tempZipPath = Path.GetTempFileName() + ".zip";

                    await File.WriteAllBytesAsync(tempZipPath, modData);

                    string extractPath = Path.Combine(_thisGame.GamePath, "mods", _modName);

                    if (Directory.Exists(extractPath))
                        Directory.Delete(extractPath, true);

                    Directory.CreateDirectory(extractPath);

                    ZipFile.ExtractToDirectory(tempZipPath, extractPath);

                    try
                    {
                        File.Delete(tempZipPath);
                    }
                    catch
                    {
                        // Ignore deletion errors
                    }

                    // Update installed mods list
                    if (_modMenu.installedGUIDs == null)
                        _modMenu.installedGUIDs = new System.Collections.Generic.List<string>();

                    if (!_modMenu.installedGUIDs.Contains(_zipUrl))
                        _modMenu.installedGUIDs.Add(_zipUrl);

                    WriteToXmlFile("InstalledMods.xml", _modMenu.installedGUIDs);

                    buttonDl.Content = "Downloaded!";
                    isDone = true;

                    await Task.Delay(1000);
                    buttonDl.Content = "Redownload";
                    buttonDl.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                buttonDl.Content = "Error";
                Debug.WriteLine($"Download error: {ex.Message}");
                await Task.Delay(1000);
                buttonDl.Content = "Download";
                buttonDl.IsEnabled = true;
            }
        }

        static void WriteToXmlFile<T>(string filePath, T objectToWrite, bool append = false) where T : new()
        {
            // TextWriter writer = null;
            // try
            // {
            //     var serializer = new XmlSerializer(typeof(T));
            //     writer = new StreamWriter(filePath, append);
            //     serializer.Serialize(writer, objectToWrite);
            // }
            // finally
            // {
            //     writer?.Close();
            // }
        }
    }
}