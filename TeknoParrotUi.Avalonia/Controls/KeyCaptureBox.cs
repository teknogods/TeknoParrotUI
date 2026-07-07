using System;
using Avalonia.Controls;
using Avalonia.Threading;
using TeknoParrotUi.Avalonia.Services;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Avalonia.Controls;

/// <summary>
/// A button that captures a keyboard key (as a Win32 virtual-key code) when clicked.
/// Displays the key name; Escape cancels the capture. Values are stored in the same
/// "0x79" hex format the classic UI and teknoparrot.ini use.
/// </summary>
public sealed class KeyCaptureBox : Button
{
    private static readonly RawInputCaptureService SharedCapture = new();
    private static KeyCaptureBox? _armed;

    private int _virtualKey;

    public event Action? VirtualKeyChanged;

    public KeyCaptureBox()
    {
        Content = "(none)";
        Click += (_, _) => Arm();
        SharedCapture.BindingCaptured += OnCaptured;
    }

    public int VirtualKey
    {
        get => _virtualKey;
        set
        {
            _virtualKey = value;
            Content = value == 0 ? "(none)" : ((Keys)value).ToString();
        }
    }

    /// <summary>Value in the "0x1B" format used by ParrotData/game profiles.</summary>
    public string HexValue
    {
        get => $"0x{_virtualKey:X}";
        set
        {
            var text = value?.Replace("0x", "") ?? "";
            VirtualKey = int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var vk) ? vk : 0;
        }
    }

    private void Arm()
    {
        if (!OperatingSystem.IsWindows())
            return;
        if (_armed != null && _armed != this)
            _armed.Disarm();
        _armed = this;
        Content = "Press a key...";
        SharedCapture.Start(registerKeyboard: true);
    }

    private void Disarm()
    {
        VirtualKey = _virtualKey; // restore display
        if (_armed == this)
        {
            _armed = null;
            SharedCapture.Stop();
        }
    }

    private void OnCaptured(string name, RawInputButton button, bool isEscape)
    {
        if (_armed != this || button.DeviceType != RawDeviceType.Keyboard)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (isEscape)
            {
                Disarm();
                return;
            }
            VirtualKey = (int)button.KeyboardKey;
            VirtualKeyChanged?.Invoke();
            Disarm();
        });
    }
}
