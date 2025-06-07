using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Serialization;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using TeknoParrotUi.Properties;
using Color = System.Drawing.Color;
using Path = System.IO.Path;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for ModControl.xaml
    /// </summary>
    public partial class ModControl : UserControl
    {
        private string _modName;
        private string _gameName;
        private string _description;
        private string _zipUrl;
        private string _creator;
        private GameProfile _thisGame;
        bool isDone = false;
        private ModMenu _modMenu;
        public ModControl(string modName, string gameName, string description, string zipUrl, string creator, GameProfile thisGame, ModMenu modmenu)
        {
            InitializeComponent();
            _modName = modName;
            _gameName = gameName;
            _description = description;
            _zipUrl = zipUrl;
            _creator = creator;
            _thisGame = thisGame;
            _modMenu = modmenu;

            labelGameName.Text = _gameName;
            labelModName.Text = _modName;
            buttonDescription.Text = _description;
            tbCreator.Text = _creator;

            if (Lazydata.ParrotData.UiDarkMode)
            {
                uiGrid.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF,0x36, 0x36,0x36));
                buttonDescription.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF,0x55,0x55,0x55));
            }
        }

        private async void buttonDl_Click(object sender, RoutedEventArgs e)
        {
            /* 
             * Ok this is the dumb part, writing steps to remind myself how this is meant to work..
             * 1. download zip from gh
             * 2. extract zip in game folder root
             * 3. find all xdelta files
             * 4. patch all files
             * 5. ???
             * 6. profit
             *
             * i don't think normal downloadcontrol will work for this one, it's more tailored for updates
             */

  
            string gameRoot = Path.GetDirectoryName(_thisGame.GamePath);
            if (Directory.Exists(gameRoot))
            {

                var patchZip = new DownloadWindow(_zipUrl, _modName, true);

                patchZip.Closed += (x, x2) =>
                {
                    if (patchZip.data == null)
                        return;
                    using (var memoryStream = new MemoryStream(patchZip.data))
                    using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Read))
                    {
                        foreach (var entry in zip.Entries)
                        {
                            //remove TeknoParrotUIThumbnails-master/
                            var name = entry.FullName.Substring(entry.FullName.IndexOf('/') + 1);
                            if (string.IsNullOrEmpty(name)) continue;
                            Debug.WriteLine($"Extracting {name}");

                            try
                            {
                                using (var entryStream = entry.Open())
                                using (var dll = File.Create(gameRoot + "\\" + name))
                                {
                                    entryStream.CopyTo(dll);
                                    entryStream.Close();
                                }

                                string xDeltaFile = gameRoot + "\\" + name;
                                if (name.Contains(".xdeltanew"))
                                {
                                    byte[] patchedFile = XDelta3.ApplyPatch(File.ReadAllBytes(xDeltaFile), new byte[0]);
                                    File.WriteAllBytes(xDeltaFile.Replace(".xdeltanew", ""), patchedFile);
                                }
                                else
                                {
                                    byte[] patchedFile = XDelta3.ApplyPatch(File.ReadAllBytes(xDeltaFile),
                                        File.ReadAllBytes(xDeltaFile.Replace(".xdelta", "")));
                                    File.WriteAllBytes(xDeltaFile.Replace(".xdelta", ""), patchedFile);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                                // ignore..?
                            }
                        }
                    }

                    isDone = true;
                };
                patchZip.Show();
                await isItDone();
                Application.Current.Windows.OfType<MainWindow>().Single()
                    .ShowMessage(TeknoParrotUi.Properties.Resources.ModControlModDownloadedSuccessfully);
                buttonDl.IsEnabled = false;
                _modMenu.installedGUIDs.Add(Path.GetFileNameWithoutExtension(_zipUrl));
                WriteToXmlFile("InstalledMods.xml", _modMenu.installedGUIDs, false);
            }
            else
            {
                Application.Current.Windows.OfType<MainWindow>().Single()
                        .ShowMessage(TeknoParrotUi.Properties.Resources.ModControlGameDirectoryNotExist);
            }
        }

        private async Task isItDone()
        {
            while (!isDone)
            {
                await Task.Delay(100);
            }
            return;
        }

        static void WriteToXmlFile<T>(string filePath, T objectToWrite, bool append = false) where T : new()
        {
            TextWriter writer = null;
            try
            {
                var serializer = new XmlSerializer(typeof(T));
                writer = new StreamWriter(filePath, append);
                serializer.Serialize(writer, objectToWrite);
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }
    }
}
