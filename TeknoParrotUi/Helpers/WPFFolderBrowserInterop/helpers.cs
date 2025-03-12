using System;
using Avalonia;
using Avalonia.Controls;
using System.Collections.Generic;
using System.Text;

namespace TeknoParrotUi.Helpers
{
    internal static class Helpers
    {
        // Get the default owner window in Avalonia
        internal static Window GetDefaultOwnerWindow()
        {
            Window defaultWindow = null;

            // In Avalonia, we need to access the window through ApplicationLifetime
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                defaultWindow = desktop.MainWindow;
            }

            return defaultWindow;
        }
    }
}