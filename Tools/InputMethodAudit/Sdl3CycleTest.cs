using System;
using System.Threading;
using TeknoParrotUi.Common.InputListening.Gamepad;

namespace InputMethodAudit
{
    /// <summary>
    /// Reproduces the Controller Setup enter/leave lifecycle: Acquire the SDL3
    /// backend, verify the pad is detected, Release (polling stops), then
    /// Acquire again and check whether the pad is still detected.
    /// Usage: dotnet run --project Tools/InputMethodAudit -- sdl3-cycle-test
    /// </summary>
    internal static class Sdl3CycleTest
    {
        public static int Run()
        {
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                SDL3GamepadBackend.Acquire();
                // give the poll thread time to attach devices
                bool connected = false;
                for (int i = 0; i < 30 && !connected; i++)
                {
                    Thread.Sleep(100);
                    for (int slot = 0; slot < SDL3GamepadBackend.MaxSlots; slot++)
                        connected |= SDL3GamepadBackend.IsConnected(slot);
                }
                Console.WriteLine($"Cycle {cycle}: pad detected = {connected}");
                SDL3GamepadBackend.Release();
                Thread.Sleep(300);
            }
            Console.WriteLine("Done.");
            return 0;
        }
    }
}
