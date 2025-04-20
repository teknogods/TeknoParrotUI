using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using System;

namespace TeknoParrotUi.Views
{
    public partial class ThemeTesterWindow : Window
    {
        public ThemeTesterWindow()
        {
            InitializeComponent();

            // Initialize ComboBox
            var themeSelector = this.FindControl<ComboBox>("ThemeSelector");

            // Add items with proper Tag values
            themeSelector.Items.Clear();
            themeSelector.Items.Add(new ComboBoxItem { Content = "Default", Tag = "Default" });
            themeSelector.Items.Add(new ComboBoxItem { Content = "Whiteout", Tag = "Whiteout" });
            themeSelector.Items.Add(new ComboBoxItem { Content = "Bluehat", Tag = "Bluehat" });
            themeSelector.Items.Add(new ComboBoxItem { Content = "Obsidian", Tag = "Obsidian" });
            themeSelector.Items.Add(new ComboBoxItem { Content = "Ember", Tag = "Ember" });
            themeSelector.Items.Add(new ComboBoxItem { Content = "Frost", Tag = "Frost" });
            themeSelector.Items.Add(new ComboBoxItem { Content = "Echo", Tag = "Echo" });
            themeSelector.Items.Add(new ComboBoxItem { Content = "Void", Tag = "Void" });
            themeSelector.Items.Add(new ComboBoxItem { Content = "Cyber", Tag = "Cyber" });

            themeSelector.SelectedIndex = 0; // Default to first theme
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedTheme = selectedItem.Tag as string;
                if (selectedTheme != null)
                {
                    // Remove all theme classes first
                    Classes.Remove("Theme33");
                    Classes.Remove("ThemeWhiteout");
                    Classes.Remove("ThemeBluehat");
                    Classes.Remove("ThemeObsidian");
                    Classes.Remove("ThemeEmber");
                    Classes.Remove("ThemeFrost");
                    Classes.Remove("ThemeEcho");
                    Classes.Remove("ThemeVoid");
                    Classes.Remove("ThemeCyber");

                    // Set new theme class
                    string themeClass = selectedTheme == "Default" ? "Theme33" : "Theme" + selectedTheme;
                    Classes.Add(themeClass);

                    // Force theme update through the App
                    if (Application.Current is App app)
                    {
                        // This is the key change: specify proper parameters for the theme
                        bool isDarkMode = selectedTheme != "Whiteout"; // All themes except Whiteout are dark mode
                        App.LoadTheme(selectedTheme.ToLower(), isDarkMode, true);

                        // Refresh all visual elements
                        this.InvalidateVisual();
                        RefreshAllControls(this);
                    }
                }
            }
        }

        // Helper method to refresh all controls
        private void RefreshAllControls(Visual parent)
        {
            if (parent is Control control)
            {
                control.InvalidateVisual();
            }

            // Recurse through visual tree
            foreach (var child in parent.GetVisualChildren())
            {
                RefreshAllControls(child);
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}