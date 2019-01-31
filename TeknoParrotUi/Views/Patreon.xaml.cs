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
using Microsoft.Win32;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for Patreon.xaml
    /// </summary>
    public partial class Patreon : UserControl
    {
        bool isPatreon = false;
        public Patreon()
        {
            InitializeComponent();
            //opening the subkey  
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot");

            //if it does exist, retrieve the stored values  
            if (key != null)
            {
                Console.WriteLine(key.GetValue("PatreonSerialKey"));
                if (key.GetValue("PatreonSerialKey") != null)
                {
                    isPatreon = true;
                }
                key.Close();
            }
            if (isPatreon == true)
            {
                buttonRegister.Visibility = Visibility.Hidden;
            }
            else
            {
                buttonDereg.Visibility = Visibility.Hidden;
            }
        }
    }
}
