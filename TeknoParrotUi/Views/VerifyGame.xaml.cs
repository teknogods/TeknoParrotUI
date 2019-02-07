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
using System.IO;
using System.Security.Cryptography;


namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for VerifyGame.xaml
    /// </summary>
    public partial class VerifyGame : UserControl
    {
        private string _gameExe;
        private string _validMd5;
        List<string> md5s = new List<string>();

        public VerifyGame(string gameExe, string validMd5)
        {
            InitializeComponent();
            _validMd5 = validMd5;
            _gameExe = gameExe;
        }

        /// <summary>
        /// This calculates the MD5 hash for a specified file
        /// </summary>
        /// <param name="filename">Filename of the file you want to calculate a MD5 hash for</param>
        /// <returns></returns>
        static string CalculateMD5(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }


        static async Task<string> CalculateMD5Async(string filename)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true)) // true means use IO async operations
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
            List<string> invalidFiles = new List<string>();
            md5s = File.ReadAllLines(_validMd5).Where(l => !l.Trim().StartsWith(";")).ToList();
            string gamePath = Path.GetDirectoryName(_gameExe);
            for (int i = 0; i < md5s.Count; i++)
            {
                string[] temp = md5s[i].Split(' ');
                string fileToCheck = temp[1].Replace("*", "");
                string tempMd5 = await CalculateMD5Async(Path.Combine(gamePath, fileToCheck));
                if (tempMd5 != temp[0])
                {
                    invalidFiles.Add(fileToCheck);
                    listBoxFiles.Items.Add("Invalid: " + fileToCheck);
                    listBoxFiles.SelectedIndex = listBoxFiles.Items.Count - 1;
                    listBoxFiles.ScrollIntoView(listBoxFiles.SelectedItem);
                }
                else
                {
                    listBoxFiles.Items.Add("Valid: " + fileToCheck);
                    listBoxFiles.SelectedIndex = listBoxFiles.Items.Count - 1;
                    listBoxFiles.ScrollIntoView(listBoxFiles.SelectedItem);
                }
            }
            if(invalidFiles.Count > 0)
            {
                verifyText.Text = "Game files invalid";
                //TODO: add listbox
            }
            else
            {
                verifyText.Text = "Game files valid";
            }
        }
    }
}
