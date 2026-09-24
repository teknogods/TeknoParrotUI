using System;
using System.Runtime.InteropServices;

namespace TeknoParrotUi.Common.InputListening.Gamepad
{
    /// <summary>
    /// The small SDL3 ABI surface used by TPUI. SDL3's native bool is one byte,
    /// and strings returned by SDL are borrowed UTF-8 pointers.
    /// </summary>
    internal static class SDL3Native
    {
        internal const uint InitGamepad = 0x00002000;
        internal const string JoystickAllowBackgroundEvents = "SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS";

        internal enum GamepadButton
        {
            South = 0,
            East = 1,
            West = 2,
            North = 3,
            Back = 4,
            Guide = 5,
            Start = 6,
            LeftStick = 7,
            RightStick = 8,
            LeftShoulder = 9,
            RightShoulder = 10,
            DPadUp = 11,
            DPadDown = 12,
            DPadLeft = 13,
            DPadRight = 14
        }

        internal enum GamepadAxis
        {
            LeftX = 0,
            LeftY = 1,
            RightX = 2,
            RightY = 3,
            LeftTrigger = 4,
            RightTrigger = 5
        }

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte SDL_SetHint(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte SDL_InitSubSystem(uint flags);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SDL_GetVersion();

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_GetError();

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SDL_UpdateGamepads();

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SDL_UpdateJoysticks();

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr SDL_GetJoysticks(out int count);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SDL_free(IntPtr memory);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte SDL_IsGamepad(uint instanceId);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr SDL_OpenGamepad(uint instanceId);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SDL_CloseGamepad(IntPtr gamepad);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr SDL_GetGamepadJoystick(IntPtr gamepad);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte SDL_GetGamepadButton(IntPtr gamepad, GamepadButton button);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern short SDL_GetGamepadAxis(IntPtr gamepad, GamepadAxis axis);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr SDL_OpenJoystick(uint instanceId);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SDL_CloseJoystick(IntPtr joystick);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte SDL_JoystickConnected(IntPtr joystick);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_GetJoystickNameForID(uint instanceId);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SDL_GetNumJoystickButtons(IntPtr joystick);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SDL_GetNumJoystickAxes(IntPtr joystick);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SDL_GetNumJoystickHats(IntPtr joystick);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte SDL_GetJoystickButton(IntPtr joystick, int button);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern short SDL_GetJoystickAxis(IntPtr joystick, int axis);

        [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl)]
        internal static extern byte SDL_GetJoystickHat(IntPtr joystick, int hat);

        internal static string GetError() => Marshal.PtrToStringUTF8(SDL_GetError()) ?? "Unknown SDL3 error";

        internal static string GetJoystickNameForID(uint instanceId) =>
            Marshal.PtrToStringUTF8(SDL_GetJoystickNameForID(instanceId)) ?? "Joystick";
    }
}
