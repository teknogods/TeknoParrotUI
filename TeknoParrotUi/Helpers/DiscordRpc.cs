//from official Discord code sample
//https://raw.githubusercontent.com/discordapp/discord-rpc/af380116a07a5169c8e0f72de06db92ac178a22b/examples/button-clicker/Assets/DiscordRpc.cs
//A few small changes have been made.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using TeknoParrotUi.Common;

public class DiscordRPC
{
    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct RichPresenceStruct
    {
        public IntPtr state; /* max 128 bytes */
        public IntPtr details; /* max 128 bytes */
        public long startTimestamp;
        public long endTimestamp;
        public IntPtr largeImageKey; /* max 32 bytes */
        public IntPtr largeImageText; /* max 128 bytes */
        public IntPtr smallImageKey; /* max 32 bytes */
        public IntPtr smallImageText; /* max 128 bytes */
        public IntPtr partyId; /* max 128 bytes */
        public int partySize;
        public int partyMax;
        public IntPtr matchSecret; /* max 128 bytes */
        public IntPtr joinSecret; /* max 128 bytes */
        public IntPtr spectateSecret; /* max 128 bytes */
        public bool instance;
    }

    // Discord Rich Presence application ID
    private const string APP_ID = "508838453937438752";
    // DLL path
    private const string RPC_PATH = "libs\\discord-rpc.dll";

    [DllImport(RPC_PATH, EntryPoint = "Discord_Initialize", CallingConvention = CallingConvention.Cdecl)]
    private static extern void Initialize(string applicationId, IntPtr handlers, bool autoRegister, string optionalSteamId);

    [DllImport(RPC_PATH, EntryPoint = "Discord_Shutdown", CallingConvention = CallingConvention.Cdecl)]
    public static extern void Shutdown();

    [DllImport(RPC_PATH, EntryPoint = "Discord_UpdatePresence", CallingConvention = CallingConvention.Cdecl)]
    private static extern void UpdatePresenceNative(ref RichPresenceStruct presence);

    [DllImport(RPC_PATH, EntryPoint = "Discord_ClearPresence", CallingConvention = CallingConvention.Cdecl)]
    public static extern void ClearPresence();

    public static void UpdatePresence(RichPresence presence)
    {
        try
        {
            if (presence == null)
            {
                ClearPresence();
            }
            else
            {
                var presencestruct = presence.GetStruct();
                UpdatePresenceNative(ref presencestruct);
                presence.FreeMem();
            }
        }
        catch (Exception e)
        {
            // skip?
        }
    }

    public static void StartOrShutdown()
    {
        try
        {
            // download the DLL if it doesn't exist
            if (!File.Exists(RPC_PATH))
            {
                var request = (HttpWebRequest)WebRequest.Create("https://github.com/discordapp/discord-rpc/releases/download/v3.4.0/discord-rpc-win.zip");
                request.Timeout = 10000;
                request.Proxy = null;

                using (var response = request.GetResponse().GetResponseStream())
                using (var zip = new ZipArchive(response, ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.FullName == "discord-rpc/win32-dynamic/bin/discord-rpc.dll")
                        {
                            using (var entryStream = entry.Open())
                            using (var dll = File.Create(RPC_PATH))
                            {
                                entryStream.CopyTo(dll);
                            }
                        }
                    }
                }
            }

            // calling Initialize / Shutdown if the library is already/hasn't been initialized is fine.
            if (Lazydata.ParrotData.UseDiscordRPC)
            {
                Initialize(APP_ID, IntPtr.Zero, false, null);
            }
            else
            {
                Shutdown();
            }
        }
        catch (Exception e)
        {
            // don't bother showing a messagebox or anything
            Lazydata.ParrotData.UseDiscordRPC = false;
            JoystickHelper.Serialize();
            return;
        }
    }

    public class RichPresence
    {
        private RichPresenceStruct _presence;
        private readonly List<IntPtr> _buffers = new List<IntPtr>(10);

        public string state; /* max 128 bytes */
        public string details; /* max 128 bytes */
        public long startTimestamp;
        public long endTimestamp;
        public string largeImageKey; /* max 32 bytes */
        public string largeImageText; /* max 128 bytes */
        public string smallImageKey; /* max 32 bytes */
        public string smallImageText; /* max 128 bytes */
        public string partyId; /* max 128 bytes */
        public int partySize;
        public int partyMax;
        public string matchSecret; /* max 128 bytes */
        public string joinSecret; /* max 128 bytes */
        public string spectateSecret; /* max 128 bytes */
        public bool instance;

        /// <summary>
        /// Get the <see cref="RichPresenceStruct"/> reprensentation of this instance
        /// </summary>
        /// <returns><see cref="RichPresenceStruct"/> reprensentation of this instance</returns>
        internal RichPresenceStruct GetStruct()
        {
            if (_buffers.Count > 0)
            {
                FreeMem();
            }

            _presence.state = StrToPtr(state);
            _presence.details = StrToPtr(details);
            _presence.startTimestamp = startTimestamp;
            _presence.endTimestamp = endTimestamp;
            _presence.largeImageKey = StrToPtr(largeImageKey);
            _presence.largeImageText = StrToPtr(largeImageText);
            _presence.smallImageKey = StrToPtr(smallImageKey);
            _presence.smallImageText = StrToPtr(smallImageText);
            _presence.partyId = StrToPtr(partyId);
            _presence.partySize = partySize;
            _presence.partyMax = partyMax;
            _presence.matchSecret = StrToPtr(matchSecret);
            _presence.joinSecret = StrToPtr(joinSecret);
            _presence.spectateSecret = StrToPtr(spectateSecret);
            _presence.instance = instance;

            return _presence;
        }

        /// <summary>
        /// Returns a pointer to a representation of the given string with a size of maxbytes
        /// </summary>
        /// <param name="input">String to convert</param>
        /// <returns>Pointer to the UTF-8 representation of <see cref="input"/></returns>
        private IntPtr StrToPtr(string input)
        {
            if (string.IsNullOrEmpty(input)) return IntPtr.Zero;
            var convbytecnt = Encoding.UTF8.GetByteCount(input);
            var buffer = Marshal.AllocHGlobal(convbytecnt + 1);
            for (int i = 0; i < convbytecnt + 1; i++)
            {
                Marshal.WriteByte(buffer, i, 0);
            }
            _buffers.Add(buffer);
            Marshal.Copy(Encoding.UTF8.GetBytes(input), 0, buffer, convbytecnt);
            return buffer;
        }

        /// <summary>
        /// Free the allocated memory for conversion to <see cref="RichPresenceStruct"/>
        /// </summary>
        internal void FreeMem()
        {
            for (var i = _buffers.Count - 1; i >= 0; i--)
            {
                Marshal.FreeHGlobal(_buffers[i]);
                _buffers.RemoveAt(i);
            }
        }
    }
}