using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using R = TeknoParrotUi.Properties.Resources;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Senjou no Kizuna: what the library does before it starts the game (Kizuna's twin of the registration prompt of
    /// InitialDLaunchFlow). Kizuna plays online only, on TeknoParrot's server (a fixed address in the DLL), which takes
    /// only cabinets signed in with this PC's own Senjou no Kizuna Online ID. Without one in the profile (or in ParrotData,
    /// which the auto-fill puts into the profile for this start) a start does not begin: a dialog says why and opens the
    /// Account page, where the ID is registered or taken over (Senjou no Kizuna Online). There is no "play offline". A
    /// start with a credential file named by TP_ONLINE_CRED (tests) goes ahead as before.
    /// Library launches only: command-line and frontend launches never see a dialog. Nothing here calls the website.
    /// </summary>
    internal static class KizunaLaunchFlow
    {
        /// <summary>True = start the game. Every profile whose OnlineIdType is not Kizuna starts as before.</summary>
        public static bool BeforeLaunch(GameProfile profile, Window owner)
        {
            if (profile == null || profile.OnlineIdType != OnlineIdType.Kizuna)
                return true;
            // TP_ONLINE_CRED names a credential file the DLL uses instead of the profile's pair (tests, several pods)
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TP_ONLINE_CRED")))
                return true;
            if (HasCredential(profile))
                return true;
            var name = string.IsNullOrEmpty(profile.GameNameInternal) ? profile.ProfileName : profile.GameNameInternal;
            if (Ask(owner, R.KizunaRegisterTitle, string.Format(R.KizunaRegisterText, name),
                    R.KizunaRegisterOpenAccount, R.InitialDCancel) == 0)
                OpenAccountPage();
            return false;
        }

        /// <summary>
        /// This PC's Kizuna pair is in the profile (the ini the DLL reads), or in ParrotData and the auto-fill rule puts it
        /// into the profile for this start (an empty OnlineID, or this PC's ID without its secret).
        /// </summary>
        internal static bool HasCredential(GameProfile profile)
        {
            if (KizunaOnlineHelper.ProfileHasCredential(profile))
                return true;
            return KizunaOnlineHelper.LocalCredential() != null && KizunaOnlineHelper.AutoFill(profile) &&
                   KizunaOnlineHelper.ProfileHasCredential(profile);
        }

        /// <summary>Test seams, never set by TPUI itself: the dialog and the Account page.</summary>
        internal static class Seams
        {
            internal static Func<Window, string, string, string[], int> Choose { get; set; }
            internal static Action OpenAccount { get; set; }
        }

        private static int Ask(Window owner, string title, string message, params string[] buttons) =>
            Seams.Choose != null ? Seams.Choose(owner, title, message, buttons) : Choose(owner, title, message, buttons);

        private static void OpenAccountPage()
        {
            if (Seams.OpenAccount != null)
            {
                Seams.OpenAccount();
                return;
            }
            var main = Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault();
            if (main != null)
                main.contentControl.Content = new AccountPage();
        }

        /// <summary>A modal message with buttons (the first is the default); the index pressed, -1 when the window was closed.</summary>
        public static int Choose(Window owner, string title, string message, params string[] buttons)
        {
            var w = BuildChoose(owner, title, message, buttons, out var result);
            w.ShowDialog();
            return result();
        }

        internal static Window BuildChoose(Window owner, string title, string message, string[] buttons, out Func<int> result)
        {
            var w = new Window
            {
                Title = title,
                Width = 560,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            };
            if (owner != null && owner.IsVisible)
                w.Owner = owner;
            w.SetResourceReference(Control.BackgroundProperty, "MaterialDesignPaper");
            w.SetResourceReference(Control.ForegroundProperty, "MaterialDesignBody");

            int pressed = -1;
            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new Thickness(0, 0, 0, 12) });
            var row = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                var b = new Button { Content = buttons[i], Margin = new Thickness(8, 4, 0, 0), MinWidth = 90, IsDefault = i == 0 };
                if (i > 0)
                    b.SetResourceReference(FrameworkElement.StyleProperty, "MaterialDesignFlatButton");
                b.Click += (s, e) =>
                {
                    pressed = index;
                    w.Close();
                };
                row.Children.Add(b);
            }
            panel.Children.Add(row);
            w.Content = panel;
            result = () => pressed;
            return w;
        }
    }
}
