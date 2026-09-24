using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TeknoParrotUi.Common
{
    // Stock compatibility data remains authoritative when loading an older user copy.
    // Keep this outside GameProfileLoader: its parallel workers must never wait on
    // the GameProfileLoader class-initialization lock.
    public static class StockProfileMetadata
    {
        public static void Apply(GameProfile target, GameProfile stock)
        {
            if (target == null || stock == null || ReferenceEquals(target, stock))
                return;
            target.LinuxOk = stock.LinuxOk;
            target.ProtonVersion ??= stock.ProtonVersion;
            target.GamescopeGameWindowCompatibility = stock.GamescopeGameWindowCompatibility;
        }
    }

    public static class GameProfileLoader
    {
        public static List<GameProfile> GameProfiles { get; set; } = new();
        public static List<GameProfile> UserProfiles { get; set; } = new();

        public static void LoadProfiles(bool onlyUserProfiles)
        {
            Directory.CreateDirectory("GameProfiles");
            Directory.CreateDirectory("UserProfiles");
            var stockFiles = Directory.GetFiles("GameProfiles", "*.xml")
                .ToDictionary(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
            var userFiles = Directory.GetFiles("UserProfiles", "*.xml")
                .ToDictionary(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
            var loaded = new List<GameProfile>();
            var installed = new List<GameProfile>();
            var metadataCatalog = JoystickHelper.LoadMetadataCatalog();
            var files = onlyUserProfiles
                ? userFiles.Keys.Where(stockFiles.ContainsKey).Select(name => stockFiles[name])
                : stockFiles.Values.AsEnumerable();
            var sync = new object();

            Parallel.ForEach(files, file =>
            {
                var stock = JoystickHelper.DeSerializeGameProfile(file, false);
                if (stock == null)
                    return;

                GameProfile profile = stock;
                var hasUser = userFiles.TryGetValue(Path.GetFileName(file), out var userFile);
                var migrated = false;
                if (hasUser)
                {
                    var user = JoystickHelper.DeSerializeGameProfile(userFile, true);
                    if (user == null)
                        return;
                    if (user.GameProfileRevision == stock.GameProfileRevision)
                    {
                        profile = user;
                        StockProfileMetadata.Apply(profile, stock);
                    }
                    else
                    {
                        MergeUserSettings(stock, user);
                        stock.FileName = userFile;
                        migrated = !onlyUserProfiles;
                    }
                }

                PopulateMetadata(profile, file, metadataCatalog);
                if (migrated)
                    JoystickHelper.SerializeGameProfile(profile);

                // The library and installed lists must not share mutable bindings.
                var libraryProfile = hasUser && !onlyUserProfiles ? profile.Clone() : profile;
                lock (sync)
                {
                    if (!onlyUserProfiles)
                        loaded.Add(profile);
                    if (hasUser)
                        installed.Add(libraryProfile);
                }

                if (!hasUser && !File.Exists(profile.IconName))
                    Debug.WriteLine($"{profile.FileName} icon is missing! - {profile.IconName}");
            });

            if (!onlyUserProfiles)
                GameProfiles = loaded.Where(IsVisibleOnThisPlatform)
                    .OrderBy(x => x.GameNameInternal).ToList();
            UserProfiles = installed.Where(IsVisibleOnThisPlatform)
                .OrderBy(x => x.GameNameInternal).ToList();

            // JSON bindings are the single source of truth after profile migration.
            foreach (var profile in UserProfiles)
                InputListening.ProfileStorage.BindingsStore.Apply(profile);
            foreach (var profile in GameProfiles)
                InputListening.ProfileStorage.BindingsStore.Apply(profile);
        }

        private static void PopulateMetadata(GameProfile profile, string file,
            IReadOnlyDictionary<string, Metadata> catalog)
        {
            profile.ProfileName = Path.GetFileNameWithoutExtension(file);
            profile.IconName = "Icons/" + profile.ProfileName + ".png";
            profile.GameInfo = catalog != null &&
                catalog.TryGetValue(profile.ProfileName, out var metadata) && metadata != null
                ? metadata.Clone()
                : JoystickHelper.DeSerializeMetadata(file);
            if (profile.GameInfo == null)
            {
                profile.GameNameInternal = profile.ProfileName + " (Metadata Missing)";
                return;
            }

            profile.GameNameInternal = profile.GameInfo.game_name;
            profile.GameGenreInternal = profile.GameInfo.game_genre;
            if (!string.IsNullOrEmpty(profile.GameInfo.icon_name))
                profile.IconName = "Icons/" + profile.GameInfo.icon_name;
        }

        private static void MergeUserSettings(GameProfile stock, GameProfile user)
        {
            foreach (var oldButton in user.JoystickButtons ?? Enumerable.Empty<JoystickButtons>())
            {
                var button = stock.JoystickButtons?.FirstOrDefault(x => x.ButtonName == oldButton.ButtonName);
                if (button == null && stock.EmulatorType == EmulatorType.TeknoModel2 &&
                    stock.ExecutableName == "desert.zip" && oldButton.ButtonName == "Brake")
                    button = stock.JoystickButtons?.FirstOrDefault(x => x.ButtonName == "Turret");
                if (button == null)
                    continue;

                button.DirectInputButton = oldButton.DirectInputButton;
                button.XInputButton = oldButton.XInputButton;
                button.RawInputButton = oldButton.RawInputButton;
                button.BindNameDi = oldButton.BindNameDi;
                button.BindNameXi = oldButton.BindNameXi;
                button.BindNameRi = oldButton.BindNameRi;
                button.BindName = oldButton.BindName;
                if (button.BindNameRi?.Contains("DolphinBar") == true &&
                    string.IsNullOrWhiteSpace(button.RawInputButton?.DevicePath))
                {
                    button.RawInputButton = new RawInputButton
                    {
                        DevicePath = "",
                        DeviceType = RawDeviceType.None,
                        MouseButton = RawMouseButton.None,
                        KeyboardKey = Keys.None
                    };
                    button.BindNameRi = "";
                }
            }

            var previousFields = (user.ConfigValues ?? new List<FieldInformation>())
                .GroupBy(x => x.FieldName)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            foreach (var field in stock.ConfigValues ?? Enumerable.Empty<FieldInformation>())
                if (previousFields.TryGetValue(field.FieldName, out var previous))
                    field.FieldValue = previous.FieldValue;

            var layout = stock.ConfigValues?.FirstOrDefault(f => f.FieldName == "Screen Layout");
            if (stock.EmulatorType == EmulatorType.TeknoHornet && layout?.FieldValue == "Original" &&
                layout.FieldOptions?.Contains("Dual Screen") == true)
                layout.FieldValue = "Dual Screen";

            var uncentering = stock.ConfigValues?.FirstOrDefault(f => f.FieldName == "Enable Uncentering Effect");
            if (uncentering != null && !previousFields.ContainsKey("Enable Uncentering Effect") &&
                previousFields.TryGetValue("Enable Spring Effect", out var spring))
                uncentering.FieldValue = spring.FieldValue;

            var mode = stock.ConfigValues?.FirstOrDefault(f => f.FieldName == "Uncentering Effect Mode");
            if (mode?.FieldValue == "Sine vibration (experimental)")
                mode.FieldValue = "Sine vibration";
            else if (mode?.FieldValue == "Push away from centre")
                mode.FieldValue = "Push away from center";

            var outputs = stock.ConfigValues?.FirstOrDefault(CabinetOutputSettings.IsOutputField);
            if (outputs != null && !previousFields.Values.Any(CabinetOutputSettings.IsOutputField))
                outputs.FieldValue = CabinetOutputSettings.GetRoute(user);

            stock.CabinetOutputSettings = user.CabinetOutputSettings?.Clone() ?? new CabinetOutputSettings();
            stock.GamePath = user.GamePath;
            stock.GamePath2 = user.GamePath2;
            stock.WineRunnerPath = user.WineRunnerPath;
            stock.WinePrefixMode = user.WinePrefixMode;
            stock.FullscreenScalingMode = user.FullscreenScalingMode;
            stock.AndroidDebugLogging = user.AndroidDebugLogging;
            stock.AndroidDisplayMode = user.AndroidDisplayMode;
        }

        // Android only exposes profiles backed by its shipped runtime.
        private static bool IsVisibleOnThisPlatform(GameProfile profile) =>
            !OperatingSystem.IsAndroid() || PlatformCapabilities.IsAndroidGameProfileSupported(profile);
    }
}
