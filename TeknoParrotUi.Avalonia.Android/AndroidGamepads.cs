using System;
using System.Collections.Generic;
using Android.Views;
using TeknoParrotUi.Common.InputListening.Gamepad;

namespace TeknoParrotUi.Avalonia.Android;

/// <summary>Feeds Android physical controllers into the shared button editor and game listener.</summary>
internal static class AndroidGamepads
{
    private const int SourceDpad = 0x00000201;
    private const int SourceGamepad = 0x00000401;
    private const int SourceJoystick = 0x01000010;

    // Some HID pads report button keycodes but advertise only SOURCE_KEYBOARD.
    private static readonly HashSet<int> ButtonOnlyDevices = new();
    private static readonly Dictionary<int, GamepadButtonFlags> KeyButtons = new();
    private static readonly Dictionary<int, GamepadButtonFlags> HatButtons = new();

    public static bool IsController(InputDevice? device)
    {
        if (device == null || device.Id < 0) return false;
        var sources = (int)device.Sources;
        return (sources & SourceGamepad) == SourceGamepad ||
               (sources & SourceJoystick) == SourceJoystick ||
               (sources & SourceDpad) == SourceDpad ||
               device.ControllerNumber > 0 || ButtonOnlyDevices.Contains(device.Id);
    }

    private static bool IsControllerRange(InputDevice.MotionRange range)
    {
        var source = (int)range.Source;
        return (source & SourceJoystick) == SourceJoystick ||
               (source & SourceGamepad) == SourceGamepad;
    }

    public static void Refresh()
    {
        var devices = new List<SDL2GamepadBackend.PlatformGamepadDevice>();
        var liveIds = new HashSet<int>();
        foreach (var deviceId in InputDevice.GetDeviceIds())
        {
            var device = InputDevice.GetDevice(deviceId);
            if (device == null) continue;
            liveIds.Add(deviceId);
            if (!IsController(device)) continue;

            var rest = new short[64];
            foreach (var range in device.MotionRanges)
            {
                if (!IsControllerRange(range)) continue;
                var axis = (int)range.Axis;
                if (axis >= 0 && axis < rest.Length && range.Min >= 0 && range.Max > range.Min)
                    rest[axis] = short.MinValue;
            }
            devices.Add(new SDL2GamepadBackend.PlatformGamepadDevice(
                deviceId, device.Name ?? "Android controller", rest));
        }
        ButtonOnlyDevices.IntersectWith(liveIds);
        foreach (var id in new List<int>(KeyButtons.Keys))
            if (!liveIds.Contains(id)) KeyButtons.Remove(id);
        foreach (var id in new List<int>(HatButtons.Keys))
            if (!liveIds.Contains(id)) HatButtons.Remove(id);
        SDL2GamepadBackend.UpdatePlatformDevices(devices);
    }

    public static void OnKey(KeyEvent keyEvent)
    {
        if (keyEvent.Action is not (KeyEventActions.Down or KeyEventActions.Up)) return;
        var device = keyEvent.Device;
        if (device == null) return;
        var flag = AndroidGamepadMapping.Button((int)keyEvent.KeyCode);
        if (!IsController(device))
        {
            // Controller button keycodes are distinct from normal typing keys.
            if (flag == GamepadButtonFlags.None || (int)keyEvent.KeyCode < 96 || device.Id <= 0)
                return;
            ButtonOnlyDevices.Add(device.Id);
        }
        if (!SDL2GamepadBackend.HasPlatformDevice(device.Id)) Refresh();
        if (!SDL2GamepadBackend.HasPlatformDevice(device.Id)) return;

        var pressed = keyEvent.Action == KeyEventActions.Down;
        SDL2GamepadBackend.UpdatePlatformButton(device.Id, (int)keyEvent.KeyCode, pressed);
        if (flag == GamepadButtonFlags.None) return;
        KeyButtons.TryGetValue(device.Id, out var keyFlags);
        keyFlags = pressed ? keyFlags | flag : keyFlags & ~flag;
        KeyButtons[device.Id] = keyFlags;
        HatButtons.TryGetValue(device.Id, out var hatFlags);
        var gamepad = SDL2GamepadBackend.GetPlatformGamepad(device.Id);
        gamepad.Buttons = keyFlags | hatFlags;
        SDL2GamepadBackend.UpdatePlatformGamepad(device.Id, gamepad);
    }

    public static void OnMotion(MotionEvent motionEvent)
    {
        var device = motionEvent.Device;
        if (!IsController(device) || motionEvent.ActionMasked != MotionEventActions.Move)
            return;
        if (!SDL2GamepadBackend.HasPlatformDevice(device!.Id)) Refresh();
        if (!SDL2GamepadBackend.HasPlatformDevice(device.Id)) return;

        var gamepad = SDL2GamepadBackend.GetPlatformGamepad(device.Id);
        foreach (var range in device.MotionRanges)
        {
            if (!IsControllerRange(range) || range.Max <= range.Min) continue;
            var axis = (int)range.Axis;
            if (axis < 0 || axis >= 64) continue;
            var value = motionEvent.GetAxisValue(range.Axis);
            var scaled = (value - range.Min) / (range.Max - range.Min) * 65535f - 32768f;
            SDL2GamepadBackend.UpdatePlatformAxis(device.Id, axis,
                (short)Math.Clamp((int)Math.Round(scaled), short.MinValue, short.MaxValue));

            switch (range.Axis)
            {
                case Axis.X: gamepad.LeftThumbX = AndroidGamepadMapping.Stick(value, range.Min, range.Max); break;
                case Axis.Y: gamepad.LeftThumbY = AndroidGamepadMapping.Stick(value, range.Min, range.Max, invert: true); break;
                case Axis.Z: gamepad.RightThumbX = AndroidGamepadMapping.Stick(value, range.Min, range.Max); break;
                case Axis.Rz: gamepad.RightThumbY = AndroidGamepadMapping.Stick(value, range.Min, range.Max, invert: true); break;
                case Axis.Rx when device.GetMotionRange(Axis.Z) == null:
                    gamepad.RightThumbX = AndroidGamepadMapping.Stick(value, range.Min, range.Max); break;
                case Axis.Ry when device.GetMotionRange(Axis.Rz) == null:
                    gamepad.RightThumbY = AndroidGamepadMapping.Stick(value, range.Min, range.Max, invert: true); break;
                case Axis.Ltrigger:
                    gamepad.LeftTrigger = AndroidGamepadMapping.Trigger(value, range.Min, range.Max); break;
                case Axis.Rtrigger:
                    gamepad.RightTrigger = AndroidGamepadMapping.Trigger(value, range.Min, range.Max); break;
                case Axis.Brake when device.GetMotionRange(Axis.Ltrigger) == null:
                    gamepad.LeftTrigger = AndroidGamepadMapping.Trigger(value, range.Min, range.Max); break;
                case Axis.Gas when device.GetMotionRange(Axis.Rtrigger) == null:
                    gamepad.RightTrigger = AndroidGamepadMapping.Trigger(value, range.Min, range.Max); break;
            }
        }
        // Android can report the D-pad as hat axes, keycodes, or both.
        var hat = AndroidGamepadMapping.Hat(
            motionEvent.GetAxisValue(Axis.HatX), motionEvent.GetAxisValue(Axis.HatY));
        HatButtons[device.Id] = hat;
        KeyButtons.TryGetValue(device.Id, out var keyButtons);
        gamepad.Buttons = keyButtons | hat;
        SDL2GamepadBackend.UpdatePlatformGamepad(device.Id, gamepad);
    }
}
