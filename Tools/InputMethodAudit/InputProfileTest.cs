using System;
using System.IO;
using System.Linq;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening.ProfileStorage;

namespace InputMethodAudit
{
    /// <summary>
    /// Verifies InputProfile generation against every game profile (Phase 4 testing):
    /// generates an InputProfile from each GameProfile, checks invariants, and
    /// round-trips one through JSON.
    /// Usage: dotnet run --project Tools/InputMethodAudit -- profiles-test
    /// </summary>
    internal static class InputProfileTest
    {
        public static int Run(string profilesDir)
        {
            int total = 0, failures = 0, gunDefaults = 0, sdl2Defaults = 0, trackballDefaults = 0;

            foreach (var file in Directory.GetFiles(profilesDir, "*.xml").OrderBy(f => f))
            {
                var gameProfile = JoystickHelper.DeSerializeGameProfile(file, false);
                if (gameProfile == null)
                    continue;
                total++;

                var profile = InputProfileLoader.GenerateFromGameProfile(gameProfile);
                var errors = Validate(gameProfile, profile);
                if (errors.Count > 0)
                {
                    failures++;
                    Console.WriteLine($"FAIL {Path.GetFileNameWithoutExtension(file)}:");
                    foreach (var e in errors)
                        Console.WriteLine($"  - {e}");
                }

                switch (profile.DefaultInputMethod)
                {
                    case InputProfile.Methods.RawInput: gunDefaults++; break;
                    case InputProfile.Methods.RawInputTrackball: trackballDefaults++; break;
                    case InputProfile.Methods.SDL2Gamepad: sdl2Defaults++; break;
                }
            }

            // JSON round-trip check on one gun game
            var sample = JoystickHelper.DeSerializeGameProfile(Path.Combine(profilesDir, "2Spicy.xml"), false);
            if (sample != null)
            {
                var generated = InputProfileLoader.GenerateFromGameProfile(sample);
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(generated, Newtonsoft.Json.Formatting.Indented);
                var roundTripped = Newtonsoft.Json.JsonConvert.DeserializeObject<InputProfile>(json);
                bool ok = roundTripped != null &&
                          roundTripped.DefaultInputMethod == generated.DefaultInputMethod &&
                          roundTripped.InputMethods.Count == generated.InputMethods.Count;
                Console.WriteLine($"\nJSON round-trip (2Spicy): {(ok ? "OK" : "FAIL")}");
                Console.WriteLine($"  default={roundTripped?.DefaultInputMethod}, methods={roundTripped?.InputMethods.Count}");
                Console.WriteLine($"  available on this platform: {string.Join(", ", roundTripped?.AvailableMethods() ?? Enumerable.Empty<string>())}");
                if (!ok) failures++;
            }

            Console.WriteLine($"\nGenerated {total} input profiles: {sdl2Defaults} default SDL2Gamepad, " +
                              $"{gunDefaults} default RawInput, {trackballDefaults} default Trackball, {failures} failures");
            return failures == 0 ? 0 : 1;
        }

        private static System.Collections.Generic.List<string> Validate(GameProfile game, InputProfile profile)
        {
            var errors = new System.Collections.Generic.List<string>();

            if (profile.GameProfileName != game.ProfileName)
                errors.Add("GameProfileName mismatch");
            if (!profile.InputMethods.TryGetValue(InputProfile.Methods.SDL2Gamepad, out var sdl2) || !sdl2.Enabled)
                errors.Add("SDL2Gamepad must always be enabled");
            if (string.IsNullOrEmpty(profile.DefaultInputMethod) || !profile.InputMethods.ContainsKey(profile.DefaultInputMethod))
                errors.Add($"DefaultInputMethod '{profile.DefaultInputMethod}' not in InputMethods");

            var apiField = game.ConfigValues?.Find(cv => cv.FieldName == "Input API");
            bool hasRawInput = apiField?.FieldOptions?.Contains("RawInput") == true;
            bool hasTrackball = apiField?.FieldOptions?.Contains("RawInputTrackball") == true;
            bool gunCapable = hasRawInput || hasTrackball || game.GunGame;

            if (profile.InputMethods[InputProfile.Methods.RawInput].Enabled != hasRawInput)
                errors.Add("RawInput enablement does not match Input API options");
            if (profile.InputMethods[InputProfile.Methods.RawInputTrackball].Enabled != hasTrackball)
                errors.Add("RawInputTrackball enablement does not match Input API options");
            if (!profile.InputMethods[InputProfile.Methods.EvdevMouse].Enabled)
                errors.Add("EvdevMouse must be enabled for every game (merged input always)");
            if (profile.InputMethods[InputProfile.Methods.AndroidTouch].Enabled != gunCapable)
                errors.Add("AndroidTouch should be enabled exactly for gun/trackball-capable games");

            return errors;
        }
    }
}
