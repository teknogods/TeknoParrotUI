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

        private async void Form1_Load(object sender, EventArgs e)
        {
            LogMessage("Waiting for TeknoParrotUI to close...");
            await Task.Run(() => checkForTPUI());
            LogMessage("Closed!");
            LogMessage("Checking what needs to be updated...");
            await Task.Run(() =>
            {
                string[] zipList = getZipsToExtract();
                if (zipList.Length > 0)
                {
                    foreach (string zipp in zipList)
                    {
                        try
                        {
                            string zipFile = Path.GetFileName(zipp);
                            foreach (UpdaterComponent component in uc.components)
                            {
                                if (Regex.IsMatch(zipFile, $"^{component.name}\\d+\\.\\d+\\.\\d+\\.\\d+\\.zip"))
                                {
                                    this.Invoke((MethodInvoker)delegate
                                    {
                                        LogMessage($"Found update for {component.name}, extracting...");
                                    });
                                    using (var memoryStream = new FileStream(zipp, FileMode.Open))
                                    using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Read))
                                    {
                                        bool isUI = component.name == "TeknoParrotUI";
                                        bool isUsingFolderOverride = !string.IsNullOrEmpty(component.folderOverride);
                                        string destinationFolder = isUsingFolderOverride ? component.folderOverride : component.name;
                                        foreach (var entry in zip.Entries)
                                        {
                                            var name = entry.FullName;

                                            if (name.EndsWith("/"))
                                            {
                                                name = isUsingFolderOverride ? Path.Combine(component.folderOverride, name) : name;
                                                Directory.CreateDirectory(name);
                                                this.Invoke((MethodInvoker)delegate
                                                {
                                                    LogMessage($"Updater directory entry: {name}");
                                                });
                                                continue;
                                            }

                                            var dest = isUI ? name : Path.Combine(destinationFolder, name);
                                            this.Invoke((MethodInvoker)delegate
                                            {
                                                LogMessage($"Updater file: {name} extracting to: {dest}");
                                            });

                                            try
                                            {
                                                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                                            }
                                            catch
                                            {
                                                // ignore
                                            }
                                            try
                                            {
                                                if (File.Exists(dest))
                                                    File.Delete(dest);
                                            }
                                            catch (UnauthorizedAccessException)
                                            {
                                                File.Move(dest, dest + ".bak");
                                            }

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

                                        string versionString = zipFile.Replace(component.name, "");
                                        versionString = versionString.Replace(".zip", "");
                                        Console.WriteLine("VERSION FOUND: " + versionString);
                                        if (component.manualVersion)
                                        {
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

                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            this.Invoke((MethodInvoker)delegate
                            {
                                LogMessage($"Failed to extract ZIP {zipp}! Delete this from the cache folder in your TeknoParrot UI folder! Error: {ex.Message}");
                            });
                        }
                    }
                }
                else
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        LogMessage("No cores to update!");
                    });
                }
            });
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