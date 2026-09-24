using System;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace TeknoParrotUi.Avalonia.Views;

/// <summary>
/// Android's Avalonia accessibility bridge cannot enumerate the native WebView
/// interop peer. The surrounding news controls remain accessible.
/// </summary>
public class NewsWebView : NativeWebView
{
    protected override AutomationPeer OnCreateAutomationPeer()
        => OperatingSystem.IsAndroid() ? new NoneAutomationPeer(this) : base.OnCreateAutomationPeer();
}
