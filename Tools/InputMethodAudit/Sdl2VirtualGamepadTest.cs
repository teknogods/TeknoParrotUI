using System;
using System.Threading;
using SDL2;
using TeknoParrotUi.Common.InputListening.Gamepad;

namespace InputMethodAudit
{
    /// <summary>Exercises Linux SDL loading, hot-plug and raw button delivery without hardware.</summary>
    internal static class Sdl2VirtualGamepadTest
    {
        public static int Run()
        {
            SDL2GamepadBackend.Acquire();
            Console.WriteLine(SDL2GamepadBackend.BackendStatus);
            int deviceIndex = -1;
            IntPtr joystick = IntPtr.Zero;
            try
            {
                deviceIndex = SDL.SDL_JoystickAttachVirtual(
                    (int)SDL.SDL_JoystickType.SDL_JOYSTICK_TYPE_GAMECONTROLLER, 2, 2, 1);
                if (deviceIndex < 0)
                    throw new InvalidOperationException("Virtual joystick attach failed: " + SDL.SDL_GetError());
                joystick = SDL.SDL_JoystickOpen(deviceIndex);
                if (joystick == IntPtr.Zero)
                    throw new InvalidOperationException("Virtual joystick open failed: " + SDL.SDL_GetError());

                int slot = -1;
                for (int i = 0; i < 100 && slot < 0; i++)
                {
                    for (int candidate = 0; candidate < SDL2GamepadBackend.MaxSlots; candidate++)
                        if (SDL2GamepadBackend.IsConnected(candidate) &&
                            (SDL2GamepadBackend.GetDeviceName(candidate) ?? "").Contains("Virtual"))
                            slot = candidate;
                    Thread.Sleep(10);
                }
                if (slot < 0)
                    throw new InvalidOperationException("SDL backend did not discover the virtual controller");

                SDL.SDL_JoystickSetVirtualButton(joystick, 0, 1);
                for (int i = 0; i < 100; i++)
                {
                    if (SDL2GamepadBackend.GetRawState(slot).Button(0))
                    {
                        Console.WriteLine($"PASS: virtual controller slot {slot} delivered button 0; mapped buttons: {SDL2GamepadBackend.GetState(slot).Gamepad.Buttons}");
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
                if (joystick != IntPtr.Zero) SDL.SDL_JoystickClose(joystick);
                if (deviceIndex >= 0) SDL.SDL_JoystickDetachVirtual(deviceIndex);
                SDL2GamepadBackend.Release();
            }
        }
    }
}
