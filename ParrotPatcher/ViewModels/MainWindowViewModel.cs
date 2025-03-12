using ReactiveUI;
using System.ComponentModel;
using System.Reactive;

namespace ParrotPatcher.ViewModels
{
    public class MainWindowViewModel : ViewModelBase, IReactiveObject
    {
        private string _title = "Parrot Patcher";

        public event PropertyChangingEventHandler PropertyChanging;


        public string Title
        {
            get => _title;
            set => this.RaiseAndSetIfChanged(ref _title, value);
        }

        public ReactiveCommand<Unit, Unit> UpdateCommand { get; }

        public MainWindowViewModel()
        {
            UpdateCommand = ReactiveCommand.Create(OnUpdate);
        }

        private void OnUpdate()
        {
            // Logic for updating goes here
        }

        public void RaisePropertyChanging(PropertyChangingEventArgs args)
        {
        }

        public void RaisePropertyChanged(PropertyChangedEventArgs args)
        {
        }

    }
}