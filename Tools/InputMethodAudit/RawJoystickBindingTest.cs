using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml.Serialization;
using SDL2;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening.Gamepad;
using TeknoParrotUi.Common.InputProfiles.Helpers;

namespace InputMethodAudit;

internal static class RawJoystickBindingTest
{
    public static int Run()
    {
        var rest = new short[64];
        rest[17] = short.MinValue;
        SDL2GamepadBackend.UpdatePlatformDevices(new[]
        {
            new SDL2GamepadBackend.PlatformGamepadDevice(73, "Arcade HID", rest)
        });
        try
        {
            if (!SDL2GamepadBackend.IsConnected(0) || SDL2GamepadBackend.GetDeviceName(0) != "Arcade HID")
                throw new Exception("Generic device was not enumerated.");
            var button = new XInputButton
            {
                XInputIndex = 0, IsButton = true, SdlControl = SdlControlKind.Button, SdlControlIndex = 31
            };
            SDL2GamepadBackend.UpdatePlatformButton(73, 31, true);
            if (DigitalHelper.GetButtonPressXinput(button, SDL2GamepadBackend.GetState(0), 0) != true ||
                DigitalHelper.GetButtonPressXinput(button, SDL2GamepadBackend.GetState(0), 1) != null)
                throw new Exception("Button 32 was not mapped to the correct device.");

            var axis = new XInputButton
            {
                XInputIndex = 0, SdlControl = SdlControlKind.Axis, SdlControlIndex = 17, SdlDirection = 1
            };
            SDL2GamepadBackend.UpdatePlatformAxis(73, 17, 27000);
            if (DigitalHelper.GetButtonPressXinput(axis, SDL2GamepadBackend.GetState(0), 0) != true)
                throw new Exception("High-numbered axis was not captured.");

            var hats = new byte[3];
            hats[2] = 0x04;
            var hatBinding = new XInputButton
            {
                SdlControl = SdlControlKind.Hat, SdlControlIndex = 2, SdlDirection = 0x04
            };
            if (!new RawJoystickState(Array.Empty<bool>(), Array.Empty<short>(), hats).IsPressed(hatBinding))
                throw new Exception("Hat direction was not captured.");

            var serializer = new XmlSerializer(typeof(XInputButton));
            using var writer = new StringWriter();
            serializer.Serialize(writer, button);
            using var reader = new StringReader(writer.ToString());
            var restored = (XInputButton)serializer.Deserialize(reader)!;
            if (restored.SdlControl != SdlControlKind.Button || restored.SdlControlIndex != 31)
                throw new Exception("Generic binding did not survive XML serialization.");

            SDL2GamepadBackend.UpdatePlatformButton(73, 31, false);
            if (DigitalHelper.GetButtonPressXinput(button, SDL2GamepadBackend.GetState(0), 0) != false)
                throw new Exception("Button release was not observed.");
            Console.WriteLine("Generic joystick buttons, axes, hats and binding persistence: PASS");
            if (!OperatingSystem.IsAndroid()) RunVirtualSdlDevice();
            return 0;
        }
        finally
        {
            SDL2GamepadBackend.UpdatePlatformDevices(Array.Empty<SDL2GamepadBackend.PlatformGamepadDevice>());
        }
    }

    private static void RunVirtualSdlDevice()
    {
        SDL2GamepadBackend.Acquire();
        var deviceIndex = -1;
        var handle = IntPtr.Zero;
        try
        {
            // An unmapped virtual joystick is the regression case: the old
            // SDL_IsGameController gate skipped it completely.
            deviceIndex = SDL.SDL_JoystickAttachVirtual(0, 8, 40, 1);
            if (deviceIndex < 0) throw new Exception("SDL virtual joystick unavailable: " + SDL.SDL_GetError());
            handle = SDL.SDL_JoystickOpen(deviceIndex);
            if (handle == IntPtr.Zero) throw new Exception("Could not open SDL virtual joystick.");
            int slot = -1;
            for (int attempt = 0; attempt < 100 && slot < 0; attempt++)
            {
                for (int i = 0; i < SDL2GamepadBackend.MaxSlots; i++)
                    if (SDL2GamepadBackend.IsConnected(i) && SDL2GamepadBackend.GetRawState(i).Buttons.Length == 40)
                        slot = i;
                if (slot < 0) Thread.Sleep(20);
            }
            if (slot < 0) throw new Exception("SDL backend skipped an unmapped virtual joystick.");

            SDL.SDL_JoystickSetVirtualButton(handle, 31, 1);
            var observed = false;
            for (int attempt = 0; attempt < 100 && !observed; attempt++)
            {
                observed = SDL2GamepadBackend.GetRawState(slot).Button(31);
                if (!observed) Thread.Sleep(20);
            }
            if (!observed) throw new Exception("SDL backend did not poll generic button 32.");
            Console.WriteLine("Unmapped SDL virtual joystick enumeration and button 32: PASS");
        }
        finally
        {
            if (handle != IntPtr.Zero) SDL.SDL_JoystickClose(handle);
            if (deviceIndex >= 0) SDL.SDL_JoystickDetachVirtual(deviceIndex);
            SDL2GamepadBackend.Release();
        }
    }
}
