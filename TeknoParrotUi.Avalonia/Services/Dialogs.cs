using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>Small modal message/confirm dialogs (Avalonia has no built-in MessageBox).</summary>
public static class Dialogs
{
    public static Task InfoAsync(Window owner, string title, string message) =>
        ShowAsync(owner, title, message, new[] { ("OK", (bool?)true) });

    public static async Task<bool> ConfirmAsync(Window owner, string title, string message)
    {
        var result = await ShowAsync(owner, title, message, new[] { ("Yes", (bool?)true), ("No", (bool?)false) });
        return result == true;
    }

    /// <summary>Yes = true, No = false, Cancel = null.</summary>
    public static Task<bool?> ConfirmCancelAsync(Window owner, string title, string message) =>
        ShowAsync(owner, title, message, new[] { ("Yes", (bool?)true), ("No", (bool?)false), ("Cancel", (bool?)null) });

    private static async Task<bool?> ShowAsync(Window owner, string title, string message, (string label, bool? value)[] buttons)
    {
        var tcs = new TaskCompletionSource<bool?>();

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var dialog = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new global::Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap },
                    buttonPanel
                }
            }
        };

        foreach (var (label, value) in buttons)
        {
            var btn = new Button { Content = label, MinWidth = 80 };
            btn.Click += (_, _) =>
            {
                tcs.TrySetResult(value);
                dialog.Close();
            };
            buttonPanel.Children.Add(btn);
        }

        dialog.Closed += (_, _) => tcs.TrySetResult(null);
        await dialog.ShowDialog(owner);
        return await tcs.Task;
    }
}
