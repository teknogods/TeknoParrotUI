using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeknoParrotUi.Common;

namespace InputMethodAudit
{
    internal static class ProfileMigrationTest
    {
        public static int Run()
        {
            var originalDirectory = Environment.CurrentDirectory;
            var originalStock = GameProfileLoader.GameProfiles;
            var originalInstalled = GameProfileLoader.UserProfiles;
            var root = Path.Combine(Path.GetTempPath(), "tpui-profile-migration-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "GameProfiles"));
                Directory.CreateDirectory(Path.Combine(root, "UserProfiles"));
                Environment.CurrentDirectory = root;
                var stock = new GameProfile
                {
                    GameProfileRevision = 2,
                    LinuxOk = true,
                    GamescopeGameWindowCompatibility =
                        TeknoParrotUi.Common.Proton.GamescopeGameWindowCompatibility.RequireWindowed,
                    ConfigValues = new List<FieldInformation>
                    {
                        new() { FieldName = "Windowed", FieldValue = "0" }
                    },
                    JoystickButtons = new List<JoystickButtons>
                    {
                        new() { ButtonName = "Start", InputMapping = InputMapping.P1ButtonStart }
                    }
                };
                var user = stock.Clone();
                user.GameProfileRevision = 1;
                user.LinuxOk = false;
                user.GamePath = "user-game-path";
                user.WineRunnerPath = "user-wine";
                user.ConfigValues[0].FieldValue = "1";
                JoystickHelper.SerializeGameProfile(stock,
                    Path.Combine("GameProfiles", "migration.xml"));
                JoystickHelper.SerializeGameProfile(user,
                    Path.Combine("UserProfiles", "migration.xml"));

                GameProfileLoader.LoadProfiles(false);
                var migrated = GameProfileLoader.GameProfiles.Single();
                var installed = GameProfileLoader.UserProfiles.Single();
                Require(migrated.GameProfileRevision == 2, "stock revision retained");
                Require(migrated.GamePath == "user-game-path", "game path migrated");
                Require(migrated.WineRunnerPath == "user-wine", "2.0 runner preference migrated");
                Require(migrated.ConfigValues.Single().FieldValue == "1", "setting migrated");
                Require(migrated.LinuxOk, "stock Linux compatibility retained");
                Require(migrated.GamescopeGameWindowCompatibility ==
                    TeknoParrotUi.Common.Proton.GamescopeGameWindowCompatibility.RequireWindowed,
                    "stock window policy retained");
                Require(!ReferenceEquals(installed, migrated) &&
                    !ReferenceEquals(installed.ConfigValues, migrated.ConfigValues),
                    "library and installed profiles do not share mutable settings");
                var persisted = JoystickHelper.DeSerializeGameProfile(
                    Path.Combine("UserProfiles", "migration.xml"), true);
                Require(persisted.GameProfileRevision == 2, "migration saved to user profile");
                Console.WriteLine("Profile migration: PASS");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("Profile migration failed: " + error.Message);
                return 1;
            }
            finally
            {
                Environment.CurrentDirectory = originalDirectory;
                GameProfileLoader.GameProfiles = originalStock;
                GameProfileLoader.UserProfiles = originalInstalled;
                if (Directory.Exists(root) &&
                    Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                    Directory.Delete(root, recursive: true);
            }
        }

        private static void Require(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name);
        }
    }
}
