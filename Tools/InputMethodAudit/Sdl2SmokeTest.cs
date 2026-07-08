using System;
using System.Threading;
using TeknoParrotUi.Common.InputListening.Gamepad;

namespace InputMethodAudit
{
    /// <summary>
    /// Manual verification for the SDL2 gamepad backend (Phase 1 testing).
    /// Prints connected pads and live XInput-shaped state for 15 seconds.
    /// Usage: dotnet run --project Tools/InputMethodAudit -- sdl2-test
    /// </summary>
    internal static class Sdl2SmokeTest
    {
        public static int Run()
        {
            Console.WriteLine("SDL2 gamepad smoke test — press buttons/move sticks; Ctrl+C to quit.");
            SDL2GamepadBackend.Acquire();
            try
            {
                var end = DateTime.UtcNow.AddSeconds(15);
                while (DateTime.UtcNow < end)
                {
                    for (int slot = 0; slot < SDL2GamepadBackend.MaxSlots; slot++)
                    {
                        if (!SDL2GamepadBackend.IsConnected(slot))
                            continue;
                        var s = SDL2GamepadBackend.GetState(slot);
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
                SDL2GamepadBackend.Release();
            }
            Console.WriteLine("Done.");
            return 0;
        }
    }
}
