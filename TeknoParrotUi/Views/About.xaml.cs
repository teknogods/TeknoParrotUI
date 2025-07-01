using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static TeknoParrotUi.MainWindow;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About
    {
        public void UpdateVersions()
        {
            versionText.Text = GameVersion.CurrentVersion;
            components.Items.Clear();
            foreach (var component in MainWindow.components)
            {
                // reset version so it's updated
                component._localVersion = null;
                components.Items.Add(new ListBoxItem
                {
                    Tag = component,
                    Content = $"{component.name} - {component.localVersion}"
                });
            }
        }

        /// <summary>
        /// This is stuff that happens as soon as the UserControl is initialized
        /// </summary>
        public About()
        {
            InitializeComponent();
            UpdateVersions();
        }

        /// <summary>
        /// When clicked, this will open the link for the discord invite
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PackIcon_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            Process.Start("https://discord.gg/bntkyXZ");
        }

        /// <summary>
        /// When clicked, this will open the link to the patreon page for Teknogods
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PackIcon_MouseLeftButtonDown_1(object sender, RoutedEventArgs e)
        {
            Process.Start("https://teknoparrot.shop");
        }

        /// <summary>
        /// When clicked, this will open the link to the github page for teknogods
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PackIcon_MouseLeftButtonDown_2(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/teknogods/");
        }

        private void Components_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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