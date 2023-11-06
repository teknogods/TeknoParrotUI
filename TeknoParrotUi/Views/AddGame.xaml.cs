using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media; // needed to change text colors.
using System.IO;
using TeknoParrotUi.Common;
using System.Diagnostics;
using System.Linq;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for AddGame.xaml
    /// </summary>
    public partial class AddGame
    {
        private GameProfile _selected = new GameProfile();
        private ContentControl _contentControl;
        private Library _library;

        public AddGame(ContentControl control, Library library)
        {
            InitializeComponent();
            _contentControl = control;
            _library = library;
        }

        /// <summary>
        /// This is executed when the control is loaded, it grabs all the default game profiles and adds them to the list box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            //this prevents duplicates if you leave the window then come back
            stockGameList.Items.Clear();

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

                var item = new ListBoxItem
                {
                    Content = gameProfile.GameNameInternal +
                                (gameProfile.Patreon ? " (Patreon)" : "") +
                                (thirdparty ? $" (Third-Party - {gameProfile.EmulatorType})" : "") +
                                (existing ? " (added)" : ""),
                    Tag = gameProfile
                };


                if (existing)
                {
                    item.Foreground = Brushes.Green;
                    item.SetResourceReference(Control.ForegroundProperty, "PrimaryHueMidBrush");
                }

                var genreItem = (ComboBoxItem)GenreBox.SelectedValue;
                var genreContent = (string)genreItem.Content;


                string searchName = "";
                if (GameSearchBox != null)
                {
                    searchName = GameSearchBox.Text;
                }

                if (gameProfile.GameNameInternal.IndexOf(searchName, 0, StringComparison.OrdinalIgnoreCase) != -1 || String.IsNullOrWhiteSpace(searchName))
                {
                    if (genreContent == "All")
                        stockGameList.Items.Add(item);
                    else if (genreContent == "Installed")
                    {
                        if (existing)
                        {
                            {
                                stockGameList.Items.Add(item);
                            }
                        }
                    }
                    else if (genreContent == "Patreon")
                    {
                        if (gameProfile.Patreon)
                        {
                            stockGameList.Items.Add(item);
                        }

                    }
                    else if (gameProfile.GameGenreInternal == genreContent)
                        stockGameList.Items.Add(item);
                }

            }

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

            var gameItem = (ListBoxItem)stockGameList.SelectedValue;
            _selected = (GameProfile)gameItem.Tag;
            //_selected = GameProfileLoader.GameProfiles[stockGameList.SelectedIndex];
            Library.UpdateIcon(_selected.IconName.Split('/')[1], ref gameIcon);

            var added = ((ListBoxItem)stockGameList.SelectedItem).Content.ToString().Contains("(added)");
            AddButton.IsEnabled = !added;
            DeleteButton.IsEnabled = added;
        }

        /// <summary>
        /// This is the code for the Add Game button, that copies the default game profile over to the UserProfiles folder so it shows up in the menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddGameButton(object sender, RoutedEventArgs e)
        {
            if (_selected == null || _selected.FileName == null) return;
            Debug.WriteLine($@"Adding {_selected.GameNameInternal} to TP...");
            var splitString = _selected.FileName.Split('\\');
            if (splitString.Length < 1) return;
            try
            {
                File.Copy(_selected.FileName, Path.Combine("UserProfiles", splitString[1]));
            }
            catch
            {

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
            if (_selected == null || _selected.FileName == null) return;
            var splitString = _selected.FileName.Split('\\');
            try
            {
                Debug.WriteLine($@"Removing {_selected.GameNameInternal} from TP...");
                File.Delete(Path.Combine("UserProfiles", splitString[1]));
            }
            catch
            {
                // ignored
            }

            //_library.ListUpdate();
            _library.listRefreshNeeded = true;
            _contentControl.Content = _library;
        }
    }
}
