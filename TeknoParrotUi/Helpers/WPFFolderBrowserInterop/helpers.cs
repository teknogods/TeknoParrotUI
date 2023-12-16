using System;
using System.Windows;
using System.Collections.Generic;
using System.Text;

namespace WPFFolderBrowser.Interop
{
    internal static class Helpers
    {
        // TODO: Remove Helpers class, refactor
        internal static Window GetDefaultOwnerWindow()
        {
            Window defaultWindow = null;

            // TODO: Detect active window and change to that instead
            if (Application.Current != null && Application.Current.MainWindow != null)
            {
                defaultWindow = Application.Current.MainWindow;
            }
            return defaultWindow;
        }

    }
}
