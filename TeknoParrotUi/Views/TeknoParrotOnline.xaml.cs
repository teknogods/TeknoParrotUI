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
    /// Interaction logic for TeknoParrotOnline.xaml
    /// </summary>
    public partial class TeknoParrotOnline : UserControl
    {
        bool isLoaded = false;
        public TeknoParrotOnline()
        {
            InitializeComponent();
        }

        private bool IsBusy()
        {
            return ListenThread.IsInLobby || ListenThread.JoinLobby || ListenThread.CreateLobby || ListenThread.WaitingForCreation || ListenThread.WaitingForJoin;
        }

        private void BtnRefresh_OnClick(object sender, RoutedEventArgs e)
        {
            ListenThread.SelectedGameId = (GameId)((FrameworkElement)GameListCombo.SelectedItem).Tag;
            BtnRefresh.IsEnabled = false;
            ListenThread.RefreshList = true;
        }

        private void BtnJoinGame_OnClick(object sender, RoutedEventArgs e)
        {
            if (IsBusy())
            {
                return;
            }

            if (!(GridLobbies.SelectedItem is LobbyList data))
                return;
            BtnJoinGame.IsEnabled = false;
            ListenThread.LobbyToJoin = data.LobbyData;
            ListenThread.JoinLobby = true;
            this.IsEnabled = false;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (IsBusy())
            {
                return;
            }
            Application.Current.Windows.OfType<MainWindow>().SingleOrDefault(x => x.IsActive).contentControl.Content = new Views.TPOnlineCreate();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (isLoaded == false)
            {
                ListenThread.StateSection = MemoryMappedFile.CreateOrOpen("TeknoParrot_NetState", Marshal.SizeOf<TpNetStateStruct.TpNetState>());
                ListenThread.StateView = ListenThread.StateSection.CreateViewAccessor();
                MainWindow mainWindow = Application.Current.Windows.OfType<MainWindow>().SingleOrDefault(x => x.IsActive);
                isLoaded = true;
                new Thread(() => ListenThread.Listen(GridLobbies, BtnRefresh, BtnJoinGame, mainWindow)).Start();
                ListenThread.SelectedGameId = (GameId)((FrameworkElement)GameListCombo.SelectedItem).Tag;
                BtnRefresh.IsEnabled = false;
                ListenThread.RefreshList = true;
            }
            else
            {
                //don't
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            //ListenThread.WantsQuit = true;
        }

        private void GameListCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoaded == true)
            {
                ListenThread.SelectedGameId = (GameId)((FrameworkElement)GameListCombo.SelectedItem).Tag;
                BtnRefresh.IsEnabled = false;
                ListenThread.RefreshList = true;
            }
            else
            {
                //no
            }
        }
    }
}

