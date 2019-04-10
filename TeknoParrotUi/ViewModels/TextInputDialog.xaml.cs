namespace TeknoParrotUi.ViewModels
{
    /// <summary>
    /// Interaction logic for TextInputDialog.xaml
    /// </summary>
    public partial class TextInputDialog
    {
        public TextInputDialog(TextInputDialogViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }

    public class TextInputDialogViewModel : Helpers.ViewModelBase
    {
        public string Message { get; set; } = "Enter some text";
        public string AffirmativeButtonText { get; set; } = "OK";
        public string NegativeButtonText { get; set; } = "CANCEL";

        private string _text;

        public string Text
        {
            get => _text;
            set { _text = value; OnPropertyChanged(); }
        }
    }
}
