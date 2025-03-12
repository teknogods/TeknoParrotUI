using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace ParrotPatcher.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();


            // Example of wiring up the event handler
            var checkUpdateButton = this.FindControl<Button>("button1");
            if (checkUpdateButton != null)
            {
                checkUpdateButton.Click += CheckUpdateButton_Click;
            }
        }

        private async void CheckUpdateButton_Click(object? sender, RoutedEventArgs e)
        {
            listBox1.Items.Add("Waiting for TeknoParrotUI to close...");
            await Task.Run(() => checkForTPUI());
            listBox1.Items.Add("Closed!");
            listBox1.Items.Add("Checking what needs to be updated...");
            await Task.Run(async () =>
            {
                string[] zipList = getZipsToExtract();
                if (zipList.Length > 0)
                {
                    //^example\d+\.\d+\.\d+\.\d+\.zip$
                    foreach (string zipp in zipList)
                    {
                        try
                        {
                            string zipFile = Path.GetFileName(zipp);
                            foreach (var component in Components.UpdateComponents.ListOfComponents)
                            {
                                if (Regex.IsMatch(zipFile, $"^{component.name}\\d+\\.\\d+\\.\\d+\\.\\d+\\.zip"))
                                {
                                    await Dispatcher.UIThread.InvokeAsync(() =>
                                    {
                                        listBox1.Items.Add($"Found update for {component.name}, extracting...");
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

                                            // directory
                                            if (name.EndsWith("/"))
                                            {
                                                name = isUsingFolderOverride ? Path.Combine(component.folderOverride, name) : name;
                                                Directory.CreateDirectory(name);
                                                await Dispatcher.UIThread.InvokeAsync(() =>
                                                {
                                                    listBox1.Items.Add($"Updater directory entry: {name}");
                                                });
                                                continue;
                                            }

                                            var dest = isUI ? name : Path.Combine(destinationFolder, name);
                                            await Dispatcher.UIThread.InvokeAsync(() =>
                                          {
                                              listBox1.Items.Add($"Updater file: {name} extracting to: {dest}");
                                          });

                                            try
                                            {
                                                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                                            }
                                            catch
                                            {
                                                // ignore x)
                                            }
                                            try
                                            {
                                                if (File.Exists(dest))
                                                    File.Delete(dest);
                                            }
                                            catch (UnauthorizedAccessException)
                                            {
                                                // couldn't delete, just move for now
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
                                                // ignore..??
                                            }
                                        }

                                        string versionString = zipFile.Replace(component.name, "");

                                        versionString = versionString.Replace(".zip", "");
                                        Console.WriteLine("VERSION FOUND: " + versionString);
                                        if (component.manualVersion)
                                        {
                                            File.WriteAllText(component.folderOverride + "\\.version", versionString);
                                        }
                                        await Dispatcher.UIThread.InvokeAsync(() =>
                                        {
                                            listBox1.Items.Add($"Successfully updated {component.name}!");
                                        });
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                listBox1.Items.Add($"Failed to extract ZIP {zipp}! Delete this from the cache folder in your TeknoParrot UI folder!");
                            });
                        }
                    }
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        listBox1.Items.Add("No cores to update!");
                    });
                }
            });
            if (Directory.Exists("./cache"))
            {
                Directory.Delete("./cache", true);
            }
            listBox1.Items.Add("Restarting TeknoParrotUI...");
            string currentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string targetExePath = Path.Combine(currentDirectory, "TeknoParrotUi.exe");
            Process.Start(targetExePath);
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
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