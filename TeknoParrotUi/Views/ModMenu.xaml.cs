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
using System.Xml.Serialization;
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
        private List<ModControl> modControls = new List<ModControl>();
        public List<string> installedGUIDs;
        public ModMenu(ContentControl control, Library library)
        {
            InitializeComponent();
            _contentControl = control;
            _library = library;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            cbGameList.ItemsSource = _library._gameNames;
            if (modList.Children.Count <= 0)
            {
                WebClient wc = new WebClient();

                byte[] modXML =
                    await wc.DownloadDataTaskAsync(
                        "https://github.com/nzgamer41/tpgamemods/releases/latest/download/mods.xml");

                List<ModData> mods = ReadFromXmlFile<List<ModData>>(modXML);
                if (File.Exists("InstalledMods.xml"))
                {
                    installedGUIDs = ReadFromXmlFile<List<string>>(File.ReadAllBytes("InstalledMods.xml"));
                }
                else
                {
                    installedGUIDs = new List<string>();
                }

                foreach (ModData m in mods)
                {
                    if (File.Exists("UserProfiles\\" + m.GameXML))
                    {
                        GameProfile gp = JoystickHelper.DeSerializeGameProfile("UserProfiles\\" + m.GameXML, true);
                        string url = "https://github.com/nzgamer41/tpgamemods/raw/master/" + m.GUID + ".zip";
                        ModControl mc = new ModControl(m.ModName, gp.GameNameInternal, m.Description, url, m.Creator, gp, this);
                        if (installedGUIDs.Contains(m.GUID))
                        {
                            mc.buttonDl.IsEnabled = false;
                        }
                        modList.Children.Add(mc);
                        modControls.Add(mc);
                    }
                }

                if (modList.Children.Count == 0)
                {
                    Application.Current.Windows.OfType<MainWindow>().Single().ShowMessage("You have no games added that have mods available!");
                }

            }

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CreateMod cm = new CreateMod(_contentControl, this, _library);
            _contentControl.Content = cm;
        }

        static T ReadFromXmlFile<T>(byte[] filePath) where T : new()
        {
            MemoryStream reader = null;
            try
            {
                var serializer = new XmlSerializer(typeof(T));
                reader = new MemoryStream(filePath);
                return (T)serializer.Deserialize(reader);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }
        
        private void cbGameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            List<ModControl> lmc = new List<ModControl>();
            GameProfile temp = (GameProfile)cbGameList.SelectedItem;
            foreach (ModControl mc in modControls)
            {
                if (mc.labelGameName.Text == temp.GameNameInternal)
                {
                    lmc.Add(mc);
                }
            }
            modList.Children.Clear();

            foreach (ModControl mc in lmc)
            {
                modList.Children.Add(mc);
            }
        }
    }
}
