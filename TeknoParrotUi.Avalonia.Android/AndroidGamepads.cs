using System;
using System.Collections.Generic;
using Android.Views;
using TeknoParrotUi.Common.InputListening.Gamepad;

namespace TeknoParrotUi.Avalonia.Android;

/// <summary>Feeds Android's physical controller APIs into the shared button editor and game listener.</summary>
internal static class AndroidGamepads
{
    private const int SourceGamepad = 0x00000401;
    private const int SourceJoystick = 0x01000010;

    public static bool IsController(InputDevice? device)
    {
        if (device == null) return false;
        var sources = (int)device.Sources;
        return (sources & SourceGamepad) == SourceGamepad ||
               (sources & SourceJoystick) == SourceJoystick;
    }

    public static void Refresh()
    {
        var devices = new List<SDL2GamepadBackend.PlatformGamepadDevice>();
        foreach (var deviceId in InputDevice.GetDeviceIds())
        {
            var device = InputDevice.GetDevice(deviceId);
            if (!IsController(device)) continue;

            var rest = new short[64];
            foreach (var range in device!.MotionRanges)
            {
                var axis = (int)range.Axis;
                if (axis >= 0 && axis < rest.Length && range.Min >= 0 && range.Max > range.Min)
                    rest[axis] = short.MinValue;
            }
            devices.Add(new SDL2GamepadBackend.PlatformGamepadDevice(
                deviceId, device.Name ?? "Android controller", rest));
        }
        SDL2GamepadBackend.UpdatePlatformDevices(devices);
    }

    public static void OnKey(KeyEvent keyEvent)
    {
        if (!IsController(keyEvent.Device) || keyEvent.Action is not (KeyEventActions.Down or KeyEventActions.Up))
            return;
        if (!SDL2GamepadBackend.HasPlatformDevice(keyEvent.DeviceId)) Refresh();
        SDL2GamepadBackend.UpdatePlatformButton(
            keyEvent.DeviceId, (int)keyEvent.KeyCode, keyEvent.Action == KeyEventActions.Down);
    }

    public static void OnMotion(MotionEvent motionEvent)
    {
        var device = motionEvent.Device;
        if (!IsController(device) || motionEvent.ActionMasked != MotionEventActions.Move)
            return;
        if (!SDL2GamepadBackend.HasPlatformDevice(motionEvent.DeviceId)) Refresh();
        foreach (var range in device!.MotionRanges)
        {
            var axis = (int)range.Axis;
            if (axis < 0 || axis >= 64 || range.Max <= range.Min) continue;
            var value = motionEvent.GetAxisValue(range.Axis);
            var scaled = (value - range.Min) / (range.Max - range.Min) * 65535f - 32768f;
            SDL2GamepadBackend.UpdatePlatformAxis(
                motionEvent.DeviceId, axis, (short)Math.Clamp((int)Math.Round(scaled), short.MinValue, short.MaxValue));
        }
    }
}
