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
using System.Diagnostics;
using System.ComponentModel;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using TeknoParrotUi.AvailCode;
using System.Windows.Threading;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for Patreon.xaml
    /// </summary>
    public partial class Patreon : UserControl
    {
        //define variables
        bool isPatreon = false;
    
            ProcessStartInfo cmdStartInfo = new ProcessStartInfo();


            Process cmdProcess = new Process();


        public Patreon()
        {
            InitializeComponent();
            //opening the subkey  
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot");

            //if it does exist, retrieve the stored values  
            if (key != null)
            {
                //check whether the user is a patron
                if (key.GetValue("PatreonSerialKey") != null)
                {
                    isPatreon = true;
                }
            }
            if (isPatreon == true)
            {
                patreonKey.IsReadOnly = true;
                buttonRegister.Visibility = Visibility.Hidden;
                var value = (byte[])key.GetValue("PatreonSerialKey");
                byte[] data = FromHex(BitConverter.ToString(value));
                string valueAsString = Encoding.ASCII.GetString(data); // GatewayServer
                patreonKey.Text = valueAsString;
                key.Close();
                cmdStartInfo.FileName = "ParrotLoader.exe";
                cmdStartInfo.RedirectStandardOutput = true;
                cmdStartInfo.RedirectStandardInput = true;
                cmdStartInfo.UseShellExecute = false;
                cmdStartInfo.CreateNoWindow = true;
                cmdStartInfo.Arguments = "-deactivate";
                cmdProcess.StartInfo = cmdStartInfo;
                cmdProcess.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    // Prepend line numbers to each line of the output.
                    if (!String.IsNullOrEmpty(e.Data))
                    {
                        Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() => {
                            listBoxConsole.Items.Add(e.Data);
                        }));                        
                        Console.WriteLine(e.Data);
                    }
                });
                cmdProcess.EnableRaisingEvents = true;

            }
            else
            {
                buttonDereg.Visibility = Visibility.Hidden;
                key.Close();
                cmdStartInfo.FileName = @"ParrotLoader.exe";
                cmdStartInfo.RedirectStandardOutput = true;
                cmdStartInfo.RedirectStandardError = true;
                cmdStartInfo.RedirectStandardInput = true;
                cmdStartInfo.UseShellExecute = false;
                cmdStartInfo.CreateNoWindow = true;
                cmdProcess.StartInfo = cmdStartInfo;
                cmdProcess.ErrorDataReceived += cmd_Error;
                cmdProcess.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    // Prepend line numbers to each line of the output.
                    if (!String.IsNullOrEmpty(e.Data))
                    {
                        Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() => {
                            listBoxConsole.Items.Add(e.Data);
                        }));  
                        Console.WriteLine(e.Data);
                    }
                });
                cmdProcess.EnableRaisingEvents = true;
            }
        }

        /// <summary>
        /// When clicked, this will open the link to the patreon page for Teknogods
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PackIcon_MouseLeftButtonDown_1(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.patreon.com/Teknogods");
        }

        private void ButtonRegister_Click(object sender, RoutedEventArgs e)
        {
            if (patreonKey.Text == "")
            {
                MessageBox.Show("The Patreon Key must not be blank!");
            }
            else
            {
                string arguments = "-register " + patreonKey.Text;
                cmdStartInfo.Arguments = arguments;
                cmdProcess.Start();
                cmdProcess.BeginOutputReadLine();
                cmdProcess.WaitForExit();
                buttonRegister.Visibility = Visibility.Hidden;
            }
        }
        private void updateListBox(DataReceivedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke((Action)delegate {
                listBoxConsole.Items.Add(e.Data);
            });
        }

        static void cmd_Error(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine("Error from other process");
            Console.WriteLine(e.Data);
        }

        public static byte[] FromHex(string hex)
        {
            hex = hex.Replace("-", "");
            byte[] raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return raw;
        }

        private void ButtonDereg_Click(object sender, RoutedEventArgs e)
        {
            cmdProcess.Start();
            cmdProcess.BeginOutputReadLine();
            cmdProcess.WaitForExit();
            buttonDereg.Visibility = Visibility.Hidden;
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot",true);
            if (key == null)
            {
                Console.WriteLine("Deregistered without deleting registry key");
            }
            else
            {
                key.DeleteValue("PatreonSerialKey");
            }
        }
    }
}

