using System;
using System.Collections.Generic;
using System.Drawing;
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

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for ModControl.xaml
    /// </summary>
    public partial class ModControl : UserControl
    {
        private string _modName;
        private string _gameName;
        private string _description;
        private string _zipUrl;
        public ModControl(string modName, string gameName, string description, string zipUrl)
        {
            InitializeComponent();
            _modName = modName;
            _gameName = gameName;
            _description = description;
            _zipUrl = zipUrl;

            labelGameName.Text = _gameName;
            labelModName.Text = _modName;
            buttonDescription.Text = _description;
        }

        private void buttonDl_Click(object sender, RoutedEventArgs e)
        {

        }
        public DownloadControl DoUpdate()
        {
            DownloadControl downloadWindow = new DownloadControl(_zipUrl, _modName, true);
            return downloadWindow;
        }
    }
}
