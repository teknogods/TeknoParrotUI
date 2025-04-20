using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Xilium.CefGlue.Avalonia;
using Xilium.CefGlue.Common.Handlers;
using System;
using System.Text.Json;
using TeknoParrotUi.Common;
using System.Threading.Tasks;

namespace TeknoParrotUi.Views
{
    public partial class UserLogin : UserControl
    {
        private AvaloniaCefBrowser Browser;
        public bool IsActive = false;
        private TPO2Callback _TPO2Callback;

        public UserLogin()
        {
            InitializeComponent();
        }

        static Task<object> AsyncCallNativeMethod(Func<object> nativeMethod)
        {
            return Task.Run(() =>
            {
                var result = nativeMethod.Invoke();
                if (result is Task task)
                {
                    if (task.GetType().IsGenericType)
                    {
                        return ((dynamic) task).Result;
                    }

                    return task;
                }

                return result;
            });
        }
        
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            var browserWrapper = this.FindControl<Decorator>("browserWrapper");

            Browser = new AvaloniaCefBrowser();

            if (browserWrapper != null)
            {
                browserWrapper.Child = Browser;
            }
            else
            {
                throw new InvalidOperationException("Browser wrapper not found in XAML. Make sure it has x:Name='browserWrapper'");
            }

            _TPO2Callback = new TPO2Callback();
            _TPO2Callback.GameProcessExited += OnGameProcessExited;
            Browser.RegisterJavascriptObject(_TPO2Callback, "callbackObj", AsyncCallNativeMethod);

            Browser.Address = "https://teknoparrot.com:3333/Home/Chat";

        }

        private void OnGameProcessExited()
        {
            Browser.ExecuteJavaScript("onGameProcessExited();");
        }

        // Do we even need this?
        public void RefreshBrowserToStart()
        {
            if (Browser != null)
            {
                Browser.Address = "https://teknoparrot.com:3333/Home/Chat";
            }
        }

        private void UserLogin_OnPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == IsVisibleProperty && Browser != null)
            {
                if (IsVisible)
                {
                    RefreshBrowserToStart();
                    IsActive = true;
                }
                else
                {
                    RefreshBrowserToStart();
                    Browser.Reload();
                    IsActive = false;
                }
            }
        }
    }

    // Cross-Platform implementation of TPO2Callback will go here
    // You can use the implementation I provided earlier with IGameProcess interface
}