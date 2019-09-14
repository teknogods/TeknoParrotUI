using System.Diagnostics;
using System.IO;
using System.Reflection;
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
        /// <summary>
        /// This is stuff that happens as soon as the UserControl is initialized
        /// </summary>
        public About()
        {
            InitializeComponent();
            versionText.Text = GameVersion.CurrentVersion;
            foreach (var component in MainWindow.components)
            {
                components.Items.Add(new ListBoxItem
                {
                    Tag = component,
                    Content = $"{component.name} - {component.localVersion}"
                });
            }
        }

        /// <summary>
        /// When clicked, this will open the link for the discord invite
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PackIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://discord.gg/bntkyXZ");
        }

        /// <summary>
        /// When clicked, this will open the link to the patreon page for Teknogods
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PackIcon_MouseLeftButtonDown_1(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://www.patreon.com/Teknogods");
        }

        /// <summary>
        /// When clicked, this will open the link to the github page for teknogods
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PackIcon_MouseLeftButtonDown_2(object sender, MouseButtonEventArgs e)
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