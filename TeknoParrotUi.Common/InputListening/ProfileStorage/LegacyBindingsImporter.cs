using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace TeknoParrotUi.Common.InputListening.ProfileStorage
{
    /// <summary>Imports controls from 1.x UserProfiles XML without importing old game settings.</summary>
    public static class LegacyBindingsImporter
    {
        private static readonly XmlSerializer XInputSerializer =
            new XmlSerializer(typeof(XInputButton), new XmlRootAttribute("XInputButton"));
        private static readonly XmlSerializer RawInputSerializer =
            new XmlSerializer(typeof(RawInputButton), new XmlRootAttribute("RawInputButton"));

        public sealed class Preview
        {
            internal readonly List<(string File, XDocument Document)> Documents = new();
            public List<Guid> DirectInputDevices { get; } = new();
            public List<string> Warnings { get; } = new();
            public int ProfileCount => Documents.Count;
        }

        public sealed class Result
        {
            public int ProfilesMatched { get; internal set; }
            public int ProfilesSaved { get; internal set; }
            public int GamepadBindings { get; internal set; }
            public int PointerBindings { get; internal set; }
            public int DirectInputBindings { get; internal set; }
            public int Skipped { get; internal set; }
            public List<string> Warnings { get; } = new();
        }

        public static Preview Scan(string selectedFolder)
        {
            var folder = Path.GetFileName(Path.TrimEndingDirectorySeparator(selectedFolder))
                .Equals("UserProfiles", StringComparison.OrdinalIgnoreCase)
                ? selectedFolder : Path.Combine(selectedFolder, "UserProfiles");
            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException("Select a 1.0 installation or its UserProfiles folder.");

            var preview = new Preview();
            foreach (var file in Directory.GetFiles(folder, "*.xml"))
            {
                try
                {
                    using var reader = XmlReader.Create(file, new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Prohibit,
                        XmlResolver = null
                    });
                    var document = XDocument.Load(reader);
                    if (document.Root?.Name.LocalName != "GameProfile")
                        continue;
                    preview.Documents.Add((file, document));
                    foreach (var row in Rows(document))
                    {
                        var guidText = Value(row.Element("DirectInputButton"), "JoystickGuid");
                        if (Guid.TryParse(guidText, out var guid) && guid != Guid.Empty &&
                            !preview.DirectInputDevices.Contains(guid))
                            preview.DirectInputDevices.Add(guid);
                    }
                }
                catch (Exception ex)
                {
                    preview.Warnings.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }
            }
            return preview;
        }

        public static Result Import(Preview preview, IEnumerable<GameProfile> currentProfiles,
            IReadOnlyDictionary<Guid, int> directInputSlots)
        {
            var result = new Result();
            result.Warnings.AddRange(preview.Warnings);
            var profiles = currentProfiles.Where(p => !string.IsNullOrEmpty(p.ProfileName))
                .GroupBy(p => p.ProfileName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var (file, document) in preview.Documents)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (!profiles.TryGetValue(name, out var target) || target.JoystickButtons == null)
                {
                    result.Warnings.Add($"{name}: no matching 2.0 game profile.");
                    continue;
                }
                result.ProfilesMatched++;
                var working = target.Clone();
                var changed = false;
                var gamepadBindings = 0;
                var directInputBindings = 0;
                var pointerBindings = 0;
                foreach (var row in Rows(document))
                {
                    var rowChanged = false;
                    if (!Enum.TryParse(Value(row, "InputMapping"), out InputMapping mapping))
                    {
                        result.Skipped++;
                        continue;
                    }
                    var buttonName = Value(row, "ButtonName");
                    var targetRow = working.JoystickButtons.FirstOrDefault(b => b.InputMapping == mapping &&
                        string.Equals(b.ButtonName, buttonName, StringComparison.OrdinalIgnoreCase));
                    targetRow ??= working.JoystickButtons.Count(b => b.InputMapping == mapping) == 1
                        ? working.JoystickButtons.First(b => b.InputMapping == mapping) : null;
                    if (targetRow == null)
                    {
                        result.Skipped++;
                        continue;
                    }

                    // An existing 2.0 binding always wins. XInput keeps its normalized layout.
                    if (targetRow.XInputButton == null)
                    {
                        var legacyXi = Deserialize<XInputButton>(row.Element("XInputButton"));
                        if (legacyXi != null)
                        {
                            targetRow.XInputButton = legacyXi;
                            targetRow.BindNameXi = Value(row, "BindNameXi") ?? "Imported XInput binding";
                            gamepadBindings++;
                            changed = true;
                            rowChanged = true;
                        }
                        else if (row.Element("DirectInputButton") is XElement legacyDi)
                        {
                            var legacyName = Value(row, "BindNameDi") ?? string.Empty;
                            if (!legacyName.StartsWith("Keyboard", StringComparison.OrdinalIgnoreCase) &&
                                !legacyName.StartsWith("Mouse", StringComparison.OrdinalIgnoreCase) &&
                                Guid.TryParse(Value(legacyDi, "JoystickGuid"), out var guid) &&
                                directInputSlots.TryGetValue(guid, out var slot) &&
                                slot >= 0 && slot < InputListening.Gamepad.SDL2GamepadBackend.MaxSlots &&
                                TryConvertDirectInput(legacyDi, slot, out var converted))
                            {
                                targetRow.XInputButton = converted;
                                targetRow.BindNameXi = $"Imported DirectInput: {Value(row, "BindNameDi") ?? "control"} (device {slot}; verify)";
                                directInputBindings++;
                                changed = true;
                                rowChanged = true;
                            }
                            else
                                result.Skipped++;
                        }
                    }

                    // Linux keyboard listeners use the same logical Keys enum; an empty
                    // path means any keyboard. Windows mouse device paths do not travel.
                    if (targetRow.RawInputButton == null)
                    {
                        var raw = Deserialize<RawInputButton>(row.Element("RawInputButton"));
                        if (raw != null && raw.DeviceType != RawDeviceType.None &&
                            (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() &&
                             raw.DeviceType == RawDeviceType.Keyboard && raw.KeyboardKey != Keys.None))
                        {
                            if (!OperatingSystem.IsWindows()) raw.DevicePath = string.Empty;
                            targetRow.RawInputButton = raw;
                            targetRow.BindNameRi = Value(row, "BindNameRi") ?? "Imported RawInput binding";
                            pointerBindings++;
                            changed = true;
                            rowChanged = true;
                        }
                        else if (raw != null && raw.DeviceType != RawDeviceType.None)
                            result.Skipped++;
                    }
                    if (rowChanged)
                        targetRow.BindName = targetRow.BindNameXi ?? targetRow.BindNameRi;
                }
                if (changed)
                {
                    if (BindingsStore.TrySave(working))
                    {
                        target.JoystickButtons = working.JoystickButtons;
                        result.ProfilesSaved++;
                        result.GamepadBindings += gamepadBindings;
                        result.DirectInputBindings += directInputBindings;
                        result.PointerBindings += pointerBindings;
                    }
                    else
                        result.Warnings.Add($"{name}: could not save imported bindings.");
                }
            }
            if (result.DirectInputBindings > 0)
                result.Warnings.Add("DirectInput offsets were translated to SDL raw controls. Verify button, axis and hat order on each device.");
            if (preview.DirectInputDevices.Any(g => !directInputSlots.ContainsKey(g)))
                result.Warnings.Add("Unmapped DirectInput devices were skipped; connect and select each device to import those bindings.");
            if (!OperatingSystem.IsWindows())
                result.Warnings.Add(OperatingSystem.IsLinux()
                    ? "Windows mouse/lightgun paths were skipped; keyboard keys were translated to any Linux keyboard."
                    : "Windows RawInput paths were skipped on this platform.");
            else if (result.PointerBindings > 0)
                result.Warnings.Add("Imported RawInput device paths are Windows-specific and may need rebinding if hardware or drivers changed.");
            if (OperatingSystem.IsAndroid())
                result.Warnings.Add("Winlator has a separate controls editor; imported shared bindings do not populate its touch/gamepad layout.");
            return result;
        }

        private static IEnumerable<XElement> Rows(XDocument document) =>
            document.Root?.Element("JoystickButtons")?.Elements("JoystickButtons") ?? Enumerable.Empty<XElement>();

        private static string Value(XElement parent, string name) => parent?.Element(name)?.Value;

        private static T Deserialize<T>(XElement element) where T : class
        {
            if (element == null) return null;
            try
            {
                using var reader = element.CreateReader();
                var serializer = typeof(T) == typeof(XInputButton) ? XInputSerializer : RawInputSerializer;
                return serializer.Deserialize(reader) as T;
            }
            catch { return null; }
        }

        // SharpDX JoystickOffset: X/Y/Z/Rx/Ry/Rz/Slider0/Slider1=0..28,
        // POV0..3=32/36/40/44, Button0..127=48..175. SDL uses zero-based
        // raw axis/hat/button indices. Device selection remains explicit.
        public static bool TryConvertDirectInput(XElement legacy, int slot, out XInputButton converted)
        {
            converted = null;
            if (!int.TryParse(Value(legacy, "Button"), out var offset)) return false;
            var button = new XInputButton { XInputIndex = slot, IsButton = true };
            if (offset >= 48 && offset <= 175)
            {
                button.SdlControl = SdlControlKind.Button;
                button.SdlControlIndex = offset - 48;
            }
            else if (offset >= 32 && offset <= 44 && offset % 4 == 0 &&
                     int.TryParse(Value(legacy, "PovDirection"), out var pov) && pov >= 0 && pov < 36000 && pov % 4500 == 0)
            {
                button.SdlControl = SdlControlKind.Hat;
                button.SdlControlIndex = (offset - 32) / 4;
                button.SdlDirection = (pov / 4500) switch
                {
                    0 => 1, 1 => 3, 2 => 2, 3 => 6,
                    4 => 4, 5 => 12, 6 => 8, 7 => 9, _ => 0
                };
            }
            else if (offset >= 0 && offset <= 28 && offset % 4 == 0 &&
                     bool.TryParse(Value(legacy, "IsAxis"), out var isAxis) && isAxis)
            {
                button.SdlControl = SdlControlKind.Axis;
                button.SdlControlIndex = offset / 4;
                button.SdlDirection = bool.TryParse(Value(legacy, "IsAxisMinus"), out var minus) && minus ? -1 : 1;
                button.IsButton = false;
            }
            else return false;
            converted = button;
            return true;
        }
    }
}
