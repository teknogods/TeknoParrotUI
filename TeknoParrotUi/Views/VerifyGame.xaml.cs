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
        StreamReader reader;

        public VerifyGame(string gameExe, string validMd5)
        {
            InitializeComponent();
            _validMd5 = validMd5;
            _gameExe = gameExe;
        }

        private void loadFile()
        {
            md5s = File.ReadAllLines(_validMd5).Where(l => !l.Trim().StartsWith(";")).ToList();
            //while (!reader.EndOfStream)
            //{
            //    string temp = reader.ReadLine();
            //    if (temp.StartsWith(";")){
            //        continue;
            //    }
            //    else
            //    {
            //        md5s.Add(temp);
            //    }
            //}

        }

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

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            List<string> invalidFiles = new List<string>();
            md5s = File.ReadAllLines(_validMd5).Where(l => !l.Trim().StartsWith(";")).ToList();
            string gamePath = Path.GetDirectoryName(_gameExe);
            for (int i = 0; i < md5s.Count; i++)
            {
                string[] temp = md5s[i].Split(' ');
                string fileToCheck = temp[1].Replace("*", "");
                string tempMd5 = CalculateMD5(Path.Combine(gamePath, fileToCheck));
                if (tempMd5 != temp[0])
                {
                    invalidFiles.Add(fileToCheck);
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
            //string calcMd5 = CalculateMD5(_gameExe);
            //calcMd5 = calcMd5.ToUpper();
            //if (calcMd5 == _validMd5) {
            //    verifyText.Text = "Game executable Valid.";
            //}
            //else
            //{
            //    verifyText.Text = "Game executable Invalid";
            //    MessageBox.Show("Your main game executable (" + _gameExe + ") appears to be invalid. This may be due to something you've done to it, like a mod or translation, in which case ignore this message, otherwise you have an invalid dump and should probably re-acquire it.");
            //}
        }
    }
}
