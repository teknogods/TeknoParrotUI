using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Android;

namespace TeknoParrotUi.Avalonia.Views;

public partial class GameScannerView : UserControl
{
    private const string AndroidFolderBookmarkFile = ".android-games-folder.bookmark";

    private List<GameScannerCore.FoundGame> _found = new();
    private IReadOnlyList<ManagedAndroidGame> _managedFound = Array.Empty<ManagedAndroidGame>();
    private IStorageFolder? _androidFolder;
    private bool _bookmarkRestoreAttempted;

    public event Action? BackRequested;
    public event Action<int>? GamesAdded;

    public GameScannerView()
    {
        InitializeComponent();
        ConfigurePlatformLayout();
        Localize();
        Services.Loc.LanguageChanged += Localize;
        AttachedToVisualTree += async (_, _) => await RestoreAndroidFolderAsync();
    }

    private void ConfigurePlatformLayout()
    {
        if (!OperatingSystem.IsAndroid())
            return;

        // The Android activity is locked to landscape and has enough horizontal
        // room for the stock label/path/Browse row. Keeping that compact row
        // leaves the scanner log useful instead of reducing it to one line.
        // The actions likewise stay on one row above the system bar.
        ActionsPanel.Orientation = Orientation.Horizontal;
        ActionsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        foreach (var button in new[] { BtnBack, BtnScan, BtnAddAll })
        {
            button.Width = double.NaN;
            button.MinHeight = 48;
            button.HorizontalAlignment = HorizontalAlignment.Center;
        }
    }

    private void Localize()
    {
        HeaderText.Text = OperatingSystem.IsAndroid()
            ? "Import Android Games"
            : Services.Loc.T("MainRomScanner", "Game Scanner");
        DescriptionText.Text = OperatingSystem.IsAndroid()
            ? "Select the TeknoParrotGames folder once. The preferred location is " +
              "/storage/emulated/0/TeknoParrotGames; existing Downloads libraries remain supported."
            : "Scans a romset folder using the traditional layout (one subfolder per game ID) and configures all found games automatically.";
        FolderLabel.Text = OperatingSystem.IsAndroid() ? "TeknoParrotGames folder" : "Romset folder";
        BtnBrowse.Content = Services.Loc.T("GameScannerBrowse", "Browse") + "...";
        BtnBack.Content = Services.Loc.T("Back", "Back");
        BtnScan.Content = OperatingSystem.IsAndroid()
            ? "Scan Launchable Games"
            : Services.Loc.T("GameScannerScanUsingDAT", "Scan");
        BtnAddAll.Content = OperatingSystem.IsAndroid()
            ? "Import Found Games"
            : Services.Loc.T("GameScannerAddFoundGames", "Add Found Games");
    }

    private void Log(string message, bool clear = false)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (clear)
                LogText.Text = "";
            LogText.Text += message + Environment.NewLine;
            LogScroll.ScrollToEnd();
        });
    }

    private async void BtnBrowse_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null)
            return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = OperatingSystem.IsAndroid()
                ? "Select TeknoParrotGames folder"
                : "Select romset folder",
            AllowMultiple = false
        });
        if (folders.Count == 0)
            return;

        if (OperatingSystem.IsAndroid())
        {
            SetAndroidFolder(folders[0]);
            await SaveAndroidFolderBookmarkAsync(folders[0]);
        }
        else
        {
            FolderBox.Text = folders[0].TryGetLocalPath() ?? "";
            folders[0].Dispose();
        }
        BtnScan.IsEnabled = OperatingSystem.IsAndroid()
            ? _androidFolder != null
            : !string.IsNullOrEmpty(FolderBox.Text);
    }

    private async void BtnScan_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        BtnScan.IsEnabled = false;
        BtnAddAll.IsEnabled = false;
        try
        {
            if (OperatingSystem.IsAndroid())
            {
                if (_androidFolder == null)
                    return;
                Log("Scanning selected Android folder...", clear: true);
                GameProfileLoader.LoadProfiles(false);
                var supportedProfiles =
                    GameProfileLoader.GameProfiles
                        .Where(PlatformCapabilities.IsAndroidGameProfileSupported)
                        .ToList();
                var supportedProfileNames =
                    supportedProfiles
                        .Select(profile => profile.ProfileName)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var recipes =
                    AndroidLaunchRecipeCatalog.LoadAll()
                        .Where(recipe =>
                            supportedProfileNames.Contains(recipe.ProfileName))
                        .ToArray();
                var snapshots = await Task.Run(
                    () => SnapshotAndroidFolderAsync(_androidFolder, recipes));
                _managedFound = ManagedAndroidGameImporter.Scan(
                    snapshots,
                    recipes,
                    supportedProfiles,
                    message => Log(message));
                Log(
                    $"Scan complete — {_managedFound.Count} launchable game(s) found " +
                    $"from {recipes.Length} supported OpenParrot/PCSX2X6 recipe(s).");
                BtnAddAll.IsEnabled = _managedFound.Count > 0;
                return;
            }

            var dir = FolderBox.Text;
            if (string.IsNullOrWhiteSpace(dir))
                return;
            Log("Scanning romset...", clear: true);
            _found = await Task.Run(() => GameScannerCore.ScanRomFolder(dir, message => Log(message)));
            Log($"Scan complete — {_found.Count} game(s) found.");
            BtnAddAll.IsEnabled = _found.Count > 0;
        }
        catch (Exception error) when (
            error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log("Scan failed: " + error.Message);
        }
        finally
        {
            BtnScan.IsEnabled = OperatingSystem.IsAndroid()
                ? _androidFolder != null
                : !string.IsNullOrWhiteSpace(FolderBox.Text);
        }
    }

    private async void BtnAddAll_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        BtnAddAll.IsEnabled = false;
        try
        {
            if (OperatingSystem.IsAndroid())
            {
                if (_managedFound.Count == 0)
                    return;
                Log("Importing launchable Android games...");
                var result = await Task.Run(() => ManagedAndroidGameImporter.ConfigureFoundGames(
                    _managedFound,
                    log: message => Log(message)));
                GameProfileLoader.LoadProfiles(false);
                Log(
                    $"Done — added {result.Added}, completed {result.Updated}, already configured {result.Unchanged}, conflicts {result.Conflicts}, failed {result.Failed}.");
                GamesAdded?.Invoke(result.Changed);
                return;
            }

            var dir = FolderBox.Text;
            if (string.IsNullOrWhiteSpace(dir) || _found.Count == 0)
                return;
            Log("Configuring games...");
            var added = await Task.Run(() =>
                GameScannerCore.ConfigureFoundGames(_found, dir, message => Log(message)));
            Log($"Done — {added} game(s) added to your library.");
            GamesAdded?.Invoke(added);
        }
        finally
        {
            BtnAddAll.IsEnabled = OperatingSystem.IsAndroid()
                ? _managedFound.Count > 0
                : _found.Count > 0;
        }
    }

    private async Task RestoreAndroidFolderAsync()
    {
        if (!OperatingSystem.IsAndroid() || _bookmarkRestoreAttempted)
            return;
        _bookmarkRestoreAttempted = true;
        var top = TopLevel.GetTopLevel(this);
        if (top == null || !File.Exists(AndroidFolderBookmarkFile))
            return;

        try
        {
            var bookmark = File.ReadAllText(AndroidFolderBookmarkFile);
            if (string.IsNullOrWhiteSpace(bookmark))
                return;
            var folder = await top.StorageProvider.OpenFolderBookmarkAsync(bookmark);
            if (folder == null)
                return;
            SetAndroidFolder(folder);
            BtnScan.IsEnabled = true;
            Log("Restored the saved TeknoParrotGames folder.", clear: true);
        }
        catch (Exception error) when (
            error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log("Could not reopen the saved games folder; select it again. " + error.Message, clear: true);
        }
    }

    private static async Task SaveAndroidFolderBookmarkAsync(IStorageFolder folder)
    {
        if (!folder.CanBookmark)
            return;
        var bookmark = await folder.SaveBookmarkAsync();
        if (!string.IsNullOrWhiteSpace(bookmark))
            File.WriteAllText(AndroidFolderBookmarkFile, bookmark);
    }

    private void SetAndroidFolder(IStorageFolder folder)
    {
        if (!ReferenceEquals(_androidFolder, folder))
            _androidFolder?.Dispose();
        _androidFolder = folder;
        FolderBox.Text = ResolveAndroidPhysicalPath(folder) ?? folder.Name;
        _managedFound = Array.Empty<ManagedAndroidGame>();
        BtnAddAll.IsEnabled = false;
    }

    private async Task<IReadOnlyList<AndroidGameFolderSnapshot>> SnapshotAndroidFolderAsync(
        IStorageFolder root,
        IReadOnlyList<AndroidLaunchRecipe> recipes)
    {
        var snapshots = new List<AndroidGameFolderSnapshot>();
        var rootPhysicalPath = ResolveAndroidPhysicalPath(root);

        try
        {
            await foreach (var item in root.GetItemsAsync().ConfigureAwait(false))
            {
                try
                {
                    if (item is IStorageFolder childFolder)
                    {
                        var childPath = ResolveAndroidPhysicalPath(childFolder) ??
                            CombineAndroidPath(rootPhysicalPath, childFolder.Name);
                        var files = await CaptureAndroidCandidateFilesAsync(
                            childFolder,
                            childPath,
                            ManagedAndroidGameImporter.GetCandidateExecutablePaths(
                                childFolder.Name, recipes)).ConfigureAwait(false);
                        if (files.Count > 0)
                        {
                            snapshots.Add(new AndroidGameFolderSnapshot(
                                childFolder.Name, childPath ?? childFolder.Name, files));
                        }
                    }
                }
                finally
                {
                    item.Dispose();
                }
            }
        }
        catch (Exception error) when (
            (error is IOException or UnauthorizedAccessException or InvalidOperationException) &&
            !string.IsNullOrWhiteSpace(rootPhysicalPath) && Directory.Exists(rootPhysicalPath))
        {
            // Some Android document providers reject enumeration of an otherwise
            // readable shared-storage tree (Samsung reports it as a hidden tree).
            // Continue with the physical-path fallback below.
        }

        // SAF directory listings can be stale after ADB transfers, and some
        // providers reject them entirely. Merge any readable physical children
        // that were not already captured without giving up the SAF fallback used
        // by non-local document providers.
        if (!string.IsNullOrWhiteSpace(rootPhysicalPath) && Directory.Exists(rootPhysicalPath))
        {
            string[] physicalDirectories;
            try
            {
                physicalDirectories = Directory.GetDirectories(rootPhysicalPath);
            }
            catch (Exception error) when (
                error is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                physicalDirectories = Array.Empty<string>();
            }

            var capturedPaths = new HashSet<string>(
                snapshots.Select(snapshot => snapshot.FullPath.TrimEnd('/', '\\')),
                StringComparer.OrdinalIgnoreCase);
            foreach (var physicalDirectory in physicalDirectories)
            {
                var normalizedDirectory = physicalDirectory.Replace('\\', '/').TrimEnd('/');
                if (!capturedPaths.Add(normalizedDirectory))
                    continue;
                var folderName = Path.GetFileName(normalizedDirectory);
                var files = CapturePhysicalAndroidCandidateFiles(
                    normalizedDirectory,
                    ManagedAndroidGameImporter.GetCandidateExecutablePaths(folderName, recipes));
                if (files.Count > 0)
                    snapshots.Add(new AndroidGameFolderSnapshot(
                        folderName, normalizedDirectory, files));
            }
        }

        var rootFiles = await CaptureAndroidCandidateFilesAsync(
            root,
            rootPhysicalPath,
            ManagedAndroidGameImporter.GetCandidateExecutablePaths(root.Name, recipes))
            .ConfigureAwait(false);
        if (rootFiles.Count > 0)
            snapshots.Add(new AndroidGameFolderSnapshot(
                root.Name, rootPhysicalPath ?? root.Name, rootFiles));
        return snapshots;
    }

    private static IReadOnlyList<AndroidGameFileSnapshot> CapturePhysicalAndroidCandidateFiles(
        string folderPhysicalPath,
        IReadOnlyList<string> candidatePaths)
    {
        var files = new List<AndroidGameFileSnapshot>();
        foreach (var candidatePath in candidatePaths)
        {
            var normalizedPath = candidatePath.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrWhiteSpace(normalizedPath))
                continue;
            var physicalPath = CombineAndroidPath(folderPhysicalPath, normalizedPath);
            if (!string.IsNullOrWhiteSpace(physicalPath) && File.Exists(physicalPath))
                files.Add(new AndroidGameFileSnapshot(normalizedPath, physicalPath));
        }
        return files;
    }

    private static async Task<IReadOnlyList<AndroidGameFileSnapshot>> CaptureAndroidCandidateFilesAsync(
        IStorageFolder folder,
        string? folderPhysicalPath,
        IReadOnlyList<string> candidatePaths)
    {
        var files = new List<AndroidGameFileSnapshot>();
        foreach (var candidatePath in candidatePaths)
        {
            var normalizedPath = candidatePath.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrWhiteSpace(normalizedPath))
                continue;

            // Android's document provider can lag behind files copied over ADB.
            // Prefer the physical shared-storage path when it is available, then
            // retain the SAF lookup as the fallback for bookmarked document trees.
            var directPhysicalPath = CombineAndroidPath(folderPhysicalPath, normalizedPath);
            if (!string.IsNullOrWhiteSpace(directPhysicalPath) && File.Exists(directPhysicalPath))
            {
                files.Add(new AndroidGameFileSnapshot(normalizedPath, directPhysicalPath));
                continue;
            }

            var file = await TryGetAndroidFileAsync(folder, normalizedPath).ConfigureAwait(false);
            if (file == null)
                continue;
            try
            {
                var physicalPath = ResolveAndroidPhysicalPath(file) ??
                    CombineAndroidPath(folderPhysicalPath, normalizedPath);
                if (!string.IsNullOrWhiteSpace(physicalPath))
                    files.Add(new AndroidGameFileSnapshot(normalizedPath, physicalPath));
            }
            finally
            {
                file.Dispose();
            }
        }
        return files;
    }

    private static async Task<IStorageFile?> TryGetAndroidFileAsync(
        IStorageFolder root,
        string relativePath)
    {
        var segments = relativePath.Split(
            '/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            return null;

        var openedFolders = new List<IStorageFolder>();
        var current = root;
        try
        {
            for (var index = 0; index < segments.Length - 1; index++)
            {
                var next = await current.GetFolderAsync(segments[index]).ConfigureAwait(false);
                if (next == null)
                    return null;
                openedFolders.Add(next);
                current = next;
            }
            return await current.GetFileAsync(segments[^1]).ConfigureAwait(false);
        }
        catch (Exception error) when (
            error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
        finally
        {
            for (var index = openedFolders.Count - 1; index >= 0; index--)
                openedFolders[index].Dispose();
        }
    }

    private static string? ResolveAndroidPhysicalPath(IStorageItem item)
    {
        var local = item.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(local))
            return local.Replace('\\', '/').TrimEnd('/');

        var value = item.Path?.OriginalString;
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return Services.PlatformDocumentPathResolver.TryResolve(value, out var resolved)
            ? resolved.TrimEnd('/')
            : null;
    }

    private static string? CombineAndroidPath(string? root, string relative)
    {
        if (string.IsNullOrWhiteSpace(root))
            return null;
        return root.TrimEnd('/', '\\') + "/" + relative.Replace('\\', '/').TrimStart('/');
    }

    private void BtnBack_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        BackRequested?.Invoke();

}
