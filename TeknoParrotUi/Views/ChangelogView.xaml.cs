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
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

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

        public class ChangelogData
        {
            [JsonProperty("generatedAt")]
            public DateTime GeneratedAt { get; set; }
            
            [JsonProperty("commits")]
            public List<CommitInfo> Commits { get; set; }
        }

        public class CommitInfo
        {
            [JsonProperty("author")]
            public string Author { get; set; }
            
            [JsonProperty("date")]
            public DateTime Date { get; set; }
            
            [JsonProperty("message")]
            public string Message { get; set; }
            
            [JsonProperty("repository")]
            public string Repository { get; set; }
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
            bool FORCE_NON_PATRON_VIEW = false;
            
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

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Show loading indicator
            loadingPanel.Visibility = Visibility.Visible;
            changelogScroller.Visibility = Visibility.Collapsed;
            
            await PopulateChangelogsAsync();
            
            // Hide loading indicator and show content
            loadingPanel.Visibility = Visibility.Collapsed;
            changelogScroller.Visibility = Visibility.Visible;
            
            CustomizeSubscriptionPromotion();
        }

        private async Task PopulateChangelogsAsync()
        {
            changelogList.Children.Clear();

            // Fetch changelog data from API
            List<CommitInfo> commits = await FetchChangelogDataAsync();
            
            if (commits == null || commits.Count == 0)
            {
                // Fallback to old behavior if API fails
                PopulateChangelogsFromComponents();
                return;
            }

            // Create main card for all changes
            var mainCard = new Border
            {
                Background = (Brush)FindResource("MaterialDesignCardBackground"),
                BorderBrush = (Brush)FindResource("MaterialDesignDivider"),
                BorderThickness = new Thickness(1),
                CornerRadius = new System.Windows.CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 20),
                Padding = new Thickness(20)
            };

            var mainStack = new StackPanel();

            // Header: "Last 10 changes across cores"
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            
            var headerIcon = new MaterialDesignThemes.Wpf.PackIcon
            {
                Kind = MaterialDesignThemes.Wpf.PackIconKind.History,
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(98, 0, 234))
            };

            var headerText = new TextBlock
            {
                Text = "Last 10 changes across TeknoParrot components",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            headerPanel.Children.Add(headerIcon);
            headerPanel.Children.Add(headerText);
            mainStack.Children.Add(headerPanel);

            // Separator
            var separator = new Border
            {
                Height = 1,
                Background = (Brush)FindResource("MaterialDesignDivider"),
                Margin = new Thickness(0, 0, 0, 15)
            };
            mainStack.Children.Add(separator);

            // Get last 10 commits sorted by date (most recent first)
            var recentCommits = commits.OrderByDescending(c => c.Date).Take(10).ToList();

            // List all commits in chronological order
            foreach (var commit in recentCommits)
            {
                var commitPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
                
                // Commit header with repository, author and date
                var commitHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
                
                // Repository badge
                var repoBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, 98, 0, 234)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(98, 0, 234)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new System.Windows.CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var repoText = new TextBlock
                {
                    Text = commit.Repository,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(98, 0, 234))
                };
                repoBadge.Child = repoText;
                commitHeader.Children.Add(repoBadge);

                var authorText = new TextBlock
                {
                    Text = commit.Author,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(98, 0, 234))
                };
                
                var dateText = new TextBlock
                {
                    Text = $" • {commit.Date:MMM d, yyyy HH:mm}",
                    FontSize = 12,
                    Opacity = 0.7,
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                commitHeader.Children.Add(authorText);
                commitHeader.Children.Add(dateText);
                commitPanel.Children.Add(commitHeader);
                
                // Commit message
                var messageText = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14,
                    LineHeight = 20,
                    Margin = new Thickness(0, 0, 0, 0)
                };
                
                ParseMarkdownChangelog(messageText, commit.Message);
                commitPanel.Children.Add(messageText);
                
                mainStack.Children.Add(commitPanel);
            }

            mainCard.Child = mainStack;
            changelogList.Children.Add(mainCard);
        }

        private async Task<List<CommitInfo>> FetchChangelogDataAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // Reduced timeout to 5 seconds to avoid long wait on unreachable server
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var response = await client.GetStringAsync("https://teknoparrot.com/en/Home/Changes");
                    var data = JsonConvert.DeserializeObject<ChangelogData>(response);
                    return data?.Commits ?? new List<CommitInfo>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to fetch changelog data: {ex.Message}");
                return null;
            }
        }

        private void PopulateChangelogsFromComponents()
        {
            // Fallback to original implementation
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
                    ParseMarkdownChangelog(changelogText, component.Release.body);
                }
                else
                {
                    changelogText.Text = TeknoParrotUi.Properties.Resources.ChangelogNoInformation;
                    changelogText.Opacity = 0.6;
                }

                componentStack.Children.Add(changelogText);

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
                case "teknoparrotdotcom":
                    return MaterialDesignThemes.Wpf.PackIconKind.Web;
                case "elfloader 2.0":
                    return MaterialDesignThemes.Wpf.PackIconKind.FileCode;
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
