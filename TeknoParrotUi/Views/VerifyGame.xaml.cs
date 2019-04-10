﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Security.Cryptography;


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
            var gamePath = Path.GetDirectoryName(_gameExe);
            foreach (var t in _md5S)
            {
                if (_cancel)
                {
                    break;
                }

                var temp = t.Split(' ');
                var fileToCheck = temp[1].Replace("*", "");
                var tempMd5 =
                    await CalculateMd5Async(Path.Combine(gamePath ?? throw new InvalidOperationException(),
                        fileToCheck));
                if (tempMd5 != temp[0])
                {
                    invalidFiles.Add(fileToCheck);
                    listBoxFiles.Items.Add("Invalid: " + fileToCheck);
                    listBoxFiles.SelectedIndex = listBoxFiles.Items.Count - 1;
                    listBoxFiles.ScrollIntoView(listBoxFiles.SelectedItem);
                    var first = _current / _total;
                    var calc = first * 100;
                    progressBar1.Dispatcher.Invoke(() => progressBar1.Value = calc,
                        System.Windows.Threading.DispatcherPriority.Background);
                }
                else
                {
                    listBoxFiles.Items.Add("Valid: " + fileToCheck);
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
                verifyText.Text = "Verification Cancelled.";
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
            }
            else if (invalidFiles.Count > 0)
            {
                verifyText.Text = "Game files invalid";
                MessageBox.Show(
                    "Your game appears to have invalid files. This could be due to a bad download, bad dump, virus infection, or you have modifications installed like resolution and english patches.");
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
                //TODO: add listbox
            }
            else
            {
                verifyText.Text = "Game files valid";
                Application.Current.Windows.OfType<MainWindow>().Single().menuButton.IsEnabled = true;
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            _cancel = true;
        }
    }
}