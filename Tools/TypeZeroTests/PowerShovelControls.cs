using System;
using System.Linq;
using System.Reflection;

using TeknoParrotUi.Common.InputListening.Gamepad;
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
        var mapper = new InputListenerXInput();
        var profileField = typeof(InputListenerXInput).GetField("_gameProfile", Static);
        var previous = profileField.GetValue(null);
        profileField.SetValue(null, profile);
        var convert = typeof(InputListenerXInput).GetMethod("ModifyAnalog", Instance);
        try
        {
            foreach (var button in profile.JoystickButtons.Where(row => row.HideWithKeyboardForAxis))
            {
                var channel = (int)button.InputMapping - (int)InputMapping.Analog0;
                var horizontal = channel % 4 == 0;
                button.XInputButton = new XInputButton { IsLeftThumbX = horizontal, IsLeftThumbY = !horizontal };
                foreach (short raw in new short[] { short.MinValue, -16384, 0, 16384, short.MaxValue })
                {
                    var state = new State { Gamepad = new XiGamepad { LeftThumbX = horizontal ? raw : (short)0, LeftThumbY = horizontal ? (short)0 : raw } };
                    var value = (byte)convert.Invoke(mapper, new object[] { button, state, 0, 0 });
                    var currentType = button.AnalogType;
                    button.AnalogType = horizontal ? AnalogType.AnalogJoystick : AnalogType.AnalogJoystickReverse;
                    var oldValue = (byte)convert.Invoke(mapper, new object[] { button, state, 0, 0 });
                    button.AnalogType = currentType;
                    Require(value == (horizontal ? (byte)~oldValue : oldValue), "X must reverse while Y remains unchanged");
                }
            }
            profile.ConfigValues.First(field => field.FieldName == "Use Keyboard/Button For Axis").FieldValue = "1";
            profile.ConfigValues.First(field => field.FieldName == "Keyboard/Button Axis X/Y Sensitivity").FieldValue = "17";
            foreach (var button in profile.JoystickButtons.Where(row => row.HideWithoutKeyboardForAxis))
            {
                var engine = new KeyboardAxisEngine(); engine.Initialize(profile);
                for (var axis = 0; axis < 16; axis += 2) InputCode.AnalogBytes[axis] = 128;
                var channel = (int)button.InputMapping - (int)InputMapping.Analog0;
                var maximum = button.AnalogType == AnalogType.Maximum;
                Require(engine.HandleButton(button, true), "Keyboard axis binding ignored"); engine.Tick();
                for (var axis = 0; axis < 16; axis += 2)
                    Require(InputCode.AnalogBytes[axis] == (axis == channel ? (maximum ? 145 : 111) : 128), "Keyboard lever coupling/polarity");
                engine.HandleButton(button, false); engine.Tick();
                Require(InputCode.AnalogBytes[channel] == 128, "Keyboard lever did not recenter");
            }
            Console.WriteLine("Power Shovel: SDL3 lever axes and all 16 independent keyboard directions passed.");
        }
        finally { profileField.SetValue(null, previous); }
    }
}
