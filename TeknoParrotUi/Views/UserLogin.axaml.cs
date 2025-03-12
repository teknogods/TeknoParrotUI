using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.ComponentModel;

namespace TeknoParrotUi.Views
{
    // TODO: FIX ENTIRE THING
    public partial class UserLogin : UserControl
    {
        public UserLogin()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // Find control references
            //Browser = this.FindControl<WebViewControl.WebView>("Browser");
        }

        private void UserLogin_OnPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == IsVisibleProperty)
            {
                // Handle visibility changes
                if (IsVisible)
                {
                    // Control became visible - perform any necessary actions
                    //Browser?.NavigateToUrl("https://teknoparrot.com:3333/Home/Chat");
                }
                else
                {
                    // Control became invisible - perform cleanup if needed
                }
            }
        }
    }
}