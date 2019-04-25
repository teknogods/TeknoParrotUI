using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;
using TeknoParrotUi.Common;
using System.Diagnostics;

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
                var item = new ListBoxItem
                {
                    Content = gameProfile.GameName + (gameProfile.Patreon ? " (Patreon Only)" : string.Empty),
                    Tag = gameProfile
                };
                stockGameList.Items.Add(item);
            }
        }

        private static void DownloadFile(string urlAddress, string filePath)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(urlAddress);
                request.Timeout = 5000;
                request.Proxy = null;

                using (var response = request.GetResponse().GetResponseStream())
                using (var file = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    response.CopyTo(file);
                }
            }
            catch
            {
                // ignore
            }
        }

        static BitmapImage LoadImage(string filename)
        {
            //https://stackoverflow.com/a/13265190
            BitmapImage iconimage = new BitmapImage();

            using (var file = File.OpenRead(filename))
            {
                iconimage.BeginInit();
                iconimage.CacheOption = BitmapCacheOption.OnLoad;
                iconimage.StreamSource = file;
                iconimage.EndInit();
            }

            return iconimage;
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
            var icon = _selected.IconName;
            string[] splitString = icon.Split('/');

            if (!File.Exists(icon))
            {
                DownloadFile(
                    "https://raw.githubusercontent.com/teknogods/TeknoParrotUIThumbnails/master/Icons/" +
                    splitString[1], icon);
            }
      
            try
            {
                image1.Source = LoadImage(icon);
            }
            catch
            {
                //delete icon since it's probably corrupted, then load default icons
                if (File.Exists(icon)) File.Delete(icon);
                image1.Source = new BitmapImage(new Uri("../Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));
            }
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

            _library.ListUpdate(true);

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

            _library.ListUpdate(true);

            _contentControl.Content = _library;
        }
    }
}