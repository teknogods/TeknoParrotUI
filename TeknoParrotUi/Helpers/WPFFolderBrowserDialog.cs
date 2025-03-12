using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace TeknoParrotUi.Helpers
{
    public class AvaloniaFolderBrowserDialog : IDisposable
    {
        private readonly Collection<string> fileNames;
        private bool? canceled;
        private Window parentWindow;

        #region Constructors

        public AvaloniaFolderBrowserDialog()
        {
            fileNames = new Collection<string>();
        }

        public AvaloniaFolderBrowserDialog(string title)
        {
            fileNames = new Collection<string>();
            this.Title = title;
        }

        #endregion

        #region Public Properties

        public string Title { get; set; }

        public bool ShowHiddenItems { get; set; }

        public bool AddToMruList { get; set; } = true;

        public string InitialDirectory { get; set; }

        public string FileName
        {
            get
            {
                CheckFileNamesAvailable();
                if (fileNames.Count > 1)
                    throw new InvalidOperationException("Multiple files selected - the FileNames property should be used instead");
                return fileNames[0];
            }
            set
            {
                if (fileNames.Count == 0)
                    fileNames.Add(value);
                else
                    fileNames[0] = value;
            }
        }

        public Collection<string> FileNames => fileNames;

        #endregion

        #region Public Methods

        public bool? ShowDialog(Window owner)
        {
            parentWindow = owner;
            return ShowDialogAsync().GetAwaiter().GetResult();
        }

        public bool? ShowDialog()
        {
            return ShowDialogAsync().GetAwaiter().GetResult();
        }

        public async Task<bool?> ShowDialogAsync()
        {
            bool? result = null;

            try
            {
                // Create folder picker options
                var options = new FolderPickerOpenOptions
                {
                    Title = Title,
                    AllowMultiple = false,
                    SuggestedStartLocation = !string.IsNullOrEmpty(InitialDirectory)
                        ? await GetStorageProviderAsync(InitialDirectory)
                        : null
                };

                // Get the top level window if parent isn't set
                parentWindow ??= GetDefaultOwnerWindow();

                // Show the folder picker
                var folders = await parentWindow.StorageProvider.OpenFolderPickerAsync(options);

                // Process results
                if (folders != null && folders.Count > 0)
                {
                    canceled = false;
                    fileNames.Clear();

                    foreach (var folder in folders)
                    {
                        fileNames.Add(folder.Path.LocalPath);
                    }

                    result = true;
                }
                else
                {
                    canceled = true;
                    fileNames.Clear();
                    result = false;
                }
            }
            catch (Exception ex)
            {
                // Fall back to platform-specific dialog if available
                try
                {
#if WINDOWS
                    // Windows fallback to classic dialog
                    using var dialog = new System.Windows.Forms.FolderBrowserDialog();
                    dialog.SelectedPath = InitialDirectory;
                    dialog.ShowNewFolderButton = true;
                    dialog.Description = this.Title;
                    
                    result = (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK);
                    if (result.HasValue && result.Value)
                    {
                        canceled = false;
                        fileNames.Clear();
                        fileNames.Add(dialog.SelectedPath);
                    }
                    else
                    {
                        fileNames.Clear();
                        canceled = true;
                    }
#else
                    // No fallback available
                    canceled = true;
                    fileNames.Clear();
                    result = false;
#endif
                }
                catch
                {
                    // If all else fails
                    canceled = true;
                    fileNames.Clear();
                    result = false;
                }
            }

            return result;
        }

        #endregion

        #region Helper Methods

        private async Task<IStorageFolder> GetStorageProviderAsync(string path)
        {
            try
            {
                // Try to convert the path to a storage folder
                return await parentWindow.StorageProvider.TryGetFolderFromPathAsync(path);
            }
            catch
            {
                return null;
            }
        }

        private Window GetDefaultOwnerWindow()
        {
            // Get the current active window
            return Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
        }

        private void CheckFileNamesAvailable()
        {
            if (canceled.GetValueOrDefault())
                throw new InvalidOperationException("Filename not available - dialog was canceled");
            if (fileNames.Count == 0)
                throw new InvalidOperationException("No folders were selected");
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            // Nothing to dispose in our case
        }

        #endregion
    }
}