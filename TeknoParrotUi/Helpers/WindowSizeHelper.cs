using System;
using System.Windows;

namespace TeknoParrotUi.Helpers
{
    class WindowSizeHelper
    {
        public double WindowTop { get; set; }
        public double WindowLeft { get; set; }
        public double WindowHeight { get; set; }
        public double WindowWidth { get; set; }

        public WindowSizeHelper()
        {
            //Load the settings
            Load();

            //System.Diagnostics.Trace.WriteLine($"Loaded window position: {WindowTop} {WindowLeft}");

            //Size it to fit the current screen
            SizeToFit();

            //Move the window at least partially into view
            MoveIntoView();

            //System.Diagnostics.Trace.WriteLine($"Window position after MoveIntoView: {WindowTop} {WindowLeft}");
        }

        public void SizeToFit()
        {
            // TODO: FIX
            // if (WindowHeight > SystemParameters.VirtualScreenHeight)
            //     WindowHeight = SystemParameters.VirtualScreenHeight - 40;

            // if (WindowWidth > SystemParameters.VirtualScreenWidth)
            //     WindowWidth = SystemParameters.VirtualScreenWidth;
        }

        public void MoveIntoView()
        {
            // TODO: FIX
            // if (WindowHeight > SystemParameters.VirtualScreenHeight)
            // {
            //     WindowHeight = SystemParameters.VirtualScreenHeight - 40;
            // }

            // if (WindowWidth > SystemParameters.VirtualScreenWidth)
            // {
            //     WindowWidth = SystemParameters.VirtualScreenWidth;
            // }

            // if (WindowTop < SystemParameters.VirtualScreenTop)
            // {
            //     WindowTop = SystemParameters.VirtualScreenTop;
            // }
            // else if (WindowTop + WindowHeight >
            //     SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
            // {
            //     WindowTop = SystemParameters.VirtualScreenTop +
            //                  SystemParameters.VirtualScreenHeight - WindowHeight;
            // }

            // if (WindowLeft < SystemParameters.VirtualScreenLeft)
            // {
            //     WindowLeft = SystemParameters.VirtualScreenLeft;
            // }
            // else if (WindowLeft + WindowWidth >
            //    SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth)
            // {
            //     WindowLeft = SystemParameters.VirtualScreenLeft +
            //                   SystemParameters.VirtualScreenWidth - WindowWidth;
            // }
        }

        private void Load()
        {
            try
            {
                Properties.Settings.Default.Reload();
                WindowTop = Properties.Settings.Default.WindowTop;
                WindowLeft = Properties.Settings.Default.WindowLeft;
                WindowHeight = Properties.Settings.Default.WindowHeight;
                WindowWidth = Properties.Settings.Default.WindowWidth;
            }
            catch
            {
                // do nothing...
            }
        }

        public void Save()
        {
            try
            {
                Properties.Settings.Default.Reload();
                Properties.Settings.Default.WindowTop = WindowTop;
                Properties.Settings.Default.WindowLeft = WindowLeft;
                Properties.Settings.Default.WindowHeight = WindowHeight;
                Properties.Settings.Default.WindowWidth = WindowWidth;
                Properties.Settings.Default.Save();
            }
            catch
            {
                // do nothing...
            }
        }
    }
}
