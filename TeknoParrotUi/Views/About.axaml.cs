using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using TeknoParrotUi.Components;
using static TeknoParrotUi.MainWindow;

namespace TeknoParrotUi.Views
{
    public partial class About : UserControl
    {
        public void UpdateVersions()
        {
            if (versionText != null)
                versionText.Text = GameVersion.CurrentVersion;

            if (components != null)
            {
                components.Items.Clear();
                foreach (var component in UpdaterComponent.components)
                {
                    // Reset version so it's updated
                    component._localVersion = null;
                    components.Items.Add(new ListBoxItem
                    {
                        Tag = component,
                        Content = $"{component.name} - {component.localVersion}"
                    });
                }
            }
        }

        public About()
        {
            InitializeComponent();
            UpdateVersions();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            versionText = this.FindControl<TextBlock>("versionText");
            components = this.FindControl<ListBox>("components");
            components.DoubleTapped += Components_DoubleTapped;
        }

        private void Discord_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://discord.gg/bntkyXZ");
        }

        private void Website_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://teknoparrot.shop");
        }

        private void GitHub_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/teknogods/TeknoParrotUI");
        }

        private void OpenUrl(string url)
        {
            try
            {
                // Cross-platform way to open URLs
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch
            {
                // Fallback for Linux/Unix systems
                if (OperatingSystem.IsLinux())
                {
                    Process.Start("xdg-open", url);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    Process.Start("open", url);
                }
            }
        }

        private void Components_DoubleTapped(object sender, RoutedEventArgs e)
        {
            if (components.SelectedItem != null)
            {
                var component = (UpdaterComponent)((ListBoxItem)components.SelectedItem).Tag;

                if (component != null)
                {
                    Process.Start(component.fullUrl);
                }
            }
        }
    }
}