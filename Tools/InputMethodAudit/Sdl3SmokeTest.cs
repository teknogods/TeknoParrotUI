using System;
using System.Threading;
using TeknoParrotUi.Common.InputListening.Gamepad;

namespace InputMethodAudit
{
    /// <summary>
    /// Manual verification for the SDL3 gamepad backend (Phase 1 testing).
    /// Prints connected pads and live XInput-shaped state for 15 seconds.
    /// Usage: dotnet run --project Tools/InputMethodAudit -- sdl3-test
    /// </summary>
    internal static class Sdl3SmokeTest
    {
        public static int Run()
        {
            Console.WriteLine("SDL3 gamepad smoke test — press buttons/move sticks; Ctrl+C to quit.");
            SDL3GamepadBackend.Acquire();
            Console.WriteLine(SDL3GamepadBackend.BackendStatus);
            try
            {
                var end = DateTime.UtcNow.AddSeconds(15);
                while (DateTime.UtcNow < end)
                {
                    for (int slot = 0; slot < SDL3GamepadBackend.MaxSlots; slot++)
                    {
                        if (!SDL3GamepadBackend.IsConnected(slot))
                            continue;
                        var s = SDL3GamepadBackend.GetState(slot);
                        Console.WriteLine(
                            $"[{slot}] pkt={s.PacketNumber} btn={s.Gamepad.Buttons} " +
                            $"LX={s.Gamepad.LeftThumbX} LY={s.Gamepad.LeftThumbY} " +
                            $"RX={s.Gamepad.RightThumbX} RY={s.Gamepad.RightThumbY} " +
                            $"LT={s.Gamepad.LeftTrigger} RT={s.Gamepad.RightTrigger}");
                    }
                    Thread.Sleep(500);
                }
            }
            finally
            {
                SDL3GamepadBackend.Release();
            }
            Console.WriteLine("Done.");
            return 0;
        }
    }
}
