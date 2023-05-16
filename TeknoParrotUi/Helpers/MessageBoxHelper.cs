using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;

namespace TeknoParrotUi.Helpers
{
    public static class MessageBoxHelper
    {
        static List<string> alreadyPrompted = new List<string>();

        private static MessageBoxResult msg(string message, string title, MessageBoxButton btn, MessageBoxImage img, bool onlyOnce = false)
        {
            if (onlyOnce)
            {
                if (alreadyPrompted.Contains(message))
                {
                    Debug.WriteLine($"MessageBoxHelper: already shown {message}, not showing again, assuming No");
                    return MessageBoxResult.No;
                }

                alreadyPrompted.Add(message);
            }

            Debug.WriteLine($"MessageBoxHelper: showing {message}");

            return MessageBox.Show(message, title, btn, img);
        }

        public static void ErrorOK(string message, bool onlyOnce = false)
        {
            msg(message, Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error, onlyOnce);
        }

        public static bool ErrorYesNo(string message, bool onlyOnce = false)
        {
            return msg(message, Properties.Resources.Error, MessageBoxButton.YesNo, MessageBoxImage.Error, onlyOnce) == MessageBoxResult.Yes;
        }

        public static void InfoOK(string message, bool onlyOnce = false)
        {
            msg(message, Properties.Resources.Information, MessageBoxButton.OK, MessageBoxImage.Information, onlyOnce);
        }

        public static bool InfoYesNo(string message, bool onlyOnce = false)
        {
            return msg(message, Properties.Resources.Information, MessageBoxButton.YesNo, MessageBoxImage.Information, onlyOnce) == MessageBoxResult.Yes;
        }

        public static void WarningOK(string message, bool onlyOnce = false)
        {
            msg(message, Properties.Resources.Warning, MessageBoxButton.OK, MessageBoxImage.Warning, onlyOnce);
        }

        public static bool WarningYesNo(string message, bool onlyOnce = false)
        {
            return msg(message, Properties.Resources.Warning, MessageBoxButton.YesNo, MessageBoxImage.Warning, onlyOnce) == MessageBoxResult.Yes;
        }
    }
}
