using System;
using Avalonia.Controls;
using TeknoParrotUi.Avalonia.Services;

namespace TeknoParrotUi.Avalonia.Views;

public partial class AnnouncementWindow : Window
{
    public AnnouncementWindow() => InitializeComponent();

    public AnnouncementWindow(Uri pageUrl, bool isSubscribed) : this()
    {
        Title = Loc.T("AnnouncementTitle", "TeknoParrot announcement");
        NewsContent.Configure(pageUrl, isSubscribed);
        NewsContent.CloseRequested += (_, _) => Close();
    }
}
