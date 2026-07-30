using System;
using Avalonia.Controls;
using Avalonia.Layout;
using TeknoParrotUi.Common.Auth;

namespace TeknoParrotUi.Avalonia.Views;

public partial class AccountView : UserControl
{
    private readonly OAuthClient _oauth = new();

    public AccountView()
    {
        InitializeComponent();
        if (OperatingSystem.IsAndroid())
        {
            AccountActions.Orientation = Orientation.Vertical;
            AccountActions.HorizontalAlignment = HorizontalAlignment.Stretch;
            foreach (var button in new[] { BtnLogin, BtnLogout })
            {
                button.MinHeight = 48;
                button.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
        }
        Localize();
        Services.Loc.LanguageChanged += Localize;
        UpdateState();
    }

    private void Localize()
    {
        HeaderText.Text = Services.Loc.T("AccountPageTitle", "TeknoParrot Account");
        BtnLogin.Content = OperatingSystem.IsAndroid()
            ? Services.Loc.T(
                "AccountPageLoginDisabledAndroid",
                "Login (Currently Disabled)")
            : Services.Loc.T("AccountPageLoginButton", "Log In with Browser");
        BtnLogout.Content = Services.Loc.T("AccountPageLogoutButton", "Log Out");
    }

    private void UpdateState()
    {
        if (OperatingSystem.IsAndroid())
        {
            StatusText.Text = "Account login is currently disabled on Android.";
            BtnLogin.IsVisible = true;
            BtnLogin.IsEnabled = false;
            BtnLogin.Opacity = 0.5;
            BtnLogout.IsVisible = false;
            return;
        }

        if (_oauth.IsLoggedIn)
        {
            var name = _oauth.GetUserName() ?? "user";
            var email = _oauth.GetEmail();
            StatusText.Text = email != null
                ? $"Logged in as {name} ({email})."
                : $"Logged in as {name}.";
            BtnLogin.IsVisible = false;
            BtnLogout.IsVisible = true;
        }
        else
        {
            StatusText.Text = "Not logged in.";
            BtnLogin.IsVisible = true;
            BtnLogout.IsVisible = false;
        }
    }

    private async void BtnLogin_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (OperatingSystem.IsAndroid())
        {
            StatusText.Text = "Account login is currently disabled on Android.";
            return;
        }

        BtnLogin.IsEnabled = false;
        StatusText.Text = "Waiting for browser login...";
        try
        {
            var ok = await _oauth.LoginAsync();
            StatusText.Text = ok ? StatusText.Text : "Login failed or was cancelled.";
        }
        catch (System.Exception ex)
        {
            StatusText.Text = $"Login error: {ex.Message}";
        }
        finally
        {
            BtnLogin.IsEnabled = true;
            UpdateState();
        }
    }

    private void BtnLogout_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _oauth.Logout();
        UpdateState();
    }
}
