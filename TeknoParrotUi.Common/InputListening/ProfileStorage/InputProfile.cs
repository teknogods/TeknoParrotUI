using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace TeknoParrotUi.Common.InputListening.ProfileStorage
{
    /// <summary>
    /// Per-game input-method availability and defaults, stored separately from
    /// GameProfile XML in <c>InputProfiles/&lt;ProfileName&gt;.json</c> (runtime
    /// folder). Derived automatically from the game's <c>Input API</c>
    /// ConfigValues field when no JSON file exists, so no pregenerated files
    /// need to ship.
    /// </summary>
    public class InputProfile
    {
        public string GameProfileName { get; set; }
        public Dictionary<string, InputMethodInfo> InputMethods { get; set; } = new Dictionary<string, InputMethodInfo>();
        public string DefaultInputMethod { get; set; }
        public InputProfileMetadata Metadata { get; set; } = new InputProfileMetadata();

        /// <summary>Well-known input method names.</summary>
        public static class Methods
        {
            public const string SDL2Gamepad = "SDL2Gamepad";
            public const string DirectInput = "DirectInput";
            public const string XInput = "XInput";
            public const string RawInput = "RawInput";
            public const string RawInputTrackball = "RawInputTrackball";
            public const string EvdevMouse = "EvdevMouse";
            public const string AndroidTouch = "AndroidTouch";
        }

        /// <summary>Methods available for the current platform.</summary>
        public IEnumerable<string> AvailableMethods()
        {
            var platform = CurrentPlatform();
            return InputMethods
                .Where(kv => kv.Value.Enabled && kv.Value.Platforms.Contains(platform))
                .Select(kv => kv.Key);
        }

        public static string CurrentPlatform()
        {
            if (OperatingSystem.IsWindows()) return "windows";
            if (OperatingSystem.IsAndroid()) return "android";
            if (OperatingSystem.IsLinux()) return "linux";
            if (OperatingSystem.IsMacOS()) return "macos";
            return "unknown";
        }
    }

    public class InputMethodInfo
    {
        public bool Enabled { get; set; }
        public bool IsDefault { get; set; }
        public string Description { get; set; }
        public List<string> Platforms { get; set; } = new List<string>();
        /// <summary>Why this method is unavailable for this game, if disabled.</summary>
        public string Reason { get; set; }
    }

    public class InputProfileMetadata
    {
        public bool GeneratedFromGameProfile { get; set; }
        public string Version { get; set; } = "1.0";
        public DateTime LastModified { get; set; }
    }

    public static class InputProfileLoader
    {
        public const string FolderName = "InputProfiles";

        /// <summary>
        /// Load the input profile for a game. Falls back to generating one from
        /// the GameProfile's "Input API" field when no JSON file exists.
        /// </summary>
        public static InputProfile Load(GameProfile gameProfile)
        {
            var path = Path.Combine(FolderName, gameProfile.ProfileName + ".json");
            if (File.Exists(path))
            {
                try
                {
                    var profile = JsonConvert.DeserializeObject<InputProfile>(File.ReadAllText(path));
                    if (profile != null)
                        return profile;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"InputProfileLoader: failed to read {path}: {ex.Message}");
                }
            }
            return GenerateFromGameProfile(gameProfile);
        }

        public static void Save(InputProfile profile)
        {
            Directory.CreateDirectory(FolderName);
            profile.Metadata.LastModified = DateTime.UtcNow;
            var path = Path.Combine(FolderName, profile.GameProfileName + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(profile, Formatting.Indented));
        }

        /// <summary>
        /// Derive input-method availability from the legacy GameProfile:
        /// the "Input API" ConfigValues dropdown lists supported APIs, and
        /// SDL2Gamepad is available everywhere DirectInput/XInput were.
        /// </summary>
        public static InputProfile GenerateFromGameProfile(GameProfile gameProfile)
        {
            var apiField = gameProfile.ConfigValues?.Find(cv => cv.FieldName == "Input API");
            var options = apiField?.FieldOptions ?? new List<string>();
            bool hasRawInput = options.Contains("RawInput");
            bool hasTrackball = options.Contains("RawInputTrackball");
            // Platform gun listeners (evdev/touch) also serve games that carry the
            // GunGame flag without offering RawInput options — same rule as the
            // manager's gun-intent detection.
            bool gunCapable = hasRawInput || hasTrackball || gameProfile.GunGame;
            string legacyDefault = apiField?.FieldValue;

            var profile = new InputProfile
            {
                GameProfileName = gameProfile.ProfileName,
                Metadata = new InputProfileMetadata
                {
                    GeneratedFromGameProfile = true,
                    LastModified = DateTime.UtcNow
                }
            };

            profile.InputMethods[InputProfile.Methods.SDL2Gamepad] = new InputMethodInfo
            {
                Enabled = true,
                Description = "Gamepad/joystick via SDL2 (cross-platform; replaces DirectInput/XInput)",
                Platforms = new List<string> { "windows", "linux", "android", "macos" }
            };
            profile.InputMethods[InputProfile.Methods.DirectInput] = new InputMethodInfo
            {
                Enabled = options.Contains("DirectInput"),
                Description = "Legacy DirectInput gamepad (Windows only)",
                Platforms = new List<string> { "windows" }
            };
            profile.InputMethods[InputProfile.Methods.XInput] = new InputMethodInfo
            {
                Enabled = options.Contains("XInput"),
                Description = "Legacy XInput gamepad (Windows only)",
                Platforms = new List<string> { "windows" }
            };
            profile.InputMethods[InputProfile.Methods.RawInput] = new InputMethodInfo
            {
                Enabled = hasRawInput,
                Description = "Light gun / mouse via Win32 RawInput",
                Platforms = new List<string> { "windows" },
                Reason = hasRawInput ? null : "Game does not offer RawInput"
            };
            profile.InputMethods[InputProfile.Methods.RawInputTrackball] = new InputMethodInfo
            {
                Enabled = hasTrackball,
                Description = "Trackball via Win32 RawInput",
                Platforms = new List<string> { "windows" },
                Reason = hasTrackball ? null : "Game does not offer trackball input"
            };
            profile.InputMethods[InputProfile.Methods.EvdevMouse] = new InputMethodInfo
            {
                Enabled = gunCapable,
                Description = "Light gun / mouse via evdev (Linux)",
                Platforms = new List<string> { "linux" },
                Reason = gunCapable ? null : "Not a gun/trackball game"
            };
            profile.InputMethods[InputProfile.Methods.AndroidTouch] = new InputMethodInfo
            {
                Enabled = gunCapable,
                Description = "Light gun via touch (Android)",
                Platforms = new List<string> { "android" },
                Reason = gunCapable ? null : "Not a gun/trackball game"
            };

            profile.DefaultInputMethod = legacyDefault switch
            {
                "RawInputTrackball" => InputProfile.Methods.RawInputTrackball,
                "RawInput" => InputProfile.Methods.RawInput,
                _ => InputProfile.Methods.SDL2Gamepad
            };
            if (profile.InputMethods.TryGetValue(profile.DefaultInputMethod, out var def))
                def.IsDefault = true;

            return profile;
        }
    }
}
