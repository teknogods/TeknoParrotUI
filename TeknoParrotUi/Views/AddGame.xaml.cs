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
        bool network = false;
        bool mainNet = false;

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
            mainNet = CheckNet("Icons/");

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
            if (mainNet == true)
            {
                network = CheckNet(selected.IconName);
                if (network == true)
                {
                    BitmapImage imageBitmap = new BitmapImage(new Uri("http://localhost:8000/" + icon, UriKind.Absolute));
                    image1.Source = imageBitmap;
                }
            }
            else
            {
                BitmapImage imageBitmap = new BitmapImage(new Uri("../Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));
                image1.Source = imageBitmap;
            }
        }

        private bool CheckNet(string icon)
        {
            string url = "http://localhost:8000/" + icon;
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
            if (network == true)
            {
                DownloadWindow update = new DownloadWindow("http://localhost:8000/" + selected.IconName, selected.IconName, false);
                update.ShowDialog();
            }
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
