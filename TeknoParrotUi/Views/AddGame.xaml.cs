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
using TeknoParrotUi.Properties;
using TeknoParrotUi.Helpers;

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
            InitializeGenreComboBox();
        }

        private void InitializeGenreComboBox()
        {
            var genreItems = TeknoParrotUi.Helpers.GenreTranslationHelper.GetGenreItems(true);
            GenreBox.ItemsSource = genreItems;
            GenreBox.SelectedIndex = 0;
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
            int fullGameCount = 0;
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
                                (gameProfile.Patreon ? TeknoParrotUi.Properties.Resources.AddGameSubscriptionSuffix : "") +
                                (thirdparty ? string.Format(TeknoParrotUi.Properties.Resources.AddGameThirdPartySuffix, gameProfile.EmulatorType) : "") +
                                (existing ? TeknoParrotUi.Properties.Resources.AddGameAddedSuffix : ""),
                    Tag = gameProfile
                };


                if (existing)
                {
                    item.SetResourceReference(ForegroundProperty, "MaterialDesign.Brush.Primary.Dark");
                }

                string selectedInternalGenre = "All";
                if (GenreBox != null && GenreBox.SelectedItem != null)
                {
                    var genreItem = GenreBox.SelectedItem as TeknoParrotUi.Helpers.GenreItem;
                    selectedInternalGenre = genreItem?.InternalName ?? "All";
                }

                string searchName = "";
                if (GameSearchBox != null)
                {
                    searchName = GameSearchBox.Text;
                }

                if (gameProfile.GameNameInternal.IndexOf(searchName, 0, StringComparison.OrdinalIgnoreCase) != -1 || string.IsNullOrWhiteSpace(searchName))
                {
                    bool matchesGenre = TeknoParrotUi.Helpers.GenreTranslationHelper.DoesGameMatchGenre(selectedInternalGenre, gameProfile);

                    if (matchesGenre)
                    {
                        stockGameList.Items.Add(item);
                    }
                }

            }

            if (GameProfileLoader.GameProfiles != null && stockGameList.Items != null && GameCountLabel != null)
            {
                GameCountLabel.Content = string.Format(TeknoParrotUi.Properties.Resources.AddGameGamesShownCount, stockGameList.Items.Count, fullGameCount);
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

            var added = ((ListBoxItem)stockGameList.SelectedItem).Content.ToString().Contains(TeknoParrotUi.Properties.Resources.AddGameAddedSuffix);
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
            //Trace.WriteLine($@"Adding {_selected.GameNameInternal} to TP (Path: {_selected.FileName}...");
            var splitString = _selected.FileName.Split('\\');
            if (splitString.Length < 1) return;
            try
            {
                _selected.FileName = _selected.FileName.Replace("UserProfiles", "GameProfiles"); // make sure we are copying from GameProfiles
                File.Copy(_selected.FileName, Path.Combine("UserProfiles", splitString[1]));

                var addedProfile = JoystickHelper.DeSerializeGameProfile(Path.Combine("UserProfiles", splitString[1]), true);
                if (addedProfile != null && !string.IsNullOrEmpty(addedProfile.OnlineIdFieldName) && addedProfile.OnlineIdType != OnlineIdType.None)
                {
                    AutoFillOnlineId(addedProfile);
                    JoystickHelper.SerializeGameProfile(addedProfile);
                }
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

        private void AutoFillOnlineId(GameProfile profile)
        {
            if (string.IsNullOrEmpty(profile.OnlineIdFieldName))
                return;

            var configField = profile.ConfigValues.FirstOrDefault(x => x.FieldName == profile.OnlineIdFieldName);
            if (configField == null || !string.IsNullOrEmpty(configField.FieldValue))
                return;

            switch (profile.OnlineIdType)
            {
                case OnlineIdType.SegaId:
                    if (!string.IsNullOrEmpty(Lazydata.ParrotData.SegaId))
                        configField.FieldValue = Lazydata.ParrotData.SegaId;
                    break;
                case OnlineIdType.NamcoId:
                    if (!string.IsNullOrEmpty(Lazydata.ParrotData.NamcoId))
                        configField.FieldValue = Lazydata.ParrotData.NamcoId;
                    break;
                case OnlineIdType.HighscoreSerial:
                    if (!string.IsNullOrEmpty(Lazydata.ParrotData.ScoreSubmissionID))
                        configField.FieldValue = Lazydata.ParrotData.ScoreSubmissionID;
                    break;
                case OnlineIdType.MarioKartId:
                    if (!string.IsNullOrEmpty(Lazydata.ParrotData.MarioKartId))
                        configField.FieldValue = Lazydata.ParrotData.MarioKartId;
                    break;
            }
        }
    }
}
