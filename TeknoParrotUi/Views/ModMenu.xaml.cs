using System;
using System.Collections.Generic;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media; // needed to change text colors.
using System.IO;
using TeknoParrotUi.Common;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Windows.Documents;
using Microsoft.Win32;
using TeknoParrotUi.Helpers;
using WPFFolderBrowser;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for AddGame.xaml
    /// </summary>
    public partial class ModMenu
    {
        private ContentControl _contentControl;
        private Library _library;

        public ModMenu(ContentControl control, Library library)
        {
            InitializeComponent();
            _contentControl = control;
            _library = library;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < 5; i++)
            {
                ModControl mc = new ModControl("test" + i, "game " + i, "something useful", "https://google.com");
                modList.Children.Add(mc);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CreateMod cm = new CreateMod(_contentControl, this, _library);
            _contentControl.Content = cm;
        }
    }
}
