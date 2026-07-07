using Avalonia.Controls;
using TeknoParrotUi.Common.Auth;

namespace TeknoParrotUi.Avalonia.Views;

public partial class AccountView : UserControl
{
    private readonly OAuthClient _oauth = new();

    public AccountView()
    {
        InitializeComponent();
        UpdateState();
    }

    private void UpdateState()
    {
        if (_oauth.IsLoggedIn)
        {
            var name = _oauth.GetClaim("unique_name") ?? _oauth.GetClaim("sub") ?? "user";
            StatusText.Text = $"Logged in as {name}.";
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
