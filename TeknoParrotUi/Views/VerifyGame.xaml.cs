using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Security.Cryptography;
using TeknoParrotUi.Helpers;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for VerifyGame.xaml
    /// </summary>
    public partial class VerifyGame
    {
        private readonly string _gameExe;
        private readonly string _validMd5;
        private List<string> _md5S = new List<string>();
        private bool _cancel;
        private double _total;
        private double _current;


        public VerifyGame(string gameExe, string validMd5)
        {
            InitializeComponent();
            Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = false;
            _validMd5 = validMd5;
            _gameExe = gameExe;
        }

        static async Task<string> CalculateMd5Async(string filename)
        {
            if (!System.IO.File.Exists(filename))
            {
                return null;
            }
            using (var md5 = MD5.Create())
            {
                using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true)
                ) // true means use IO async operations
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;
                    do
                    {
                        bytesRead = await stream.ReadAsync(buffer, 0, 4096);
                        if (bytesRead > 0)
                        {
                            md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                        }
                    } while (bytesRead > 0);

                    md5.TransformFinalBlock(buffer, 0, 0);
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        /// <summary>
        /// When the control is loaded, it starts checking every file. TODO: change the actual check to async
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            var invalidFiles = new List<string>();
            _md5S = File.ReadAllLines(_validMd5).Where(l => !l.Trim().StartsWith(";")).ToList();
            _total = _md5S.Count;
            var gamePath = "";
            try
            {
                gamePath = Path.GetDirectoryName(_gameExe);
            }
            catch
            {
                MessageBox.Show("You don't have a valid game executable path configured.", "Invalid game executable path", MessageBoxButton.OK, MessageBoxImage.Warning);
                verifyText.Text = Properties.Resources.VerifyCancelled;
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                return;
            }
            foreach (var t in _md5S)
            {
                if (_cancel)
                {
                    break;
                }

                var temp = t.Split(new[] {' '}, 2);
                var fileToCheck = temp[1].Replace("*", "");
                var tempMd5 =
                    await CalculateMd5Async(Path.Combine(gamePath ?? throw new InvalidOperationException(),
                        fileToCheck));
                if (tempMd5 != temp[0])
                {
                    invalidFiles.Add(fileToCheck);
                    listBoxFiles.Items.Add($"{Properties.Resources.VerifyInvalid}: {fileToCheck}");
                    listBoxFiles.SelectedIndex = listBoxFiles.Items.Count - 1;
                    listBoxFiles.ScrollIntoView(listBoxFiles.SelectedItem);
                    var first = _current / _total;
                    var calc = first * 100;
                    progressBar1.Dispatcher.Invoke(() => progressBar1.Value = calc,
                        System.Windows.Threading.DispatcherPriority.Background);
                }
                else
                {
                    listBoxFiles.Items.Add($"{Properties.Resources.VerifyValid}: {fileToCheck}");
                    listBoxFiles.SelectedIndex = listBoxFiles.Items.Count - 1;
                    listBoxFiles.ScrollIntoView(listBoxFiles.SelectedItem);
                    var first = _current / _total;
                    var calc = first * 100;
                    progressBar1.Dispatcher.Invoke(() => progressBar1.Value = calc,
                        System.Windows.Threading.DispatcherPriority.Background);
                }

                _current++;
            }

            if (_cancel)
            {
                verifyText.Text = Properties.Resources.VerifyCancelled;
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
            }
            else if (invalidFiles.Count > 0)
            {
                verifyText.Text = Properties.Resources.VerifyFilesInvalid;
                MessageBoxHelper.WarningOK(Properties.Resources.VerifyFilesInvalidExplain);
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                //TODO: add listbox
            }
            else
            {
                verifyText.Text = Properties.Resources.VerifyFilesValid;
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            _cancel = true;
        }
    }
}
