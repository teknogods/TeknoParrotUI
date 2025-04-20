using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views
{
    public partial class ModMenu : UserControl
    {
        private ContentControl _contentControl;
        private Library _library;
        private HttpClient _httpClient;
        public List<string> installedGUIDs = new List<string>();

        public ModMenu()
        {
            InitializeComponent();
        }

        public ModMenu(ContentControl contentControl, Library library)
        {
            InitializeComponent();
            _contentControl = contentControl;
            _library = library;
            _httpClient = new HttpClient();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Find control references
            modList = this.FindControl<StackPanel>("modList");
            cbGameList = this.FindControl<ComboBox>("cbGameList");
            cbGameList.SelectionChanged += cbGameList_SelectionChanged;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Load game list into combo box
            if (cbGameList != null && _library != null)
            {
                cbGameList.Items.Clear();
                cbGameList.Items.Add("All");
                cbGameList.SelectedIndex = 0;

                foreach (var game in GameProfileLoader.GameProfiles)
                {
                    // cbGameList.Items.Add(game.GameName);
                }
            }

            // Load installed mods
            LoadInstalledMods();

            // Load online mods
            await RefreshModList();
        }

        private void LoadInstalledMods()
        {
            try
            {
                if (File.Exists("InstalledMods.xml"))
                {
                    //installedGUIDs = ReadFromXmlFile<List<string>>("InstalledMods.xml");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading installed mods: {ex.Message}");
            }
        }

        private async Task RefreshModList()
        {
            try
            {
                modList.Children.Clear();

                string modsJson = await _httpClient.GetStringAsync("https://github.com/nzgamer41/tpgamemods/releases/latest/download/mods.xml");
                var mods = JsonSerializer.Deserialize<List<ModInfo>>(modsJson);

                if (mods == null)
                    return;

                foreach (var mod in mods)
                {
                    if (cbGameList.SelectedIndex == 0 ||
                        mod.GameName == cbGameList.SelectedItem.ToString())
                    {
                        // Create mod control for each mod
                        var gameProfile = GetGameProfileByName(mod.GameName);
                        if (gameProfile != null)
                        {
                            var control = new ModControl(
                                mod.ModName,
                                mod.GameName,
                                mod.Description,
                                mod.DownloadUrl,
                                mod.Creator,
                                gameProfile,
                                this);

                            modList.Children.Add(control);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing mod list: {ex.Message}");
            }
        }

        private void cbGameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Refresh mod list when game selection changes
            _ = RefreshModList();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Open create mod dialog
            _contentControl.Content = new CreateMod(_contentControl, this, _library);
        }

        private GameProfile GetGameProfileByName(string name)
        {
            foreach (var gameProfile in GameProfileLoader.GameProfiles)
            {
                // if (gameProfile.GameName == name)
                //     return gameProfile;
            }
            return null;
        }

        // public static T ReadFromXmlFile<T>(string filePath) where T : new()
        // {
        //     // TODO FIX
        //     return null;
        //     // TextReader reader = null;
        //     // try
        //     // {
        //     //     var serializer = new XmlSerializer(typeof(T));
        //     //     reader = new StreamReader(filePath);
        //     //     return (T)serializer.Deserialize(reader);
        //     // }
        //     // finally
        //     // {
        //     //     reader?.Close();
        //     // }
        // }
    }

    // Class to represent mod info from JSON
    public class ModInfo
    {
        public string ModName { get; set; }
        public string GameName { get; set; }
        public string Description { get; set; }
        public string DownloadUrl { get; set; }
        public string Creator { get; set; }
    }
}