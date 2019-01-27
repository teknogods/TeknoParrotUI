using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MahApps.Metro.Controls;
using TeknoParrotUi.Common;
using System.IO;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for RemoveGameWindow.xaml
    /// </summary>
    public partial class RemoveGameWindow : MetroWindow
    {
        public RemoveGameWindow()
        {
            InitializeComponent();
        }
        GameProfile selected = new GameProfile();
        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (var gameProfile in GameProfileLoader.UserProfiles)
            {
                ListBoxItem item = new ListBoxItem
                {
                    Content = gameProfile.GameName,
                    Tag = gameProfile
                };

                gameListBox.Items.Add(item);
            }
        }

        private void GameListBox_SelectionChanged_1(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
            selected = GameProfileLoader.UserProfiles[gameListBox.SelectedIndex];
            selectedGame.Text = selected.GameName;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Removing " + selected.GameName + " from TP...");
            string[] splitString = selected.FileName.Split('\\');
            File.Delete("UserProfiles\\" + splitString[1]);
            this.Close();
        }
    }
}

