using System;
using System.Collections.Generic;
using System.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening.Gamepad;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// A captured input event ready to be assigned to a JoystickButtons entry.
/// XInput-shaped bindings (produced by SDL3 on desktop or native Android
/// controller events) are interpreted identically by the game listener.
/// </summary>
public sealed record CapturedBinding(string DisplayName, XInputButton? XInput);

/// <summary>
/// Polls the shared gamepad backend and captures mapped SDL controller input
/// or physical controls from generic SDL/Android joysticks.
/// </summary>
public sealed class InputCaptureService : IDisposable
{
    private readonly List<Thread> _threads = new();
    private volatile bool _stop = true;
    private bool _sdlAcquired;

    public event Action<CapturedBinding>? BindingCaptured;

    public IReadOnlyList<string> GetConnectedDevices()
    {
        var names = new List<string>();
        for (int slot = 0; slot < SDL3GamepadBackend.MaxSlots; slot++)
        {
            if (SDL3GamepadBackend.IsConnected(slot))
                names.Add($"Input Device {slot}: {SDL3GamepadBackend.GetDeviceName(slot) ?? "Joystick"}");
        }
        return names;
    }

    public void Start(InputApi api)
    {
        Stop();
        _stop = false;
        // Every gamepad API selection captures through the shared backend;
        // legacy DirectInput/XInput selections keep XInput-shaped bindings.
        SpawnSdl3Capture();
    }

    public void Stop()
    {
        _stop = true;
        foreach (var t in _threads)
            t.Join(1000);
        _threads.Clear();
        if (_sdlAcquired)
        {
            SDL3GamepadBackend.Release();
            _sdlAcquired = false;
        }
    }

    public void Dispose() => Stop();

    private void SpawnSdl3Capture()
    {
        SDL3GamepadBackend.Acquire();
        _sdlAcquired = true;

        var thread = new Thread(() =>
        {
            const int maxSlots = SDL3GamepadBackend.MaxSlots;
            var previous = new State[maxSlots];
            var previousRaw = new RawJoystickState[maxSlots];
            var wasConnected = new bool[maxSlots];
            for (int slot = 0; slot < maxSlots; slot++)
            {
                previous[slot] = SDL3GamepadBackend.GetState(slot);
                previousRaw[slot] = SDL3GamepadBackend.GetRawState(slot);
                wasConnected[slot] = SDL3GamepadBackend.IsConnected(slot);
            }

            while (!_stop)
            {
                for (int slot = 0; slot < maxSlots; slot++)
                {
                    if (!SDL3GamepadBackend.IsConnected(slot))
                    {
                        previous[slot] = default;
                        previousRaw[slot] = RawJoystickState.Empty;
                        wasConnected[slot] = false;
                        continue;
                    }
                    var state = SDL3GamepadBackend.GetState(slot);
                    var raw = SDL3GamepadBackend.GetRawState(slot);
                    if (!wasConnected[slot])
                    {
                        previous[slot] = state;
                        previousRaw[slot] = raw;
                        wasConnected[slot] = true;
                        continue;
                    }
                    if (state.PacketNumber != previous[slot].PacketNumber)
                    {
                        if (!DetectXInput(state, previous[slot], slot))
                            DetectRawJoystick(raw, previousRaw[slot], slot);
                    }
                    previous[slot] = state;
                    previousRaw[slot] = raw;
                }
                Thread.Sleep(10);
            }
        }) { IsBackground = true };
        thread.Start();
        _threads.Add(thread);
    }

    private bool DetectXInput(State ns, State os, int index)
    {
        var prefix = $"Input Device {index} ";

        if (ns.Gamepad.Buttons != os.Gamepad.Buttons && ns.Gamepad.Buttons != GamepadButtonFlags.None)
        {
            // Single-flag presses only, same as the classic UI
            foreach (GamepadButtonFlags flag in Enum.GetValues(typeof(GamepadButtonFlags)))
            {
                if (flag == GamepadButtonFlags.None || ns.Gamepad.Buttons != flag)
                    continue;
                Raise(prefix + flag, new XInputButton { IsButton = true, ButtonCode = (short)flag, XInputIndex = index });
                return true;
            }
        }

        if (GamepadAxisCapture.TrySelectDominantThumb(
                ns.Gamepad,
                os.Gamepad,
                index,
                out var thumbBinding,
                out var thumbName))
        {
            Raise(prefix + thumbName, thumbBinding);
            return true;
        }

        if (ns.Gamepad.LeftTrigger != os.Gamepad.LeftTrigger && ns.Gamepad.LeftTrigger > 30)
        {
            Raise(prefix + "LeftTrigger", new XInputButton { IsLeftTrigger = true, XInputIndex = index });
            return true;
        }
        if (ns.Gamepad.RightTrigger != os.Gamepad.RightTrigger && ns.Gamepad.RightTrigger > 30)
        {
            Raise(prefix + "RightTrigger", new XInputButton { IsRightTrigger = true, XInputIndex = index });
            return true;
        }
        return false;
    }

    private void DetectRawJoystick(RawJoystickState now, RawJoystickState before, int slot)
    {
        var prefix = $"Input Device {slot} ({SDL3GamepadBackend.GetDeviceName(slot) ?? "Joystick"}) ";
        for (int i = 0; i < now.Buttons.Length; i++)
        {
            if (!now.Button(i) || before.Button(i)) continue;
            Raise(prefix + $"Button {i + 1}", new XInputButton
            {
                XInputIndex = slot, IsButton = true,
                SdlControl = SdlControlKind.Button, SdlControlIndex = i
            });
            return;
        }
        for (int i = 0; i < now.Hats.Length; i++)
        {
            var added = now.Hat(i) & ~before.Hat(i);
            if (added == 0) continue;
            var direction = added & -added;
            Raise(prefix + $"Hat {i + 1} direction {direction}", new XInputButton
            {
                XInputIndex = slot, IsButton = true,
                SdlControl = SdlControlKind.Hat, SdlControlIndex = i, SdlDirection = direction
            });
            return;
        }
        for (int i = 0; i < now.Axes.Length; i++)
        {
            var value = now.Axis(i);
            if (Math.Abs(value - before.Axis(i)) < 12000 || Math.Abs((int)value) < 15000)
                continue;
            Raise(prefix + $"Axis {i + 1} {(value < 0 ? "-" : "+")}", new XInputButton
            {
                XInputIndex = slot, SdlControl = SdlControlKind.Axis,
                SdlControlIndex = i, SdlDirection = value < 0 ? -1 : 1
            });
            return;
        }
    }

    private void Raise(string name, XInputButton? xi)
    {
        TeknoParrotUi.Common.InputListening.Gamepad.SDL3GamepadBackend.Trace($"capture raised '{name}'");
        BindingCaptured?.Invoke(new CapturedBinding(name, xi));
    }
}
