using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
//using Microsoft.SDK.Samples.VistaBridge.Library;
//using Microsoft.SDK.Samples.VistaBridge.Interop;
using WPFFolderBrowser.Interop;

namespace WPFFolderBrowser
{
    public class WPFFolderBrowserDialog : IDisposable //, IDialogControlHost
    {
        protected readonly Collection<string> fileNames;
        internal NativeDialogShowState showState = NativeDialogShowState.PreShow;

        private IFileDialog nativeDialog;
//        private IFileDialogCustomize nativeDialogCustomize;
        private NativeDialogEventSink nativeEventSink;
        private bool? canceled;
        private Window parentWindow;

        protected static string IllegalPropertyChangeString => TeknoParrotUi.Properties.Resources.FolderBrowserIllegalPropertyChange;

        #region Constructors
        
        public WPFFolderBrowserDialog()
        {
            fileNames = new Collection<string>();
        }

        public WPFFolderBrowserDialog(string title)
        {
            fileNames = new Collection<string>();
            this.title = title;
        }

        #endregion

        // Template method to allow derived dialog to create actual
        // specific COM coclass (e.g. FileOpenDialog or FileSaveDialog)
        private NativeFileOpenDialog openDialogCoClass;


        internal IFileDialog GetNativeFileDialog()
        {
            Debug.Assert(openDialogCoClass != null,
                "Must call Initialize() before fetching dialog interface");
            return (IFileDialog)openDialogCoClass;
        }

        internal void InitializeNativeFileDialog()
        {
            openDialogCoClass = new NativeFileOpenDialog();
        }

        internal void CleanUpNativeFileDialog()
        {
            if (openDialogCoClass != null)
                Marshal.ReleaseComObject(openDialogCoClass);
        }

        internal void PopulateWithFileNames(Collection<string> names)
        {
            IShellItemArray resultsArray;
            uint count;
            IShellItem directory;
            if (names != null)
            {
                openDialogCoClass.GetResults(out resultsArray);
                resultsArray.GetCount(out count);

                names.Clear();
                for (int i = 0; i < count; i++)
                    names.Add(GetFileNameFromShellItem(GetShellItemAt(resultsArray, i)));

                if (count > 0)
                {
                    FileName = names[0];
                }
            }
        }

        internal NativeMethods.FOS GetDerivedOptionFlags(NativeMethods.FOS flags)
        {
            
                flags |= NativeMethods.FOS.FOS_PICKFOLDERS;
            // TODO: other flags

            return flags;
        }
    

        #region Public API

        private string title;
        public string Title
        {
            get { return title; }
            set 
            { 
                title = value;
                if (NativeDialogShowing)
                    nativeDialog.SetTitle(value);
            }
        }

        // TODO: implement AddExtension
        private bool addExtension;
        internal bool AddExtension
        {
            get { return addExtension; }
            set { addExtension = value; }
        }

        // This is the first of many properties that are backed by the FOS_*
        // bitflag options set with IFileDialog.SetOptions(). SetOptions() fails
        // if called while dialog is showing (e.g. from a callback)
        private bool checkFileExists;
        internal bool CheckFileExists
        {
            get { return checkFileExists; }
            set 
            {
                ThrowIfDialogShowing("CheckFileExists" + IllegalPropertyChangeString);
                checkFileExists = value; 
            }
        }

        private bool checkPathExists;
        internal bool CheckPathExists
        {
            get { return checkPathExists; }
            set 
            {
                ThrowIfDialogShowing("CheckPathExists" + IllegalPropertyChangeString);
                checkPathExists = value;
            }
        }

        private bool checkValidNames;
        internal bool CheckValidNames
        {
            get { return checkValidNames; }
            set 
            {
                ThrowIfDialogShowing("CheckValidNames" + IllegalPropertyChangeString);
                checkValidNames = value; 
            }
        }

        private bool checkReadOnly;
        internal bool CheckReadOnly
        {
            get { return checkReadOnly; }
            set 
            {
                ThrowIfDialogShowing("CheckReadOnly" + IllegalPropertyChangeString);
                checkReadOnly = value; 
            }
        }

        private bool restoreDirectory;
        internal bool RestoreDirectory
        {
            get { return restoreDirectory; }
            set 
            {
                ThrowIfDialogShowing("RestoreDirectory" + IllegalPropertyChangeString);
                restoreDirectory = value; 
            }
        }

        private bool showPlacesList = true;
        public bool ShowPlacesList
        {

            get { return showPlacesList; }
            set 
            {
                ThrowIfDialogShowing("ShowPlacesList" + IllegalPropertyChangeString);
                showPlacesList = value; 
            }
        }

        private bool addToMruList = true;
        public bool AddToMruList
        {
            get { return addToMruList; }
            set 
            {
                ThrowIfDialogShowing("AddToMruList" + IllegalPropertyChangeString);
                addToMruList = value; 
            }
        }

        private bool showHiddenItems;
        public bool ShowHiddenItems
        {
            get { return showHiddenItems; }
            set 
            {
                ThrowIfDialogShowing("ShowHiddenItems" + IllegalPropertyChangeString);
                showHiddenItems = value; 
            }
        }

        private bool dereferenceLinks;
        public bool DereferenceLinks
        {
            get { return dereferenceLinks; }
            set 
            {
                ThrowIfDialogShowing("DereferenceLinks" + IllegalPropertyChangeString);
                dereferenceLinks = value; }
        }

        private string fileName;
        public string FileName
        {
            get
            {
                CheckFileNamesAvailable();
                if (fileNames.Count > 1)
                    throw new InvalidOperationException(TeknoParrotUi.Properties.Resources.FolderBrowserMultipleFilesSelected);
                fileName = fileNames[0];
                return fileNames[0];
            }
            set
            {
                fileName = value;
            }
        }

        private string initialDirectory;
        public string InitialDirectory
        {
            get { return initialDirectory; }
            set { initialDirectory = value; }
        }

        public bool? ShowDialog(Window owner)
        {
            parentWindow = owner;
            return ShowDialog();
        }

        public bool? ShowDialog()
        {
            bool? result = null;

            try
            {
                // Fetch derived native dialog (i.e. Save or Open)

                InitializeNativeFileDialog();
                nativeDialog = GetNativeFileDialog();

                // Process custom controls, and validate overall state
                ProcessControls();
                ValidateCurrentDialogState();

                // Apply outer properties to native dialog instance
                ApplyNativeSettings(nativeDialog);

                // Show dialog
                showState = NativeDialogShowState.Showing;
                int hresult = nativeDialog.Show(GetHandleFromWindow(parentWindow));
                showState = NativeDialogShowState.Closed;

                // Create return information
                if (ErrorHelper.Matches(hresult, Win32ErrorCode.ERROR_CANCELLED))
                {
                    canceled = true;
                    fileNames.Clear();
                }
                else
                {
                    canceled = false;

                    // Populate filenames - though only if user didn't cancel
                    PopulateWithFileNames(fileNames);
                }
                result = !canceled.Value;
            }
            catch
            {
                //If Vista Style dialog is unavailable, fall back to Windows Forms

                System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
                dialog.SelectedPath = fileName;
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
            }
            finally
            {
                CleanUpNativeFileDialog();
                showState = NativeDialogShowState.Closed;
            }
            return result;
        }
        

        #endregion

        #region Configuration

        private void ApplyNativeSettings(IFileDialog dialog)
        {
            Debug.Assert(dialog != null, "No dialog instance to configure");

            if (parentWindow == null)
                parentWindow = Helpers.GetDefaultOwnerWindow();

            // Apply option bitflags
            dialog.SetOptions(CalculateNativeDialogOptionFlags());

            // Other property sets
            dialog.SetTitle(title);

            // TODO: Implement other property sets

            string directory = (String.IsNullOrEmpty(fileName)) ? initialDirectory : System.IO.Path.GetDirectoryName(fileName);


            if (directory != null)
            {
                IShellItem folder;
                try
                {
                    SHCreateItemFromParsingName(directory, IntPtr.Zero, new System.Guid(IIDGuid.IShellItem), out folder);

                    if (folder != null)
                        dialog.SetFolder(folder);
                }
                finally
                { }
            }
           

            if (!String.IsNullOrEmpty(fileName))
            {
                string name = System.IO.Path.GetFileName(fileName);
                dialog.SetFileName(name);
            }
        }

        private NativeMethods.FOS CalculateNativeDialogOptionFlags()
        {
            // We start with only a few flags set by default, then go from there based
            // on the current state of the managed dialog's property values
            NativeMethods.FOS flags = 
                NativeMethods.FOS.FOS_NOTESTFILECREATE
                | NativeMethods.FOS.FOS_FORCEFILESYSTEM;

            // Call to derived (concrete) dialog to set dialog-specific flags
            flags = GetDerivedOptionFlags(flags);

            // Apply other optional flags
            if (checkFileExists)
                flags |= NativeMethods.FOS.FOS_FILEMUSTEXIST;
            if (checkPathExists)
                flags |= NativeMethods.FOS.FOS_PATHMUSTEXIST;
            if (!checkValidNames)
                flags |= NativeMethods.FOS.FOS_NOVALIDATE;
            if (!checkReadOnly)
                flags |= NativeMethods.FOS.FOS_NOREADONLYRETURN;
            if (restoreDirectory)
                flags |= NativeMethods.FOS.FOS_NOCHANGEDIR;
            if (!showPlacesList)
                flags |= NativeMethods.FOS.FOS_HIDEPINNEDPLACES;
            if (!addToMruList)
                flags |= NativeMethods.FOS.FOS_DONTADDTORECENT;
            if (showHiddenItems)
                flags |= NativeMethods.FOS.FOS_FORCESHOWHIDDEN;
            if (!dereferenceLinks)
                flags |= NativeMethods.FOS.FOS_NODEREFERENCELINKS;
            return flags;
        }

        private void ValidateCurrentDialogState()
        {
            // TODO: Perform validation - both cross-property and pseudo-controls
        }

        private void ProcessControls()
        {
            // TODO: Sort controls if necesarry - COM API might not require it, however
        }

        #endregion

        //#region IDialogControlHost Members

        //bool IDialogControlHost.IsCollectionChangeAllowed()
        //{
        //    throw new Exception("The method or operation is not implemented.");
        //}

        //void IDialogControlHost.ApplyCollectionChanged()
        //{
        //    throw new Exception("The method or operation is not implemented.");
        //}

        //bool IDialogControlHost.IsControlPropertyChangeAllowed(string propertyName, DialogControl control)
        //{
        //    throw new Exception("The method or operation is not implemented.");
        //}

        //void IDialogControlHost.ApplyControlPropertyChange(string propertyName, DialogControl control)
        //{
        //    throw new Exception("The method or operation is not implemented.");
        //}

        //#endregion

        #region Helpers

        protected void CheckFileNamesAvailable()
        {
            if (showState != NativeDialogShowState.Closed)
                throw new InvalidOperationException(TeknoParrotUi.Properties.Resources.FolderBrowserFilenameNotAvailableShowing);
            if (canceled.GetValueOrDefault())
                throw new InvalidOperationException(TeknoParrotUi.Properties.Resources.FolderBrowserFilenameNotAvailableCanceled);
            Debug.Assert(fileNames.Count != 0,
                    "FileNames empty - shouldn't happen dialog unless dialog canceled or not yet shown");
        }

        private IntPtr GetHandleFromWindow(Window window)
        {
            if (window == null)
                return NativeMethods.NO_PARENT;
            return (new WindowInteropHelper(window)).Handle;
        }

        private bool IsOptionSet(IFileDialog dialog, NativeMethods.FOS flag)
        {
            NativeMethods.FOS currentFlags = GetCurrentOptionFlags(dialog);

            return (currentFlags & flag) == flag;
        }

        internal NativeMethods.FOS GetCurrentOptionFlags(IFileDialog dialog)
        {
            NativeMethods.FOS currentFlags;
            dialog.GetOptions(out currentFlags);
            return currentFlags;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
        [In, MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        [In] IntPtr pbc,
        [In, MarshalAs(UnmanagedType.LPStruct)] Guid iIdIShellItem,
        [Out, MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] out IShellItem iShellItem);

        #endregion

        #region Helpers

        private bool NativeDialogShowing
        {
            get
            {
                return (nativeDialog != null)
                    && (showState == NativeDialogShowState.Showing ||
                    showState == NativeDialogShowState.Closing);
            }
        }

        internal string GetFileNameFromShellItem(IShellItem item)
        {
            string filename;
            item.GetDisplayName(NativeMethods.SIGDN.SIGDN_DESKTOPABSOLUTEPARSING, out filename);
            return filename;
        }

        internal IShellItem GetShellItemAt(IShellItemArray array, int i)
        {
            IShellItem result;
            uint index = (uint)i;
            array.GetItemAt(index, out result);
            return result;
        }

        protected void ThrowIfDialogShowing(string message)
        {
            if (NativeDialogShowing)
                throw new NotSupportedException(message);
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            throw new Exception(TeknoParrotUi.Properties.Resources.FolderBrowserMethodNotImplemented);
        }

        #endregion

        #region Event handling members

        protected virtual void OnFileOk(CancelEventArgs e)
        {
            //CancelEventHandler handler = FileOk;
            //if (handler != null)
            //{
            //    handler(this, e);
            //}
        }

        //protected virtual void OnFolderChanging(CommonFileDialogFolderChangeEventArgs e)
        //{
        //    //EventHandler<CommonFileDialogFolderChangeEventArgs> handler = FolderChanging;
        //    //if (handler != null)
        //    //{
        //    //    handler(this, e);
        //    //}
        //}

        protected virtual void OnFolderChanged(EventArgs e)
        {
            //EventHandler handler = FolderChanged;
            //if (handler != null)
            //{
            //    handler(this, e);
            //}
        }

        protected virtual void OnSelectionChanged(EventArgs e)
        {
            //EventHandler handler = SelectionChanged;
            //if (handler != null)
            //{
            //    handler(this, e);
            //}
        }

        protected virtual void OnFileTypeChanged(EventArgs e)
        {
            //EventHandler handler = FileTypeChanged;
            //if (handler != null)
            //{
            //    handler(this, e);
            //}   
        }

        protected virtual void OnOpening(EventArgs e)
        {
            //EventHandler handler = Opening;
            //if (handler != null)
            //{
            //    handler(this, e);
            //}
        }

        #endregion

        #region NativeDialogEventSink Nested Class

        private class NativeDialogEventSink : IFileDialogEvents //, IFileDialogControlEvents
        {
            private WPFFolderBrowserDialog parent;
            private bool firstFolderChanged = true; 

            public NativeDialogEventSink(WPFFolderBrowserDialog commonDialog)
            {
                this.parent = commonDialog;
            }

            private uint cookie;
            public uint Cookie
            {
                get { return cookie; }
                set { cookie = value; }
            }
	
            public HRESULT OnFileOk(IFileDialog pfd)
            {
                CancelEventArgs args = new CancelEventArgs();
                parent.OnFileOk(args);
                return (args.Cancel ? HRESULT.S_FALSE : HRESULT.S_OK);
            }

            public HRESULT OnFolderChanging(IFileDialog pfd, IShellItem psiFolder)
            {
                return HRESULT.S_OK;
                //CommonFileDialogFolderChangeEventArgs args =
                //    new CommonFileDialogFolderChangeEventArgs(parent.GetFileNameFromShellItem(psiFolder));
                //if (!firstFolderChanged)
                //    parent.OnFolderChanging(args);
                //return (args.Cancel ? HRESULT.S_FALSE : HRESULT.S_OK);
            }

            public void OnFolderChange(IFileDialog pfd)
            {
                if (firstFolderChanged)
                {
                    firstFolderChanged = false;
                    parent.OnOpening(EventArgs.Empty);
                }
                else
                    parent.OnFolderChanged(EventArgs.Empty);
            }

            public void OnSelectionChange(IFileDialog pfd)
            {
                parent.OnSelectionChanged(EventArgs.Empty);
            }

            public void OnShareViolation(IFileDialog pfd, IShellItem psi, out NativeMethods.FDE_SHAREVIOLATION_RESPONSE pResponse)
            {
                // Do nothing: we will ignore share violations, and don't register
                // for them, so this method should never be called
                pResponse = NativeMethods.FDE_SHAREVIOLATION_RESPONSE.FDESVR_ACCEPT;
            }

            public void OnTypeChange(IFileDialog pfd)
            {
                parent.OnFileTypeChanged(EventArgs.Empty);
            }

            public void OnOverwrite(IFileDialog pfd, IShellItem psi, out NativeMethods.FDE_OVERWRITE_RESPONSE pResponse)
            {
                // TODO: Implement overwrite notification support
                pResponse = NativeMethods.FDE_OVERWRITE_RESPONSE.FDEOR_ACCEPT;
            }
            //public void OnItemSelected(IFileDialogCustomize pfdc, int dwIDCtl, int dwIDItem)
            //{
            //    // TODO: Implement OnItemSelected
            //}

            //public void OnButtonClicked(IFileDialogCustomize pfdc, int dwIDCtl)
            //{
            //    // TODO: Implement OnButtonClicked
            //}

            //public void OnCheckButtonToggled(IFileDialogCustomize pfdc, int dwIDCtl, bool bChecked)
            //{
            //    // TODO: Implement OnCheckButtonToggled
            //}

            //public void OnControlActivating(IFileDialogCustomize pfdc, int dwIDCtl)
            //{
            //    // TODO: Implement OnControlActivating
            //}
        }

        #endregion
    }


}
