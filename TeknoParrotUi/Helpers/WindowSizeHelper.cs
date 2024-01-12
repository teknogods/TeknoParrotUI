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

            System.Diagnostics.Trace.WriteLine($"Loaded window position: {WindowTop} {WindowLeft}");

            //Size it to fit the current screen
            SizeToFit();

            //Move the window at least partially into view
            MoveIntoView();

            System.Diagnostics.Trace.WriteLine($"Window position after MoveIntoView: {WindowTop} {WindowLeft}");
        }

        public void SizeToFit()
        {
            if (WindowHeight > SystemParameters.VirtualScreenHeight)
                WindowHeight = SystemParameters.VirtualScreenHeight - 40;

            if (WindowWidth > SystemParameters.VirtualScreenWidth)
                WindowWidth = SystemParameters.VirtualScreenWidth;
        }

        public void MoveIntoView()
        {
            if (WindowHeight > System.Windows.SystemParameters.VirtualScreenHeight)
            {
                WindowHeight = System.Windows.SystemParameters.VirtualScreenHeight - 40;
            }

            if (WindowWidth > System.Windows.SystemParameters.VirtualScreenWidth)
            {
                WindowWidth = System.Windows.SystemParameters.VirtualScreenWidth;
            }

            if (WindowTop < System.Windows.SystemParameters.VirtualScreenTop)
            {
                WindowTop = System.Windows.SystemParameters.VirtualScreenTop;
            }
            else if (WindowTop + WindowHeight >
                System.Windows.SystemParameters.VirtualScreenTop + System.Windows.SystemParameters.VirtualScreenHeight)
            {
                WindowTop = System.Windows.SystemParameters.VirtualScreenTop +
                             System.Windows.SystemParameters.VirtualScreenHeight - WindowHeight;
            }

            if (WindowLeft < System.Windows.SystemParameters.VirtualScreenLeft)
            {
                WindowLeft = System.Windows.SystemParameters.VirtualScreenLeft;
            }
            else if (WindowLeft + WindowWidth >
               System.Windows.SystemParameters.VirtualScreenLeft + System.Windows.SystemParameters.VirtualScreenWidth)
            {
                WindowLeft = System.Windows.SystemParameters.VirtualScreenLeft +
                              System.Windows.SystemParameters.VirtualScreenWidth - WindowWidth;
            }
        }

        private void Load()
        {
            Properties.Settings.Default.Reload();
            WindowTop = Properties.Settings.Default.WindowTop;
            WindowLeft = Properties.Settings.Default.WindowLeft;
            WindowHeight = Properties.Settings.Default.WindowHeight;
            WindowWidth = Properties.Settings.Default.WindowWidth;
        }

        public void Save()
        {
            Properties.Settings.Default.Reload();
            Properties.Settings.Default.WindowTop = WindowTop;
            Properties.Settings.Default.WindowLeft = WindowLeft;
            Properties.Settings.Default.WindowHeight = WindowHeight;
            Properties.Settings.Default.WindowWidth = WindowWidth;

            System.Diagnostics.Trace.WriteLine($"Saving window position: {WindowTop} {WindowLeft}");

            Properties.Settings.Default.Save();

        }
    }
}
