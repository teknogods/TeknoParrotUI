using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.GameLaunch;

namespace TeknoParrotUi.Common.Online
{
    /// <summary>
    /// Golden Tee online: the PCB ID and Card ID from the teknoparrot.com account (api/User/Profile, kept in ParrotData),
    /// and GT on TP, the golfer and equipment editor installed by the updater into Tools\GTonTP. GT on TP reads the PCB ID,
    /// Card ID and [ITNet] Address from the teknoparrot.ini next to the game's game.bin, so launching it writes that first.
    /// </summary>
    public static class GoldenTeeOnlineHelper
    {
        public const string PcbIdField = "PCB ID";
        public const string CardIdField = "Card ID";

        /// <summary>GT on TP's executable, next to TeknoParrotUi.exe in Tools\GTonTP.</summary>
        public static string ToolPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "GTonTP", "GTTPEditor.exe");

        public static bool IsToolInstalled => File.Exists(ToolPath);

        /// <summary>
        /// The GoldenTee case of the online ID auto-fill (JoystickHelper.AutoFillOnlineId, AddGame): fills the PCB ID and the
        /// Card ID field (OnlineIdFieldName) from the account, each only while it is empty. True when a field changed.
        /// </summary>
        public static bool AutoFill(GameProfile profile)
        {
            if (profile?.ConfigValues == null)
                return false;
            bool changed = Fill(profile, PcbIdField, Lazydata.ParrotData.GoldenTeePcbId);
            var cardField = string.IsNullOrEmpty(profile.OnlineIdFieldName) ? CardIdField : profile.OnlineIdFieldName;
            return Fill(profile, cardField, Lazydata.ParrotData.GoldenTeeCardId) | changed;
        }

        private static bool Fill(GameProfile profile, string fieldName, string value)
        {
            var field = profile.ConfigValues.FirstOrDefault(x => x.FieldName == fieldName);
            if (field == null || string.IsNullOrEmpty(value) || !string.IsNullOrWhiteSpace(field.FieldValue))
                return false;
            field.FieldValue = value;
            return true;
        }

        /// <summary>
        /// Opens GT on TP for the profile's game: fills in the account's IDs (saving the profile when they change), writes
        /// teknoparrot.ini next to the game like a launch does, and starts GT on TP pointed at the game. Returns an error to
        /// show, or null when GT on TP started.
        /// </summary>
        public static string LaunchTool(GameProfile profile)
        {
            if (!IsToolInstalled)
                return OnlineText.Get("LibraryGtOnTpNotInstalled");
            if (string.IsNullOrEmpty(profile.GamePath))
                return OnlineText.Get("LibraryGameLocationNotSet");
            var gamePath = Path.GetFullPath(profile.GamePath);
            if (!File.Exists(gamePath))
                return string.Format(OnlineText.Get("LibraryCantFindGame"), gamePath);

            if (AutoFill(profile))
                JoystickHelper.SerializeGameProfile(profile);

            try
            {
                TeknoParrotIniWriter.WriteConfigIni(profile, gamePath, null, false);
                Process.Start(new ProcessStartInfo(ToolPath, $"--game \"{gamePath}\"")
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(ToolPath)
                });
                return null;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"GT on TP failed to start: {exception}");
                return string.Format(OnlineText.Get("LibraryGtOnTpFailed"), exception.Message);
            }
        }
    }
}
