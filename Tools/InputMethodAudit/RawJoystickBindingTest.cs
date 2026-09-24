using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml.Serialization;
using Newtonsoft.Json;
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
        SDL3GamepadBackend.UpdatePlatformDevices(new[]
        {
            new SDL3GamepadBackend.PlatformGamepadDevice(73, "Arcade HID", rest)
        });
        try
        {
            if (!SDL3GamepadBackend.IsConnected(0) || SDL3GamepadBackend.GetDeviceName(0) != "Arcade HID")
                throw new Exception("Generic device was not enumerated.");
            var button = new XInputButton
            {
                XInputIndex = 0, IsButton = true, SdlControl = SdlControlKind.Button, SdlControlIndex = 31
            };
            SDL3GamepadBackend.UpdatePlatformButton(73, 31, true);
            if (DigitalHelper.GetButtonPressXinput(button, SDL3GamepadBackend.GetState(0), 0) != true ||
                DigitalHelper.GetButtonPressXinput(button, SDL3GamepadBackend.GetState(0), 1) != null)
                throw new Exception("Button 32 was not mapped to the correct device.");

            var axis = new XInputButton
            {
                XInputIndex = 0, SdlControl = SdlControlKind.Axis, SdlControlIndex = 17, SdlDirection = 1
            };
            SDL3GamepadBackend.UpdatePlatformAxis(73, 17, 27000);
            if (DigitalHelper.GetButtonPressXinput(axis, SDL3GamepadBackend.GetState(0), 0) != true)
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
            var jsonRestored = JsonConvert.DeserializeObject<XInputButton>(JsonConvert.SerializeObject(button));
            if (jsonRestored?.SdlControl != SdlControlKind.Button || jsonRestored.SdlControlIndex != 31)
                throw new Exception("Generic binding did not survive authoritative JSON serialization.");

            SDL3GamepadBackend.UpdatePlatformButton(73, 31, false);
            if (DigitalHelper.GetButtonPressXinput(button, SDL3GamepadBackend.GetState(0), 0) != false)
                throw new Exception("Button release was not observed.");
            var mapped = new XiGamepad
            {
                Buttons = AndroidGamepadMapping.Button(96) | AndroidGamepadMapping.Hat(0, -1),
                LeftThumbY = AndroidGamepadMapping.Stick(-1, -1, 1, invert: true),
                LeftTrigger = AndroidGamepadMapping.Trigger(1, 0, 1)
            };
            SDL3GamepadBackend.UpdatePlatformGamepad(73, mapped);
            var mappedState = SDL3GamepadBackend.GetState(0);
            if (mappedState.Gamepad.Buttons != (GamepadButtonFlags.A | GamepadButtonFlags.DPadUp) ||
                mappedState.Gamepad.LeftThumbY < 30000 || mappedState.Gamepad.LeftTrigger != 255 ||
                DigitalHelper.GetButtonPressXinput(new XInputButton
                {
                    XInputIndex = 0, IsButton = true, ButtonCode = (short)GamepadButtonFlags.A
                }, mappedState, 0) != true)
                throw new Exception("Android controller mapping did not reach shared XInput bindings.");
            if (AndroidGamepadMapping.Button(0) != GamepadButtonFlags.None ||
                AndroidGamepadMapping.Hat(1, 1) !=
                (GamepadButtonFlags.DPadRight | GamepadButtonFlags.DPadDown))
                throw new Exception("Android controller mapping produced an unexpected button.");
            Console.WriteLine("Generic joystick buttons, axes, hats and binding persistence: PASS");
            Console.WriteLine("Android controller button, stick, trigger and D-pad mapping: PASS");
            if (!OperatingSystem.IsAndroid()) RunVirtualSdlDevice();
            return 0;
        }
        finally
        {
            SDL3GamepadBackend.UpdatePlatformDevices(Array.Empty<SDL3GamepadBackend.PlatformGamepadDevice>());
        }
    }

    private static void RunVirtualSdlDevice()
    {
        SDL3GamepadBackend.Acquire();
        try
        {
            // An unmapped virtual joystick is the regression case: the old
            // mapped-gamepad gate skipped it completely.
            using var joystick = new Sdl3VirtualJoystick("Virtual Arcade HID", type: 0, axes: 8, buttons: 40, hats: 1);
            int slot = -1;
            for (int attempt = 0; attempt < 100 && slot < 0; attempt++)
            {
                for (int i = 0; i < SDL3GamepadBackend.MaxSlots; i++)
                    if (SDL3GamepadBackend.IsConnected(i) && SDL3GamepadBackend.GetRawState(i).Buttons.Length == 40)
                        slot = i;
                if (slot < 0) Thread.Sleep(20);
            }
            if (slot < 0) throw new Exception("SDL backend skipped an unmapped virtual joystick.");

            joystick.SetButton(31, true);
            var observed = false;
            for (int attempt = 0; attempt < 100 && !observed; attempt++)
            {
                observed = SDL3GamepadBackend.GetRawState(slot).Button(31);
                if (!observed) Thread.Sleep(20);
            }
            if (!observed) throw new Exception("SDL backend did not poll generic button 32.");
            Console.WriteLine("Unmapped SDL virtual joystick enumeration and button 32: PASS");
        }
        finally
        {
            SDL3GamepadBackend.Release();
        }
    }
}
