using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ParrotPatcher
{
    public partial class Form1 : Form
    {
        Components uc = null;
        private string logFilePath;

        public Form1()
        {
            InitializeComponent();
            progressBar1.Style = ProgressBarStyle.Marquee;
            uc = new Components();

            // Set up log file path
            string currentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            logFilePath = Path.Combine(currentDirectory, "ParrotPatcher_Log.txt");

            // Initialize log file
            File.WriteAllText(logFilePath, $"ParrotPatcher Log - {DateTime.Now}\r\n");
        }

        private void LogMessage(string message)
        {
            // Add to listbox
            listBox1.Items.Add(message);

            // Write to log file
            try
            {
                File.AppendAllText(logFilePath, $"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
            }
            catch
            {
                // Silently fail if we can't write to the log file
            }
        }

        /// <summary>
        /// Checks if a file path matches any of the excluded file patterns
        /// </summary>
        /// <param name="filePath">The file path to check</param>
        /// <param name="excludedPatterns">List of wildcard patterns to exclude</param>
        /// <returns>True if the file should be excluded from deletion, false otherwise</returns>
        private bool IsFileExcluded(string filePath, List<string> excludedPatterns)
        {
            if (excludedPatterns == null || excludedPatterns.Count == 0)
                return false;

            // Normalize the file path (convert backslashes to forward slashes)
            string normalizedPath = filePath.Replace('\\', '/');
            string fileName = Path.GetFileName(filePath);

            foreach (string pattern in excludedPatterns)
            {
                // Normalize the pattern as well
                string normalizedPattern = pattern.Replace('\\', '/');
                
                // Simple wildcard matching - much more generic
                if (IsWildcardMatch(normalizedPath, normalizedPattern) || 
                    IsWildcardMatch(fileName, normalizedPattern))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Simple wildcard pattern matching that supports * and ? wildcards
        /// </summary>
        /// <param name="text">Text to match against</param>
        /// <param name="pattern">Wildcard pattern</param>
        /// <returns>True if pattern matches text</returns>
        private bool IsWildcardMatch(string text, string pattern)
        {
            // Convert to lowercase for case-insensitive matching
            text = text.ToLowerInvariant();
            pattern = pattern.ToLowerInvariant();
            
            int textIndex = 0;
            int patternIndex = 0;
            int starIndex = -1;
            int match = 0;

            while (textIndex < text.Length)
            {
                // If we encounter a * in pattern
                if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
                {
                    starIndex = patternIndex;
                    match = textIndex;
                    patternIndex++;
                }
                // If characters match or we have a ? wildcard
                else if (patternIndex < pattern.Length && 
                        (pattern[patternIndex] == '?' || pattern[patternIndex] == text[textIndex]))
                {
                    patternIndex++;
                    textIndex++;
                }
                // If we have a previous *, backtrack
                else if (starIndex != -1)
                {
                    patternIndex = starIndex + 1;
                    match++;
                    textIndex = match;
                }
                // No match
                else
                {
                    return false;
                }
            }

            // Skip any remaining * in pattern
            while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                patternIndex++;
            }

            return patternIndex == pattern.Length;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            LogMessage("Waiting for TeknoParrotUI to close...");
            await Task.Run(() => checkForTPUI());
            LogMessage("Closed!");
            
            await ProcessUpdatesAsync();
            
            CleanupAndRestart();
        }

        private async Task ProcessUpdatesAsync()
        {
            LogMessage("Checking what needs to be updated...");
            await Task.Run(() =>
            {
                string[] zipList = getZipsToExtract();
                if (zipList.Length > 0)
                {
                    ProcessZipFiles(zipList);
                }
                else
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        LogMessage("No cores to update!");
                    });
                }
            });
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
                    this.Invoke((MethodInvoker)delegate
                    {
                        LogMessage($"Failed to extract ZIP {zipPath}! Delete this from the cache folder in your TeknoParrot UI folder! Error: {ex.Message}");
                    });
                }
            }
        }

        private void ProcessSingleZip(string zipPath)
        {
            string zipFile = Path.GetFileName(zipPath);
            
            foreach (UpdaterComponent component in uc.components)
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
            this.Invoke((MethodInvoker)delegate
            {
                LogMessage($"Found update for {component.name}, extracting...");
            });

            using (var memoryStream = new FileStream(zipPath, FileMode.Open))
            using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Read))
            {
                ExtractZipEntries(zip, component);
                WriteVersionFile(component, zipFile);
            }
        }

        private void ExtractZipEntries(ZipArchive zip, UpdaterComponent component)
        {
            bool isUI = component.name == "TeknoParrotUI";
            bool isUsingFolderOverride = !string.IsNullOrEmpty(component.folderOverride);
            string destinationFolder = isUsingFolderOverride ? component.folderOverride : component.name;

            foreach (var entry in zip.Entries)
            {
                var name = entry.FullName;

                if (name.EndsWith("/"))
                {
                    CreateDirectory(name, component);
                    continue;
                }

                var dest = isUI ? name : Path.Combine(destinationFolder, name);
                ExtractSingleFile(entry, dest, component);
            }
        }

        private void CreateDirectory(string name, UpdaterComponent component)
        {
            bool isUsingFolderOverride = !string.IsNullOrEmpty(component.folderOverride);
            name = isUsingFolderOverride ? Path.Combine(component.folderOverride, name) : name;
            Directory.CreateDirectory(name);
            
            this.Invoke((MethodInvoker)delegate
            {
                LogMessage($"Updater directory entry: {name}");
            });
        }

        private void ExtractSingleFile(ZipArchiveEntry entry, string dest, UpdaterComponent component)
        {
            this.Invoke((MethodInvoker)delegate
            {
                LogMessage($"Updater file: {entry.FullName} extracting to: {dest}");
            });

            // Create destination directory
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
            }
            catch
            {
                // ignore
            }

            // Handle existing file deletion
            HandleExistingFile(dest, component);

            // Check if file should be excluded from extraction
            if (ShouldSkipExtraction(dest, component))
            {
                return;
            }

            // Extract the file
            ExtractFile(entry, dest);
        }

        private void HandleExistingFile(string dest, UpdaterComponent component)
        {
            try
            {
                if (File.Exists(dest))
                {
                    // Check if this file should be excluded from deletion
                    if (IsFileExcluded(dest.Replace(component.name + "\\", ""), component.excludedFiles))
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            LogMessage($"Skipping deletion of excluded file: {dest}");
                        });
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
        }

        private bool ShouldSkipExtraction(string dest, UpdaterComponent component)
        {
            if (IsFileExcluded(dest.Replace(component.name + "\\", ""), component.excludedFiles))
            {
                this.Invoke((MethodInvoker)delegate
                {
                    LogMessage($"Skipping extraction of excluded file: {dest}");
                });
                return true;
            }
            return false;
        }

        private void ExtractFile(ZipArchiveEntry entry, string dest)
        {
            try
            {
                using (var entryStream = entry.Open())
                using (var dll = File.Create(dest))
                {
                    entryStream.CopyTo(dll);
                }
            }
            catch
            {
                // ignore
            }
        }

        private void WriteVersionFile(UpdaterComponent component, string zipFile)
        {
            if (!component.manualVersion) return;

            string versionString = zipFile.Replace(component.name, "").Replace(".zip", "");
            Console.WriteLine("VERSION FOUND: " + versionString);

            try
            {
                string versionFilePath = component.folderOverride + "\\.version";

                if (File.Exists(versionFilePath))
                {
                    FileAttributes attributes = File.GetAttributes(versionFilePath);
                    File.SetAttributes(versionFilePath, FileAttributes.Normal);
                }

                File.WriteAllText(versionFilePath, versionString);
                LogMessage($"Successfully updated {component.name}!");
            }
            catch (Exception ex)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    listBox1.Items.Add($"Failed to write version file for {component.name}: {ex.Message}");
                });
            }
        }

        private void CleanupAndRestart()
        {
            if (Directory.Exists("./cache"))
            {
                Directory.Delete("./cache", true);
            }
            
            LogMessage("Restarting TeknoParrotUI...");
            LogMessage($"Log file saved to: {logFilePath}");

            string currentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string targetExePath = Path.Combine(currentDirectory, "TeknoParrotUi.exe");
            Process.Start(targetExePath);
            Application.Exit();
        }

        private async void checkForTPUI()
        {
            bool tpRunning = true;
            //Thread.CurrentThread.IsBackground = true;
            while (tpRunning)
            {
                // check TPUI isn't running
                Process[] pname = Process.GetProcessesByName("TeknoParrotUi");
                if (pname.Length == 0)
                    tpRunning = false;
                Thread.Sleep(1000);
            }
        }

        private string[] getZipsToExtract()
        {
            if (Directory.Exists("./cache"))
            {
                string[] zips = Directory.GetFiles("./cache", "*.zip");
                return zips;
            }
            else
            {
                return new string[0];
            }

        }
    }
}