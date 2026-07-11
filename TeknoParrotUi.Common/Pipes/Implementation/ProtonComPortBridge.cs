using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using TeknoParrotUi.Common.Pipes.Abstractions;
using TeknoParrotUi.Common.Proton;

namespace TeknoParrotUi.Common.Pipes.Implementation
{
    /// <summary>
    /// Linux+Proton implementation of <see cref="IComPortBridge"/> for games
    /// that talk JVS over a serial port (Type-X2, Ex-Board).
    ///
    /// Wine maps COM ports through symlinks in $WINEPREFIX/dosdevices, so no
    /// in-prefix helper is required:
    ///   1. Create a pseudo-terminal (PTY) pair in raw mode.
    ///   2. Symlink $WINEPREFIX/dosdevices/com1 -> PTY slave.
    ///   3. The game opens COM1; TPUI reads/writes the PTY master.
    /// </summary>
    public class ProtonComPortBridge : IComPortBridge
    {
        private const int O_RDWR = 0x0002;
        private const int O_NOCTTY = 0x0100;
        private const int TCSANOW = 0;
        private const int TermiosBufferSize = 128; // >= sizeof(struct termios) on glibc/musl

        [DllImport("libc", SetLastError = true)]
        private static extern int posix_openpt(int flags);

        [DllImport("libc", SetLastError = true)]
        private static extern int grantpt(int fd);

        [DllImport("libc", SetLastError = true)]
        private static extern int unlockpt(int fd);

        [DllImport("libc", SetLastError = true)]
        private static extern int ptsname_r(int fd, byte[] buf, IntPtr buflen);

        [DllImport("libc", SetLastError = true)]
        private static extern int symlink(string target, string linkpath);

        [DllImport("libc", SetLastError = true)]
        private static extern int unlink(string pathname);

        [DllImport("libc", SetLastError = true)]
        private static extern int tcgetattr(int fd, byte[] termios);

        [DllImport("libc", SetLastError = true)]
        private static extern int tcsetattr(int fd, int optionalActions, byte[] termios);

        [DllImport("libc")]
        private static extern void cfmakeraw(byte[] termios);

        private readonly string _winePrefix;
        private SafeFileHandle _masterHandle;
        private FileStream _masterStream;
        private string _slavePath;
        private string _dosDeviceLink;
        private string _backupLink;
        private bool _started;

        public string PortName { get; }
        public bool IsConnected => _started;

        /// <param name="portName">Port the game opens, e.g. "COM1".</param>
        /// <param name="winePrefix">
        /// Wine prefix of the game. When null, resolved via ProtonRuntime /
        /// process detection at <see cref="Start"/> time.
        /// </param>
        public ProtonComPortBridge(string portName = "COM1", string winePrefix = null)
        {
            PortName = portName;
            _winePrefix = winePrefix;
        }

        public void Start()
        {
            if (_started)
                return;

            var prefix = _winePrefix
                         ?? ProtonRuntime.CurrentGame?.WinePrefix
                         ?? ProtonProcessDetector.FindRunningProtonGame(ProtonRuntime.ExpectedExecutable)?.WinePrefix;
            if (prefix == null)
                throw new InvalidOperationException(
                    "Cannot resolve WINEPREFIX for COM port bridge. Launch the game in Proton first or pass the prefix explicitly.");

            // 1. PTY pair in raw mode.
            var masterFd = posix_openpt(O_RDWR | O_NOCTTY);
            if (masterFd < 0)
                throw new IOException($"posix_openpt failed (errno {Marshal.GetLastWin32Error()})");

            if (grantpt(masterFd) != 0 || unlockpt(masterFd) != 0)
                throw new IOException($"grantpt/unlockpt failed (errno {Marshal.GetLastWin32Error()})");

            var nameBuf = new byte[256];
            if (ptsname_r(masterFd, nameBuf, (IntPtr)nameBuf.Length) != 0)
                throw new IOException($"ptsname_r failed (errno {Marshal.GetLastWin32Error()})");
            _slavePath = Encoding.UTF8.GetString(nameBuf, 0, Array.IndexOf(nameBuf, (byte)0));

            // Raw mode: no echo/line discipline mangling the JVS byte stream.
            var termios = new byte[TermiosBufferSize];
            if (tcgetattr(masterFd, termios) == 0)
            {
                cfmakeraw(termios);
                tcsetattr(masterFd, TCSANOW, termios);
            }

            _masterHandle = new SafeFileHandle((IntPtr)masterFd, ownsHandle: true);
            _masterStream = new FileStream(_masterHandle, FileAccess.ReadWrite);

            // 2. Point the Wine COM device at the PTY slave.
            var dosDevices = Path.Combine(prefix, "dosdevices");
            Directory.CreateDirectory(dosDevices);
            _dosDeviceLink = Path.Combine(dosDevices, PortName.ToLowerInvariant());
            _backupLink = _dosDeviceLink + ".tp-backup";

            if (File.Exists(_dosDeviceLink) || Directory.Exists(_dosDeviceLink) || IsSymlink(_dosDeviceLink))
            {
                if (File.Exists(_backupLink) || IsSymlink(_backupLink))
                    unlink(_backupLink);
                File.Move(_dosDeviceLink, _backupLink);
            }

            if (symlink(_slavePath, _dosDeviceLink) != 0)
                throw new IOException(
                    $"Failed to create {_dosDeviceLink} -> {_slavePath} (errno {Marshal.GetLastWin32Error()})");

            _started = true;
        }

        public void Stop()
        {
            if (!_started)
                return;
            _started = false;

            try
            {
                if (_dosDeviceLink != null && IsSymlink(_dosDeviceLink))
                    unlink(_dosDeviceLink);
                if (_backupLink != null && (File.Exists(_backupLink) || IsSymlink(_backupLink)))
                    File.Move(_backupLink, _dosDeviceLink);
            }
            catch { /* best effort restore */ }

            try { _masterStream?.Dispose(); } catch { /* ignored */ }
            _masterStream = null;
            _masterHandle = null;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            EnsureStarted();
            return _masterStream.Read(buffer, offset, count);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            EnsureStarted();
            _masterStream.Write(buffer, offset, count);
            _masterStream.Flush();
        }

        public void Dispose() => Stop();

        private void EnsureStarted()
        {
            if (!_started)
                throw new InvalidOperationException("COM port bridge not started.");
        }

        private static bool IsSymlink(string path)
        {
            try
            {
                return new FileInfo(path).LinkTarget != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
