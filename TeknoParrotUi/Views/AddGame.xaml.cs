using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for AddGame.xaml
    /// </summary>
    public partial class AddGame
    {
        private GameProfile _selected = new GameProfile();
        private readonly WebClient _wc = new WebClient();
        private bool _network;

        public AddGame()
        {
            InitializeComponent();
        }

        /// <summary>
        /// This is executed when the control is loaded, it grabs all the default game profiles and adds them to the list box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (var gameProfile in GameProfileLoader.GameProfiles)
            {
                var item = new ListBoxItem
                {
                    Content = gameProfile.GameName,
                    Tag = gameProfile
                };
                stockGameList.Items.Add(item);
            }
        }

        /// <summary>
        /// This method downloads the update from the TeknoParrot server.
        /// </summary>
        private void Download(string link, string output)
        {
            var wc = new WebClient();
            File.Delete(Environment.GetEnvironmentVariable("TEMP") + "\\teknoparrot.zip");

            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            // This will download a large image from the web, you can change the value
            // i.e a textbox : textBox1.Text
            try
            {
                using (wc)
                {
                    wc.DownloadFile(new Uri(link), output);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private static void DownLoadFileByWebRequest(string urlAddress, string filePath)
        {
            try
            {
                var request = (HttpWebRequest) WebRequest.Create(urlAddress);
                request.Timeout = 30000; //8000 Not work
                var response = (HttpWebResponse) request.GetResponse();
                var s = response.GetResponseStream();
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                var os = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
                var buff = new byte[102400];
                int c;
                while (s != null && (c = s.Read(buff, 0, 10400)) > 0)
                {
                    os.Write(buff, 0, c);
                    os.Flush();
                }

                os.Close();
                s?.Close();
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// This cancels the download
        /// </summary>
        private void CancelDownload()
        {
            _wc.CancelAsync();
        }

        /// <summary>
        /// When the selection in the listbox is changed, it loads the appropriate game profile as the selected one.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StockGameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
            _selected = GameProfileLoader.GameProfiles[stockGameList.SelectedIndex];
            var icon = _selected.IconName;
            string[] splitString = _selected.IconName.Split('/');
            if (!File.Exists(_selected.IconName))
            {
                _network = CheckNet(splitString[1]);
                if (_network)
                {
                    DownLoadFileByWebRequest(
                        "https://raw.githubusercontent.com/teknogods/TeknoParrotUIThumbnails/master/Icons/" +
                        splitString[1], _selected.IconName);
                }

                BitmapImage imageBitmap = new BitmapImage(File.Exists(icon)
                    ? new Uri("pack://siteoforigin:,,,/" + icon, UriKind.Absolute)
                    : new Uri("../Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));
                image1.Source = imageBitmap;
            }
            else
            {
                BitmapImage imageBitmap = new BitmapImage(File.Exists(icon)
                    ? new Uri("pack://siteoforigin:,,,/" + icon, UriKind.Absolute)
                    : new Uri("../Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));
                image1.Source = imageBitmap;
            }
        }

        private bool CheckNet(string icon)
        {
            var url = "https://raw.githubusercontent.com/teknogods/TeknoParrotUIThumbnails/master/Icons/" + icon;
            var request = WebRequest.Create(url);
            HttpWebResponse response = (HttpWebResponse) request.GetResponse();
            try
            {
                return response.StatusDescription == "OK";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// This is the code for the Add Game button, that copies the default game profile over to the UserProfiles folder so it shows up in the menu, then restarts the UI to load it in.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null || _selected.FileName == null) return;
            Console.WriteLine($@"Adding {_selected.GameName} to TP...");
            var splitString = _selected.FileName.Split('\\');
            if (splitString.Length < 1) return;
            File.Copy(_selected.FileName, "UserProfiles\\" + splitString[1]);
            var psargs = Environment.GetCommandLineArgs();
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location, psargs[0]);
            Application.Current.Shutdown();
        }

        /// <summary>
        /// This is the code for the Remove Game button, that deletes the game profile in the UserProfiles folder so it doesn't show up in the menu, then restarts the UI to load it in.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (_selected == null || _selected.FileName == null) return;
            var splitString = _selected.FileName.Split('\\');
            try
            {
                Console.WriteLine($@"Removing {_selected.GameName} from TP...");
                File.Delete("UserProfiles\\" + splitString[1]);
                var args = Environment.GetCommandLineArgs();
                System.Diagnostics.Process.Start(Application.ResourceAssembly.Location, args[0]);
                Application.Current.Shutdown();
            }
            catch
            {
                // ignored
            }
        }
    }
}