using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;

namespace TeknoParrotUi.ViewModels
{
    public partial class TextInputDialog : UserControl
    {
        public event EventHandler<DialogResultEventArgs> DialogClosed;

        public string Message { get; set; }
        public string AffirmativeButtonText { get; set; } = "OK";
        public string NegativeButtonText { get; set; } = "CANCEL";

        public TextInputDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Find control references if needed
            CancelButton = this.FindControl<Button>("CancelButton");
            OkButton = this.FindControl<Button>("OkButton");
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogClosed?.Invoke(this, new DialogResultEventArgs(false));
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogClosed?.Invoke(this, new DialogResultEventArgs(true));
        }
    }

    public class DialogResultEventArgs : EventArgs
    {
        public bool Result { get; }

        public DialogResultEventArgs(bool result)
        {
            Result = result;
        }
    }
}