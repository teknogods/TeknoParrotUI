using System;
using System.Threading;
using TeknoParrotUi.Common.InputListening.Gamepad;

namespace InputMethodAudit
{
    /// <summary>Exercises Linux SDL loading, hot-plug and raw button delivery without hardware.</summary>
    internal static class Sdl3VirtualGamepadTest
    {
        public static int Run()
        {
            SDL3GamepadBackend.Acquire();
            Console.WriteLine(SDL3GamepadBackend.BackendStatus);
            try
            {
                using var joystick = new Sdl3VirtualJoystick("Virtual SDL3 Gamepad", type: 1, axes: 2, buttons: 2, hats: 1);

                int slot = -1;
                for (int i = 0; i < 100 && slot < 0; i++)
                {
                    for (int candidate = 0; candidate < SDL3GamepadBackend.MaxSlots; candidate++)
                        if (SDL3GamepadBackend.IsConnected(candidate) &&
                            (SDL3GamepadBackend.GetDeviceName(candidate) ?? "").Contains("Virtual"))
                            slot = candidate;
                    Thread.Sleep(10);
                }
                if (slot < 0)
                    throw new InvalidOperationException("SDL backend did not discover the virtual controller");

                joystick.SetButton(0, true);
                for (int i = 0; i < 100; i++)
                {
                    if (SDL3GamepadBackend.GetRawState(slot).Button(0))
                    {
                        Console.WriteLine($"PASS: virtual controller slot {slot} delivered button 0; mapped buttons: {SDL3GamepadBackend.GetState(slot).Gamepad.Buttons}");
                        return 0;
                    }
                    Thread.Sleep(10);
                }
                throw new InvalidOperationException("SDL backend did not deliver the virtual button press");
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("FAIL: " + error.Message);
                return 1;
            }
            finally
            {
                SDL3GamepadBackend.Release();
            }
        }
    }
}
