using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Security.Cryptography;
using TeknoParrotUi.Helpers;
using System.Diagnostics;

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
        private Library _library;
        private bool _cancel;
        private double _total;
        private double _current;
        private bool _verificationComplete = false;


        public VerifyGame(string gameExe, string validMd5, Library library)
        {
            InitializeComponent();
            Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = false;
            _validMd5 = validMd5;
            _gameExe = gameExe;
            _library = library;
        }

        static async Task<string> CalculateMd5Async(string filename)
        {
            if (!System.IO.File.Exists(filename))
            {
                Trace.WriteLine("Couldn't find: " + filename);
                return null;
            }

            if(filename.Contains("teknoparrot.ini"))
            {
                return null;
            }
            using (var md5 = MD5.Create())
            {
                using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true)
                ) // true means use IO async operations
                {
                    // Let's use a big buffer size to speed up checking on games like IDAC where some files are HUGE
                    byte[] buffer = new byte[81920];
                    int bytesRead;
                    do
                    {
                        bytesRead = await stream.ReadAsync(buffer, 0, 81920);
                        if (bytesRead > 0)
                        {
                            md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                        }
                    } while (bytesRead > 0);

                    md5.TransformFinalBlock(buffer, 0, 0);
                    return BitConverter.ToString(md5.Hash).Replace("-", "").ToLowerInvariant();

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

            CompleteVerification();
        }

        private void CompleteVerification()
        {
            _verificationComplete = true;
            buttonCancel.Content = Properties.Resources.Back;
            verifyText.Text = Properties.Resources.VerifyValid;
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_verificationComplete)
            {
                // If verification is complete, close/return to previous screen
                var parent = Parent as ContentControl;
                if (parent != null)
                    parent.Content = _library; // Assuming _library is the previous screen
                else
                    Application.Current.Windows.OfType<MainWindow>().Single().contentControl.Content = _library;
            }
            else
            {
                // If verification is still in progress, cancel it
                _cancel = true;
            }
        }
    }
}
