using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for DebugJVS.axaml
    /// </summary>
    public partial class DebugJVS : Window
    {
        public bool JvsOverride;

        public DebugJVS()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            JvsOverride = false;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Find and assign all control references
            // For better performance, you might want to only find controls when they're needed
        }

        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            JvsOverride = !JvsOverride;
        }

        public void StartDebugInputThread()
        {
            Thread timerThread = new Thread(DebugInputThread);
            timerThread.Start();
        }

        public void DebugInputThread()
        {
            while (true)
            {
                if (JvsOverride)
                {
                    Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(DoCheckBoxesDude);
                }
                Thread.Sleep(16);
            }
        }

        public void DoCheckBoxesDude()
        {
            // Implementation should access controls via FindControl 
            // or have fields initialized in InitializeComponent
            // Example:
            // var p1Start = this.FindControl<CheckBox>("P1Start");
            // if (p1Start?.IsChecked == true)
            //     JvsHelper.StateView.PlayerDigitalButtons.Start = true;
        }

        private void AddCoin1_OnClick(object sender, RoutedEventArgs e)
        {
            // Coin insert implementation
        }

        private void AddCoin2_OnClick(object sender, RoutedEventArgs e)
        {
            // Coin insert implementation 
        }
    }
}