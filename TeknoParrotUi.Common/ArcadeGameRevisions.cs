using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace TeknoParrotUi.Common
{
    internal sealed class ArcadeGameRevision
    {
        internal readonly string Id, FamilyId, Name, FamilyName;
        internal ArcadeGameRevision(string id, string familyId, string name, string familyName)
        { Id = id; FamilyId = familyId; Name = name; FamilyName = familyName; }
    }

    /// <summary>
    /// Library grouping keeps persistent profile identities intact. Emulation always
    /// receives a detached, exact revision template. No helper writes profile files.
    /// </summary>
    public static partial class ArcadeGameRevisions
    {
        public const string SettingName = "Game Revision";
        private static readonly Lazy<Dictionary<string, ArcadeGameRevision>> Catalog =
            new Lazy<Dictionary<string, ArcadeGameRevision>>(() =>
            {
                var entries = Mvs.Concat(Cps).Concat(Ss32).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
                foreach (var entry in entries.Values)
                    if (!entries.TryGetValue(entry.FamilyId, out var root) || root.Id != root.FamilyId ||
                        Emulator(entry.Id) != Emulator(root.Id))
                        throw new InvalidDataException("Invalid arcade revision parent: " + entry.Id);
                return entries;
            });

        private sealed class EditSession
        {
            internal string ActiveId;
            internal bool Materialized;
            internal readonly Dictionary<string, GameProfile> Snapshots =
                new Dictionary<string, GameProfile>(StringComparer.OrdinalIgnoreCase);
        }
        private static readonly ConditionalWeakTable<GameProfile, EditSession> Sessions =
            new ConditionalWeakTable<GameProfile, EditSession>();

        // The UI may supply its already-loaded user list. Tests substitute private
        // loaders. Defaults use only validated catalog IDs under the normal folders.
        private static Func<string, GameProfile> stockProfileLoader = id => ReadProfile(id, false);
        private static readonly Dictionary<string, bool> DevelopmentProfiles =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        public static Func<string, GameProfile> StockProfileLoader
        {
            get => stockProfileLoader;
            set { lock (DevelopmentProfiles) { stockProfileLoader = value; DevelopmentProfiles.Clear(); } }
        }
        public static Func<string, GameProfile> UserProfileLoader { get; set; } = id => ReadProfile(id, true);
        public static Func<IEnumerable<GameProfile>> UserProfilesProvider { get; set; } = ReadUserProfiles;

        private static bool ShowDevelopmentProfiles
        {
            get
            {
#if DEBUG
                return true;
#else
                var value = Environment.GetEnvironmentVariable("TPUI_SHOW_DEVONLY_PROFILES");
                return value == "1" || bool.TryParse(value, out var enabled) && enabled;
#endif
            }
        }

        private static bool IsDevelopmentProfile(string id)
        {
            lock (DevelopmentProfiles)
            {
                if (!DevelopmentProfiles.TryGetValue(id, out var development))
                {
                    var template = StockProfileLoader?.Invoke(id);
                    if (template == null) throw new FileNotFoundException("The selected revision template is missing: " + id);
                    development = template.DevOnly;
                    DevelopmentProfiles[id] = development;
                }
                return development;
            }
        }

        private static bool IsVisible(string id) => ShowDevelopmentProfiles || !IsDevelopmentProfile(id);

        private static EmulatorType Emulator(string id) => id.StartsWith("mvs_", StringComparison.Ordinal)
            ? EmulatorType.TeknoMVS : id.StartsWith("cps_", StringComparison.Ordinal)
                ? EmulatorType.TeknoCPS : EmulatorType.TeknoSS32;

        private static ArcadeGameRevision Entry(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !Catalog.Value.TryGetValue(id.Trim(), out var entry))
                throw new ArgumentException("Unknown arcade game revision.");
            return entry;
        }

        public static bool Handles(GameProfile profile) => profile != null &&
            profile.ProfileName != null && Catalog.Value.TryGetValue(profile.ProfileName, out var entry) &&
            profile.EmulatorType == Emulator(entry.Id);

        public static string FamilyId(GameProfile profile) => Handles(profile)
            ? Entry(profile.ProfileName).FamilyId : profile?.ProfileName;
        public static string FamilyName(GameProfile profile) => Handles(profile)
            ? Entry(profile.ProfileName).FamilyName : profile?.GameNameInternal;
        public static bool IsPrimary(GameProfile profile) => !Handles(profile) ||
            string.Equals(profile.ProfileName, FamilyId(profile), StringComparison.OrdinalIgnoreCase);

        public static List<DynamicDropdownOption> GetOptions(GameProfile profile)
        {
            if (!Handles(profile)) return new List<DynamicDropdownOption>();
            var family = FamilyId(profile);
            return Catalog.Value.Values.Where(x => x.FamilyId == family && IsVisible(x.Id))
                .OrderBy(x => x.Id == family ? 0 : 1).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id, StringComparer.Ordinal)
                .Select(x => new DynamicDropdownOption { DisplayName = x.Name, Value = x.Id }).ToList();
        }

        private static FieldInformation RevisionField(GameProfile profile) =>
            profile.ConfigValues?.FirstOrDefault(x => x != null && x.FieldName == SettingName);

        private static void EnsureField(GameProfile profile, string selected)
        {
            if (profile.ConfigValues == null) profile.ConfigValues = new List<FieldInformation>();
            var field = RevisionField(profile);
            if (field == null)
            {
                field = new FieldInformation { CategoryName = "General", FieldName = SettingName };
                profile.ConfigValues.Insert(0, field);
            }
            field.FieldType = FieldType.DynamicDropdown;
            field.DynamicOptions = GetOptions(profile);
            field.FieldValue = selected;
        }

        private static string ValidateSelection(GameProfile profile, string selected)
        {
            if (!Handles(profile)) throw new ArgumentException("This profile has no arcade revision family.");
            var entry = Entry(selected);
            if (!string.Equals(entry.FamilyId, FamilyId(profile), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Invalid Game Revision for this game. Select a revision from this game's family.");
            if (!IsVisible(entry.Id))
                throw new ArgumentException("This game revision is a developer-only profile.");
            return entry.Id;
        }

        public static string ResolveId(GameProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!Handles(profile)) return profile.ProfileName;
            var selected = RevisionField(profile)?.FieldValue;
            return ValidateSelection(profile, string.IsNullOrWhiteSpace(selected) ? profile.ProfileName : selected);
        }

        public static void Populate(GameProfile profile)
        {
            if (!Handles(profile)) return;
            var selected = RevisionField(profile)?.FieldValue;
            if (string.IsNullOrWhiteSpace(selected)) selected = Entry(profile.ProfileName).Id;
            EnsureField(profile, selected);
            profile.GameNameInternal = FamilyName(profile);
            DeriveTestMode(profile);
            // Keep a bad stored selection available for correction in Settings;
            // ResolveId and ApplyRevision reject it before any template/path use.
            string actual;
            try { actual = ValidateSelection(profile, selected); }
            catch (ArgumentException)
            {
                Sessions.GetValue(profile, value => new EditSession { ActiveId = Entry(value.ProfileName).Id, Materialized = true });
                return;
            }
            var exists = Sessions.TryGetValue(profile, out var session);
            if (exists && session.Materialized) return;
            if (session == null) session = new EditSession();
            if (!string.Equals(actual, profile.ProfileName, StringComparison.OrdinalIgnoreCase))
            {
                CopyForEditing(profile, MaterializeSelection(profile, actual));
            }
            else
            {
                // Reload can carry metadata from a canceled settings view. The
                // selected stock revision, not that stale view, owns its metadata.
                var template = Stock(actual);
                profile.GameInfo = template.GameInfo;
                profile.GameGenreInternal = template.GameGenreInternal;
                profile.IconName = template.IconName;
            }
            session.ActiveId = actual;
            session.Materialized = true;
            if (!exists) Sessions.Add(profile, session);
        }

        public static IEnumerable<GameProfile> GetLibraryProfiles(IEnumerable<GameProfile> profiles)
        {
            var source = (profiles ?? Enumerable.Empty<GameProfile>()).Where(x => x != null).ToList();
            var chosen = source.Where(Handles).GroupBy(FamilyId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.OrderBy(x => string.IsNullOrWhiteSpace(x.GamePath) ? 1 : 0)
                    .ThenBy(x => IsPrimary(x) ? 0 : 1)
                    .ThenBy(x => x.ProfileName, StringComparer.OrdinalIgnoreCase).First(), StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var profile in source)
                if (!Handles(profile)) yield return profile;
                else if (seen.Add(FamilyId(profile))) yield return chosen[FamilyId(profile)];
        }

        private static EditSession Session(GameProfile profile, bool forced = false) => Sessions.GetValue(profile,
            value =>
            {
                string active;
                try { active = ResolveId(value); }
                catch (ArgumentException) when (forced) { active = Entry(value.ProfileName).Id; }
                return new EditSession { ActiveId = active,
                    Materialized = string.Equals(active, value.ProfileName, StringComparison.OrdinalIgnoreCase) };
            });

        public static void ApplyRevision(GameProfile profile, string selectedId)
        {
            var selected = ValidateSelection(profile, selectedId); // Reject without mutation.
            var session = Session(profile);
            lock (session)
            {
                var outgoing = profile.Clone();
                EnsureField(outgoing, session.ActiveId);
                // Complete loading/validation before mutating the object or session.
                var incoming = Build(profile, selected, session, outgoing);
                session.Snapshots[session.ActiveId] = outgoing;
                CopyForEditing(profile, incoming);
                session.ActiveId = selected;
                session.Materialized = true;
            }
        }

        public static GameProfile CreateLaunchProfile(GameProfile profile, string forcedId = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!Handles(profile))
            {
                if (forcedId != null) throw new ArgumentException("Cannot force an arcade revision on another emulator.");
                return profile.Clone();
            }
            var selected = forcedId == null ? ResolveId(profile) : ValidateSelection(profile, forcedId);
            var session = Session(profile, forcedId != null);
            lock (session)
                return Build(profile, selected, session, profile.Clone());
        }

        private static GameProfile Build(GameProfile owner, string selected, EditSession session, GameProfile current)
        {
            var target = Stock(selected);
            var same = string.Equals(session.ActiveId, selected, StringComparison.OrdinalIgnoreCase);
            if (same && !session.Materialized) return MaterializeSelection(current, selected);
            GameProfile preferred = null;
            if (same) preferred = current;
            else if (session.Snapshots.TryGetValue(selected, out var snapshot)) preferred = snapshot;
            else
            {
                var saved = Saved(selected);
                if (HasSelection(saved, selected))
                    preferred = saved;
            }
            // Cross-revision fallback copies shared host choices, not another
            // game's physical DIP defaults. A saved/session target wins afterward.
            MergeSettings(target, current, same);
            if (preferred != null && !ReferenceEquals(preferred, current)) MergeSettings(target, preferred, true);
            target.ProfileName = selected;
            target.FileName = Path.Combine("GameProfiles", selected + ".xml");
            target.GameNameInternal = Entry(selected).Name;
            EnsureField(target, selected);
            return target;
        }

        private static bool HasSelection(GameProfile profile, string selected)
        {
            if (profile == null) return false;
            try { return string.Equals(ResolveId(profile), selected, StringComparison.OrdinalIgnoreCase); }
            catch (ArgumentException) { return false; }
        }

        private static GameProfile MaterializeSelection(GameProfile owner, string selected)
        {
            var target = Stock(selected);
            // A root-template schema migration can omit clone-only controls and
            // inject root DIP defaults. Recover the untouched selected/owner files
            // instead of interpreting those root defaults as edited child DIPs.
            MergeSettings(target, owner, false);
            var savedTarget = Saved(selected);
            if (HasSelection(savedTarget, selected)) MergeSettings(target, savedTarget, true);
            var savedOwner = Saved(owner.ProfileName);
            if (HasSelection(savedOwner, selected)) MergeSettings(target, savedOwner, true);
            if (string.IsNullOrWhiteSpace(target.GamePath))
                target.GamePath = !string.IsNullOrWhiteSpace(owner.GamePath) ? owner.GamePath : savedTarget?.GamePath;
            target.ProfileName = selected;
            target.FileName = Path.Combine("GameProfiles", selected + ".xml");
            target.GameNameInternal = Entry(selected).Name;
            EnsureField(target, selected);
            return target;
        }

        public static GameProfile LoadOnlineProfile(string id)
        {
            var selected = Entry(id);
            var exact = Saved(selected.Id);
            var profiles = (UserProfilesProvider?.Invoke() ?? Enumerable.Empty<GameProfile>())
                .Where(x => Handles(x) && FamilyId(x) == selected.FamilyId).ToList();
            if (exact != null) profiles.Insert(0, exact);
            var source = profiles.Where(x => !string.IsNullOrWhiteSpace(x.GamePath))
                .OrderBy(x => string.Equals(x.ProfileName, selected.Id, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(x => IsPrimary(x) ? 0 : 1).ThenBy(x => x.ProfileName, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            // No user path is invented. Launchers present their existing ROM-path
            // error when the family has never been configured.
            var result = CreateLaunchProfile(source ?? exact ?? Stock(selected.Id), selected.Id);
            if (string.IsNullOrWhiteSpace(result.GamePath) && source != null) result.GamePath = source.GamePath;
            return result;
        }

        private static void MergeSettings(GameProfile target, GameProfile source, bool sameRevision)
        {
            var copy = source.Clone();
            foreach (var field in target.ConfigValues ?? Enumerable.Empty<FieldInformation>())
            {
                if (field == null || field.FieldName == SettingName ||
                    (!sameRevision && field.CategoryName != "General" && field.CategoryName != "Video")) continue;
                var old = copy.ConfigValues?.FirstOrDefault(x => x != null &&
                    x.CategoryName == field.CategoryName && x.FieldName == field.FieldName);
                if (old != null) field.FieldValue = old.FieldValue;
            }
            foreach (var button in target.JoystickButtons ?? Enumerable.Empty<JoystickButtons>())
            {
                if (button == null) continue;
                var old = copy.JoystickButtons?.FirstOrDefault(x => x != null && x.ButtonName == button.ButtonName);
                if (old == null) continue;
                button.DirectInputButton = old.DirectInputButton; button.XInputButton = old.XInputButton;
                button.RawInputButton = old.RawInputButton; button.BindName = old.BindName;
                button.BindNameDi = old.BindNameDi; button.BindNameXi = old.BindNameXi; button.BindNameRi = old.BindNameRi;
            }
            if (sameRevision || !string.IsNullOrWhiteSpace(copy.GamePath)) target.GamePath = copy.GamePath;
            if (sameRevision || !string.IsNullOrWhiteSpace(copy.GamePath2)) target.GamePath2 = copy.GamePath2;
            target.CustomArguments = copy.CustomArguments;
            target.Rotary1Sensitivity = copy.Rotary1Sensitivity; target.Rotary2Sensitivity = copy.Rotary2Sensitivity;
            target.Rotary3Sensitivity = copy.Rotary3Sensitivity; target.Rotary4Sensitivity = copy.Rotary4Sensitivity;
            target.Rotary1Increment = copy.Rotary1Increment; target.Rotary2Increment = copy.Rotary2Increment;
            target.Rotary3Increment = copy.Rotary3Increment; target.Rotary4Increment = copy.Rotary4Increment;
            target.CabinetOutputSettings = copy.CabinetOutputSettings;
            target.WineRunnerPath = copy.WineRunnerPath;
            target.WinePrefixMode = copy.WinePrefixMode;
            target.FullscreenScalingMode = copy.FullscreenScalingMode;
            target.ProtonVersion = copy.ProtonVersion;
            target.AndroidDebugLogging = copy.AndroidDebugLogging;
            target.AndroidDisplayMode = copy.AndroidDisplayMode;
        }

        private static void CopyForEditing(GameProfile destination, GameProfile incoming)
        {
            var id = destination.ProfileName; var file = destination.FileName; var heading = FamilyName(destination);
            var copy = incoming.Clone();
            foreach (var property in typeof(GameProfile).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                    property.SetValue(destination, property.GetValue(copy, null), null);
            destination.ProfileName = id; destination.FileName = file; destination.GameNameInternal = heading;
            // This flag controls whether the persistent library owner is loaded.
            // A developer-selected child must not hide its ordinary parent later.
            destination.DevOnly = IsDevelopmentProfile(id);
            EnsureField(destination, incoming.ProfileName);
        }

        private static GameProfile Stock(string id)
        {
            var entry = Entry(id);
            var profile = StockProfileLoader?.Invoke(entry.Id)?.Clone();
            if (profile == null) throw new FileNotFoundException("The selected revision template is missing: " + entry.Id);
            profile.ProfileName = entry.Id;
            if (!Handles(profile)) throw new InvalidDataException("Revision template uses the wrong emulator: " + entry.Id);
            DeriveTestMode(profile);
            EnsureField(profile, entry.Id);
            return profile;
        }

        private static void DeriveTestMode(GameProfile profile) => profile.HasSeparateTestMode =
            profile.HasSeparateTestMode || profile.TestMenuIsExecutable || !string.IsNullOrWhiteSpace(profile.TestMenuParameter);

        private static GameProfile Saved(string id)
        {
            var entry = Entry(id);
            var profile = UserProfileLoader?.Invoke(entry.Id)?.Clone();
            if (profile == null) return null;
            profile.ProfileName = entry.Id;
            if (!Handles(profile)) throw new InvalidDataException("Saved revision uses the wrong emulator: " + entry.Id);
            return profile;
        }

        private static GameProfile ReadProfile(string id, bool user)
        {
            var entry = Entry(id);
            var path = Path.Combine(user ? "UserProfiles" : "GameProfiles", entry.Id + ".xml");
            if (!File.Exists(path)) return null;
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            GameProfile profile;
            using (var reader = XmlReader.Create(path, settings))
                profile = (GameProfile)new XmlSerializer(typeof(GameProfile)).Deserialize(reader);
            profile.ProfileName = entry.Id; profile.FileName = path;
            if (user) InputListening.ProfileStorage.BindingsStore.Apply(profile);
            var metadata = Path.Combine("Metadata", entry.Id + ".json");
            if (File.Exists(metadata)) profile.GameInfo = JsonConvert.DeserializeObject<Metadata>(File.ReadAllText(metadata));
            profile.GameNameInternal = profile.GameInfo?.game_name ?? entry.Name;
            profile.GameGenreInternal = profile.GameInfo?.game_genre;
            profile.IconName = "Icons/" + (string.IsNullOrEmpty(profile.GameInfo?.icon_name) ? entry.Id + ".png" : profile.GameInfo.icon_name);
            return profile;
        }

        private static IEnumerable<GameProfile> ReadUserProfiles()
        {
            if (!Directory.Exists("UserProfiles")) yield break;
            foreach (var path in Directory.GetFiles("UserProfiles", "*.xml").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                if (Catalog.Value.ContainsKey(Path.GetFileNameWithoutExtension(path)))
                {
                    var profile = Saved(Path.GetFileNameWithoutExtension(path));
                    if (profile != null) yield return profile;
                }
        }
    }
}
