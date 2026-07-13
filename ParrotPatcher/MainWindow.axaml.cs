using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace ParrotPatcher;

/// <summary>
/// Applies downloaded component updates (cache/*.zip) after the UI closes,
/// then restarts TeknoParrotUi. Cross-platform port of the WinForms updater.
/// </summary>
public partial class MainWindow : Window
{
    private readonly Components _uc = new();
    private readonly ObservableCollection<string> _log = new();
    private readonly string _logFilePath;
    private readonly string _baseDirectory;

    public MainWindow()
    {
        InitializeComponent();
        LogList.ItemsSource = _log;

        _baseDirectory = AppContext.BaseDirectory;
        _logFilePath = Path.Combine(_baseDirectory, "ParrotPatcher_Log.txt");
        File.WriteAllText(_logFilePath, $"ParrotPatcher Log - {DateTime.Now}{Environment.NewLine}");

        Opened += async (_, _) => await RunAsync();
    }

    private void LogMessage(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _log.Add(message);
            LogScroll.ScrollToEnd();
        });

        try
        {
            File.AppendAllText(_logFilePath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Silently fail if we can't write to the log file
        }
    }

    private async Task RunAsync()
    {
        LogMessage("Waiting for TeknoParrotUI to close...");
        await Task.Run(WaitForTpuiExit);
        LogMessage("Closed!");

        await ProcessUpdatesAsync();

        CleanupAndRestart();
    }

    private static void WaitForTpuiExit()
    {
        var currentId = Environment.ProcessId;
        while (true)
        {
            var running = Process.GetProcessesByName("TeknoParrotUi")
                .Any(p => p.Id != currentId);
            if (!running)
                return;
            Thread.Sleep(1000);
        }
    }

    private async Task ProcessUpdatesAsync()
    {
        LogMessage("Checking what needs to be updated...");
        await Task.Run(() =>
        {
            string[] zipList = GetZipsToExtract();
            if (zipList.Length > 0)
            {
                SaveUpdateInfo(zipList);
                ProcessZipFiles(zipList);
            }
            else
            {
                LogMessage("No cores to update!");
            }
        });
    }

    private string[] GetZipsToExtract()
    {
        var cache = Path.Combine(_baseDirectory, "cache");
        return Directory.Exists(cache) ? Directory.GetFiles(cache, "*.zip") : Array.Empty<string>();
    }

    private void ProcessZipFiles(string[] zipList)
    {
        foreach (string zipPath in zipList)
        {
            try
            {
                ProcessSingleZip(zipPath);
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to extract ZIP {zipPath}! Delete this from the cache folder in your TeknoParrot UI folder! Error: {ex.Message}");
            }
        }
    }

    private void ProcessSingleZip(string zipPath)
    {
        string zipFile = Path.GetFileName(zipPath);

        foreach (UpdaterComponent component in _uc.components)
        {
            if (Regex.IsMatch(zipFile, $"^{component.name}\\d+\\.\\d+\\.\\d+\\.\\d+\\.zip"))
            {
                ExtractComponentUpdate(zipPath, component, zipFile);
                break;
            }
        }
    }

    private void ExtractComponentUpdate(string zipPath, UpdaterComponent component, string zipFile)
    {
        LogMessage($"Found update for {component.name}, extracting...");

        using var stream = new FileStream(zipPath, FileMode.Open);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        ExtractZipEntries(zip, component);
        WriteVersionFile(component, zipFile);
    }

    private void ExtractZipEntries(ZipArchive zip, UpdaterComponent component)
    {
        bool isUI = component.name == "TeknoParrotUI";
        bool isUsingFolderOverride = !string.IsNullOrEmpty(component.folderOverride);
        string destinationFolder = isUsingFolderOverride ? component.folderOverride! : component.name;

        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName;

            if (name.EndsWith("/"))
            {
                CreateDirectoryEntry(name, component);
                continue;
            }

            var dest = isUI ? name : Path.Combine(destinationFolder, name);
            ExtractSingleFile(entry, Path.Combine(_baseDirectory, dest), component);
        }
    }

    private void CreateDirectoryEntry(string name, UpdaterComponent component)
    {
        bool isUsingFolderOverride = !string.IsNullOrEmpty(component.folderOverride);
        name = isUsingFolderOverride ? Path.Combine(component.folderOverride!, name) : name;
        Directory.CreateDirectory(Path.Combine(_baseDirectory, name));
        LogMessage($"Updater directory entry: {name}");
    }

    private void ExtractSingleFile(ZipArchiveEntry entry, string dest, UpdaterComponent component)
    {
        LogMessage($"Updater file: {entry.FullName} extracting to: {dest}");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        }
        catch
        {
            // ignore
        }

        HandleExistingFile(dest, component);

        if (ShouldSkipExtraction(dest, component))
            return;

        ExtractFile(entry, dest);
    }

    private void HandleExistingFile(string dest, UpdaterComponent component)
    {
        try
        {
            if (File.Exists(dest))
            {
                if (IsFileExcluded(StripComponentPrefix(dest, component), component.excludedFiles))
                {
                    LogMessage($"Skipping deletion of excluded file: {dest}");
                }
                else
                {
                    File.Delete(dest);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            File.Move(dest, dest + ".bak");
        }
        catch (IOException)
        {
            // file in use (Windows) — same recovery as before
            File.Move(dest, dest + ".bak", true);
        }
    }

    private bool ShouldSkipExtraction(string dest, UpdaterComponent component)
    {
        if (IsFileExcluded(StripComponentPrefix(dest, component), component.excludedFiles))
        {
            LogMessage($"Skipping extraction of excluded file: {dest}");
            return true;
        }
        return false;
    }

    private static string StripComponentPrefix(string dest, UpdaterComponent component) =>
        dest.Replace(component.name + Path.DirectorySeparatorChar, "")
            .Replace(component.name + "/", "");

    private void ExtractFile(ZipArchiveEntry entry, string dest)
    {
        try
        {
            using var entryStream = entry.Open();
            using var file = File.Create(dest);
            entryStream.CopyTo(file);
        }
        catch
        {
            // ignore
        }
    }

    private void WriteVersionFile(UpdaterComponent component, string zipFile)
    {
        if (!component.manualVersion || string.IsNullOrEmpty(component.folderOverride))
            return;

        string versionString = zipFile.Replace(component.name, "").Replace(".zip", "");

        try
        {
            string versionFilePath = Path.Combine(_baseDirectory, component.folderOverride, ".version");

            if (File.Exists(versionFilePath))
                File.SetAttributes(versionFilePath, FileAttributes.Normal);

            File.WriteAllText(versionFilePath, versionString);
            LogMessage($"Successfully updated {component.name}!");
        }
        catch (Exception ex)
        {
            LogMessage($"Failed to write version file for {component.name}: {ex.Message}");
        }
    }

    private void SaveUpdateInfo(string[] zipList)
    {
        // TeknoParrotUI's self-update path (UpdaterCore.LaunchSelfUpdate) already
        // writes a richer ".lastupdate" marker (component|version|base64-changelog)
        // before launching us — don't clobber it with the plain name|version line
        // this method would otherwise derive from the zip filename alone.
        string updateFilePath = Path.Combine(_baseDirectory, ".lastupdate");
        if (File.Exists(updateFilePath))
        {
            LogMessage("'.lastupdate' already present (written before restart) - keeping it as-is.");
            return;
        }

        // Save information about what was updated to show changelog later
        try
        {
            var updateInfo = new StringBuilder();
            foreach (string zipPath in zipList)
            {
                string zipFile = Path.GetFileName(zipPath);
                foreach (UpdaterComponent component in _uc.components)
                {
                    if (Regex.IsMatch(zipFile, $"^{component.name}\\d+\\.\\d+\\.\\d+\\.\\d+\\.zip"))
                    {
                        string versionString = zipFile.Replace(component.name, "").Replace(".zip", "");
                        updateInfo.AppendLine($"{component.name}|{versionString}");
                        break;
                    }
                }
            }

            if (updateInfo.Length > 0)
            {
                // Save OUTSIDE the cache folder so it doesn't get deleted
                File.WriteAllText(updateFilePath, updateInfo.ToString());
                LogMessage($"Saved update info to: {updateFilePath}");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Failed to save update info: {ex.Message}");
        }
    }

    private void CleanupAndRestart()
    {
        var cache = Path.Combine(_baseDirectory, "cache");
        if (Directory.Exists(cache))
            Directory.Delete(cache, true);

        LogMessage("Restarting TeknoParrotUI...");
        LogMessage($"Log file saved to: {_logFilePath}");

        // Windows apphost is TeknoParrotUi.exe; on Linux/macOS it has no extension
        var candidates = new[]
        {
            Path.Combine(_baseDirectory, "TeknoParrotUi.exe"),
            Path.Combine(_baseDirectory, "TeknoParrotUi")
        };
        var target = candidates.FirstOrDefault(File.Exists);
        if (target != null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { WorkingDirectory = _baseDirectory, UseShellExecute = false });
            }
            catch (Exception ex)
            {
                LogMessage($"Could not restart TeknoParrotUi: {ex.Message}");
            }
        }

        Close();
    }

    // ---------- exclusion pattern matching ----------

    /// <summary>Checks if a file path matches any of the excluded file patterns.</summary>
    private static bool IsFileExcluded(string filePath, List<string> excludedPatterns)
    {
        if (excludedPatterns == null || excludedPatterns.Count == 0)
            return false;

        string normalizedPath = filePath.Replace('\\', '/');
        string fileName = Path.GetFileName(filePath);

        foreach (string pattern in excludedPatterns)
        {
            string normalizedPattern = pattern.Replace('\\', '/');

            if (IsWildcardMatch(normalizedPath, normalizedPattern) ||
                IsWildcardMatch(fileName, normalizedPattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Simple wildcard pattern matching that supports * and ? wildcards.</summary>
    private static bool IsWildcardMatch(string text, string pattern)
    {
        text = text.ToLowerInvariant();
        pattern = pattern.ToLowerInvariant();

        int textIndex = 0;
        int patternIndex = 0;
        int starIndex = -1;
        int match = 0;

        while (textIndex < text.Length)
        {
            if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starIndex = patternIndex;
                match = textIndex;
                patternIndex++;
            }
            else if (patternIndex < pattern.Length &&
                     (pattern[patternIndex] == '?' || pattern[patternIndex] == text[textIndex]))
            {
                patternIndex++;
                textIndex++;
            }
            else if (starIndex != -1)
            {
                patternIndex = starIndex + 1;
                match++;
                textIndex = match;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            patternIndex++;

        return patternIndex == pattern.Length;
    }
}
