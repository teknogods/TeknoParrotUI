using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;

namespace TeknoParrotUi.Views
{
    public partial class AddGame : UserControl
    {
        private ContentControl _contentControl;
        private Library _library;

        public AddGame()
        {
            InitializeComponent();
        }

        public AddGame(ContentControl contentControl, Library library)
        {
            InitializeComponent();
            _contentControl = contentControl;
            _library = library;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            stockGameList = this.FindControl<ListBox>("stockGameList");
            GenreBox = this.FindControl<ComboBox>("GenreBox");
            GameSearchBox = this.FindControl<TextBox>("GameSearchBox");
            gameIcon = this.FindControl<Image>("gameIcon");
            AddButton = this.FindControl<Button>("AddButton");
            DeleteButton = this.FindControl<Button>("DeleteButton");
            GameCountLabel = this.FindControl<TextBlock>("GameCountLabel");
            stockGameList.SelectionChanged += StockGameList_SelectionChanged;
            GenreBox.SelectionChanged += UserControl_Loaded;
            GameSearchBox.TextChanged += UserControl_Loaded;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Original loading logic would go here
            // You'll need to port the WPF logic to Avalonia
        }

        private void StockGameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Original selection changed logic would go here
        }

        private void AddGameButton(object sender, RoutedEventArgs e)
        {
            // Original add game logic would go here
        }

        private void DeleteGameButton(object sender, RoutedEventArgs e)
        {
            // Original delete game logic would go here
        }
    }
}