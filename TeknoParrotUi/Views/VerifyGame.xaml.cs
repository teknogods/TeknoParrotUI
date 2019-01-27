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

        public VerifyGame(string gameExe, string validMd5)
        {
            InitializeComponent();
            _validMd5 = validMd5;
            _gameExe = gameExe;
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
            string calcMd5 = CalculateMD5(_gameExe);
            calcMd5 = calcMd5.ToUpper();
            if (calcMd5 == _validMd5) {
                verifyText.Text = "Game executable Valid.";
            }
            else
            {
                verifyText.Text = "Game executable Invalid";
                MessageBox.Show("Your main game executable (" + _gameExe + ") appears to be invalid. This may be due to something you've done to it, like a mod or translation, in which case ignore this message, otherwise you have an invalid dump and should probably re-acquire it.");
            }
        }
    }
}
