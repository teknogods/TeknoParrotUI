using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Activation;

namespace TeknoParrotUi.Avalonia.Views;

/// <summary>
/// Subscription (Patreon) registration — ported from the classic Patreon view:
/// serial keys are registered/deactivated through BudgieLoader.exe and stored
/// in the registry by it.
/// </summary>
public partial class SubscriptionView : UserControl
{
    public SubscriptionView()
    {
        InitializeComponent();
        ConfigurePlatformLayout();
        Localize();
        Services.Loc.LanguageChanged += () => Dispatcher.UIThread.Post(() =>
        {
            Localize();
            _ = RefreshStateAsync();
        });
        Loaded += (_, _) => _ = RefreshStateAsync();
    }

    private void ConfigurePlatformLayout()
    {
        if (!OperatingSystem.IsAndroid())
            return;

        ActivationKeyGrid.ColumnDefinitions = new ColumnDefinitions("*,Auto");
        ActivationKeyGrid.RowDefinitions = new RowDefinitions("Auto,Auto");
        Grid.SetColumn(LblSerial, 0);
        Grid.SetColumnSpan(LblSerial, 2);
        Grid.SetRow(LblSerial, 0);
        Grid.SetColumn(KeyBox, 0);
        Grid.SetRow(KeyBox, 1);
        Grid.SetColumn(BtnReveal, 1);
        Grid.SetRow(BtnReveal, 1);
        LblSerial.Margin = new Thickness(0, 0, 0, 4);

        ActivationActions.Orientation = Orientation.Vertical;
        ActivationActions.HorizontalAlignment = HorizontalAlignment.Stretch;
        foreach (var button in new[] { BtnRegister, BtnDeregister, BtnWebsite })
        {
            button.MinHeight = 48;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
    }

    private void Localize()
    {
        LblSerial.Text = Services.Loc.T("PatreonSubscriptionKey", "Serial key").TrimEnd(':');
        BtnRegister.Content = Services.Loc.T("PatreonRegisterKey", "Register");
        BtnDeregister.Content = Services.Loc.T("PatreonDeregisterKey", "Deactivate");
        BtnWebsite.Content = Services.Loc.T("PatreonBecomeAPatron", "Get a Subscription");
    }

    private async Task RefreshStateAsync(bool updateConsole = true)
    {
        var patreonGames = GameProfileLoader.GameProfiles?.Count(p => p.Patreon && !p.DevOnly) ?? 0;
        GameCountText.Text = patreonGames > 0
            ? string.Format(Services.Loc.T("PatreonViewSubscriptionGameList", "View Subscription Game List ({0} games!)"), patreonGames)
            : "";
        GameCountButton.IsVisible = patreonGames > 0;

        TeknoParrotActivationStatus status;
        try
        {
            status = await TeknoParrotActivation.GetStatusAsync();
        }
        catch (Exception error)
        {
            status = new TeknoParrotActivationStatus(
                false, "Could not read subscription state: " + error.Message);
        }

        if (status.IsActivated)
        {
            // The stored value is generated subscription activation data, not
            // the serial the user entered. Never place that secret in a UI
            // control or offer to reveal it.
            KeyBox.Text = "Activated on this device";
            KeyBox.IsReadOnly = true;
            KeyBox.PasswordChar = '\0';
            KeyBox.RevealPassword = false;
            BtnReveal.IsVisible = false;
            BtnReveal.IsChecked = false;
            BtnRegister.IsVisible = false;
            BtnDeregister.IsVisible = true;
        }
        else
        {
            KeyBox.Text = string.Empty;
            KeyBox.IsReadOnly = false;
            KeyBox.PasswordChar = '●';
            KeyBox.RevealPassword = false;
            BtnReveal.IsChecked = false;
            BtnReveal.IsVisible = true;
            BtnRegister.IsVisible = true;
            BtnDeregister.IsVisible = false;
        }

        if (updateConsole && !string.IsNullOrWhiteSpace(status.Message))
            ConsoleText.Text = status.Message;
    }

    private void Log(string line) =>
        Dispatcher.UIThread.Post(() => ConsoleText.Text += line + Environment.NewLine);

    private async Task RunActivationAsync(bool deactivate)
    {
        ConsoleText.Text = "";
        BtnRegister.IsEnabled = false;
        BtnDeregister.IsEnabled = false;
        KeyBox.IsEnabled = false;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var result = deactivate
                ? await TeknoParrotActivation.DeactivateAsync(timeout.Token)
                : await TeknoParrotActivation.ActivateAsync(KeyBox.Text?.Trim() ?? "", timeout.Token);
            ConsoleText.Text = result.Message;
            foreach (var line in result.Output)
                Log(line);
        }
        catch (OperationCanceledException)
        {
            ConsoleText.Text = "Subscription activation timed out.";
        }
        catch (Exception error)
        {
            ConsoleText.Text = "Subscription activation failed: " + error.Message;
        }
        finally
        {
            KeyBox.IsEnabled = true;
            BtnRegister.IsEnabled = true;
            BtnDeregister.IsEnabled = true;
            await RefreshStateAsync(updateConsole: false);
        }
    }

    private async void BtnRegister_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(KeyBox.Text))
        {
            ConsoleText.Text = "Serial key must not be blank.";
            return;
        }
        await RunActivationAsync(deactivate: false);
    }

    private async void BtnDeregister_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        await RunActivationAsync(deactivate: true);

    private void GameCountButton_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Populate the prompt with all subscription games, sorted by name
        SubGamesList.ItemsSource = (GameProfileLoader.GameProfiles ?? new System.Collections.Generic.List<GameProfile>())
            .Where(p => p.Patreon && !p.DevOnly)
            .Select(p => p.GameNameInternal ?? p.ProfileName)
            .OrderBy(n => n)
            .ToList();
    }

    private void BtnReveal_IsCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        KeyBox.RevealPassword = BtnReveal.IsChecked == true;

    private async void BtnWebsite_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        await Services.ExternalUrlLauncher.OpenAsync(this, "https://teknoparrot.com");
}
