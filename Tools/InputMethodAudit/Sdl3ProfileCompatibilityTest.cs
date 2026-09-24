using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Newtonsoft.Json;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening.ProfileStorage;

namespace InputMethodAudit;

internal static class Sdl3ProfileCompatibilityTest
{
    public static int Run()
    {
        // This is the string parsed by GameSession from older UserProfiles XML.
        const string legacyXml = "<GameProfile><ProfileName>LegacySDL2</ProfileName>" +
            "<ConfigValues><FieldInformation><FieldName>Input API</FieldName>" +
            "<FieldValue>SDL2</FieldValue></FieldInformation></ConfigValues></GameProfile>";
        using (var reader = new StringReader(legacyXml))
        {
            var game = (GameProfile?)new XmlSerializer(typeof(GameProfile)).Deserialize(reader);
            var savedApi = game?.ConfigValues?.Find(field => field.FieldName == "Input API")?.FieldValue;
            if (!Enum.TryParse<InputApi>(savedApi, out var parsedApi) || parsedApi != InputApi.SDL3)
                throw new InvalidOperationException("Legacy XML Input API value did not route to SDL3.");
        }

        // InputProfiles JSON has its own persisted method key, separate from
        // the XML Input API value. Loading must migrate both key and default.
        var originalDirectory = Environment.CurrentDirectory;
        var testDirectory = Path.Combine(Path.GetTempPath(), "tpui-sdl3-profile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(testDirectory, InputProfileLoader.FolderName));
        try
        {
            Environment.CurrentDirectory = testDirectory;
            var legacyProfile = new InputProfile
            {
                GameProfileName = "LegacySDL2",
                DefaultInputMethod = InputProfile.Methods.SDL2Gamepad,
                InputMethods = new Dictionary<string, InputMethodInfo>
                {
                    [InputProfile.Methods.SDL2Gamepad] = new()
                    {
                        Enabled = true,
                        IsDefault = true,
                        Description = "Gamepad via SDL2",
                        Platforms = new List<string> { "windows", "linux", "android" }
                    }
                }
            };
            var path = Path.Combine(InputProfileLoader.FolderName, "LegacySDL2.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(legacyProfile));
            var upgraded = InputProfileLoader.Load(new GameProfile { ProfileName = "LegacySDL2" });
            if (upgraded.DefaultInputMethod != InputProfile.Methods.SDL3Gamepad ||
                !upgraded.InputMethods.TryGetValue(InputProfile.Methods.SDL3Gamepad, out var gamepad) ||
                !gamepad.Enabled ||
                upgraded.InputMethods.ContainsKey(InputProfile.Methods.SDL2Gamepad) ||
                gamepad.Description.Contains("SDL2", StringComparison.Ordinal))
                throw new InvalidOperationException("Legacy JSON gamepad method did not upgrade to SDL3.");

            InputProfileLoader.Save(upgraded);
            var saved = File.ReadAllText(path);
            if (saved.Contains("\"SDL2Gamepad\"", StringComparison.Ordinal))
                throw new InvalidOperationException("Saving wrote the legacy SDL2 method key again.");
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            Directory.Delete(testDirectory, recursive: true);
        }

        Console.WriteLine("Legacy SDL2 XML Input API and InputProfiles JSON route to SDL3: PASS");
        return 0;
    }
}
