using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using R = TeknoParrotUi.Properties.Resources;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Initial D: what the library does before it starts one of the five matchmaking titles (InitialDServer
    /// docs/UNIFIED_MODE.md 2.7 "Before the launch", 4.2, 6.7):
    /// <list type="number">
    /// <item>once: the first-run notice (every start now contacts the online server; cards become online cards; offline
    /// progress is merged later); the server check is always enabled;</item>
    /// <item>without this PC's Online ID on the official server: "Play online (register this PC) / Play offline for now /
    /// Don't ask again" (no guests on the official server: an unregistered PC plays offline / LAN);</item>
    /// <item>"Play with friends": the friends code dialog (a new code with "Copy invite", or a friend's code);</item>
    /// <item>the server's ping: maintenance, and friends codes switched off.</item>
    /// </list>
    /// Library launches only: command-line and frontend launches never see a dialog. Nothing here calls the website.
    /// </summary>
    internal static class InitialDLaunchFlow
    {
        private static readonly TimeSpan PingBudget = TimeSpan.FromSeconds(1.5);

        /// <summary>
        /// True = start the game. Registers the launch's friends code (or none) with InitialDUnifiedMode.PrepareLaunch;
        /// every other profile only gets PrepareLaunch(null), which drops a stale code.
        /// </summary>
        public static async Task<bool> BeforeLaunchAsync(GameProfile profile, bool withFriends, Window owner)
        {
            if (!InitialDUnifiedMode.IsMatchmakingProfile(profile))
            {
                InitialDUnifiedMode.PrepareLaunch(profile, null);
                return true;
            }
            var data = Lazydata.ParrotData;
            if (!data.InitialDUnifiedNoticeShown)
            {
                if (Seams.Notice != null)
                    Seams.Notice(owner);
                else
                    ShowFirstRunNotice(owner);
                data.InitialDUnifiedNoticeShown = true;
                Save();
            }

            var server = InitialDUnifiedMode.ServerOf(profile);
            var name = profile.GameNameInternal ?? InitialDUnifiedMode.ProfileKey(profile);
            if (server.Official && !HasCredential(profile))
            {
                if (withFriends)
                {
                    if (Ask(owner, R.InitialDFriendsTitle, R.InitialDFriendsNeedsRegistration, R.InitialDOnlineRegisterButton, R.InitialDCancel) == 0)
                        OpenAccountPage();
                    return false;
                }
                if (!data.InitialDRegisterPromptOff)
                {
                    var choice = Ask(owner, R.InitialDRegisterTitle, string.Format(R.InitialDRegisterText, name),
                        R.InitialDRegisterPlayOnline, R.InitialDRegisterPlayOffline, R.InitialDRegisterDontAsk);
                    if (choice == 0)
                    {
                        OpenAccountPage();
                        return false;
                    }
                    if (choice < 0)
                        return false;
                    if (choice == 2)
                    {
                        data.InitialDRegisterPromptOff = true;
                        Save();
                    }
                }
                // the boot ends offline anyway (no credential): no ping needed
                InitialDUnifiedMode.PrepareLaunch(profile, null);
                return true;
            }

            string code = null;
            if (withFriends)
            {
                code = Seams.Friends != null ? Seams.Friends(owner, profile) : FriendsDialog(owner, profile);
                if (code == null)
                    return false;
            }

            var ping = await InitialDUnifiedMode.PingAsync(server, PingBudget, Seams.PingHandler).ConfigureAwait(true);
            if (ping != null && ping.Maintenance)
            {
                if (withFriends)
                {
                    Ask(owner, R.InitialDFriendsTitle, string.Format(R.InitialDMaintenanceText, name), R.InitialDOk);
                    return false;
                }
                if (Ask(owner, R.InitialDOnlineTitle, string.Format(R.InitialDMaintenanceText, name), R.InitialDStartOffline, R.InitialDCancel) != 0)
                    return false;
            }
            else if (withFriends && ping != null && ping.Party == false)
            {
                Ask(owner, R.InitialDFriendsTitle, R.InitialDFriendsOff, R.InitialDOk);
                return false;
            }
            InitialDUnifiedMode.PrepareLaunch(profile, code);
            return true;
        }

        // -------------------------------------------------------------------------------------------------------------
        // LAN linking (UNIFIED_MODE.md 3.3 "Link consent", 3.8 "Firewall"; LAN_AGENT.md 7 / 8), phase P2b
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The one-time link question for a peer PC: Always / This time / Never. True = link for this start (Always is
        /// kept, Never is kept, a closed window is "not now"). The answer is kept per peer install id.
        /// </summary>
        public static bool AskLink(Window owner, GameProfile profile, InitialDLanPresence.Peer peer)
        {
            if (peer == null)
                return false;
            var name = profile?.GameNameInternal ?? InitialDUnifiedMode.ProfileKey(profile);
            var who = string.IsNullOrWhiteSpace(peer.Name) ? peer.Address?.ToString() ?? "?" : peer.Name;
            var choice = Ask(owner, R.InitialDLanLinkTitle, string.Format(R.InitialDLanLinkText, who, name),
                R.InitialDLanLinkAlways, R.InitialDLanLinkOnce, R.InitialDLanLinkNever);
            if (choice == 0)
            {
                InitialDLanPresence.SetConsent(peer.InstallId, InitialDLanPresence.Consent.Always);
                peer.Consent = InitialDLanPresence.Consent.Always;
                return true;
            }
            if (choice == 1)
            {
                // "This time": nothing is saved, but the game started now must still know (LAN_AGENT.md 7)
                InitialDLanPresence.NoteConsentOnce(peer.InstallId);
                return true;
            }
            if (choice == 2)
            {
                InitialDLanPresence.SetConsent(peer.InstallId, InitialDLanPresence.Consent.Never);
                peer.Consent = InitialDLanPresence.Consent.Never;
            }
            return false;
        }

        /// <summary>
        /// Offers the inbound firewall rules once, before the first launch that could link (UNIFIED_MODE.md 3.8). The
        /// question is asked only when another TeknoParrot PC is actually on the LAN; nothing is added unless the user
        /// presses "Add the rules".
        /// </summary>
        public static void OfferFirewall(Window owner, GameProfile profile)
        {
            if (!InitialDFirewall.ShouldOffer(profile))
                return;
            var rules = InitialDFirewall.Rules(profile);
            if (rules.Count == 0)
                return;
            InitialDFirewall.MarkOffered();
            var list = string.Join(Environment.NewLine, InitialDFirewall.Describe(rules));
            if (Ask(owner, R.InitialDFirewallTitle, string.Format(R.InitialDFirewallText, list), R.InitialDFirewallAdd,
                    R.InitialDFirewallSkip) != 0)
                return;
            if (!InitialDFirewall.Apply(rules))
                Ask(owner, R.InitialDFirewallTitle, R.InitialDFirewallFailed, R.InitialDOk);
        }

        /// <summary>Test seams, never set by TPUI itself: the dialogs, the Account page and the ping transport.</summary>
        internal static class Seams
        {
            internal static Func<Window, string, string, string[], int> Choose { get; set; }
            internal static Func<Window, GameProfile, string> Friends { get; set; }
            internal static Action<Window> Notice { get; set; }
            internal static Action OpenAccount { get; set; }
            internal static System.Net.Http.HttpMessageHandler PingHandler { get; set; }
        }

        private static int Ask(Window owner, string title, string message, params string[] buttons) =>
            Seams.Choose != null ? Seams.Choose(owner, title, message, buttons) : Choose(owner, title, message, buttons);

        /// <summary>This PC has a well-formed Online ID: in the profile (the ini the DLL reads) or in ParrotData (auto-filled).</summary>
        internal static bool HasCredential(GameProfile profile)
        {
            bool Valid()
            {
                var id = profile.ConfigValues?.FirstOrDefault(x => x.FieldName == InitialDOnlineHelper.IdFieldName)?.FieldValue;
                var secret = profile.ConfigValues?.FirstOrDefault(x => x.FieldName == InitialDOnlineHelper.SecretFieldName)?.FieldValue;
                return InitialDOnlineHelper.NormalizePcbId(id) != null && InitialDOnlineHelper.IsValidSecret(secret);
            }
            if (Valid())
                return true;
            // the library's auto-fill rule for this start: an empty OnlineID, or this PC's ID without its secret
            return InitialDOnlineHelper.LocalCredential() != null && InitialDOnlineHelper.AutoFill(profile) && Valid();
        }

        private static void Save()
        {
            try
            {
                JoystickHelper.Serialize();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitialDOnline: cannot save ParrotData: {ex.Message}");
            }
        }

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

        // -------------------------------------------------------------------------------------------------------------
        // Dialogs (built in code, styled by the application's MaterialDesign resources)
        // -------------------------------------------------------------------------------------------------------------

        private static Window NewDialog(Window owner, string title, double width = 560)
        {
            var w = new Window
            {
                Title = title,
                Width = width,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            };
            if (owner != null && owner.IsVisible)
                w.Owner = owner;
            w.SetResourceReference(Control.BackgroundProperty, "MaterialDesignPaper");
            w.SetResourceReference(Control.ForegroundProperty, "MaterialDesignBody");
            return w;
        }

        private static TextBlock Text(string text, double size = 14, double bottom = 12) => new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = size,
            Margin = new Thickness(0, 0, 0, bottom),
        };

        private static WrapPanel ButtonRow(Window w, Action<int> pressed, params string[] buttons)
        {
            var row = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                var b = new Button { Content = buttons[i], Margin = new Thickness(8, 4, 0, 0), MinWidth = 90, IsDefault = i == 0 };
                if (i > 0)
                    b.SetResourceReference(FrameworkElement.StyleProperty, "MaterialDesignFlatButton");
                b.Click += (s, e) =>
                {
                    pressed(index);
                };
                row.Children.Add(b);
            }
            return row;
        }

        /// <summary>A dialog built but not shown yet (the tests render and drive it without showing it).</summary>
        internal sealed class DialogHandle<T>
        {
            public Window Window { get; set; }
            public Func<T> Result { get; set; }

            public T ShowDialog()
            {
                Window.ShowDialog();
                return Result();
            }
        }

        /// <summary>A modal message with buttons; the index of the button pressed, -1 when the window was closed.</summary>
        public static int Choose(Window owner, string title, string message, params string[] buttons) =>
            BuildChoose(owner, title, message, buttons).ShowDialog();

        internal static DialogHandle<int> BuildChoose(Window owner, string title, string message, params string[] buttons)
        {
            var w = NewDialog(owner, title);
            int result = -1;
            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(Text(message));
            panel.Children.Add(ButtonRow(w, i =>
            {
                result = i;
                w.Close();
            }, buttons));
            w.Content = panel;
            return new DialogHandle<int> { Window = w, Result = () => result };
        }

        /// <summary>The one-time notice explaining automatic Initial D online play.</summary>
        public static void ShowFirstRunNotice(Window owner) => BuildFirstRunNotice(owner).ShowDialog();

        internal static DialogHandle<bool> BuildFirstRunNotice(Window owner)
        {
            var w = NewDialog(owner, R.InitialDFirstRunTitle, 600);
            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(Text(R.InitialDFirstRunTitle, 18));
            panel.Children.Add(Text(R.InitialDFirstRunText));
            panel.Children.Add(Text(R.InitialDAutomaticOnlineNote, 12, 4));
            panel.Children.Add(ButtonRow(w, i => w.Close(), R.InitialDOk));
            w.Content = panel;
            return new DialogHandle<bool>
            {
                Window = w,
                Result = () => true,
            };
        }

        /// <summary>The friends code for this start: a new code (with "Copy invite") or a friend's code; null = cancelled.</summary>
        public static string FriendsDialog(Window owner, GameProfile profile) => BuildFriendsDialog(owner, profile, null).ShowDialog();

        /// <param name="confirmWeak">asks whether a guessable typed code is wanted anyway (default: a modal question)</param>
        internal static DialogHandle<string> BuildFriendsDialog(Window owner, GameProfile profile, Func<string, bool> confirmWeak)
        {
            var generated = InitialDUnifiedMode.GeneratePartyCode();
            var name = profile.GameNameInternal ?? InitialDUnifiedMode.ProfileKey(profile);
            var w = NewDialog(owner, R.InitialDFriendsTitle);
            string result = null;
            confirmWeak ??= code => Choose(w, R.InitialDFriendsTitle, string.Format(R.InitialDFriendsWeakCode, code), R.InitialDFriendsStart, R.InitialDCancel) == 0;

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(Text(string.Format(R.InitialDFriendsIntro, name)));

            var newRadio = new RadioButton { Content = R.InitialDFriendsNew, IsChecked = true, GroupName = "idparty", Margin = new Thickness(0, 4, 0, 4) };
            panel.Children.Add(newRadio);
            var newRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(28, 0, 0, 10) };
            newRow.Children.Add(new TextBlock
            {
                Text = InitialDUnifiedMode.FormatPartyCode(generated),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = generated,
            });
            var copy = new Button { Content = R.InitialDFriendsCopyInvite, Margin = new Thickness(16, 0, 0, 0) };
            copy.SetResourceReference(FrameworkElement.StyleProperty, "MaterialDesignFlatButton");
            copy.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(InitialDUnifiedMode.InviteText(profile, generated));
                    copy.Content = R.InitialDFriendsCopied;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"InitialDOnline: clipboard: {ex.Message}");
                }
            };
            newRow.Children.Add(copy);
            panel.Children.Add(newRow);

            var joinRadio = new RadioButton { Content = R.InitialDFriendsJoin, GroupName = "idparty", Margin = new Thickness(0, 4, 0, 4) };
            panel.Children.Add(joinRadio);
            var box = new TextBox { Margin = new Thickness(28, 0, 0, 4), MaxLength = 24, FontFamily = new FontFamily("Consolas"), FontSize = 18 };
            box.GotFocus += (s, e) => joinRadio.IsChecked = true;
            box.TextChanged += (s, e) =>
            {
                if (box.Text.Length > 0)
                    joinRadio.IsChecked = true;
            };
            panel.Children.Add(box);
            var error = Text("", 13, 4);
            error.Foreground = Brushes.IndianRed;
            error.Visibility = Visibility.Collapsed;
            panel.Children.Add(error);
            var note = Text(R.InitialDFriendsNote, 12, 4);
            note.Opacity = 0.75;
            panel.Children.Add(note);

            panel.Children.Add(ButtonRow(w, i =>
            {
                if (i != 0)
                {
                    w.Close();
                    return;
                }
                if (newRadio.IsChecked == true)
                {
                    result = generated;
                    w.Close();
                    return;
                }
                var typed = InitialDUnifiedMode.NormalizePartyCode(box.Text);
                if (typed == null)
                {
                    error.Text = R.InitialDFriendsBadCode;
                    error.Visibility = Visibility.Visible;
                    return;
                }
                if (InitialDUnifiedMode.IsWeakPartyCode(typed) && !confirmWeak(typed))
                    return;
                result = typed;
                w.Close();
            }, R.InitialDFriendsStart, R.InitialDCancel));
            w.Content = panel;
            return new DialogHandle<string> { Window = w, Result = () => result };
        }
    }
}
