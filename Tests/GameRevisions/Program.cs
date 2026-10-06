using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Serialization;
using TeknoParrotUi.Common;

internal static class Program
{
    private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(GameProfile));
    private static Dictionary<string, GameProfile> templates;
    private static readonly Dictionary<string, GameProfile> saved = new Dictionary<string, GameProfile>();
    private static int assertions;
    private static void Check(bool value, string message)
    {
        assertions++;
        if (!value) throw new InvalidOperationException(message);
    }
    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (ArgumentException) { assertions++; return; }
        throw new InvalidOperationException(message);
    }
    private static GameProfile Profile(string id) => templates[id].Clone();
    private static FieldInformation Field(GameProfile p, string name) => p.ConfigValues.Single(x => x.FieldName == name);
    private static void Select(GameProfile p, string id) => Field(p, ArcadeGameRevisions.SettingName).FieldValue = id;
    private static string Fingerprint(GameProfile p)
    {
        using (var text = new StringWriter()) { Serializer.Serialize(text, p); return text + "|" + p.FileName + "|" + p.GameInfo?.game_name; }
    }
    private static string Digest(string path)
    {
        using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));
    }
    private static string Controls(GameProfile p) => string.Join("|", p.JoystickButtons.Select(x =>
        x.ButtonName + ":" + x.InputMapping + ":" + x.AnalogType + ":" + x.VisibleWhen + ":" + x.VisibleWhenValue));

    private static void CatalogChecks()
    {
        var groups = templates.Values.GroupBy(ArcadeGameRevisions.FamilyId).ToList();
        Check(templates.Count == 995 && groups.Count == 275, "Catalog coverage changed unexpectedly");
        foreach (var count in new[] { Tuple.Create("mvs_", 287, 164), Tuple.Create("cps_", 641, 87), Tuple.Create("ss32_", 67, 24) })
        {
            var members = templates.Values.Where(x => x.ProfileName.StartsWith(count.Item1)).ToList();
            Check(members.Count == count.Item2, "System catalog count " + count.Item1);
            Check(members.Select(ArcadeGameRevisions.FamilyId).Distinct().Count() == count.Item3, "System family count " + count.Item1);
        }
        foreach (var group in groups)
        {
            var root = Profile(group.Key);
            Check(ArcadeGameRevisions.IsPrimary(root), "Canonical root " + group.Key);
            var options = ArcadeGameRevisions.GetOptions(root);
            Check(options.First().Value == group.Key, "Canonical option first " + group.Key);
            Check(options.Select(x => x.Value).OrderBy(x => x).SequenceEqual(group.Select(x => x.ProfileName).OrderBy(x => x)), "Authoritative family members " + group.Key);
            foreach (var expected in group)
            {
                Check(ArcadeGameRevisions.Handles(expected), "Handled " + expected.ProfileName);
                var actual = ArcadeGameRevisions.CreateLaunchProfile(root, expected.ProfileName);
                Check(actual.ProfileName == expected.ProfileName && ArcadeGameRevisions.ResolveId(actual) == expected.ProfileName, "Exact launch ID " + expected.ProfileName);
                Check(Controls(actual) == Controls(expected), "Exact controls/mapping/analog schema " + expected.ProfileName);
                Check(actual.DevOnly == expected.DevOnly && actual.HasTpoSupport == expected.HasTpoSupport, "Exact eligibility " + expected.ProfileName);
                Check(actual.ExecutableName == expected.ExecutableName && actual.EmulationProfile == expected.EmulationProfile && actual.GameProfileRevision == expected.GameProfileRevision, "Exact template executable/version " + expected.ProfileName);
                Check(actual.TestMenuParameter == expected.TestMenuParameter && actual.HasSeparateTestMode == (expected.HasSeparateTestMode || expected.TestMenuIsExecutable || !string.IsNullOrWhiteSpace(expected.TestMenuParameter)), "Exact operator launch " + expected.ProfileName);
                Check(actual.xAxisMin == expected.xAxisMin && actual.xAxisMax == expected.xAxisMax && actual.yAxisMin == expected.yAxisMin && actual.yAxisMax == expected.yAxisMax && actual.Use16BitAnalog == expected.Use16BitAnalog, "Exact axis configuration " + expected.ProfileName);
                Check(actual.GameInfo?.game_name == expected.GameInfo?.game_name && actual.GameInfo?.general_issues == expected.GameInfo?.general_issues, "Exact metadata " + expected.ProfileName);
                foreach (var dip in expected.ConfigValues.Where(x => x.CategoryName != "General"))
                    Check(Field(actual, dip.FieldName).FieldValue == dip.FieldValue, "Variant hardware defaults " + expected.ProfileName + "/" + dip.FieldName);
                var repeat = ArcadeGameRevisions.CreateLaunchProfile(actual);
                Check(Fingerprint(repeat) == Fingerprint(actual), "Repeated launch is idempotent " + expected.ProfileName);
            }
        }
        Check(ArcadeGameRevisions.FamilyId(Profile("cps_ssf2tb")) == "cps_ssf2", "Tournament Battle authoritative family");
        var tournament = ArcadeGameRevisions.CreateLaunchProfile(Profile("cps_ssf2"), "cps_ssf2tb");
        Check(tournament.HasTpoSupport && tournament.GameInfo.general_issues.Contains("ssf2tb.zip"), "Tournament Battle keeps its multiplayer eligibility and exact ROM requirements");
        var unrelated = new GameProfile { ProfileName = "unrelated", GameNameInternal = "Unrelated", ConfigValues = new List<FieldInformation>() };
        Check(!ArcadeGameRevisions.Handles(unrelated) && ArcadeGameRevisions.ResolveId(unrelated) == "unrelated", "Unrelated emulator stays outside helper");
        Check(ArcadeGameRevisions.CreateLaunchProfile(unrelated) != unrelated, "Unrelated launch clone is detached");
        Reject(() => ArcadeGameRevisions.CreateLaunchProfile(unrelated, "cps_ssf2"), "Forced wrong emulator accepted");
        Console.WriteLine("All 995 exact revision templates and 275 authoritative families passed.");
    }

    private static void SwitchChecks()
    {
        saved.Clear();
        var owner = Profile("mvs_kizuna");
        owner.FileName = "UserProfiles/mvs_kizuna.xml";
        owner.GamePath = "C:/media/base.zip";
        ArcadeGameRevisions.Populate(owner);
        Check(owner.HasSeparateTestMode, "Root Test button without revision switch");
        var heading = owner.GameNameInternal;
        Field(owner, "DIP Switch 3").FieldValue = "On";
        owner.JoystickButtons[0].DirectInputButton = new JoystickButton { Button = 7 };
        owner.JoystickButtons[0].BindName = "root custom";
        Select(owner, "mvs_kizuna4p"); // WPF binding changes before the selection event.
        ArcadeGameRevisions.ApplyRevision(owner, "mvs_kizuna4p");
        Check(owner.ProfileName == "mvs_kizuna" && owner.FileName == "UserProfiles/mvs_kizuna.xml" && owner.GameNameInternal == heading, "Persistent library identity survives switch");
        Check(Field(owner, "DIP Switch 2").FieldValue == "On" && Field(owner, "DIP Switch 3").FieldValue == "Off", "First switch uses 4-player DIP defaults");
        Check(Controls(owner) == Controls(Profile("mvs_kizuna4p")), "Four-player controls loaded");
        Check(owner.JoystickButtons[0].DirectInputButton.Button == 7, "Common host binding inherited");
        owner.GamePath = "C:/media/four.zip";
        Field(owner, "DIP Switch 4").FieldValue = "On";
        owner.JoystickButtons[0].DirectInputButton.Button = 9;
        owner.JoystickButtons.Last().BindName = "four-only";
        Select(owner, "mvs_kizuna"); ArcadeGameRevisions.ApplyRevision(owner, "mvs_kizuna");
        Check(Field(owner, "DIP Switch 3").FieldValue == "On" && Field(owner, "DIP Switch 2").FieldValue == "Off", "Root edited DIPs restored");
        Check(owner.GamePath == "C:/media/base.zip" && owner.JoystickButtons[0].DirectInputButton.Button == 7, "Root media and binding restored");
        Select(owner, "mvs_kizuna4p"); ArcadeGameRevisions.ApplyRevision(owner, "mvs_kizuna4p");
        Check(Field(owner, "DIP Switch 4").FieldValue == "On" && owner.GamePath == "C:/media/four.zip", "Child edited DIPs and media restored");
        Check(owner.JoystickButtons.Last().BindName == "four-only" && owner.JoystickButtons[0].DirectInputButton.Button == 9, "Child-specific bindings restored");
        owner.GamePath = "";
        ArcadeGameRevisions.ApplyRevision(owner, "mvs_kizuna"); ArcadeGameRevisions.ApplyRevision(owner, "mvs_kizuna4p");
        Check(owner.GamePath == "", "Explicitly cleared target path stays cleared");
        var before = Fingerprint(owner);
        Reject(() => ArcadeGameRevisions.ApplyRevision(owner, "mvs_mslug"), "Wrong family switch accepted");
        Reject(() => ArcadeGameRevisions.CreateLaunchProfile(owner, "cps_ssf2"), "Wrong family forced launch accepted");
        Reject(() => ArcadeGameRevisions.ApplyRevision(owner, "../mvs_kizuna"), "Path injection accepted");
        Check(before == Fingerprint(owner), "Invalid switch mutated owner");
        var launched = ArcadeGameRevisions.CreateLaunchProfile(owner);
        launched.JoystickButtons[0].DirectInputButton.Button = 100;
        Field(launched, "DIP Switch 4").FieldValue = "Off";
        launched.GameInfo.game_name = "modified detached metadata";
        Check(before == Fingerprint(owner), "Launch clone shares mutable settings/metadata/bindings");

        var invalid = Profile("mvs_kizuna");
        Select(invalid, "bogus"); ArcadeGameRevisions.Populate(invalid);
        Select(invalid, "mvs_kizuna4p"); ArcadeGameRevisions.ApplyRevision(invalid, "mvs_kizuna4p");
        Check(Field(invalid, "DIP Switch 2").FieldValue == "On" && Controls(invalid) == Controls(Profile("mvs_kizuna4p")), "Invalid stored selection can be corrected without root DIP bleed");
        var blank = Profile("mvs_kizuna"); Select(blank, "");
        Check(ArcadeGameRevisions.ResolveId(blank) == "mvs_kizuna", "Blank revision defaults to persistent ID");
        Console.WriteLine("Per-revision edit sessions, invalid selections and detached launches passed.");
    }

    private static void SavedAndReloadChecks()
    {
        saved.Clear();
        var child = Profile("mvs_kizuna4p");
        child.GamePath = "D:/four.zip";
        Field(child, "DIP Switch 5").FieldValue = "On";
        child.JoystickButtons.Last().BindName = "saved-four-only";
        saved.Add(child.ProfileName, child);
        var untouched = Fingerprint(child);
        var root = Profile("mvs_kizuna"); root.GamePath = "D:/base.zip"; ArcadeGameRevisions.Populate(root);
        ArcadeGameRevisions.ApplyRevision(root, child.ProfileName);
        Check(root.GamePath == child.GamePath && Field(root, "DIP Switch 5").FieldValue == "On" && root.JoystickButtons.Last().BindName == "saved-four-only", "Saved target settings win over family fallback");
        Check(Fingerprint(child) == untouched, "Saved child object mutated");
        var unconfiguredRoot = Profile("mvs_kizuna"); Select(unconfiguredRoot, "mvs_kizuna4p");
        Check(ArcadeGameRevisions.CreateLaunchProfile(unconfiguredRoot).GamePath == child.GamePath, "Unmaterialized root launch uses configured selected-variant path");
        ArcadeGameRevisions.Populate(unconfiguredRoot);
        Check(unconfiguredRoot.GamePath == child.GamePath && Field(unconfiguredRoot, "DIP Switch 5").FieldValue == "On", "Populate uses configured selected-variant path and settings");

        // Simulate a saved root currently using a child, followed by root-template
        // schema migration in the existing profile loader.
        var ownerSaved = child.Clone(); ownerSaved.ProfileName = "mvs_kizuna";
        Select(ownerSaved, "mvs_kizuna4p"); saved["mvs_kizuna"] = ownerSaved;
        var migrated = Profile("mvs_kizuna"); Select(migrated, "mvs_kizuna4p");
        ArcadeGameRevisions.Populate(migrated);
        Check(Field(migrated, "DIP Switch 2").FieldValue == "On" && Field(migrated, "DIP Switch 5").FieldValue == "On", "Reload recovers child defaults and saved DIPs, not migrated root defaults");
        Check(Controls(migrated) == Controls(child) && migrated.JoystickButtons.Last().BindName == "saved-four-only", "Reload recovers omitted child-only controls");
        Check(migrated.GameInfo.game_name == child.GameInfo.game_name, "Reload applies selected metadata");
        var cancelReload = Profile("mvs_kizuna"); cancelReload.GameInfo = child.GameInfo.CloneForTest();
        ArcadeGameRevisions.Populate(cancelReload);
        Check(cancelReload.GameInfo.game_name == templates["mvs_kizuna"].GameInfo.game_name, "Cancel/reload restores root metadata");
        saved.Clear();

        var cps = Profile("cps_ffight"); ArcadeGameRevisions.Populate(cps);
        ArcadeGameRevisions.ApplyRevision(cps, "cps_ffightae");
        Check(Controls(cps) == Controls(Profile("cps_ffightae")) && cps.JoystickButtons.Any(x => x.ButtonName.StartsWith("Player 3")), "Final Fight AE acquires its third player controls");
        ArcadeGameRevisions.ApplyRevision(cps, "cps_ffight");
        Check(!cps.JoystickButtons.Any(x => x.ButtonName.StartsWith("Player 3")), "Final Fight base restores two-player schema");
        Console.WriteLine("Saved target precedence, schema migration and canceled metadata reload passed.");
    }

    private static void OnlineAndLibraryChecks()
    {
        saved.Clear();
        var root = Profile("mvs_kizuna"); root.GamePath = "R:/family.zip"; Select(root, "mvs_kizuna4p");
        saved[root.ProfileName] = root;
        var original = Fingerprint(root);
        var online = ArcadeGameRevisions.LoadOnlineProfile("mvs_kizuna");
        Check(online.ProfileName == "mvs_kizuna" && ArcadeGameRevisions.ResolveId(online) == "mvs_kizuna", "Online exact ID overrides offline child");
        Check(online.GamePath == root.GamePath && Controls(online) == Controls(templates["mvs_kizuna"]), "Online family media with exact controls");
        var target = ArcadeGameRevisions.LoadOnlineProfile("mvs_kizuna4p");
        Check(target.ProfileName == "mvs_kizuna4p" && target.GamePath == root.GamePath, "Online clone without clone user XML falls back to family media");
        Check(original == Fingerprint(root), "Online resolution changed saved root");
        var exact = Profile("mvs_kizuna4p"); exact.GamePath = "R:/exact.zip"; saved[exact.ProfileName] = exact;
        Check(ArcadeGameRevisions.LoadOnlineProfile(exact.ProfileName).GamePath == exact.GamePath, "Online exact media takes precedence");
        exact.GamePath = "";
        Check(ArcadeGameRevisions.LoadOnlineProfile(exact.ProfileName).GamePath == root.GamePath, "Blank exact media falls back to configured family");
        Select(root, "mvs_kizuna"); Select(exact, "bogus");
        Check(ArcadeGameRevisions.LoadOnlineProfile(exact.ProfileName).GamePath == root.GamePath, "Forced room ignores invalid target-file offline revision");
        Select(root, "cps_ssf2");
        Check(ArcadeGameRevisions.LoadOnlineProfile("mvs_kizuna").ProfileName == "mvs_kizuna", "Valid forced online ID overrides corrupt offline selection");
        Reject(() => ArcadeGameRevisions.ResolveId(root), "Wrong-family stored revision accepted offline");
        Reject(() => ArcadeGameRevisions.LoadOnlineProfile("../../user"), "Unknown online path accepted");
        saved.Clear();
        Check(string.IsNullOrWhiteSpace(ArcadeGameRevisions.LoadOnlineProfile("mvs_kizuna4p").GamePath), "Never invent a media path");

        var blankRoot = Profile("mvs_kizuna");
        var configuredChild = Profile("mvs_kizuna4p"); configuredChild.GamePath = "R:/old-child.zip";
        Check(ReferenceEquals(ArcadeGameRevisions.GetLibraryProfiles(new[] { blankRoot, configuredChild }).Single(), configuredChild), "Blank root must not shadow configured legacy clone");
        blankRoot.GamePath = "R:/root.zip";
        Check(ReferenceEquals(ArcadeGameRevisions.GetLibraryProfiles(new[] { configuredChild, blankRoot }).Single(), blankRoot), "Configured canonical representative preferred");
        Check(ReferenceEquals(ArcadeGameRevisions.GetLibraryProfiles(new[] { configuredChild }).Single(), configuredChild), "Existing clone-only library preserved");
        var ordinary = new GameProfile { ProfileName = "ordinary" };
        Check(ArcadeGameRevisions.GetLibraryProfiles(new[] { ordinary, configuredChild, blankRoot }).Count() == 2, "Unrelated library entries retained");
        Console.WriteLine("Forced online identity, configured media fallback and library deduplication passed.");
    }

    private static void VisibilityChecks()
    {
        saved.Clear();
        Environment.SetEnvironmentVariable("TPUI_SHOW_DEVONLY_PROFILES", null);
        var root = Profile("mvs_stakwin");
        Check(!ArcadeGameRevisions.GetOptions(root).Any(x => x.Value == "mvs_stakwindev"), "Developer-only child leaked into normal options (use a Release Common assembly)");
        var before = Fingerprint(root);
        Reject(() => ArcadeGameRevisions.ApplyRevision(root, "mvs_stakwindev"), "Hidden developer revision switch allowed");
        Reject(() => ArcadeGameRevisions.CreateLaunchProfile(root, "mvs_stakwindev"), "Hidden developer forced launch allowed");
        Reject(() => ArcadeGameRevisions.LoadOnlineProfile("mvs_stakwindev"), "Hidden developer online launch allowed");
        Check(Fingerprint(root) == before, "Rejected hidden child mutated saved owner");
        Select(root, "mvs_stakwindev"); ArcadeGameRevisions.Populate(root);
        Reject(() => ArcadeGameRevisions.CreateLaunchProfile(root), "Persisted hidden selection launched");
        Select(root, "mvs_stakwin"); ArcadeGameRevisions.ApplyRevision(root, "mvs_stakwin");
        Check(!root.DevOnly, "Correcting hidden selection hides normal family");
        Environment.SetEnvironmentVariable("TPUI_SHOW_DEVONLY_PROFILES", "true");
        Check(ArcadeGameRevisions.GetOptions(root).Any(x => x.Value == "mvs_stakwindev"), "Explicit developer flag ignored");
        ArcadeGameRevisions.ApplyRevision(root, "mvs_stakwindev");
        Check(!root.DevOnly && ArcadeGameRevisions.CreateLaunchProfile(root).DevOnly, "Persistent owner visibility and exact child eligibility are separate");
        Environment.SetEnvironmentVariable("TPUI_SHOW_DEVONLY_PROFILES", "1");
        Console.WriteLine("Release visibility, hidden-selection rejection and explicit developer override passed.");
    }

    private static void FileLoaderChecks(Func<string, GameProfile> stockLoader,
        Func<string, GameProfile> userLoader, Func<IEnumerable<GameProfile>> provider)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var oldStock = ArcadeGameRevisions.StockProfileLoader;
        var oldUser = ArcadeGameRevisions.UserProfileLoader;
        var oldProvider = ArcadeGameRevisions.UserProfilesProvider;
        var temporary = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "tpui-revisions-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(temporary);
        try
        {
            Directory.SetCurrentDirectory(temporary);
            foreach (var folder in new[] { "GameProfiles", "UserProfiles", "Metadata" }) Directory.CreateDirectory(folder);
            foreach (var id in new[] { "mvs_kizuna", "mvs_kizuna4p" })
                using (var writer = File.CreateText(Path.Combine("GameProfiles", id + ".xml"))) Serializer.Serialize(writer, Profile(id));
            var root = Profile("mvs_kizuna"); root.GamePath = "X:/isolated-media/kizuna.zip";
            using (var writer = File.CreateText("UserProfiles/mvs_kizuna.xml")) Serializer.Serialize(writer, root);
            File.WriteAllText("Metadata/mvs_kizuna4p.json", "{\"game_name\":\"Fixture child\",\"icon_name\":\"fixture.png\"}");
            var files = Directory.GetFiles(temporary, "*", SearchOption.AllDirectories).ToDictionary(x => x, Digest);
            ArcadeGameRevisions.StockProfileLoader = stockLoader;
            ArcadeGameRevisions.UserProfileLoader = userLoader;
            ArcadeGameRevisions.UserProfilesProvider = provider;
            var online = ArcadeGameRevisions.LoadOnlineProfile("mvs_kizuna4p");
            Check(online.ProfileName == "mvs_kizuna4p" && online.GamePath == root.GamePath, "Real XML family fallback without child user file");
            Check(online.IconName == "Icons/fixture.png" && online.GameInfo.game_name == "Fixture child", "Metadata icon suffix and selected metadata from disk");
            Reject(() => stockLoader("../mvs_kizuna"), "File loader accepted traversal");
            foreach (var file in files) Check(Digest(file.Key) == file.Value, "Resolver wrote a fixture profile");
            Check(Directory.GetFiles(temporary, "*", SearchOption.AllDirectories).Length == files.Count, "Resolver created a user profile");
            File.WriteAllText("UserProfiles/mvs_kizuna4p.xml", "<!DOCTYPE GameProfile [<!ENTITY forbidden SYSTEM 'file:///missing'>]><GameProfile>&forbidden;</GameProfile>");
            bool rejected = false;
            try { userLoader("mvs_kizuna4p"); }
            catch (InvalidOperationException) { rejected = true; }
            catch (System.Xml.XmlException) { rejected = true; }
            Check(rejected, "Profile loader accepted a DTD/external entity");
            Console.WriteLine("Isolated real XML/metadata loading, read-only behavior and DTD rejection passed.");
        }
        finally
        {
            ArcadeGameRevisions.StockProfileLoader = oldStock;
            ArcadeGameRevisions.UserProfileLoader = oldUser;
            ArcadeGameRevisions.UserProfilesProvider = oldProvider;
            Directory.SetCurrentDirectory(currentDirectory);
            var expectedPrefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar + "tpui-revisions-";
            if (!temporary.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid test cleanup path");
            Directory.Delete(temporary, true);
        }
    }

    private static int Main(string[] args)
    {
        var oldDirectory = Directory.GetCurrentDirectory();
        var oldFlag = Environment.GetEnvironmentVariable("TPUI_SHOW_DEVONLY_PROFILES");
        var originalStock = ArcadeGameRevisions.StockProfileLoader;
        var originalUser = ArcadeGameRevisions.UserProfileLoader;
        var originalProvider = ArcadeGameRevisions.UserProfilesProvider;
        try
        {
            Directory.SetCurrentDirectory(Path.Combine(Path.GetFullPath(args[0]), "TeknoParrotUi.Common"));
            var paths = Directory.GetFiles("GameProfiles", "*.xml").Where(x => new[] { "mvs_", "cps_", "ss32_" }.Any(prefix => Path.GetFileName(x).StartsWith(prefix))).ToList();
            var hashes = paths.ToDictionary(x => x, Digest);
            templates = paths.Select(x => originalStock(Path.GetFileNameWithoutExtension(x))).ToDictionary(x => x.ProfileName);
            ArcadeGameRevisions.StockProfileLoader = id => templates.TryGetValue(id, out var p) ? p : null;
            ArcadeGameRevisions.UserProfileLoader = id => saved.TryGetValue(id, out var p) ? p : null;
            ArcadeGameRevisions.UserProfilesProvider = () => saved.Values;
            Environment.SetEnvironmentVariable("TPUI_SHOW_DEVONLY_PROFILES", "1");
            CatalogChecks(); SwitchChecks(); SavedAndReloadChecks(); OnlineAndLibraryChecks(); VisibilityChecks();
            FileLoaderChecks(originalStock, originalUser, originalProvider);
            foreach (var path in paths) Check(Digest(path) == hashes[path], "Stock XML was modified: " + path);
            Console.WriteLine("PASS: " + assertions + " assertions; no production profile files written.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            ArcadeGameRevisions.StockProfileLoader = originalStock;
            ArcadeGameRevisions.UserProfileLoader = originalUser;
            ArcadeGameRevisions.UserProfilesProvider = originalProvider;
            Environment.SetEnvironmentVariable("TPUI_SHOW_DEVONLY_PROFILES", oldFlag);
            Directory.SetCurrentDirectory(oldDirectory);
        }
    }
}

internal static class TestClone
{
    internal static Metadata CloneForTest(this Metadata metadata) => new GameProfile { GameInfo = metadata }.Clone().GameInfo;
}
