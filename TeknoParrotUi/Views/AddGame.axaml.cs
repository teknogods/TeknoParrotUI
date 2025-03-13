using System;
using System.Net;
using System.Linq;
using System.IO;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TeknoParrotUi.Common;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for AddGame.axaml
    /// </summary>
    public partial class AddGame : UserControl
    {
        private GameProfile _selected = new GameProfile();
        private readonly ContentControl _contentControl;
        private readonly Library _library;
        public AddGame()
        {
            InitializeComponent();
        }

        public AddGame(ContentControl control, Library library)
        {
            InitializeComponent();
            _contentControl = control;
            _library = library;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Find all necessary controls after XAML is loaded
            stockGameList = this.FindControl<ListBox>("stockGameList");
            GenreBox = this.FindControl<ComboBox>("GenreBox");
            GameSearchBox = this.FindControl<TextBox>("GameSearchBox");
            gameIcon = this.FindControl<Image>("gameIcon");
            AddButton = this.FindControl<Button>("AddButton");
            DeleteButton = this.FindControl<Button>("DeleteButton");
            GameCountLabel = this.FindControl<TextBlock>("GameCountLabel");
            AddButton.Click += AddGameButton;
            DeleteButton.Click += DeleteGameButton;
        }

        /// <summary>
        /// This is executed when the control is loaded, it grabs all the default game profiles and adds them to the list box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Clear existing items to prevent duplicates
            stockGameList.ItemsSource = null;

            int fullGameCount = 0;
            var items = new List<ListBoxItem>();

            foreach (var gameProfile in GameProfileLoader.GameProfiles)
            {
                // third-party emulators
                var thirdparty = gameProfile.EmulatorType == EmulatorType.SegaTools;

                // check the existing user profiles
                var existing = GameProfileLoader.UserProfiles.FirstOrDefault((profile) => profile.ProfileName == gameProfile.ProfileName) != null;

                if (gameProfile.IsLegacy && !existing)
                {
                    continue; // skip this profile
                }

                fullGameCount += 1;
                var item = new ListBoxItem
                {
                    Content = gameProfile.GameNameInternal +
                                (gameProfile.Patreon ? " (Subscription)" : "") +
                                (thirdparty ? $" (Third-Party - {gameProfile.EmulatorType})" : "") +
                                (existing ? " (added)" : ""),
                    Tag = gameProfile
                };

                // In Avalonia, we set foreground directly with a SolidColorBrush
                if (existing)
                {
                    // Use resource system in Avalonia
                    item.Foreground = Application.Current.Resources["PrimaryHueMidBrush"] as IBrush ?? new SolidColorBrush(Colors.Green);
                }

                var genreItem = GenreBox.SelectedItem as ComboBoxItem;
                var genreContent = genreItem?.Content?.ToString() ?? "All";

                string searchName = GameSearchBox?.Text ?? string.Empty;

                if (gameProfile.GameNameInternal.IndexOf(searchName, 0, StringComparison.OrdinalIgnoreCase) != -1 || string.IsNullOrWhiteSpace(searchName))
                {
                    if (genreContent == "All")
                        items.Add(item);
                    else if (genreContent == "Installed")
                    {
                        if (existing)
                        {
                            items.Add(item);
                        }
                    }
                    else if (genreContent == "Not Installed")
                    {
                        if (!existing)
                        {
                            items.Add(item);
                        }
                    }
                    else if (genreContent == "Subscription")
                    {
                        if (gameProfile.Patreon)
                        {
                            items.Add(item);
                        }
                    }
                    else if (gameProfile.GameGenreInternal == genreContent)
                        items.Add(item);
                }
            }

            // Set the items collection
            stockGameList.ItemsSource = items;

            // Update the game count label
            if (GameProfileLoader.GameProfiles != null && stockGameList.Items != null && GameCountLabel != null)
            {
                GameCountLabel.Text = $"Games shown: {stockGameList.Items.Count}/{fullGameCount}";
            }

            // Reset selection state if nothing is selected
            if (stockGameList.SelectedIndex < 0)
            {
                if (gameIcon != null)
                {
                    gameIcon.Source = Library.defaultIcon;
                    _selected = new GameProfile();
                    AddButton.IsEnabled = false;
                    DeleteButton.IsEnabled = false;
                }
            }
        }

        /// <summary>
        /// When the selection in the listbox is changed, it loads the appropriate game profile as the selected one.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StockGameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (stockGameList.SelectedIndex < 0) return;

            e.Handled = true;

            var gameItem = stockGameList.SelectedItem as ListBoxItem;
            if (gameItem == null) return;

            _selected = (GameProfile)gameItem.Tag;

            // Update the game icon
            UpdateGameIcon();

            // Check if the game is already added
            var content = gameItem.Content.ToString();
            var added = content != null && content.Contains("(added)");

            AddButton.IsEnabled = !added;
            DeleteButton.IsEnabled = added;
        }

        /// <summary>
        /// Helper method to update the game icon
        /// </summary>
        private void UpdateGameIcon()
        {
            if (_selected == null || string.IsNullOrEmpty(_selected.IconName))
            {
                gameIcon.Source = Library.defaultIcon;
                return;
            }

            try
            {
                // Splitting the icon path and getting the file name
                var iconName = _selected.IconName.Split('/').Last();
                _library.UpdateIcon(iconName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading game icon: {ex.Message}");
                gameIcon.Source = Library.defaultIcon;
            }
        }

        /// <summary>
        /// This is the code for the Add Game button, that copies the default game profile over to the UserProfiles folder so it shows up in the menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddGameButton(object sender, RoutedEventArgs e)
        {
            if (_selected == null || string.IsNullOrEmpty(_selected.FileName)) return;

            var splitString = _selected.FileName.Split('\\');
            if (splitString.Length < 1) return;
            try
            {
                _selected.FileName = _selected.FileName.Replace("UserProfilesJSON", "GameProfilesJSON"); // make sure we are copying from GameProfiles
                File.Copy(_selected.FileName, Path.Combine("UserProfilesJSON", splitString[1]));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying game profile: {ex.Message}");
            }

            _library.ListUpdate(_selected.GameNameInternal);
            _contentControl.Content = _library;
        }

        /// <summary>
        /// This is the code for the Remove Game button, that deletes the game profile in the UserProfiles folder so it doesn't show up in the menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteGameButton(object sender, RoutedEventArgs e)
        {
            if (_selected == null || string.IsNullOrEmpty(_selected.FileName)) return;

            var splitString = _selected.FileName.Split('\\');
            try
            {
                Debug.WriteLine($@"Removing {_selected.GameNameInternal} from TP...");
                File.Delete(Path.Combine("UserProfilesJSON", splitString[1]));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting game profile: {ex.Message}");
            }

            _library.listRefreshNeeded = true;
            _contentControl.Content = _library;
        }
    }
}