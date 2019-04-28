using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using System.Diagnostics;
using System.Windows.Threading;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for Patreon.xaml
    /// </summary>
    public partial class Patreon
    {
        readonly ProcessStartInfo _cmdStartInfo = new ProcessStartInfo();
        readonly Process _cmdProcess = new Process();

        public Patreon()
        {
            InitializeComponent();

            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot"))
            {
                var isPatron = key != null && key.GetValue("PatreonSerialKey") != null;

                if (isPatron)
                {
                    patreonKey.IsReadOnly = true;
                    buttonRegister.Visibility = Visibility.Hidden;
                    var value = (byte[]) key.GetValue("PatreonSerialKey");
                    var data = FromHex(BitConverter.ToString(value));
                    var valueAsString = Encoding.ASCII.GetString(data); // GatewayServer
                    patreonKey.Text = valueAsString;
                    key.Close();
                    _cmdStartInfo.FileName = ".\\TeknoParrot\\BudgieLoader.exe";
                    _cmdStartInfo.RedirectStandardOutput = true;
                    _cmdStartInfo.RedirectStandardInput = true;
                    _cmdStartInfo.UseShellExecute = false;
                    _cmdStartInfo.CreateNoWindow = true;
                    _cmdStartInfo.Arguments = "-deactivate";
                    _cmdProcess.StartInfo = _cmdStartInfo;
                    _cmdProcess.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                    {
                        // Prepend line numbers to each line of the output.
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Application.Current.Dispatcher.BeginInvoke(
                                DispatcherPriority.Background,
                                new Action(() => { listBoxConsole.Items.Add(e.Data); }));
                            Console.WriteLine(e.Data);
                        }
                    });
                    _cmdProcess.EnableRaisingEvents = true;
                }
                else
                {
                    buttonDereg.Visibility = Visibility.Hidden;
                    _cmdStartInfo.FileName = ".\\TeknoParrot\\BudgieLoader.exe";
                    _cmdStartInfo.RedirectStandardOutput = true;
                    _cmdStartInfo.RedirectStandardError = true;
                    _cmdStartInfo.RedirectStandardInput = true;
                    _cmdStartInfo.UseShellExecute = false;
                    _cmdStartInfo.CreateNoWindow = true;
                    _cmdProcess.StartInfo = _cmdStartInfo;
                    _cmdProcess.ErrorDataReceived += cmd_Error;
                    _cmdProcess.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                    {
                        // Prepend line numbers to each line of the output.
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Application.Current.Dispatcher.BeginInvoke(
                                DispatcherPriority.Background,
                                new Action(() => { listBoxConsole.Items.Add(e.Data); }));
                            Console.WriteLine(e.Data);
                        }
                    });
                    _cmdProcess.EnableRaisingEvents = true;
                }
            }
        }

        /// <summary>
        /// When clicked, this will open the link to the patreon page for Teknogods
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PackIcon_MouseLeftButtonDown_1(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://www.patreon.com/Teknogods");
        }

        private void ButtonRegister_Click(object sender, RoutedEventArgs e)
        {
            if (patreonKey.Text == "")
            {
                MessageBox.Show("The Patreon Key must not be blank!");
            }
            else
            {
                var arguments = "-register " + patreonKey.Text;
                _cmdStartInfo.Arguments = arguments;
                _cmdProcess.Start();
                _cmdProcess.BeginOutputReadLine();
                _cmdProcess.WaitForExit();
                buttonRegister.Visibility = Visibility.Hidden;
            }
        }

        private void UpdateListBox(DataReceivedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(delegate { listBoxConsole.Items.Add(e.Data); });
        }

        static void cmd_Error(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine("Error from other process");
            Console.WriteLine(e.Data);
        }

        public static byte[] FromHex(string hex)
        {
            hex = hex.Replace("-", "");
            var raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return raw;
        }

        private void ButtonDereg_Click(object sender, RoutedEventArgs e)
        {
            _cmdProcess.Start();
            _cmdProcess.BeginOutputReadLine();
            _cmdProcess.WaitForExit();
            buttonDereg.Visibility = Visibility.Hidden;
            var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot", true);
            if (key == null)
            {
                Debug.WriteLine("Deregistered without deleting registry key");
            }
            else
            {
                key.DeleteValue("PatreonSerialKey");
            }
        }
    }
}