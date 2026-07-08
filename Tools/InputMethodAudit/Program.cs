using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TeknoParrotUi.Common;

namespace InputMethodAudit
{
    /// <summary>
    /// Phase 0 of the cross-platform input refactor: scans all GameProfiles/*.xml
    /// and reports the input-method distribution so we know which games need
    /// which listeners (SDL2 gamepad, RawInput mouse, trackball, etc.).
    ///
    /// Usage: dotnet run --project Tools/InputMethodAudit [path-to-GameProfiles]
    /// </summary>
    internal static class Program
    {
        private sealed class GameAudit
        {
            public string Profile { get; set; }
            public string GameName { get; set; }
            public List<string> InputApiOptions { get; set; } = new();
            public string InputApiDefault { get; set; }
            public bool GunGame { get; set; }
            public bool InvertedMouseAxis { get; set; }
            public bool Use16BitAnalog { get; set; }
            public bool HasXInputBindings { get; set; }
            public bool HasDirectInputBindings { get; set; }
            public bool HasRawInputBindings { get; set; }
            public bool UsesRotaryEncoders { get; set; }
            public string EmulationProfile { get; set; }
            public string EmulatorType { get; set; }
        }

        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "sdl2-test")
                return Sdl2SmokeTest.Run();
            if (args.Length > 0 && args[0] == "pipeline-test")
            {
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Usage: pipeline-test <path-to-userprofile.xml>");
                    return 1;
                }
                return PipelineTest.Run(args[1]);
            }
            if (args.Length > 0 && args[0] == "evdev-test")
                return EvdevSmokeTest.Run();
            if (args.Length > 0 && args[0] == "gun-math-test")
                return GunMathTest.Run();
            if (args.Length > 0 && args[0] == "profiles-test")
            {
                var dir = args.Length > 1 ? args[1] : FindProfilesDir();
                if (dir == null)
                {
                    Console.Error.WriteLine("GameProfiles directory not found.");
                    return 1;
                }
                return InputProfileTest.Run(dir);
            }

            var profilesDir = args.Length > 0 ? args[0] : FindProfilesDir();
            if (profilesDir == null || !Directory.Exists(profilesDir))
            {
                Console.Error.WriteLine("GameProfiles directory not found. Pass it as the first argument.");
                return 1;
            }

            var files = Directory.GetFiles(profilesDir, "*.xml").OrderBy(f => f).ToList();
            var games = new List<GameAudit>();
            var parseFailures = new List<string>();

            foreach (var file in files)
            {
                GameProfile profile;
                try
                {
                    profile = JoystickHelper.DeSerializeGameProfile(file, false);
                }
                catch
                {
                    profile = null;
                }

                if (profile == null)
                {
                    parseFailures.Add(Path.GetFileName(file));
                    continue;
                }

                var apiField = profile.ConfigValues?.Find(cv => cv.FieldName == "Input API");
                var buttons = profile.JoystickButtons ?? new List<JoystickButtons>();

                games.Add(new GameAudit
                {
                    Profile = Path.GetFileNameWithoutExtension(file),
                    GameName = profile.GameNameInternal,
                    InputApiOptions = apiField?.FieldOptions?.ToList() ?? new List<string>(),
                    InputApiDefault = apiField?.FieldValue,
                    GunGame = profile.GunGame,
                    InvertedMouseAxis = profile.InvertedMouseAxis,
                    Use16BitAnalog = profile.Use16BitAnalog,
                    HasXInputBindings = buttons.Any(b => b?.XInputButton != null),
                    HasDirectInputBindings = buttons.Any(b => b?.DirectInputButton != null),
                    HasRawInputBindings = buttons.Any(b => b?.RawInputButton != null),
                    UsesRotaryEncoders = buttons.Any(b => b?.InputMapping.ToString().StartsWith("Rotary") == true)
                                         || profile.ConfigValues?.Any(cv => cv.FieldName == "Use Buttons For Rotary Encoders") == true,
                    EmulationProfile = profile.EmulationProfile.ToString(),
                    EmulatorType = profile.EmulatorType.ToString()
                });
            }

            var report = BuildReport(games, parseFailures);
            Console.WriteLine(report);

            var outDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "report");
            outDir = Path.GetFullPath(outDir);
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "input-audit.md"), report);
            File.WriteAllText(Path.Combine(outDir, "input-audit.json"),
                JsonConvert.SerializeObject(games, Formatting.Indented));
            Console.WriteLine($"\nReport written to {outDir}");
            return 0;
        }

        private static string FindProfilesDir()
        {
            // Walk up from the binary looking for the repo's GameProfiles source folder.
            var dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                var candidate = Path.Combine(dir, "TeknoParrotUi.Common", "GameProfiles");
                if (Directory.Exists(candidate))
                    return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        private static string BuildReport(List<GameAudit> games, List<string> parseFailures)
        {
            var sw = new StringWriter();
            sw.WriteLine("# Input Method Audit — GameProfiles/");
            sw.WriteLine();
            sw.WriteLine($"Generated: {DateTime.UtcNow:u}");
            sw.WriteLine();
            sw.WriteLine("## Summary");
            sw.WriteLine();
            sw.WriteLine($"| Metric | Count |");
            sw.WriteLine($"|--------|-------|");
            sw.WriteLine($"| Total profiles parsed | {games.Count} |");
            sw.WriteLine($"| Parse failures / skipped | {parseFailures.Count} |");
            sw.WriteLine($"| Has `Input API` field | {games.Count(g => g.InputApiOptions.Count > 0)} |");
            sw.WriteLine($"| Offers DirectInput | {games.Count(g => g.InputApiOptions.Contains("DirectInput"))} |");
            sw.WriteLine($"| Offers XInput | {games.Count(g => g.InputApiOptions.Contains("XInput"))} |");
            sw.WriteLine($"| Offers RawInput | {games.Count(g => g.InputApiOptions.Contains("RawInput"))} |");
            sw.WriteLine($"| Offers RawInputTrackball | {games.Count(g => g.InputApiOptions.Contains("RawInputTrackball"))} |");
            sw.WriteLine($"| Offers RawInput AND Trackball | {games.Count(g => g.InputApiOptions.Contains("RawInput") && g.InputApiOptions.Contains("RawInputTrackball"))} |");
            sw.WriteLine($"| Gamepad-only (DI/XI, no gun) | {games.Count(g => g.InputApiOptions.Count > 0 && !g.InputApiOptions.Contains("RawInput") && !g.InputApiOptions.Contains("RawInputTrackball"))} |");
            sw.WriteLine($"| GunGame flag set | {games.Count(g => g.GunGame)} |");
            sw.WriteLine($"| Has RawInput button bindings | {games.Count(g => g.HasRawInputBindings)} |");
            sw.WriteLine($"| Uses rotary encoders | {games.Count(g => g.UsesRotaryEncoders)} |");
            sw.WriteLine($"| Uses 16-bit analog | {games.Count(g => g.Use16BitAnalog)} |");
            sw.WriteLine();

            sw.WriteLine("## Default Input API distribution");
            sw.WriteLine();
            foreach (var group in games.GroupBy(g => g.InputApiDefault ?? "(none)").OrderByDescending(g => g.Count()))
                sw.WriteLine($"- `{group.Key}`: {group.Count()}");
            sw.WriteLine();

            var gunGames = games.Where(g => g.InputApiOptions.Contains("RawInput") || g.InputApiOptions.Contains("RawInputTrackball") || g.GunGame)
                                .OrderBy(g => g.Profile).ToList();
            sw.WriteLine($"## Gun / trackball games ({gunGames.Count}) — need platform mouse/touch listeners");
            sw.WriteLine();
            sw.WriteLine("| Profile | Game | APIs offered | Default | GunGame flag |");
            sw.WriteLine("|---------|------|--------------|---------|--------------|");
            foreach (var g in gunGames)
                sw.WriteLine($"| {g.Profile} | {g.GameName} | {string.Join(", ", g.InputApiOptions)} | {g.InputApiDefault} | {(g.GunGame ? "✔" : "")} |");
            sw.WriteLine();

            var trackball = games.Where(g => g.InputApiOptions.Contains("RawInputTrackball")).OrderBy(g => g.Profile).ToList();
            sw.WriteLine($"## Trackball-capable games ({trackball.Count})");
            sw.WriteLine();
            foreach (var g in trackball)
                sw.WriteLine($"- {g.Profile} ({g.GameName})");
            sw.WriteLine();

            var noApiField = games.Where(g => g.InputApiOptions.Count == 0).OrderBy(g => g.Profile).ToList();
            sw.WriteLine($"## Profiles without an `Input API` field ({noApiField.Count})");
            sw.WriteLine();
            foreach (var g in noApiField)
                sw.WriteLine($"- {g.Profile} ({g.GameName}) — emulator: {g.EmulatorType}, profile: {g.EmulationProfile}");
            sw.WriteLine();

            if (parseFailures.Count > 0)
            {
                sw.WriteLine($"## Parse failures / skipped ({parseFailures.Count})");
                sw.WriteLine();
                foreach (var f in parseFailures)
                    sw.WriteLine($"- {f}");
            }

            return sw.ToString();
        }
    }
}
