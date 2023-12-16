using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    public class XDelta3
    {
        /// <summary>
        /// Sets the maximum buffer size that xdelta3 is allowed to write to.
        /// </summary>
        static readonly int MAX_BUFFER = 32 * 1024 * 1024; // 32 MB

        private static readonly string RPC_PATH = "libs\\xdelta3.dll";
        public static bool checkForXdelta()
        {
            if (!File.Exists(RPC_PATH))
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create("https://nzgamer41.win/TeknoParrot/TPRedists/xdelta3.zip");
                    request.Timeout = 10000;
                    request.Proxy = null;

                    using (var response = request.GetResponse().GetResponseStream())
                    using (var zip = new ZipArchive(response, ZipArchiveMode.Read))
                    {
                        foreach (var entry in zip.Entries)
                        {
                            if (entry.FullName == "xdelta3.dll")
                            {
                                using (var entryStream = entry.Open())
                                using (var dll = File.Create(RPC_PATH))
                                {
                                    entryStream.CopyTo(dll);
                                }
                            }
                        }
                    }

                    return true;
                }
                catch (Exception e)
                {
                    // don't bother showing a messagebox or anything
                    return false;
                }
            }

            return true;
        }



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
                throw new Exception("Xdelta Error!");
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
                throw new Exception("Xdelta Error!");
            }
        }


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
