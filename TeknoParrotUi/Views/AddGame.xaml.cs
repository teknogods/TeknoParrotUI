using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for AddGame.xaml
    /// </summary>
    public partial class AddGame : UserControl
    {
        public AddGame()
        {
            InitializeComponent();
        }
        GameProfile selected = new GameProfile();
        WebClient wc = new WebClient();
        bool network = false;


        /// <summary>
        /// This is executed when the control is loaded, it grabs all the default game profiles and adds them to the list box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (var gameProfile in GameProfileLoader.GameProfiles)
            {
                ListBoxItem item = new ListBoxItem
                {
                    Content = gameProfile.GameName,
                    Tag = gameProfile
                };
                stockGameList.Items.Add(item);
            }

        }

        private void wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            // In case you don't have a progressBar Log the value instead 
            // Console.WriteLine(e.ProgressPercentage);
            Console.WriteLine(e.ProgressPercentage);
        }

        /// <summary>
        /// When the download is completed, this is executed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void wc_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                selected = GameProfileLoader.GameProfiles[stockGameList.SelectedIndex];
                Console.WriteLine("Download Cancelled");
                try
                {
                    File.Delete(selected.IconName);
                }
                catch
                {
                }
                return;
            }

            if (e.Error != null) // We have an error! Retry a few times, then abort.
            {
                selected = GameProfileLoader.GameProfiles[stockGameList.SelectedIndex];
                Console.WriteLine("Error Downloading");
                try
                {
                    File.Delete(selected.IconName);
                }
                catch
                {
                }
                return;
            }

            Console.WriteLine("Download Complete");
        }

        /// <summary>
        /// This method downloads the update from the TeknoParrot server.
        /// </summary>
        private void Download(WebClient wc, string _link, string _output)
        {
            File.Delete(Environment.GetEnvironmentVariable("TEMP") + "\\teknoparrot.zip");

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            // This will download a large image from the web, you can change the value
            // i.e a textbox : textBox1.Text
            try
            {
                using (wc)
                {
                    wc.Headers.Add("Referer", "https://teknoparrot.com/download");
                    wc.DownloadProgressChanged += wc_DownloadProgressChanged;
                    wc.DownloadFileCompleted += wc_DownloadFileCompleted;
                    wc.DownloadFileAsync(new Uri(_link), _output);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// This cancels the download
        /// </summary>
        private void cancelDownload()
        {
            wc.CancelAsync();
        }







        /// <summary>
        /// When the selection in the listbox is changed, it loads the appropriate game profile as the selected one.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StockGameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
            selected = GameProfileLoader.GameProfiles[stockGameList.SelectedIndex];
            var icon = selected.IconName;
            string[] splitString = selected.IconName.Split('\\');
            if (!File.Exists(selected.IconName))
            {
                network = CheckNet(splitString[1]);
                if (network == true)
                {
                    wc = new WebClient();
                    Download(wc, "https://raw.githubusercontent.com/teknogods/TeknoParrotUIThumbnails/master/Icons/" + splitString[1], selected.IconName);
                    
                }
                    BitmapImage imageBitmap = new BitmapImage(File.Exists(icon) ? new Uri("..\\" + icon, UriKind.Relative) : new Uri("../Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));
                    image1.Source = imageBitmap;    
            }
            else
            {
                BitmapImage imageBitmap = new BitmapImage(File.Exists(icon) ? new Uri("..\\" + icon, UriKind.Relative) : new Uri("../Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));
                image1.Source = imageBitmap;
            }
        }

        private bool CheckNet(string icon)
        {
            string url = "https://raw.githubusercontent.com/teknogods/TeknoParrotUIThumbnails/master/Icons/" + icon;
            WebRequest request = WebRequest.Create(url);
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response.StatusDescription == "OK")
                {
                    return true;
                }
                else
                {
                    return false;
                }
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
            Console.WriteLine("Adding " + selected.GameName + " to TP...");
            string[] splitString = selected.FileName.Split('\\');
            File.Copy(selected.FileName, "UserProfiles\\" + splitString[1]);
            string[] psargs = Environment.GetCommandLineArgs();
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
            try
            {
                Console.WriteLine("Removing " + selected.GameName + " from TP...");
                string[] splitString = selected.FileName.Split('\\');
                File.Delete("UserProfiles\\" + splitString[1]);
                string[] psargs = Environment.GetCommandLineArgs();
                System.Diagnostics.Process.Start(Application.ResourceAssembly.Location, psargs[0]);
                Application.Current.Shutdown();
            }
            catch
            {

            }
        }
    }
}
