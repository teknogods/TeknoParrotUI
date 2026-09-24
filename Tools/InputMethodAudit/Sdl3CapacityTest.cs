using System;
using System.Collections.Generic;
using System.Threading;
using TeknoParrotUi.Common.InputListening.Gamepad;

namespace InputMethodAudit;

internal static class Sdl3CapacityTest
{
    public static int Run()
    {
        if (SDL3GamepadBackend.MaxSlots < 9)
            throw new InvalidOperationException("SDL3 backend cannot expose a ninth device.");

        var devices = new List<Sdl3VirtualJoystick>();
        SDL3GamepadBackend.Acquire();
        try
        {
            var existing = 0;
            for (var slot = 0; slot < SDL3GamepadBackend.MaxSlots; slot++)
                if (SDL3GamepadBackend.IsConnected(slot)) existing++;
            if (SDL3GamepadBackend.MaxSlots - existing < 9)
            {
                Console.WriteLine("SKIP: fewer than nine free slots because physical controllers are connected.");
                return 0;
            }

            for (var index = 0; index < 9; index++)
                devices.Add(new Sdl3VirtualJoystick($"Virtual SDL3 Capacity {index}",
                    type: 0, axes: 1, buttons: 1, hats: 0));

            var highSlot = -1;
            for (var attempt = 0; attempt < 150; attempt++)
            {
                var found = new HashSet<string>(StringComparer.Ordinal);
                highSlot = -1;
                for (var slot = 0; slot < SDL3GamepadBackend.MaxSlots; slot++)
                {
                    var name = SDL3GamepadBackend.GetDeviceName(slot);
                    if (SDL3GamepadBackend.IsConnected(slot) &&
                        name?.StartsWith("Virtual SDL3 Capacity ", StringComparison.Ordinal) == true)
                    {
                        found.Add(name);
                        if (slot >= 8) highSlot = slot;
                    }
                }
                if (found.Count == 9 && highSlot >= 8)
                {
                    Console.WriteLine($"SDL3 backend enumerated nine virtual devices, including slot {highSlot}: PASS");
                    return 0;
                }
                Thread.Sleep(20);
            }
            throw new InvalidOperationException("SDL3 backend did not expose all nine virtual devices.");
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: " + error.Message);
            return 1;
        }
        finally
        {
            for (var index = devices.Count - 1; index >= 0; index--)
                devices[index].Dispose();
            SDL3GamepadBackend.Release();
        }
    }
}
