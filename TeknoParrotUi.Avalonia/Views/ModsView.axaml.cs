using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Views;

public partial class ModsView : UserControl
{
    private bool _loaded;

    public ModsView()
    {
        InitializeComponent();
        HeaderText.Text = Services.Loc.T("MainMods", "Game Mods");
        Services.Loc.LanguageChanged += () => HeaderText.Text = Services.Loc.T("MainMods", "Game Mods");
        Loaded += async (_, _) =>
        {
            if (!_loaded)
            {
                _loaded = true;
                await LoadMods();
            }
        };
    }

    private async System.Threading.Tasks.Task LoadMods()
    {
        StatusText.Text = "Loading mod catalog...";
        ModsPanel.Children.Clear();

        List<ModInstaller.AvailableMod> mods;
        try
        {
            mods = await ModInstaller.GetAvailableModsAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not load mod catalog: {ex.Message}";
            return;
        }

        if (mods.Count == 0)
        {
            StatusText.Text = "No mods available for your installed games. Add games first, then check back.";
            return;
        }

        StatusText.Text = $"{mods.Count} mod(s) available for your games.";
        foreach (var mod in mods)
            ModsPanel.Children.Add(BuildModCard(mod));
    }

    private Control BuildModCard(ModInstaller.AvailableMod mod)
    {
        var install = new Button
        {
            Content = mod.Installed ? "Installed" : "Install",
            IsEnabled = !mod.Installed,
            VerticalAlignment = VerticalAlignment.Top
        };
        var status = new TextBlock { Opacity = 0.7, FontSize = 11, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };

        install.Click += async (_, _) =>
        {
            install.IsEnabled = false;
            install.Content = "Installing...";
            try
            {
                await ModInstaller.InstallModAsync(mod, m => global::Avalonia.Threading.Dispatcher.UIThread.Post(() => status.Text = m));
                install.Content = "Installed";
                status.Text = "Mod installed successfully.";
            }
            catch (Exception ex)
            {
                install.Content = "Install";
                install.IsEnabled = true;
                status.Text = $"Install failed: {ex.Message}";
            }
        };

        var info = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock { Text = mod.Data.ModName, FontWeight = global::Avalonia.Media.FontWeight.Bold },
                new TextBlock { Text = $"{mod.Game.GameNameInternal} — by {mod.Data.Creator}", Opacity = 0.7, FontSize = 12 },
                new TextBlock { Text = mod.Data.Description ?? "", TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, FontSize = 12 },
                status
            }
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(info, 0);
        Grid.SetColumn(install, 1);
        grid.Children.Add(info);
        grid.Children.Add(install);

        return new Border
        {
            BorderThickness = new global::Avalonia.Thickness(1),
            BorderBrush = global::Avalonia.Media.Brushes.Gray,
            CornerRadius = new global::Avalonia.CornerRadius(4),
            Padding = new global::Avalonia.Thickness(10),
            Child = grid
        };
    }
}
