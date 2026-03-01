using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
using TeknoParrotUi.Helpers;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for UpdaterDialog.xaml
    /// </summary>
    public partial class UpdaterDialog : UserControl
    {
        private List<GitHubUpdates> updatesToDo;
        private ContentControl _contentControl;
        private Library _library;
        public UpdaterDialog(List<GitHubUpdates> updates, ContentControl control, Library library)
        {
            InitializeComponent();
            updatesToDo = updates;
            _contentControl = control;
            _library = library;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            updaterList.Children.Clear();
            foreach (GitHubUpdates g in updatesToDo)
            {
                updaterList.Children.Add(g);

            }
        }

        private async Task checkIfDone()
        {
            await Task.Run(() =>
            {
                int count = 0;
                List<DownloadControl> child = new List<DownloadControl>();
                this.Dispatcher.Invoke(() =>
                {
                    count = updaterList.Children.Count;
                    foreach (DownloadControl d in updaterList.Children)
                    {
                        child.Add(d);
                    }
                });

                    List<DownloadControl> blah = new List<DownloadControl>();
                    while (blah.Count < count)
                    {
                        foreach (DownloadControl d in child)
                        {
                            if (d.isFinished && !blah.Contains(d))
                            {
                                blah.Add(d);
                            }
                        }

                        if (blah.Count == child.Count)
                        {
                            break;
                        }
                    }
            });
            return;
        }

        private async void buttonBeginUpdate_Click(object sender, RoutedEventArgs e)
        {
            bool isUi = false;
            buttonBeginUpdate.IsEnabled = false;
            List<DownloadControl> downloads = new List<DownloadControl>();
            foreach (GitHubUpdates g in updaterList.Children)
            {
                if (g.isSelectedForUpdate.IsChecked == true) 
                {
                    var dw = g.DoUpdate();
                    downloads.Add(dw);
                }

                if (g._componentUpdated.name == "TeknoParrotUI")
                {
                    isUi = true;
                }
            }
            if (downloads.Count > 0)
            {
                updaterList.Children.Clear();
                foreach (DownloadControl d in downloads)
                {
                    updaterList.Children.Add(d);
                }

                await checkIfDone();
                string currentDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string targetExePath = System.IO.Path.Combine(currentDirectory, "ParrotPatcher.exe");
                Process.Start(targetExePath);
                Process.GetCurrentProcess().Kill();
                //Application.Current.Windows.OfType<MainWindow>().Single().ShowMessage("Updater Complete!");

                ////thingy here
                //buttonCancel.Content = "Return to Library";
                //if (isUi)
                //{
                //    if (MessageBoxHelper.InfoYesNo(Properties.Resources.UpdaterRestart))
                //    {
                //        string[] psargs = Environment.GetCommandLineArgs();
                //        System.Diagnostics.Process.Start(Application.ResourceAssembly.Location, psargs[0]);

                //        Process.GetCurrentProcess().Kill();
                //    }
                //    else
                //    {
                //        Process.GetCurrentProcess().Kill();
                //    }
                //}


            }
            else
            {
                Application.Current.Windows.OfType<MainWindow>().Single().ShowMessage("Please select at least one component to update!");
                buttonBeginUpdate.IsEnabled = true;
            }
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Windows.OfType<MainWindow>().Single()._updaterComplete = true;
            _contentControl.Content = _library;
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            Application.Current.Windows.OfType<MainWindow>().Single()._updaterComplete = true;
        }
    }
}
