using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using Microsoft.Win32;
using System.Text;
using static TeknoParrotUi.MainWindow;

namespace TeknoParrotUi.Views
{
    /// <summary>
    /// Interaction logic for ChangelogView.xaml
    /// Shows changelogs after update completion with subscription promotion
    /// </summary>
    public partial class ChangelogView : UserControl
    {
        private List<UpdatedComponentInfo> _updatedComponents;
        private ContentControl _contentControl;
        private Library _library;
        private List<GameProfile> _patreonGames;
        private bool _isPatron;

        public class UpdatedComponentInfo
        {
            public UpdaterComponent Component { get; set; }
            public GithubRelease Release { get; set; }
            public string Version { get; set; }
        }

        public ChangelogView(List<UpdatedComponentInfo> updatedComponents, ContentControl control, Library library)
        {
            InitializeComponent();
            _updatedComponents = updatedComponents;
            _contentControl = control;
            _library = library;
            _patreonGames = GameProfileLoader.GameProfiles.Where((profile) => profile.Patreon && !profile.DevOnly).ToList();
            _isPatron = CheckPatronStatus();
        }

        private bool CheckPatronStatus()
        {
#if DEBUG
            // DEBUG MODE: Set this to true to test the non-patron view
            bool FORCE_NON_PATRON_VIEW = true;
            
            if (FORCE_NON_PATRON_VIEW)
            {
                return false;
            }
#endif

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot"))
                {
                    return key != null && key.GetValue("PatreonSerialKey") != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            PopulateChangelogs();
            CustomizeSubscriptionPromotion();
        }

        private void PopulateChangelogs()
        {
            changelogList.Children.Clear();

            foreach (var component in _updatedComponents)
            {
                // Component Header Card
                var componentCard = new Border
                {
                    Background = (Brush)FindResource("MaterialDesignCardBackground"),
                    BorderBrush = (Brush)FindResource("MaterialDesignDivider"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new System.Windows.CornerRadius(8),
                    Margin = new Thickness(0, 0, 0, 20),
                    Padding = new Thickness(20)
                };

                var componentStack = new StackPanel();

                // Component Name with Icon
                var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
                
                var iconPack = new MaterialDesignThemes.Wpf.PackIcon
                {
                    Kind = GetIconForComponent(component.Component.name),
                    Width = 24,
                    Height = 24,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0),
                    Foreground = new SolidColorBrush(Color.FromRgb(98, 0, 234))
                };

                var componentName = new TextBlock
                {
                    Text = component.Component.name,
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var versionBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, 98, 0, 234)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(98, 0, 234)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new System.Windows.CornerRadius(12),
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(10, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var versionText = new TextBlock
                {
                    Text = component.Version,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(98, 0, 234))
                };

                versionBadge.Child = versionText;
                headerPanel.Children.Add(iconPack);
                headerPanel.Children.Add(componentName);
                headerPanel.Children.Add(versionBadge);
                componentStack.Children.Add(headerPanel);

                // Separator
                var separator = new Border
                {
                    Height = 1,
                    Background = (Brush)FindResource("MaterialDesignDivider"),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                componentStack.Children.Add(separator);

                // Changelog Content
                var changelogText = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14,
                    LineHeight = 22
                };

                if (!string.IsNullOrWhiteSpace(component.Release?.body))
                {
                    // Parse markdown-style changelog
                    ParseMarkdownChangelog(changelogText, component.Release.body);
                }
                else
                {
                    changelogText.Text = TeknoParrotUi.Properties.Resources.ChangelogNoInformation;
                    changelogText.Opacity = 0.6;
                }

                componentStack.Children.Add(changelogText);

                // View on GitHub link
                if (!string.IsNullOrEmpty(component.Component.fullUrl))
                {
                    var linkPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 15, 0, 0) };
                    
                    var linkIcon = new MaterialDesignThemes.Wpf.PackIcon
                    {
                        Kind = MaterialDesignThemes.Wpf.PackIconKind.OpenInNew,
                        Width = 16,
                        Height = 16,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 5, 0),
                        Opacity = 0.7
                    };

                var linkText = new TextBlock
                {
                    Text = TeknoParrotUi.Properties.Resources.ChangelogViewOnGitHub,
                    FontSize = 12,
                    Opacity = 0.7,
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Hand
                };                    linkText.MouseLeftButtonDown += (s, e) =>
                    {
                        Process.Start(component.Component.fullUrl + (component.Component.opensource ? "commits/master" : $"releases/{component.Component.name}"));
                    };

                    linkPanel.Children.Add(linkIcon);
                    linkPanel.Children.Add(linkText);
                    componentStack.Children.Add(linkPanel);
                }

                componentCard.Child = componentStack;
                changelogList.Children.Add(componentCard);
            }
        }

        private MaterialDesignThemes.Wpf.PackIconKind GetIconForComponent(string componentName)
        {
            switch (componentName.ToLower())
            {
                case "teknoparrotui":
                    return MaterialDesignThemes.Wpf.PackIconKind.ApplicationCog;
                case "openparrotwin32":
                case "openparrotx64":
                    return MaterialDesignThemes.Wpf.PackIconKind.Cog;
                case "opensegaapi":
                    return MaterialDesignThemes.Wpf.PackIconKind.Api;
                case "teknoparrot":
                case "teknoparrotn2":
                case "teknoparrotelfld2":
                    return MaterialDesignThemes.Wpf.PackIconKind.Console;
                case "opensndgaelco":
                case "opensndvoyager":
                    return MaterialDesignThemes.Wpf.PackIconKind.VolumeHigh;
                case "scoresubmission":
                    return MaterialDesignThemes.Wpf.PackIconKind.Trophy;
                case "teknodraw":
                    return MaterialDesignThemes.Wpf.PackIconKind.Draw;
                case "ffbblaster":
                    return MaterialDesignThemes.Wpf.PackIconKind.Steering;
                case "crediardolphin":
                    return MaterialDesignThemes.Wpf.PackIconKind.Dolphin;
                case "play":
                    return MaterialDesignThemes.Wpf.PackIconKind.Play;
                case "rpcs3":
                    return MaterialDesignThemes.Wpf.PackIconKind.SonyPlaystation;
                default:
                    return MaterialDesignThemes.Wpf.PackIconKind.Package;
            }
        }

        private void ParseMarkdownChangelog(TextBlock textBlock, string markdown)
        {
            var lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            textBlock.Inlines.Clear();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    textBlock.Inlines.Add(new LineBreak());
                    continue;
                }

                // Headers (### or ##)
                if (line.StartsWith("### "))
                {
                    var run = new Run(line.Substring(4))
                    {
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 16,
                        Foreground = new SolidColorBrush(Color.FromRgb(98, 0, 234))
                    };
                    textBlock.Inlines.Add(run);
                    textBlock.Inlines.Add(new LineBreak());
                }
                else if (line.StartsWith("## "))
                {
                    var run = new Run(line.Substring(3))
                    {
                        FontWeight = FontWeights.Bold,
                        FontSize = 18,
                        Foreground = new SolidColorBrush(Color.FromRgb(98, 0, 234))
                    };
                    textBlock.Inlines.Add(run);
                    textBlock.Inlines.Add(new LineBreak());
                }
                // Bullet points
                else if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
                {
                    var indent = line.Length - line.TrimStart().Length;
                    var bulletRun = new Run(new string(' ', indent) + "• ")
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(98, 0, 234))
                    };
                    textBlock.Inlines.Add(bulletRun);
                    
                    var contentRun = new Run(line.TrimStart().Substring(2));
                    textBlock.Inlines.Add(contentRun);
                    textBlock.Inlines.Add(new LineBreak());
                }
                // Bold text
                else if (line.Contains("**"))
                {
                    ParseBoldText(textBlock, line);
                    textBlock.Inlines.Add(new LineBreak());
                }
                // Regular text
                else
                {
                    textBlock.Inlines.Add(new Run(line));
                    textBlock.Inlines.Add(new LineBreak());
                }
            }
        }

        private void ParseBoldText(TextBlock textBlock, string line)
        {
            var parts = line.Split(new[] { "**" }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                if (i % 2 == 0)
                {
                    // Regular text
                    if (!string.IsNullOrEmpty(parts[i]))
                        textBlock.Inlines.Add(new Run(parts[i]));
                }
                else
                {
                    // Bold text
                    if (!string.IsNullOrEmpty(parts[i]))
                        textBlock.Inlines.Add(new Run(parts[i]) { FontWeight = FontWeights.Bold });
                }
            }
        }

        private void CustomizeSubscriptionPromotion()
        {
            if (_isPatron)
            {
                // Thank the patron with special title
                subscriptionTitleText.Text = TeknoParrotUi.Properties.Resources.ChangelogSubscriptionTitlePatron;
                subscriptionDescription.Text = TeknoParrotUi.Properties.Resources.ChangelogPatronThankYou;
                subscriptionBenefits.Text = string.Format(TeknoParrotUi.Properties.Resources.ChangelogSubscriptionBenefitsPatron, _patreonGames.Count);
                
                // Change styling to a "thank you" theme
                subscriptionCard.Background = new SolidColorBrush(Color.FromArgb(26, 0, 200, 83));
                subscriptionCard.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 200, 83));
                
                // Change gold text to white for better readability on green background
                subscriptionTitleText.Foreground = new SolidColorBrush(Colors.White);
                subscriptionDescription.Foreground = new SolidColorBrush(Colors.White);
                
                var icon = (MaterialDesignThemes.Wpf.PackIcon)((Grid)subscriptionCard.Child).Children[0];
                icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckDecagram;
                icon.Foreground = new SolidColorBrush(Colors.White);
                
                // Change button to happy/positive for patrons
                continueButtonText.Text = TeknoParrotUi.Properties.Resources.ChangelogContinueButton;
                continueButtonIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.EmoticonHappy;
            }
            else if (_patreonGames.Count > 0)
            {
                // Show what they're missing - use formatted string
                subscriptionBenefits.Text = string.Format(
                    TeknoParrotUi.Properties.Resources.ChangelogSubscriptionBenefits,
                    _patreonGames.Count);
            }
        }

        private void SubscriptionCard_Click(object sender, MouseButtonEventArgs e)
        {
            // Show list of exclusive cores
            string cores = "★ " + TeknoParrotUi.Properties.Resources.ChangelogExclusiveCoresTitle + "\n\n";
            foreach (var game in _patreonGames)
            {
                string info = (game.GameInfo != null) ? $"({game.GameInfo.release_year}) - {game.GameInfo.platform}" : "";
                cores += $"  • {game.GameNameInternal} {info}\n";
            }
            
            if (_isPatron)
            {
                // Patrons just see the list
                MessageBoxHelper.InfoOK(cores);
            }
            else
            {
                // Non-patrons see the list, then website opens
                MessageBoxHelper.InfoOK(cores);
                // Open subscription page in browser
                Process.Start("https://teknoparrot.com/Home/Subscription");
            }
        }

        private void ButtonContinue_Click(object sender, RoutedEventArgs e)
        {
            _contentControl.Content = _library;
        }
    }
}
