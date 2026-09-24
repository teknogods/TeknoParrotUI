using System;
using System.Collections.Generic;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening.Gamepad;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>UI navigation actions that can be bound to controller inputs.</summary>
public enum UiNavAction
{
    Up,
    Down,
    Left,
    Right,
    Confirm,
    Back,
    ToggleFullscreen
}

/// <summary>
/// Lets players drive the UI with any gamepad, joystick or keyboard via merged
/// input. Reuses InputCaptureService as a continuous listener and matches the
/// incoming input display names against the player's saved bindings.
/// </summary>
public sealed class UiNavigationService : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(200);

    private readonly InputCaptureService _capture = new();
    private readonly Dictionary<string, UiNavAction> _map = new();
    private readonly Dictionary<UiNavAction, DateTime> _lastFired = new();
    private bool _useDefaultGamepadBindings;

    /// <summary>Pause matching while a binding editor is capturing input.</summary>
    public bool Suspended { get; set; }

    /// <summary>Raised on a background thread — marshal to the UI thread.</summary>
    public event Action<UiNavAction>? ActionTriggered;

    public UiNavigationService()
    {
        _capture.BindingCaptured += OnCaptured;
    }

    public void Restart(UiOptions options)
    {
        _capture.Stop();
        _map.Clear();
        _useDefaultGamepadBindings = false;

        if (!options.EnableControllerNavigation)
            return;

        foreach (var pair in options.NavigationBindings)
        {
            if (!string.IsNullOrWhiteSpace(pair.Value) && Enum.TryParse<UiNavAction>(pair.Key, out var action))
                _map[pair.Value] = action;
        }

        _useDefaultGamepadBindings = OperatingSystem.IsLinux() && _map.Count == 0;

        if (_map.Count > 0 || _useDefaultGamepadBindings)
            _capture.Start(InputApi.MergedInput);
    }

    private void OnCaptured(CapturedBinding captured)
    {
        if (Suspended)
            return;
        if (!_map.TryGetValue(captured.DisplayName, out var action) &&
            (!_useDefaultGamepadBindings || !TryDefaultGamepadAction(captured.XInput, out action)))
            return;

        var now = DateTime.UtcNow;
        if (_lastFired.TryGetValue(action, out var last) && now - last < Debounce)
            return;
        _lastFired[action] = now;

        ActionTriggered?.Invoke(action);
    }

    private static bool TryDefaultGamepadAction(XInputButton? input, out UiNavAction action)
    {
        action = default;
        if (input == null || input.SdlControl != SdlControlKind.None)
            return false;
        if (input.IsButton)
        {
            action = (GamepadButtonFlags)input.ButtonCode switch
            {
                GamepadButtonFlags.DPadUp => UiNavAction.Up,
                GamepadButtonFlags.DPadDown => UiNavAction.Down,
                GamepadButtonFlags.DPadLeft => UiNavAction.Left,
                GamepadButtonFlags.DPadRight => UiNavAction.Right,
                GamepadButtonFlags.A or GamepadButtonFlags.Start => UiNavAction.Confirm,
                GamepadButtonFlags.B or GamepadButtonFlags.Back => UiNavAction.Back,
                _ => default
            };
            return (GamepadButtonFlags)input.ButtonCode is
                GamepadButtonFlags.DPadUp or GamepadButtonFlags.DPadDown or
                GamepadButtonFlags.DPadLeft or GamepadButtonFlags.DPadRight or
                GamepadButtonFlags.A or GamepadButtonFlags.Start or
                GamepadButtonFlags.B or GamepadButtonFlags.Back;
        }
        if (input.IsLeftThumbY)
        {
            action = input.IsAxisMinus ? UiNavAction.Down : UiNavAction.Up;
            return true;
        }
        if (input.IsLeftThumbX)
        {
            action = input.IsAxisMinus ? UiNavAction.Left : UiNavAction.Right;
            return true;
        }
        return false;
    }

    public void Stop() => _capture.Stop();

    public void Dispose() => _capture.Dispose();
}
