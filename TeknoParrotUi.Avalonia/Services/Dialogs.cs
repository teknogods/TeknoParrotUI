using System.Threading.Tasks;
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>Small modal message/confirm dialogs (Avalonia has no built-in MessageBox).</summary>
public static class Dialogs
{
    public static async Task<int> ChooseAsync(Control host, string title, string message,
        string[] buttons, Control? editor = null)
    {
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var content = new StackPanel { Spacing = 14, Margin = new global::Avalonia.Thickness(20), MaxWidth = 560 };
        content.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = global::Avalonia.Media.FontWeight.Bold });
        content.Children.Add(new TextBlock { Text = message, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap });
        if (editor != null) content.Children.Add(editor);
        content.Children.Add(actions);
        Action close = () => { };
        for (var i = 0; i < buttons.Length; i++)
        {
            var index = i;
            var button = new Button { Content = buttons[i], Margin = new global::Avalonia.Thickness(4), MinHeight = 40 };
            button.Click += (_, _) => { completion.TrySetResult(index); close(); };
            actions.Children.Add(button);
        }
        if (TopLevel.GetTopLevel(host) is Window owner)
        {
            var window = new Window { Title = title, Width = 600, SizeToContent = SizeToContent.Height,
                CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = content };
            close = window.Close;
            window.Closed += (_, _) => completion.TrySetResult(-1);
            await window.ShowDialog(owner);
        }
        else
        {
            var root = host.GetVisualAncestors().OfType<Panel>().LastOrDefault();
            if (root == null) return -1;
            var overlay = new Border { Background = global::Avalonia.Media.Brush.Parse("#C0000000"), ZIndex = 10000,
                Child = new Border { Background = global::Avalonia.Media.Brush.Parse("#202025"),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    Child = new ScrollViewer { Content = content, MaxHeight = 600 } } };
            close = () => root.Children.Remove(overlay);
            Grid.SetRowSpan(overlay, 100);
            Grid.SetColumnSpan(overlay, 100);
            root.Children.Add(overlay);
            await completion.Task;
        }
        if (editor != null) content.Children.Remove(editor);
        return await completion.Task;
    }

    public static Task InfoAsync(Window owner, string title, string message) =>
        ShowAsync(owner, title, message, new[] { (Loc.T("OK", "OK"), (bool?)true) });

    public static async Task<bool> ConfirmAsync(Window owner, string title, string message)
    {
        var result = await ShowAsync(owner, title, message,
            new[] { (Loc.T("Yes", "Yes"), (bool?)true), (Loc.T("No", "No"), (bool?)false) });
        return result == true;
    }

    /// <summary>Yes = true, No = false, Cancel = null.</summary>
    public static Task<bool?> ConfirmCancelAsync(Window owner, string title, string message) =>
        ShowAsync(owner, title, message,
            new[] { (Loc.T("Yes", "Yes"), (bool?)true), (Loc.T("No", "No"), (bool?)false), (Loc.T("Cancel", "Cancel"), (bool?)null) });

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
