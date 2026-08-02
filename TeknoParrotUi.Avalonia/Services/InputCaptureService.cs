using System;
using System.Collections.Generic;
using System.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening.Gamepad;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// A captured input event ready to be assigned to a JoystickButtons entry.
/// XInput-shaped bindings (produced by the SDL2 backend) are interpreted
/// identically by the SDL2 game listener, so bindings survive unchanged.
/// </summary>
public sealed record CapturedBinding(string DisplayName, XInputButton? XInput);

/// <summary>
/// Polls SDL2 gamepads (the only gamepad backend on every platform) and
/// reports the first input event as an XInput-shaped binding.
/// </summary>
public sealed class InputCaptureService : IDisposable
{
    private readonly List<Thread> _threads = new();
    private volatile bool _stop = true;
    private bool _sdlAcquired;

    public event Action<CapturedBinding>? BindingCaptured;

    public void Start(InputApi api)
    {
        Stop();
        _stop = false;
        // Every gamepad API selection captures via SDL2 — legacy DirectInput/
        // XInput selections produce the same XInput-shaped bindings.
        SpawnSdl2Capture();
    }

    public void Stop()
    {
        _stop = true;
        foreach (var t in _threads)
            t.Join(1000);
        _threads.Clear();
        if (_sdlAcquired)
        {
            SDL2GamepadBackend.Release();
            _sdlAcquired = false;
        }
    }

    public void Dispose() => Stop();

    private void SpawnSdl2Capture()
    {
        SDL2GamepadBackend.Acquire();
        _sdlAcquired = true;

        var thread = new Thread(() =>
        {
            const int maxSlots = SDL2GamepadBackend.MaxSlots;
            var previous = new State[maxSlots];
            for (int slot = 0; slot < maxSlots; slot++)
                previous[slot] = SDL2GamepadBackend.GetState(slot);

            while (!_stop)
            {
                for (int slot = 0; slot < maxSlots; slot++)
                {
                    if (!SDL2GamepadBackend.IsConnected(slot))
                        continue;
                    var state = SDL2GamepadBackend.GetState(slot);
                    if (state.PacketNumber != previous[slot].PacketNumber)
                        DetectXInput(state, previous[slot], slot);
                    previous[slot] = state;
                }
                Thread.Sleep(10);
            }
        }) { IsBackground = true };
        thread.Start();
        _threads.Add(thread);
    }

    private void DetectXInput(State ns, State os, int index)
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
                return;
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
            return;
        }

        if (ns.Gamepad.LeftTrigger != os.Gamepad.LeftTrigger && ns.Gamepad.LeftTrigger > 30)
        {
            Raise(prefix + "LeftTrigger", new XInputButton { IsLeftTrigger = true, XInputIndex = index });
            return;
        }
        if (ns.Gamepad.RightTrigger != os.Gamepad.RightTrigger && ns.Gamepad.RightTrigger > 30)
        {
            Raise(prefix + "RightTrigger", new XInputButton { IsRightTrigger = true, XInputIndex = index });
        }
    }

    private void Raise(string name, XInputButton? xi)
    {
        TeknoParrotUi.Common.InputListening.Gamepad.SDL2GamepadBackend.Trace($"capture raised '{name}'");
        BindingCaptured?.Invoke(new CapturedBinding(name, xi));
    }
}
