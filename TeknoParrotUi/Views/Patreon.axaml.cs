using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for Patreon.axaml
    /// </summary>
    public partial class Patreon : UserControl
    {
        private ProcessStartInfo _cmdStartInfo;
        private Process _cmdProcess;
        List<GameProfile> _patreonGames = GameProfileLoader.GameProfiles.Where((profile) => profile.Patreon && !profile.DevOnly).ToList();

        public Patreon()
        {
            InitializeComponent();
            InitializeMe();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Find control references
            buttonRegister = this.FindControl<Button>("buttonRegister");
            buttonDereg = this.FindControl<Button>("buttonDereg");
            patreonKey = this.FindControl<TextBox>("patreonKey");
            listBoxConsole = this.FindControl<ListBox>("listBoxConsole");
            BecomeAPatron = this.FindControl<TextBlock>("BecomeAPatron");
            PatronGameListButton = this.FindControl<TextBlock>("PatronGameListButton");
        }

        private void InitializeMe()
        {
            _cmdStartInfo = new ProcessStartInfo();
            _cmdProcess = new Process();
            // in case people modify their profiles...
            if (_patreonGames.Count > 0)
            {
                PatronGameListButton.IsVisible = true;
                PatronGameListButton.Text = $"View Subscription Game List ({_patreonGames.Count} games!)";
            }
            else
            {
                PatronGameListButton.IsVisible = false;
            }

            using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot"))
            {
                var isPatron = key != null && key.GetValue("PatreonSerialKey") != null;

                if (isPatron)
                {
                    patreonKey.IsReadOnly = true;
                    buttonRegister.IsVisible = false;

                    var value = (byte[])key.GetValue("PatreonSerialKey");
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
                            Dispatcher.UIThread.InvokeAsync(
                                () => { listBoxConsole.Items.Add(e.Data); });
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
        private void PackIcon_MouseLeftButtonDown_1(object sender, PointerPressedEventArgs e)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "https://teknoparrot.shop",
                UseShellExecute = true
            };
            Process.Start(psi);
        }

        private void ButtonRegister_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(".\\TeknoParrot\\BudgieLoader.exe"))
            {
                ShowMessageBox("Missing Files", "BudgieLoader.exe is missing from TeknoParrot directory!");
                return;
            }

            if (string.IsNullOrEmpty(patreonKey.Text))
            {
                ShowMessageBox("Error", "Key must not be blank!");
                return;
            }

            listBoxConsole.Items.Clear();
            buttonRegister.IsVisible = false;
            var arguments = "-register " + patreonKey.Text;
            _cmdStartInfo.Arguments = arguments;
            _cmdProcess.Start();
            _cmdProcess.BeginOutputReadLine();
            _cmdProcess.WaitForExit();
            buttonDereg.IsVisible = true;
            InitializeMe();
        }

        private void ButtonDereg_Click(object sender, RoutedEventArgs e)
        {
            // Add deregister key implementation
        }

        private void TextBlock_MouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            // Add game list view implementation
        }

        // Helper methods
        private static byte[] FromHex(string hex)
        {
            hex = hex.Replace("-", "");
            byte[] raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return raw;
        }

        private async void ShowMessageBox(string title, string message)
        {
            var messageBoxStandardWindow = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var grid = new Grid();
            var textBlock = new TextBlock
            {
                Text = message,
                Margin = new Thickness(20),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };

            var button = new Button
            {
                Content = "OK",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 20)
            };

            button.Click += (s, e) => messageBoxStandardWindow.Close();

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(textBlock, 0);
            Grid.SetRow(button, 1);

            grid.Children.Add(textBlock);
            grid.Children.Add(button);

            messageBoxStandardWindow.Content = grid;

            await messageBoxStandardWindow.ShowDialog(GetWindow());
        }

        private Window GetWindow()
        {
            return TopLevel.GetTopLevel(this) as Window;
        }
    }
}