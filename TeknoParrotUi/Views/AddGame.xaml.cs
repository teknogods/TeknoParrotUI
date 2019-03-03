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

        /// <summary>
        /// This method downloads the update from the TeknoParrot server.
        /// </summary>
        private void Download(string _link, string _output)
        {
            WebClient wc = new WebClient();
            File.Delete(Environment.GetEnvironmentVariable("TEMP") + "\\teknoparrot.zip");

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            // This will download a large image from the web, you can change the value
            // i.e a textbox : textBox1.Text
            try
            {
                using (wc)
                {
                    wc.DownloadFile(new Uri(_link), _output);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void DownLoadFileByWebRequest(string urlAddress, string filePath)
        {
            try
            {
                HttpWebRequest request = null;
                HttpWebResponse response = null;
                request = (HttpWebRequest)System.Net.HttpWebRequest.Create(urlAddress);
                request.Timeout = 30000;  //8000 Not work
                response = (HttpWebResponse)request.GetResponse();
                Stream s = response.GetResponseStream();
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                FileStream os = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
                byte[] buff = new byte[102400];
                int c = 0;
                while ((c = s.Read(buff, 0, 10400)) > 0)
                {
                    os.Write(buff, 0, c);
                    os.Flush();
                }
                os.Close();
                s.Close();

            }
            catch
            {
                return;
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
                    
                    DownLoadFileByWebRequest("https://raw.githubusercontent.com/teknogods/TeknoParrotUIThumbnails/master/Icons/" + splitString[1], selected.IconName);
                    
                }
                BitmapImage imageBitmap = new BitmapImage(File.Exists(icon) ? new Uri("pack://siteoforigin:,,,/" + icon, UriKind.Absolute) : new Uri("../Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));
                image1.Source = imageBitmap;    
            }
            else
            {
                BitmapImage imageBitmap = new BitmapImage(File.Exists(icon) ? new Uri("pack://siteoforigin:,,,/" + icon, UriKind.Absolute) : new Uri("../Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));
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
