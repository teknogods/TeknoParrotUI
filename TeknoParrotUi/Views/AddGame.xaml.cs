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
                // 64-bit game on non-64 bit OS
                var disabled = (gameProfile.Is64Bit && !Environment.Is64BitOperatingSystem);
                var thirdparty = gameProfile.EmulatorType == EmulatorType.SegaTools;

                // check the existing user profiles
                var existing = GameProfileLoader.UserProfiles.FirstOrDefault((profile) => profile.GameName == gameProfile.GameName) != null;

                var item = new ListBoxItem
                {
                    // add a star if we have the game already.
                    Content = gameProfile.GameName + (existing ? " * " : string.Empty) + (gameProfile.Patreon ? " (Patreon)" : string.Empty) + (disabled ? " (64-bit)" : string.Empty) + (thirdparty ? $" (Third-Party - {gameProfile.EmulatorType})" : string.Empty),
                    Tag = gameProfile
                }; 

                item.IsEnabled = !disabled;

                // change added games to green
                if (existing)
                    item.Foreground = Brushes.Green;

                stockGameList.Items.Add(item);
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
            _selected = GameProfileLoader.GameProfiles[stockGameList.SelectedIndex];
            Library.UpdateIcon(_selected.IconName.Split('/')[1], ref gameIcon);

            var added = ((ListBoxItem)stockGameList.SelectedItem).Foreground == Brushes.Green;
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
            Debug.WriteLine($@"Adding {_selected.GameName} to TP...");
            var splitString = _selected.FileName.Split('\\');
            if (splitString.Length < 1) return;
            try
            {
                File.Copy(_selected.FileName, Path.Combine("UserProfiles", splitString[1]));
            }
            catch
            {

            }

            _library.ListUpdate(_selected.GameName);

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
                Debug.WriteLine($@"Removing {_selected.GameName} from TP...");
                File.Delete(Path.Combine("UserProfiles", splitString[1]));
            }
            catch
            {
                // ignored
            }

            _library.ListUpdate();

            _contentControl.Content = _library;
        }
    }
}
