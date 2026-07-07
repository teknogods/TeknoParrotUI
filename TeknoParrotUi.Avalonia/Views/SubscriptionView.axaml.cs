using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Win32;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Views;

/// <summary>
/// Subscription (Patreon) registration — ported from the classic Patreon view:
/// serial keys are registered/deactivated through BudgieLoader.exe and stored
/// in the registry by it.
/// </summary>
public partial class SubscriptionView : UserControl
{
    private const string BudgieLoader = ".\\TeknoParrot\\BudgieLoader.exe";

    public SubscriptionView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshState();
    }

    private void RefreshState()
    {
        var patreonGames = GameProfileLoader.GameProfiles?.Count(p => p.Patreon && !p.DevOnly) ?? 0;
        GameCountText.Text = patreonGames > 0 ? $"{patreonGames} subscription game(s) available" : "";
        GameCountButton.IsVisible = patreonGames > 0;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot");
            var value = key?.GetValue("PatreonSerialKey") as byte[];
            if (value != null)
            {
                KeyBox.Text = Encoding.ASCII.GetString(value);
                KeyBox.IsReadOnly = true;
                // Registered key is sensitive — mask it, reveal via the eye toggle
                KeyBox.PasswordChar = '●';
                BtnReveal.IsVisible = true;
                BtnReveal.IsChecked = false;
                BtnRegister.IsVisible = false;
                BtnDeregister.IsVisible = true;
            }
            else
            {
                KeyBox.IsReadOnly = false;
                // Typing a new key — no masking
                KeyBox.PasswordChar = '\0';
                BtnReveal.IsVisible = false;
                BtnRegister.IsVisible = true;
                BtnDeregister.IsVisible = false;
            }
        }
        catch
        {
            // registry unavailable
        }
    }

    private void Log(string line) =>
        Dispatcher.UIThread.Post(() => ConsoleText.Text += line + Environment.NewLine);

    private void RunBudgie(string arguments)
    {
        if (!File.Exists(BudgieLoader))
        {
            ConsoleText.Text = "TeknoParrot core (BudgieLoader.exe) is not installed — run Updates first.";
            return;
        }

        ConsoleText.Text = "";
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(BudgieLoader, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };
        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Log(e.Data);
        };
        process.Exited += (_, _) => Dispatcher.UIThread.Post(RefreshState);
        process.Start();
        process.BeginOutputReadLine();
    }

    private void BtnRegister_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(KeyBox.Text))
        {
            ConsoleText.Text = "Serial key must not be blank.";
            return;
        }
        RunBudgie("-register " + KeyBox.Text.Trim());
    }

    private void BtnDeregister_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        RunBudgie("-deactivate");

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

    private void BtnWebsite_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://teknoparrot.com") { UseShellExecute = true });
}
