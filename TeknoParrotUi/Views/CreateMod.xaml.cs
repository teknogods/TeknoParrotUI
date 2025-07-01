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
using TeknoParrotUi.Properties;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for AddGame.xaml
    /// </summary>
    public partial class CreateMod
    {
        private ContentControl _contentControl;
        private ModMenu _modmenu;
        private Library _library;
        List<string> filesToArchive = new List<string>();
        public CreateMod(ContentControl control, ModMenu modmenu, Library library)
        {
            InitializeComponent();
            _contentControl = control;
            _modmenu = modmenu;
            _library = library;

        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            dropDownGames.ItemsSource = _library._gameNames;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            WPFFolderBrowserDialog fbd = new WPFFolderBrowserDialog();
            fbd.Title = TeknoParrotUi.Properties.Resources.CreateModSelectRootFolder;
            if (fbd.ShowDialog() == true)
            {
                textBoxDir.Text = fbd.FileName;
            }
        }

        private void buttonScan_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(textBoxDir.Text))
            {
                if (textBoxDir.Text != "")
                {
                    string fileDir = textBoxDir.Text;
                    string[] newFiles = Directory.GetFiles(fileDir, "*.new", SearchOption.AllDirectories);
                    if (newFiles.Length > 0)
                    {
                        foreach (string s in newFiles)
                        {
                            string origFile = s.Replace(".new", "");
                            if (File.Exists(origFile))
                            {
                                byte[] patch = XDelta3.CreatePatch(File.ReadAllBytes(s), File.ReadAllBytes(origFile));
                                File.WriteAllBytes(origFile + ".xdelta", patch);
                                filesToArchive.Add(origFile + ".xdelta");
                                listBoxItems.Items.Add(origFile + ".xdelta");
                            }
                            else
                            {
                                //new file, didnt exist in original game
                                byte[] patch = XDelta3.CreatePatch(File.ReadAllBytes(s), new byte[0]);
                                File.WriteAllBytes(origFile + ".xdeltanew", patch);
                                filesToArchive.Add(origFile + ".xdeltanew");
                                listBoxItems.Items.Add(origFile + ".xdeltanew");
                            }
                        }

                        buttonScan.IsEnabled = false;
                        buttonArchive.IsEnabled = true;
                    }
                    else
                    {
                        Application.Current.Windows.OfType<MainWindow>().Single()
                            .ShowMessage(TeknoParrotUi.Properties.Resources.CreateModNoNewFilesFound);
                    }
                }
                else
                {
                    Application.Current.Windows.OfType<MainWindow>().Single()
                        .ShowMessage(TeknoParrotUi.Properties.Resources.CreateModPleaseSelectGame);
                }
            }
            else
            {
                Application.Current.Windows.OfType<MainWindow>().Single()
                    .ShowMessage(TeknoParrotUi.Properties.Resources.CreateModDirectoryNotExist);
            }
        }

        private void buttonArchive_Click(object sender, RoutedEventArgs e)
        {
            ModData md = new ModData();
            md.Creator = tbCreator.Text;
            md.Description = StringFromRichTextBox(rtbDesc);
            GameProfile selGame = (GameProfile) dropDownGames.SelectedItem;
            md.GameXML = Path.GetFileName(selGame.FileName);
            md.ModName = tbModName.Text;
            Guid obj = Guid.NewGuid();
            md.GUID = obj.ToString();
            WriteToXmlFile(textBoxDir.Text + "\\" + md.GUID + ".xml", md);
            using (FileStream zipToOpen = new FileStream(textBoxDir.Text + "\\" + md.GUID + ".zip", FileMode.Create))
            {
                using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                {
                    foreach (string ss in filesToArchive)
                    {
                        archive.CreateEntryFromFile(ss, ss.Replace(textBoxDir.Text + "\\", ""));
                    }
                }
            }
            Application.Current.Windows.OfType<MainWindow>().Single().ShowMessage(TeknoParrotUi.Properties.Resources.CreateModSuccess);
            _contentControl.Content = _modmenu;

        }
        string StringFromRichTextBox(RichTextBox rtb)
        {
            TextRange textRange = new TextRange(
                // TextPointer to the start of content in the RichTextBox.
                rtb.Document.ContentStart,
                // TextPointer to the end of content in the RichTextBox.
                rtb.Document.ContentEnd
            );

            // The Text property on a TextRange object returns a string
            // representing the plain text content of the TextRange.
            return textRange.Text;
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _contentControl.Content = _modmenu;
        }

        private void dropDownGames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            GameProfile sg = (GameProfile) dropDownGames.SelectedItem;
            if (sg.GamePath != "")
            {
                textBoxDir.Text = Path.GetDirectoryName(sg.GamePath);
            }

            buttonBrowse.IsEnabled = true;
        }
    }
}
