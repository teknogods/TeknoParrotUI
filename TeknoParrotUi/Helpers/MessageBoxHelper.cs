using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TeknoParrotUi.Helpers
{
    public static class MessageBoxHelper
    {
        public static void ErrorOK(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static bool ErrorYesNo(string message)
        {
            return MessageBox.Show(message, "Error", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes;
        }

        public static void InfoOK(string message)
        {
            MessageBox.Show(message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static bool InfoYesNo(string message)
        {
            return MessageBox.Show(message, "Information", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes;
        }

        public static void WarningOK(string message)
        {
            MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static bool WarningYesNo(string message)
        {
            return MessageBox.Show(message, "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }
    }
}
