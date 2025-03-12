using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
// TODO: REDO ENTIRE FILE
namespace TeknoParrotUi.Views
{
    public partial class CreateMod : Window
    {
        private ContentControl _contentControl;
        private ModMenu _modmenu;
        private Library _library;
        private List<string> filesToArchive = new List<string>();

        public CreateMod(ContentControl control, ModMenu modmenu, Library library)
        {
            InitializeComponent();
            _contentControl = control;
            _modmenu = modmenu;
            _library = library;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // TODO: FIX
            // Find and assign control references
            // dropDownGames = this.FindControl<ComboBox>("dropDownGames");
            // textBoxDir = this.FindControl<TextBox>("textBoxDir");
            // buttonBrowse = this.FindControl<Button>("buttonBrowse");
            // tbModName = this.FindControl<TextBox>("tbModName");
            // tbCreator = this.FindControl<TextBox>("tbCreator");
            // rtbDesc = this.FindControl<TextBox>("rtbDesc");
            // buttonScan = this.FindControl<Button>("buttonScan");
            // buttonArchive = this.FindControl<Button>("buttonArchive");
            // listBoxItems = this.FindControl<ListBox>("listBoxItems");
            // MinimizeButton.Click += BtnMinimize;
            // CloseButton.Click += BtnQuit;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            //dropDownGames.ItemsSource = _library._gameNames;
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // Folder picker dialog in Avalonia
            // var folderDialog = await TopLevel.GetTopLevel(this).StorageProvider
            //     .OpenFolderPickerAsync(new FolderPickerOpenOptions
            //     {
            //         Title = "Please select the root folder of your game.",
            //         AllowMultiple = false
            //     });

            // if (folderDialog.Count > 0)
            // {
            //     //textBoxDir.Text = folderDialog[0].Path.LocalPath;
            // }
        }

        private void dropDownGames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // if (dropDownGames.SelectedItem != null)
            // {
            //     buttonBrowse.IsEnabled = true;
            // }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Go back to mod menu
            _contentControl.Content = _modmenu;
        }

        private void buttonScan_Click(object sender, RoutedEventArgs e)
        {
            // Implement your scanning logic here
            // Will need to be updated for Avalonia-specific UI updates
        }

        private void buttonArchive_Click(object sender, RoutedEventArgs e)
        {
            // Implement your archive creation logic here
            // Will need to be updated for Avalonia-specific UI updates
        }

        private string StringFromTextBox(TextBox textBox)
        {
            return textBox.Text ?? string.Empty;
        }

        private static void WriteToXmlFile<T>(string filePath, T objectToWrite, bool append = false) where T : new()
        {
            // TextWriter writer = null;
            // try
            // {
            //     var serializer = new XmlSerializer(typeof(T));
            //     writer = new StreamWriter(filePath, append);
            //     serializer.Serialize(writer, objectToWrite);
            // }
            // finally
            // {
            //     writer?.Close();
            // }
        }
    }
}