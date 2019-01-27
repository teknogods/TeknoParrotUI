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

        private void StockGameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;

            selected = GameProfileLoader.GameProfiles[stockGameList.SelectedIndex];
            var icon = selected.IconName;
            BitmapImage imageBitmap = new BitmapImage(File.Exists(icon) ? new Uri("..\\" + icon, UriKind.Relative) : new Uri("../Resources/teknoparrot_by_pooterman-db9erxd.png", UriKind.Relative));
            image1.Source = imageBitmap;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Adding " + selected.GameName + " to TP...");
            string[] splitString = selected.FileName.Split('\\');
            File.Copy(selected.FileName, "UserProfiles\\" + splitString[1]);
            string[] psargs = Environment.GetCommandLineArgs();
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location, psargs[0]);
            Application.Current.Shutdown();
        }

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
