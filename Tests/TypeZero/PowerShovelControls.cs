using System;
using System.Linq;
using System.Reflection;
using SharpDX.DirectInput;
using SharpDX.XInput;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening;
using TeknoParrotUi.Common.Jvs;
using TeknoParrotUi.Common.Pipes;

internal static class PowerShovelControls
{
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;
    private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }

    internal static void MakeLegacy(GameProfile profile)
    {
        profile.GameProfileRevision--;
        foreach (var button in profile.JoystickButtons.Where(b => b.HideWithKeyboardForAxis || b.HideWithoutKeyboardForAxis))
        {
            var channel = (int)button.InputMapping - (int)InputMapping.Analog0;
            button.BindNameDi = "Saved DI " + button.ButtonName;
            button.BindNameXi = "Saved XI " + button.ButtonName;
            button.DirectInputButton = new JoystickButton { Button = channel, IsAxis = !button.HideWithoutKeyboardForAxis };
            button.XInputButton = new XInputButton { XInputIndex = channel / 8, IsRightThumbX = channel % 8 == 4 };
            button.ButtonName = TypeZeroControls.PreviousControlName(profile, button.ButtonName);
            button.AnalogType = channel % 4 == 0 ? AnalogType.AnalogJoystick : AnalogType.AnalogJoystickReverse;
        }
    }

    internal static void CheckMigrated(GameProfile profile)
    {
        foreach (var button in profile.JoystickButtons.Where(b => b.HideWithKeyboardForAxis || b.HideWithoutKeyboardForAxis))
        {
            var channel = (int)button.InputMapping - (int)InputMapping.Analog0;
            Require(!button.ButtonName.Contains("Joystick"), "Legacy Power Shovel label survived migration");
            Require(button.BindNameDi == "Saved DI " + button.ButtonName && button.BindNameXi == "Saved XI " + button.ButtonName,
                "Renamed Power Shovel bindings were lost");
            Require(button.DirectInputButton.Button == channel && button.XInputButton.XInputIndex == channel / 8,
                "Migration reassigned a lever to another channel or controller");
            Require(button.HideWithoutKeyboardForAxis || button.AnalogType == AnalogType.AnalogJoystickReverse,
                "Old X polarity survived migration");
        }
    }

    internal static void Check(GameProfile source)
    {
        var profile = source.Clone();
        var expected = new[] { "Player 1 Left X", "Player 1 Left Y", "Player 1 Right X", "Player 1 Right Y",
            "Player 2 Left X", "Player 2 Left Y", "Player 2 Right X", "Player 2 Right Y" };
        for (var axis = 0; axis < expected.Length; ++axis)
            Require((int)profile.JoystickButtons.Single(b => b.ButtonName == expected[axis]).InputMapping -
                (int)InputMapping.Analog0 == axis * 2, "Power Shovel lever/ADC mapping changed");

        var types = new[] { typeof(InputListenerDirectInput), typeof(InputListenerXInput) };
        var fields = types.SelectMany(type => new[] { "_gameProfile", "GunGame", "ReverseYAxis" }.Select(name => type.GetField(name, Static)))
            .Concat(new[] { types[0].GetField("KeyboardorButtonAxis", Static), types[0].GetField("_keyboardAxisStep", Static),
                types[0].GetField("KillMe", BindingFlags.Public | BindingFlags.Static) }).ToArray();
        var originalFields = fields.Select(field => field.GetValue(null)).ToArray();
        var arrays = new[] { "_keyboardAxisEnabled", "_keyboardAxisNegative", "_keyboardAxisPositive", "_keyboardAxisRest" }
            .Select(name => (Array)types[0].GetField(name, Static).GetValue(null)).ToArray();
        var originalArrays = arrays.Select(array => (Array)array.Clone()).ToArray();
        var analog = (byte[])InputCode.AnalogBytes.Clone();
        var di = new InputListenerDirectInput(); var xi = new InputListenerXInput();
        var convertDi = types[0].GetMethod("ModifyAnalog", Instance); var convertXi = types[1].GetMethod("ModifyAnalog", Instance);
        try
        {
            foreach (var type in types)
            {
                type.GetField("_gameProfile", Static).SetValue(null, profile);
                type.GetField("GunGame", Static).SetValue(null, false);
                type.GetField("ReverseYAxis", Static).SetValue(null, false);
            }
            types[0].GetField("KeyboardorButtonAxis", Static).SetValue(null, false);
            var pipe = new TeknoTZeroPipe();
            typeof(TeknoTZeroPipe).GetField("_set", Instance).SetValue(pipe, "pwrshovl_tz");
            typeof(TeknoTZeroPipe).GetField("_publishInput", Instance).SetValue(pipe, true);
            foreach (var button in profile.JoystickButtons.Where(b => b.HideWithKeyboardForAxis))
            {
                var channel = (int)button.InputMapping - (int)InputMapping.Analog0;
                bool xAxis = channel % 4 == 0;
                button.DirectInputButton = new JoystickButton { IsAxis = true, Button = (int)JoystickOffset.X };
                button.XInputButton = new XInputButton { IsLeftThumbX = xAxis, IsLeftThumbY = !xAxis };
                foreach (var raw in new[] { 0, 16384, 32768, 49152, 65535 })
                {
                    var diState = new JoystickUpdate { RawOffset = (int)JoystickOffset.X, Value = raw };
                    short thumb = (short)(raw - 32768);
                    var xiState = new State { Gamepad = new Gamepad { LeftThumbX = xAxis ? thumb : (short)0, LeftThumbY = xAxis ? (short)0 : thumb } };
                    byte currentDi = (byte)convertDi.Invoke(di, new object[] { button, diState });
                    byte currentXi = (byte)convertXi.Invoke(xi, new object[] { button, xiState, 0, 0 });
                    var newType = button.AnalogType;
                    button.AnalogType = xAxis ? AnalogType.AnalogJoystick : AnalogType.AnalogJoystickReverse;
                    byte oldDi = (byte)convertDi.Invoke(di, new object[] { button, diState });
                    byte oldXi = (byte)convertXi.Invoke(xi, new object[] { button, xiState, 0, 0 });
                    button.AnalogType = newType;
                    Require(currentDi == (xAxis ? (byte)~oldDi : oldDi) && currentXi == (xAxis ? (byte)~oldXi : oldXi),
                        "X must reverse and Y must stay unchanged: " + button.ButtonName);
                    foreach (var value in new[] { currentDi, currentXi })
                    {
                        for (var index = 0; index < InputCode.AnalogBytes.Length; ++index) InputCode.AnalogBytes[index] = 128;
                        InputCode.AnalogBytes[channel] = value; pipe.Transmit();
                        for (var axis = 0; axis < 8; ++axis)
                            Require(JvsHelper.StateView.ReadByte(20 + axis) == (axis == channel / 2 ? value : 128), "Lever crossed TTZIN channels");
                    }
                }
            }

            types[0].GetField("KeyboardorButtonAxis", Static).SetValue(null, true);
            types[0].GetField("KillMe", BindingFlags.Public | BindingFlags.Static).SetValue(null, false);
            types[0].GetField("_keyboardAxisStep", Static).SetValue(null, 17);
            Require((bool)types[0].GetProperty("UsesIndependentKeyboardAxes", Static).GetValue(null), "Power Shovel must use independent keyboard axes");
            foreach (var array in arrays) Array.Clear(array, 0, array.Length);
            for (var axis = 0; axis < 16; axis += 2)
            {
                arrays[0].SetValue(true, axis); arrays[3].SetValue((byte)128, axis); InputCode.AnalogBytes[axis] = 128;
            }
            foreach (var button in profile.JoystickButtons.Where(b => b.HideWithoutKeyboardForAxis))
            {
                var channel = (int)button.InputMapping - (int)InputMapping.Analog0;
                bool maximum = button.ButtonName.EndsWith("(Left)") || button.ButtonName.EndsWith("(Down)");
                Require(button.AnalogType == (maximum ? AnalogType.Maximum : AnalogType.Minimum), "Incorrect keyboard lever polarity");
                button.DirectInputButton = new JoystickButton { Button = (int)JoystickOffset.Buttons0 };
                convertDi.Invoke(di, new object[] { button, new JoystickUpdate { RawOffset = (int)JoystickOffset.Buttons0, Value = 128 } });
                types[0].GetMethod("ListenKeyboardButton", Instance).Invoke(di, new object[] { null, null });
                for (var axis = 0; axis < 16; axis += 2)
                    Require(InputCode.AnalogBytes[axis] == (axis == channel ? (maximum ? 145 : 111) : 128), "Keyboard levers coupled or reversed");
                convertDi.Invoke(di, new object[] { button, new JoystickUpdate { RawOffset = (int)JoystickOffset.Buttons0, Value = 0 } });
                types[0].GetMethod("ListenKeyboardButton", Instance).Invoke(di, new object[] { null, null });
                Require(InputCode.AnalogBytes[channel] == 128, "Keyboard lever did not recenter");
            }
            Console.WriteLine("Passed Power Shovel DirectInput/XInput X reversal, unchanged Y, all eight TTZIN channels and 16 independent keyboard directions.");
        }
        finally
        {
            for (var index = 0; index < fields.Length; ++index) fields[index].SetValue(null, originalFields[index]);
            for (var index = 0; index < arrays.Length; ++index) Array.Copy(originalArrays[index], arrays[index], arrays[index].Length);
            Array.Copy(analog, InputCode.AnalogBytes, analog.Length);
        }
    }
}
