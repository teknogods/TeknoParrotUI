using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TeknoParrotUi.Common
{
    public class XDelta3
    {
        /// <summary>
        /// Sets the maximum buffer size that xdelta3 is allowed to write to.
        /// </summary>
        static readonly int MAX_BUFFER = 32 * 1024 * 1024; // 32 MB

        private static readonly string RPC_PATH = Path.Combine("libs", "xdelta3.dll");
        private static readonly HttpClient DownloadClient = new(
            new SocketsHttpHandler { UseProxy = false })
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        public static bool checkForXdelta()
        {
            // This wrapper P/Invokes a Windows PE DLL. Reject non-Windows
            // platforms even if an old/shared installation happens to contain
            // libs/xdelta3.dll; file presence cannot make that binary loadable.
            if (!IsNativeDependencySupportedPlatform(OperatingSystem.IsWindows()))
                return false;

            if (!File.Exists(RPC_PATH))
            {
                try
                {
                    using var response = DownloadClient.GetAsync(
                            "https://nzgamer41.win/TeknoParrot/TPRedists/xdelta3.zip",
                            HttpCompletionOption.ResponseHeadersRead)
                        .GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                    using var responseStream = response.Content.ReadAsStream();
                    using var zip = new ZipArchive(responseStream, ZipArchiveMode.Read);
                    var entry = zip.Entries.FirstOrDefault(candidate =>
                        candidate.FullName.Equals("xdelta3.dll", StringComparison.Ordinal) &&
                        candidate.Length > 0);
                    if (entry == null)
                        return false;

                    var directory = Path.GetDirectoryName(RPC_PATH);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);

                    using (var entryStream = entry.Open())
                    using (var payload = new MemoryStream(
                               entry.Length <= int.MaxValue ? (int)entry.Length : 0))
                    {
                        entryStream.CopyTo(payload);
                        if (payload.Length != entry.Length)
                            return false;
                        File.WriteAllBytes(RPC_PATH, payload.ToArray());
                    }

                    return File.Exists(RPC_PATH);
                }
                catch (Exception)
                {
                    // don't bother showing a messagebox or anything
                    return false;
                }
            }

            return true;
        }

        internal static bool IsNativeDependencySupportedPlatform(bool isWindows) =>
            isWindows;



        /// <summary>
        /// Creates xdelta3 patch from source to target.
        /// </summary>
        /// <param name="target">The target of the patch (the outcome of patching).</param>
        /// <param name="source">The source of the patch (what will be patched).</param>
        /// <returns>Xdelta3 patch data.</returns>
        public static byte[] CreatePatch(byte[] target, byte[] source)
        {
            if (checkForXdelta())
            {
                byte[] obuf = new byte[MAX_BUFFER];
                UInt32 obufSize;

                // Call xdelta3 library
                int result = xd3_encode_memory(target, (UInt32) target.Length,
                    source, (UInt32) source.Length,
                    obuf, out obufSize,
                    (UInt32) obuf.Length, 0);

                // Check result
                if (result != 0)
                {
                    throw new xdelta3Exception(result);
                }

                // Trim the output
                byte[] output = new byte[obufSize];
                Buffer.BlockCopy(obuf, 0, output, 0, (int) obufSize);

                return output;
            }
            else
            {
                throw CreateUnavailableException();
            }
        }

        /// <summary>
        /// Applies xdelta3 patch to source.
        /// </summary>
        /// <param name="patch">xdelta3 patch data.</param>
        /// <param name="source">The data to be patched.</param>
        /// <returns>Patched data.</returns>
        public static byte[] ApplyPatch(byte[] patch, byte[] source)
        {
            if (checkForXdelta())
            {
                byte[] obuf = new byte[MAX_BUFFER];
                UInt32 obufSize;

                // Call xdelta3 library
                int result = xd3_decode_memory(patch, (UInt32) patch.Length,
                    source, (UInt32) source.Length,
                    obuf, out obufSize,
                    (UInt32) obuf.Length, 0);

                // Check result
                if (result != 0)
                {
                    throw new xdelta3Exception(result);
                }

                // Trim the output
                byte[] output = new byte[obufSize];
                Buffer.BlockCopy(obuf, 0, output, 0, (int) obufSize);

                return output;
            }
            else
            {
                throw CreateUnavailableException();
            }
        }

        private static Exception CreateUnavailableException() =>
            OperatingSystem.IsWindows()
                ? new InvalidOperationException("The xdelta3 native dependency is unavailable.")
                : new PlatformNotSupportedException(
                    "The bundled xdelta3 native dependency is Windows-only.");


        #region PInvoke wrappers

        [DllImport("libs\\xdelta3.dll", EntryPoint = "xd3_encode_memory", CallingConvention = CallingConvention.Cdecl)]
        static extern int xd3_encode_memory(
            byte[] input,
            UInt32 input_size,
            byte[] source,
            UInt32 source_size,
            byte[] output_buffer,
            out UInt32 output_size,
            UInt32 avail_output,
            int flags);

        [DllImport("libs\\xdelta3.dll", EntryPoint = "xd3_decode_memory", CallingConvention = CallingConvention.Cdecl)]
        static extern int xd3_decode_memory(
            byte[] input,
            UInt32 input_size,
            byte[] source,
            UInt32 source_size,
            byte[] output_buffer,
            out UInt32 output_size,
            UInt32 avail_output,
            int flags);

        #endregion

    }

    # region Exceptions

    public class xdelta3Exception : Exception
    {
        public int ExceptionCode { get; set; }

        public xdelta3Exception(int rCode)
        {
            this.ExceptionCode = rCode;
        }
    }

    #endregion
}
