using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using System;
using System.ComponentModel; // Make sure this is included
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views
{
    public partial class ThemeTesterWindow : Window
    {
        private string _lastTheme = "Default";

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

            // Select the current theme
            string currentTheme = GetCurrentTheme();
            for (int i = 0; i < themeSelector.Items.Count; i++)
            {
                if (themeSelector.Items[i] is ComboBoxItem item &&
                    item.Tag.ToString() == currentTheme)
                {
                    themeSelector.SelectedIndex = i;
                    break;
                }
            }

            if (themeSelector.SelectedIndex < 0)
                themeSelector.SelectedIndex = 0;

            _lastTheme = currentTheme;
        }

        // Get currently selected theme from ParrotData
        private string GetCurrentTheme()
        {
            if (Lazydata.ParrotData?.UiTheme == null)
                return "Default";

            // Convert from "Theme33" or "ThemeWhiteout" format to just "Default" or "Whiteout"
            string theme = Lazydata.ParrotData.UiTheme;
            return theme == "Theme33" ? "Default" : theme.Replace("Theme", "");
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
                    _lastTheme = selectedTheme;

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

        // This is the correct signature for Avalonia's Window.OnClosing
        protected override void OnClosing(WindowClosingEventArgs e)
        {
            SaveSelectedTheme();
            base.OnClosing(e);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSelectedTheme();
            Close();
        }

        private void SaveSelectedTheme()
        {
            // Save theme to ParrotData
            if (Lazydata.ParrotData != null)
            {
                string themeClass = _lastTheme == "Default" ? "Theme33" : "Theme" + _lastTheme;
                Lazydata.ParrotData.UiTheme = themeClass;
                JoystickHelper.Serialize(); // Save settings
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}