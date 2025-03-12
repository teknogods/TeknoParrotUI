using System;
using System.Diagnostics;
using System.Windows.Input;

namespace TeknoParrotUi.Helpers
{
    public class DelegateCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public DelegateCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            // Always true unless explicitly specified
            var result = _canExecute == null || _canExecute();
            Debug.WriteLine($"CanExecute called, returning: {result}");
            return result;
        }

        public void Execute(object parameter)
        {
            Debug.WriteLine("Execute command called");
            _execute();
        }

        public event EventHandler CanExecuteChanged;

        public void RaiseCanExecuteChanged()
        {
            Debug.WriteLine("RaiseCanExecuteChanged called");
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}