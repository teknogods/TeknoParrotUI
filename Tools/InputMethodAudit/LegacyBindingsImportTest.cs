using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening.Gamepad;
using TeknoParrotUi.Common.InputListening.ProfileStorage;

namespace InputMethodAudit;

internal static class LegacyBindingsImportTest
{
    public static int Run()
    {
        var original = Directory.GetCurrentDirectory();
        var root = Path.Combine(Path.GetTempPath(), "tpui-legacy-binding-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "old", "UserProfiles"));
        Directory.CreateDirectory(Path.Combine(root, "new"));
        try
        {
            var guid = Guid.NewGuid();
            var old = new XElement("GameProfile", new XElement("JoystickButtons",
                Row("Start", "P1ButtonStart", new XElement("XInputButton",
                    new XElement("IsButton", "true"), new XElement("ButtonCode", "4096"), new XElement("XInputIndex", "0")),
                    new XElement("BindNameXi", "XInput Start")),
                Row("Button2", "P1Button2", new XElement("XInputButton",
                    new XElement("IsButton", "true"), new XElement("ButtonCode", "4096"), new XElement("XInputIndex", "0"))),
                Row("Button1", "P1Button1", new XElement("DirectInputButton",
                    new XElement("Button", "80"), new XElement("JoystickGuid", guid)),
                    new XElement("BindNameDi", "Joystick Button32")),
                Row("Up", "P1ButtonUp", new XElement("DirectInputButton",
                    new XElement("Button", "32"), new XElement("PovDirection", "0"), new XElement("JoystickGuid", guid))),
                Row("Gas", "Analog0", new XElement("DirectInputButton",
                    new XElement("Button", "8"), new XElement("IsAxis", "true"),
                    new XElement("IsAxisMinus", "true"), new XElement("JoystickGuid", guid))),
                Row("Coin", "Coin1", new XElement("RawInputButton",
                    new XElement("DevicePath", "Windows Mouse Cursor"),
                    new XElement("DeviceType", "Mouse"), new XElement("MouseButton", "LeftButton"),
                    new XElement("KeyboardKey", "None"))),
                Row("Test", "Test", new XElement("RawInputButton",
                    new XElement("DevicePath", "legacy-keyboard-path"),
                    new XElement("DeviceType", "Keyboard"), new XElement("MouseButton", "None"),
                    new XElement("KeyboardKey", "A")))));
            old.Save(Path.Combine(root, "old", "UserProfiles", "TestGame.xml"));
            Directory.SetCurrentDirectory(Path.Combine(root, "new"));
            var target = Profile();
            target.JoystickButtons.Single(b => b.ButtonName == "Button2").XInputButton =
                new XInputButton { IsButton = true, ButtonCode = 8192, XInputIndex = 1 };
            var preview = LegacyBindingsImporter.Scan(Path.Combine(root, "old"));
            if (preview.ProfileCount != 1 || preview.DirectInputDevices.Single() != guid)
                throw new Exception("Legacy files or DirectInput GUID were not found.");
            var result = LegacyBindingsImporter.Import(preview, new[] { target }, new Dictionary<Guid, int> { [guid] = 2 });
            if (result.ProfilesSaved != 1 || result.DirectInputBindings != 3 ||
                result.GamepadBindings != 1 ||
                target.JoystickButtons.Single(b => b.ButtonName == "Button2").XInputButton!.ButtonCode != 8192 ||
                target.JoystickButtons.Single(b => b.ButtonName == "Start").XInputButton!.ButtonCode != 4096)
                throw new Exception("Import changed an existing binding or lost legacy controls.");
            var button = target.JoystickButtons.Single(b => b.ButtonName == "Button1").XInputButton!;
            if (button.SdlControl != SdlControlKind.Button || button.SdlControlIndex != 32 || button.XInputIndex != 2)
                throw new Exception("DirectInput button offset was not converted.");
            var up = target.JoystickButtons.Single(b => b.ButtonName == "Up").XInputButton!;
            var gas = target.JoystickButtons.Single(b => b.ButtonName == "Gas").XInputButton!;
            if (up.SdlControl != SdlControlKind.Hat || up.SdlDirection != 1 ||
                gas.SdlControl != SdlControlKind.Axis || gas.SdlControlIndex != 2 || gas.SdlDirection != -1)
                throw new Exception("POV or axis offset was not converted.");
            if (!LegacyBindingsImporter.TryConvertDirectInput(new XElement("DirectInputButton",
                    new XElement("Button", "36"), new XElement("PovDirection", "4500")), 2, out var diagonal) ||
                diagonal.SdlDirection != 3 ||
                new RawJoystickState(Array.Empty<bool>(), Array.Empty<short>(), new byte[] { 0, 1 }).IsPressed(diagonal) ||
                !new RawJoystickState(Array.Empty<bool>(), Array.Empty<short>(), new byte[] { 0, 3 }).IsPressed(diagonal))
                throw new Exception("Diagonal POV import did not require both hat bits.");
            if (target.JoystickButtons.Single(b => b.ButtonName == "Test").RawInputButton?.KeyboardKey !=
                    (OperatingSystem.IsAndroid() ? (Keys?)null : Keys.A) ||
                result.PointerBindings != (OperatingSystem.IsWindows() ? 2 : OperatingSystem.IsLinux() ? 1 : 0))
                throw new Exception("Legacy keyboard/mouse import was not platform-aware.");
            var loaded = Profile();
            BindingsStore.Apply(loaded);
            if (loaded.JoystickButtons.Single(b => b.ButtonName == "Button1").XInputButton?.SdlControlIndex != 32)
                throw new Exception("Imported controls did not survive JSON reload.");
            var again = LegacyBindingsImporter.Import(preview, new[] { loaded }, new Dictionary<Guid, int> { [guid] = 2 });
            if (again.ProfilesSaved != 0)
                throw new Exception("Import was not idempotent.");
            Console.WriteLine("Legacy XInput, DirectInput button/axis/POV, JSON persistence, and existing-binding preservation: PASS");
            return 0;
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(root, true);
        }
    }

    private static XElement Row(string name, string mapping, params XElement[] values) =>
        new("JoystickButtons", new XElement("ButtonName", name), new XElement("InputMapping", mapping), values);

    private static GameProfile Profile() => new()
    {
        ProfileName = "TestGame",
        JoystickButtons = new List<JoystickButtons>
        {
            new() { ButtonName = "Start", InputMapping = InputMapping.P1ButtonStart },
            new() { ButtonName = "Button2", InputMapping = InputMapping.P1Button2 },
            new() { ButtonName = "Button1", InputMapping = InputMapping.P1Button1 },
            new() { ButtonName = "Up", InputMapping = InputMapping.P1ButtonUp },
            new() { ButtonName = "Gas", InputMapping = InputMapping.Analog0 },
            new() { ButtonName = "Coin", InputMapping = InputMapping.Coin1 },
            new() { ButtonName = "Test", InputMapping = InputMapping.Test }
        }
    };
}
