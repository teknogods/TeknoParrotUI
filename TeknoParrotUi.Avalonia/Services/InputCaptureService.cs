using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SharpDX.DirectInput;
using SharpDX.XInput;
using TeknoParrotUi.Common;
using DeviceType = SharpDX.DirectInput.DeviceType;

namespace TeknoParrotUi.Avalonia.Services;

/// <summary>
/// A captured input event ready to be assigned to a JoystickButtons entry.
/// Field semantics match the classic WPF binding helpers exactly, so bindings
/// saved here are interpreted identically by InputListenerXInput/DirectInput.
/// </summary>
public sealed record CapturedBinding(string DisplayName, XInputButton? XInput, JoystickButton? DirectInput);

/// <summary>
/// Polls XInput and/or DirectInput devices and reports the first input events.
/// </summary>
public sealed class InputCaptureService : IDisposable
{
    private readonly List<Thread> _threads = new();
    private readonly List<Joystick> _joysticks = new();
    private volatile bool _stop = true;

    public event Action<CapturedBinding>? BindingCaptured;

    public void Start(InputApi api)
    {
        Stop();
        _stop = false;

        if (api is InputApi.XInput or InputApi.MergedInput)
        {
            foreach (var index in new[] { UserIndex.One, UserIndex.Two, UserIndex.Three, UserIndex.Four })
                SpawnXInput(index);
        }

        if (api is InputApi.DirectInput or InputApi.MergedInput)
        {
            SpawnDirectInput();
        }
    }

    public void Stop()
    {
        _stop = true;
        foreach (var t in _threads)
            t.Join(1000);
        _threads.Clear();
        foreach (var j in _joysticks)
        {
            try { j.Unacquire(); } catch { }
            try { j.Dispose(); } catch { }
        }
        _joysticks.Clear();
    }

    public void Dispose() => Stop();

    // ---------- XInput ----------

    private void SpawnXInput(UserIndex index)
    {
        var controller = new Controller(index);
        if (!controller.IsConnected)
            return;

        var thread = new Thread(() =>
        {
            try
            {
                var previous = controller.GetState();
                while (!_stop)
                {
                    var state = controller.GetState();
                    if (previous.PacketNumber != state.PacketNumber)
                        DetectXInput(state, previous, (int)index);
                    previous = state;
                    Thread.Sleep(10);
                }
            }
            catch
            {
                // controller unplugged mid-capture
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
                Raise(prefix + flag, new XInputButton { IsButton = true, ButtonCode = (short)flag, XInputIndex = index }, null);
                return;
            }
        }

        if (DetectThumb(ns.Gamepad.LeftThumbX, os.Gamepad.LeftThumbX, Gamepad.LeftThumbDeadZone, index, prefix, isLeft: true, isY: false)) return;
        if (DetectThumb(ns.Gamepad.RightThumbX, os.Gamepad.RightThumbX, Gamepad.RightThumbDeadZone, index, prefix, isLeft: false, isY: false)) return;
        if (DetectThumb(ns.Gamepad.LeftThumbY, os.Gamepad.LeftThumbY, Gamepad.LeftThumbDeadZone, index, prefix, isLeft: true, isY: true)) return;
        if (DetectThumb(ns.Gamepad.RightThumbY, os.Gamepad.RightThumbY, Gamepad.RightThumbDeadZone, index, prefix, isLeft: false, isY: true)) return;

        if (ns.Gamepad.LeftTrigger != os.Gamepad.LeftTrigger && ns.Gamepad.LeftTrigger > 30)
        {
            Raise(prefix + "LeftTrigger", new XInputButton { IsLeftTrigger = true, XInputIndex = index }, null);
            return;
        }
        if (ns.Gamepad.RightTrigger != os.Gamepad.RightTrigger && ns.Gamepad.RightTrigger > 30)
        {
            Raise(prefix + "RightTrigger", new XInputButton { IsRightTrigger = true, XInputIndex = index }, null);
        }
    }

    private bool DetectThumb(short value, short old, short deadZone, int index, string prefix, bool isLeft, bool isY)
    {
        if (value == old || Math.Abs((int)value) <= deadZone)
            return false;

        var button = new XInputButton { IsButton = false, XInputIndex = index, IsAxisMinus = value < 0 };
        if (isY)
        {
            if (isLeft) button.IsLeftThumbY = true; else button.IsRightThumbY = true;
        }
        else
        {
            if (isLeft) button.IsLeftThumbX = true; else button.IsRightThumbX = true;
        }
        var name = prefix + (isLeft ? "LeftThumb" : "RightThumb") + (isY ? "Y" : "X") + (value < 0 ? "-" : "+");
        Raise(name, button, null);
        return true;
    }

    // ---------- DirectInput ----------

    private void SpawnDirectInput()
    {
        var directInput = new DirectInput();
        var devices = directInput.GetDevices()
            .Where(d => d.Type != DeviceType.Mouse && d.Type != DeviceType.Keyboard)
            .ToList();

        foreach (var device in devices)
        {
            Joystick joystick;
            try
            {
                joystick = new Joystick(directInput, device.InstanceGuid);
                joystick.Properties.BufferSize = 512;
                joystick.Acquire();
            }
            catch
            {
                continue;
            }
            _joysticks.Add(joystick);

            var thread = new Thread(() =>
            {
                while (!_stop)
                {
                    try
                    {
                        joystick.Poll();
                        foreach (var update in joystick.GetBufferedData())
                            DetectDirectInput(update, device);
                    }
                    catch
                    {
                        // device lost
                    }
                    Thread.Sleep(10);
                }
            }) { IsBackground = true };
            thread.Start();
            _threads.Add(thread);
        }
    }

    private void DetectDirectInput(JoystickUpdate key, DeviceInstance device)
    {
        JoystickButton? button = null;
        string inputText = "";

        if (key.Offset is JoystickOffset.PointOfViewControllers0 or JoystickOffset.PointOfViewControllers1
            or JoystickOffset.PointOfViewControllers2 or JoystickOffset.PointOfViewControllers3)
        {
            if (key.Value != -1)
            {
                inputText = key.Value switch
                {
                    0 => key.Offset + " Up",
                    9000 => key.Offset + " Right",
                    18000 => key.Offset + " Down",
                    27000 => key.Offset + " Left",
                    _ => ""
                };
                if (inputText != "")
                    button = new JoystickButton { Button = (int)key.Offset, IsAxis = false, PovDirection = key.Value, JoystickGuid = device.InstanceGuid };
            }
        }
        else if (key.Offset is JoystickOffset.X or JoystickOffset.Y or JoystickOffset.Z
                 or JoystickOffset.RotationX or JoystickOffset.RotationY or JoystickOffset.RotationZ
                 or JoystickOffset.Sliders0 or JoystickOffset.Sliders1
                 or JoystickOffset.AccelerationX or JoystickOffset.AccelerationY or JoystickOffset.AccelerationZ)
        {
            if (key.Value > short.MaxValue + 15000)
            {
                inputText = key.Offset + " +";
                button = new JoystickButton { Button = (int)key.Offset, IsAxis = true, IsAxisMinus = false, JoystickGuid = device.InstanceGuid };
            }
            else if (key.Value < short.MaxValue - 15000)
            {
                inputText = key.Offset + " -";
                button = new JoystickButton { Button = (int)key.Offset, IsAxis = true, IsAxisMinus = true, JoystickGuid = device.InstanceGuid };
            }
        }
        else if (key.Value == 128)
        {
            inputText = key.Offset.ToString();
            button = new JoystickButton { Button = (int)key.Offset, IsAxis = false, JoystickGuid = device.InstanceGuid };
        }

        if (button != null)
            Raise(device.Type + " " + inputText, null, button);
    }

    private void Raise(string name, XInputButton? xi, JoystickButton? di) =>
        BindingCaptured?.Invoke(new CapturedBinding(name, xi, di));
}
