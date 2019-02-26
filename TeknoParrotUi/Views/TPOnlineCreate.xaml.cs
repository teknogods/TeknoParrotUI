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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using MahApps.Metro.Controls;
using TeknoParrotUi.AvailCode;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for TPOnlineCreate.xaml
    /// </summary>
    public partial class TPOnlineCreate : UserControl
    {
        public TPOnlineCreate()
        {
            InitializeComponent();
        }

        private void BtnLaunchLobby_OnClick(object sender, RoutedEventArgs e)
        {
            ListenThread.LobbyName = TxtLobbyName.Text;
            ListenThread.LobbyGame = (GameId)((FrameworkElement)GameSelectCombo.SelectedItem).Tag;
            ListenThread.CreateLobby = true;
            Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = MainWindow.tpOnline;
            this.IsEnabled = false;
        }
    }
}
