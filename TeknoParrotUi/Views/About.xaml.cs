using System.Windows.Controls;
using System.Windows.Input;

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
        }

        /// <summary>
        /// When clicked, this will open the link for the discord invite
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PackIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("https://discord.gg/bntkyXZ");
        }

        /// <summary>
        /// When clicked, this will open the link to the patreon page for Teknogods
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PackIcon_MouseLeftButtonDown_1(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.patreon.com/Teknogods");
        }

        /// <summary>
        /// When clicked, this will open the link to the github page for teknogods
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PackIcon_MouseLeftButtonDown_2(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/teknogods/");
        }
    }
}