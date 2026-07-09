using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace TeknoParrotUi.Common.InputListening.ProfileStorage
{
    /// <summary>
    /// The single source of truth for game control bindings:
    /// InputBindings/&lt;profile&gt;.json. Replaces the JoystickButtons binding
    /// data embedded in the GameProfiles/UserProfiles XML — when a JSON file
    /// exists its bindings fully replace whatever the XML carried (migration:
    /// profiles without a JSON keep their XML bindings until the first save,
    /// which writes the JSON).
    /// Game metadata/settings stay in the XML; only controls live here.
    /// </summary>
    public static class BindingsStore
    {
        public const string Folder = "InputBindings";

        private sealed class BindingEntry
        {
            public string ButtonName { get; set; }
            public InputMapping InputMapping { get; set; }
            public XInputButton XInputButton { get; set; }
            public RawInputButton RawInputButton { get; set; }
            public string BindName { get; set; }
            public string BindNameXi { get; set; }
            public string BindNameRi { get; set; }
        }

        private static string PathFor(GameProfile profile)
        {
            var name = profile.ProfileName;
            if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(profile.FileName))
                name = System.IO.Path.GetFileNameWithoutExtension(profile.FileName);
            return string.IsNullOrEmpty(name) ? null : System.IO.Path.Combine(Folder, name + ".json");
        }

        /// <summary>
        /// Apply the JSON bindings onto a freshly loaded profile — the JSON is
        /// the only controls source. Dead DirectInput bindings from the XML era
        /// are always stripped. User profiles without a JSON are migrated on the
        /// spot: their surviving XInput/RawInput bindings are written out as the
        /// JSON, which is authoritative from then on.
        /// </summary>
        public static void Apply(GameProfile profile)
        {
            // DirectInput is gone — its bindings can never fire. Drop them and
            // rebuild the display name from the systems that actually run.
            foreach (var row in profile.JoystickButtons)
            {
                row.DirectInputButton = null;
                row.BindNameDi = null;
                row.BindName = !string.IsNullOrEmpty(row.BindNameXi) ? row.BindNameXi
                             : !string.IsNullOrEmpty(row.BindNameRi) ? row.BindNameRi
                             : null;
            }

            var path = PathFor(profile);
            if (path == null)
                return;

            if (!File.Exists(path))
            {
                // One-time migration: user profiles with usable bindings get
                // their JSON written now; stock profiles carry no bindings.
                bool isUserProfile = profile.FileName != null &&
                                     profile.FileName.Replace('\\', '/').Contains("UserProfiles/");
                if (isUserProfile && profile.JoystickButtons.Any(HasBinding))
                    Save(profile);
                return;
            }

            try
            {
                var entries = JsonConvert.DeserializeObject<List<BindingEntry>>(File.ReadAllText(path));
                if (entries == null)
                    return;

                var byKey = new Dictionary<string, BindingEntry>();
                foreach (var e in entries)
                    byKey[Key(e.InputMapping, e.ButtonName)] = e;

                foreach (var row in profile.JoystickButtons)
                {
                    if (byKey.TryGetValue(Key(row.InputMapping, row.ButtonName), out var e))
                    {
                        row.XInputButton = e.XInputButton;
                        row.RawInputButton = e.RawInputButton;
                        row.BindName = e.BindName;
                        row.BindNameXi = e.BindNameXi;
                        row.BindNameRi = e.BindNameRi;
                    }
                    else
                    {
                        row.XInputButton = null;
                        row.RawInputButton = null;
                        row.BindName = null;
                        row.BindNameXi = null;
                        row.BindNameRi = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BindingsStore: failed to load {path}: {ex.Message}");
            }
        }

        private static bool HasBinding(JoystickButtons b) =>
            b.XInputButton != null ||
            (b.RawInputButton != null && b.RawInputButton.DeviceType != RawDeviceType.None);

        /// <summary>Write the profile's bindings as the authoritative JSON.</summary>
        public static void Save(GameProfile profile)
        {
            var path = PathFor(profile);
            if (path == null)
                return;

            try
            {
                Directory.CreateDirectory(Folder);
                var entries = profile.JoystickButtons
                    .Where(b => b.XInputButton != null ||
                                (b.RawInputButton != null && b.RawInputButton.DeviceType != RawDeviceType.None))
                    .Select(b => new BindingEntry
                    {
                        ButtonName = b.ButtonName,
                        InputMapping = b.InputMapping,
                        XInputButton = b.XInputButton,
                        RawInputButton = b.RawInputButton,
                        BindName = b.BindName,
                        BindNameXi = b.BindNameXi,
                        BindNameRi = b.BindNameRi
                    })
                    .ToList();
                File.WriteAllText(path, JsonConvert.SerializeObject(entries, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BindingsStore: failed to save {path}: {ex.Message}");
            }
        }

        private static string Key(InputMapping mapping, string buttonName) => $"{mapping}|{buttonName}";
    }
}
