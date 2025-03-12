using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Controls.Primitives;
using System.IO;
using Avalonia.Media.Imaging;

namespace TeknoParrotUi.Common
{
    public static class MessageBoxHelper
    {
        public static async Task<bool> ConfirmationOKCancel(string message)
        {
            var dialog = new MessageDialog
            {
                Message = message,
                ButtonDefs = MessageDialog.ButtonDefinitions.OkCancel
            };
            return await dialog.ShowDialog().ConfigureAwait(true) == MessageDialog.ResultYes;
        }

        public static async Task ErrorOK(string message)
        {
            var dialog = new MessageDialog
            {
                Message = message,
                Title = Properties.Resources.Error,
                ButtonDefs = MessageDialog.ButtonDefinitions.Ok
            };
            await dialog.ShowDialog().ConfigureAwait(true);
        }

        public static async Task<bool> ErrorYesNo(string message)
        {
            var dialog = new MessageDialog
            {
                Message = message,
                Title = Properties.Resources.Error,
                ButtonDefs = MessageDialog.ButtonDefinitions.YesNo
            };
            return await dialog.ShowDialog().ConfigureAwait(true) == MessageDialog.ResultYes;
        }

        public static async Task InfoOK(string message)
        {
            var dialog = new MessageDialog
            {
                Message = message,
                Title = Properties.Resources.Information,
                ButtonDefs = MessageDialog.ButtonDefinitions.Ok
            };
            await dialog.ShowDialog().ConfigureAwait(true);
        }

        public static async Task<bool> InfoYesNo(string message)
        {
            var dialog = new MessageDialog
            {
                Message = message,
                Title = Properties.Resources.Information,
                ButtonDefs = MessageDialog.ButtonDefinitions.YesNo
            };
            return await dialog.ShowDialog().ConfigureAwait(true) == MessageDialog.ResultYes;
        }

        public static async void WarningOK(string message)
        {
            var dialog = new MessageDialog
            {
                Message = message,
                Title = Properties.Resources.Warning,
                ButtonDefs = MessageDialog.ButtonDefinitions.Ok
            };
            await dialog.ShowDialog().ConfigureAwait(true);
        }

        public static async Task<bool> WarningYesNo(string message)
        {
            var dialog = new MessageDialog
            {
                Message = message,
                Title = Properties.Resources.Warning,
                ButtonDefs = MessageDialog.ButtonDefinitions.YesNo
            };
            return await dialog.ShowDialog().ConfigureAwait(true) == MessageDialog.ResultYes;
        }
    }

    public class MessageDialog : Window
    {
        public enum ButtonDefinitions { Ok, OkCancel, YesNo }
        public enum DialogResult { None, Yes, No }

        public string Message { get; set; }
        public ButtonDefinitions ButtonDefs { get; set; } = ButtonDefinitions.Ok;
        public DialogResult Result { get; private set; } = DialogResult.None;

        public const DialogResult ResultYes = DialogResult.Yes;
        private TaskCompletionSource<DialogResult> _resultCompletionSource;

        public MessageDialog()
        {
            // Set improved properties for the dialog
            this.Width = 500;
            this.MinHeight = 150;
            this.MaxHeight = 600;
            this.SizeToContent = SizeToContent.Height;
            this.CanResize = true;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Set the window icon using the application's embedded resources
            try
            {
                // First approach: Get from assembly resources (most reliable)
                var assembly = System.Reflection.Assembly.GetEntryAssembly();
                using (var stream = assembly?.GetManifestResourceStream("TeknoParrotUi.teknoparrot_by_pooterman-db9erxd.ico")
                      ?? assembly?.GetManifestResourceStream("teknoparrot_by_pooterman-db9erxd.ico"))
                {
                    if (stream != null)
                    {
                        this.Icon = new WindowIcon(new Bitmap(stream));
                    }
                }

                // Fallback to getting it from the main window if all else fails
                if (this.Icon == null && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    if (desktop.MainWindow?.Icon != null)
                    {
                        this.Icon = desktop.MainWindow.Icon;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load dialog icon: {ex.Message}");
            }
        }

        // This method is called on the UI thread
        private void InitializeContent()
        {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            // Create a scroll viewer for the message
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(10),
                MaxHeight = 500 // Maximum height of the text area
            };

            var messageText = new TextBlock
            {
                Text = Message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(5)
            };

            scrollViewer.Content = messageText;
            Grid.SetRow(scrollViewer, 0);
            mainGrid.Children.Add(scrollViewer);

            // Button panel is fixed at the bottom
            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 10,
                Margin = new Thickness(10),
                Background = Avalonia.Media.Brushes.Transparent
            };

            switch (ButtonDefs)
            {
                case ButtonDefinitions.OkCancel:
                    AddButton(buttonPanel, "OK", () => { Result = DialogResult.Yes; Close(); });
                    AddButton(buttonPanel, "Cancel", () => { Result = DialogResult.No; Close(); });
                    break;
                case ButtonDefinitions.YesNo:
                    AddButton(buttonPanel, "Yes", () => { Result = DialogResult.Yes; Close(); });
                    AddButton(buttonPanel, "No", () => { Result = DialogResult.No; Close(); });
                    break;
                default: // Ok
                    AddButton(buttonPanel, "OK", () => { Result = DialogResult.Yes; Close(); });
                    break;
            }

            Grid.SetRow(buttonPanel, 1);
            mainGrid.Children.Add(buttonPanel);

            this.Content = mainGrid;
        }

        // Rest of the class remains unchanged

        private void AddButton(Panel panel, string text, Action clickHandler)
        {
            var button = new Button { Content = text, Padding = new Thickness(20, 5) };
            button.Click += (s, e) =>
            {
                try
                {
                    clickHandler();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Button click error: {ex.Message}");
                    _resultCompletionSource.TrySetException(ex);
                }
            };
            panel.Children.Add(button);
        }

        public async Task<DialogResult> ShowDialog()
        {
            _resultCompletionSource = new TaskCompletionSource<DialogResult>();

            try
            {
                if (Dispatcher.UIThread.CheckAccess())
                {
                    // We're on the UI thread already, safe to show directly
                    ShowDialogImpl();
                }
                else
                {
                    // We need to switch to the UI thread
                    await Dispatcher.UIThread.InvokeAsync(() => ShowDialogImpl());
                }

                // Return the task that completes when the dialog is closed
                return await _resultCompletionSource.Task;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dialog error: {ex.Message}");
                return DialogResult.None; // Return default value on error
            }
        }

        private void ShowDialogImpl()
        {
            try
            {
                // Initialize UI elements
                InitializeContent();

                // Get the main window
                var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

                // Make sure we handle the closed event in all cases
                this.Closed += OnDialogClosed;

                if (mainWindow != null && mainWindow.IsVisible)
                {
                    // Show as modal dialog
                    this.ShowDialog(mainWindow);
                }
                else
                {
                    // Show as non-modal window
                    Show();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to show dialog: {ex.Message}");
                _resultCompletionSource.TrySetException(ex);
            }
        }


        private void OnDialogClosed(object sender, EventArgs e)
        {
            try
            {
                _resultCompletionSource.TrySetResult(Result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dialog closed error: {ex.Message}");
            }
            this.Closed -= OnDialogClosed;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            try
            {
                _resultCompletionSource?.TrySetResult(Result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OnClosed error: {ex.Message}");
            }
        }
    }
}