using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Shapes;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for PoliciesWindow.xaml
    /// </summary>
    public partial class PoliciesWindow : Window
    {
        private Application myApp;

        public PoliciesWindow(int policyNumber, Application app)
        {
            InitializeComponent();
            myApp = app;
        }

        private void PoliciesLink_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://teknoparrot.com/en/Home/Policies",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open policies link: " + ex.Message);
            }
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void QuitButton_Click(object sender, RoutedEventArgs e)
        {
            myApp.Shutdown(0);
        }
    }
}