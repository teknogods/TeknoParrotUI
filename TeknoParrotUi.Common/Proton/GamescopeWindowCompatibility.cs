using System;
using System.Collections.Generic;
using System.Linq;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Profile-driven Gamescope window-mode compatibility policy. Some old
    /// exclusive-fullscreen Direct3D games fail device creation
    /// ("Create3DDevice") inside a nested Gamescope session because the
    /// virtual display only exposes Gamescope's output mode while the game
    /// requests its own exclusive-fullscreen mode. Running the same game
    /// WINDOWED inside Gamescope works - `-S fit -f` still presents it
    /// fullscreen. This is metadata on the game PROFILE (never a game-name
    /// string comparison in GameSession, never a resolution database).
    /// </summary>
    public enum GamescopeGameWindowCompatibility
    {
        /// <summary>No known constraint - launch with the user's saved window mode.</summary>
        Default = 0,

        /// <summary>
        /// Verified: the game's exclusive-fullscreen mode fails under nested
        /// Gamescope. When AutomaticFit is effective, a launch-time-only
        /// windowed override is written to the game config (the user's saved
        /// setting is never modified).
        /// </summary>
        RequireWindowed = 1,

        /// <summary>Verified: the game cannot run under Gamescope at all - wrapping is skipped.</summary>
        Unsupported = 2
    }

    /// <summary>Result of <see cref="GamescopeWindowCompatibilityPolicy.Plan"/>.</summary>
    public sealed class GamescopeCompatibilityOverride
    {
        public bool OverrideApplied { get; init; }
        /// <summary>Launch-time copy carrying the windowed override. Null when no override applies.</summary>
        public GameProfile EffectiveProfile { get; init; }
        public string ProfileName { get; init; } = string.Empty;
        public GamescopeGameWindowCompatibility Policy { get; init; }
        public string SavedGameMode { get; init; } = string.Empty;
        public string EffectiveGameMode { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;

        public string ToLogBlock() =>
            "[GamescopeCompatibility]\n" +
            $"Profile: {ProfileName}\n" +
            $"Policy: {Policy}\n" +
            $"SavedGameMode: {SavedGameMode}\n" +
            $"EffectiveGameMode: {EffectiveGameMode}\n" +
            $"Reason: {Reason}\n" +
            "PersistentSettingModified: false";
    }

    /// <summary>
    /// Pure policy: decides whether a launch-time windowed override applies
    /// and builds the effective (temporary) profile copy. The SAVED profile
    /// object is never mutated; the copy exists only for writing this
    /// launch's teknoparrot.ini. No resolution data is involved anywhere.
    /// </summary>
    public static class GamescopeWindowCompatibilityPolicy
    {
        /// <summary>The existing per-game config field that selects windowed (1) vs fullscreen (0).</summary>
        public const string WindowedFieldName = "Windowed";

        public static GamescopeCompatibilityOverride Plan(GameProfile profile, bool automaticFitEffective)
        {
            var name = profile?.GameNameInternal;
            if (string.IsNullOrEmpty(name))
                name = profile?.ProfileName ?? "(unknown)";
            var policy = profile?.GamescopeGameWindowCompatibility ?? GamescopeGameWindowCompatibility.Default;

            if (profile == null || !automaticFitEffective || policy != GamescopeGameWindowCompatibility.RequireWindowed)
            {
                return new GamescopeCompatibilityOverride
                {
                    OverrideApplied = false,
                    ProfileName = name,
                    Policy = policy,
                    Reason = !automaticFitEffective
                        ? "AutomaticFit not effective - saved window mode preserved."
                        : "No RequireWindowed policy on this profile."
                };
            }

            var field = profile.ConfigValues?.FirstOrDefault(f => f.FieldName == WindowedFieldName);
            if (field == null)
            {
                return new GamescopeCompatibilityOverride
                {
                    OverrideApplied = false,
                    ProfileName = name,
                    Policy = policy,
                    Reason = $"Profile has no '{WindowedFieldName}' config field - nothing to override."
                };
            }

            if (field.FieldValue == "1")
            {
                return new GamescopeCompatibilityOverride
                {
                    OverrideApplied = false,
                    ProfileName = name,
                    Policy = policy,
                    SavedGameMode = "Windowed",
                    EffectiveGameMode = "Windowed",
                    Reason = "Game already configured windowed - no override needed."
                };
            }

            var effective = CreateEffectiveLaunchProfile(profile);
            effective.ConfigValues.First(f => f.FieldName == WindowedFieldName).FieldValue = "1";

            return new GamescopeCompatibilityOverride
            {
                OverrideApplied = true,
                EffectiveProfile = effective,
                ProfileName = name,
                Policy = policy,
                SavedGameMode = "Fullscreen",
                EffectiveGameMode = "Windowed",
                Reason = "Exclusive fullscreen Create3DDevice failure under nested Gamescope"
            };
        }

        /// <summary>
        /// Launch-time copy of the game configuration carrying everything the
        /// config-ini writer reads. ConfigValues are DEEP-copied so overriding
        /// a field never touches the saved profile object.
        /// </summary>
        public static GameProfile CreateEffectiveLaunchProfile(GameProfile saved)
        {
            return new GameProfile
            {
                ProfileName = saved.ProfileName,
                GameNameInternal = saved.GameNameInternal,
                GameVersion = saved.GameVersion,
                EmulatorType = saved.EmulatorType,
                EmulationProfile = saved.EmulationProfile,
                GamescopeGameWindowCompatibility = saved.GamescopeGameWindowCompatibility,
                ConfigValues = (saved.ConfigValues ?? new List<FieldInformation>())
                    .Select(f => new FieldInformation
                    {
                        CategoryName = f.CategoryName,
                        FieldName = f.FieldName,
                        FieldValue = f.FieldValue,
                        FieldType = f.FieldType,
                        FieldMin = f.FieldMin,
                        FieldMax = f.FieldMax,
                        FieldStep = f.FieldStep,
                        FieldOptions = f.FieldOptions?.ToList(),
                        Hint = f.Hint,
                        UseUnitySorting = f.UseUnitySorting
                    })
                    .ToList()
            };
        }
    }
}
