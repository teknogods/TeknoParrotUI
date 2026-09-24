using System;
using System.Collections.Generic;
using System.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening.Gamepad;

namespace InputMethodAudit;

internal static class Sdl3ListenerLifecycleTest
{
    public static int Run()
    {
        SDL3GamepadBackend.InitializeOnMainThread();
        var profile = new GameProfile { ConfigValues = new List<FieldInformation>() };
        var button = new XInputButton
        {
            IsButton = true,
            SdlControl = SdlControlKind.Button,
            SdlControlIndex = 0
        };
        var bindings = new List<JoystickButtons>
        {
            new() { InputMapping = InputMapping.P1Button1, XInputButton = button }
        };
        var listener = new SDL3JoystickListener();
        try
        {
            // Start with no virtual device, then attach it: this verifies the
            // respawner notices hot-plugged generic joysticks.
            listener.Start(profile, bindings);
            using var joystick = new Sdl3VirtualJoystick("Virtual SDL3 Lifecycle", type: 0,
                axes: 2, buttons: 2, hats: 1);
            var slot = WaitForSlot("Virtual SDL3 Lifecycle");
            button.XInputIndex = slot;
            InputCode.PlayerDigitalButtons[0].Button1 = false;
            Thread.Sleep(400); // allow the listener's 250 ms slot scan
            joystick.SetButton(0, true);
            WaitForMappedPress("initial start");

            listener.Stop();
            InputCode.PlayerDigitalButtons[0].Button1 = false;
            joystick.SetButton(0, false);
            listener.Start(profile, bindings);
            WaitFor(() => SDL3GamepadBackend.IsConnected(slot) &&
                          !SDL3GamepadBackend.GetRawState(slot).Button(0),
                "virtual button release after restart");
            Thread.Sleep(400);
            joystick.SetButton(0, true);
            WaitForMappedPress("restart");

            Console.WriteLine("SDL3 listener hot-plug, mapped button, stop and restart: PASS");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: " + error.Message);
            return 1;
        }
        finally
        {
            listener.Stop();
            InputCode.PlayerDigitalButtons[0].Button1 = false;
        }
    }

    private static int WaitForSlot(string name)
    {
        var slot = -1;
        WaitFor(() =>
        {
            for (var index = 0; index < SDL3GamepadBackend.MaxSlots; index++)
            {
                if (SDL3GamepadBackend.IsConnected(index) &&
                    SDL3GamepadBackend.GetDeviceName(index) == name)
                {
                    slot = index;
                    return true;
                }
            }
            return false;
        }, "hot-plugged SDL3 joystick discovery");
        return slot;
    }

    private static void WaitForMappedPress(string phase) =>
        WaitFor(() => InputCode.PlayerDigitalButtons[0].Button1 == true,
            $"button mapping after {phase}");

    private static void WaitFor(Func<bool> condition, string description)
    {
        for (var attempt = 0; attempt < 150; attempt++)
        {
            if (condition())
                return;
            Thread.Sleep(20);
        }
        throw new InvalidOperationException("Timed out waiting for " + description + ".");
    }
}
