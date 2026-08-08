using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Common.InputListening
{
    /// <summary>
    /// The kind of event carried by a <see cref="SunshineInputEventArgs"/>.
    /// </summary>
    public enum SunshineInputEventType
    {
        Roster,
        KeyDown,
        KeyUp,
        MouseMove,
        MouseButtonDown,
        MouseButtonUp,
        MouseWheel,
        AbsPosition,
        GamepadSlot
    }

    public class SunshineInputEventArgs : EventArgs
    {
        /// <summary>Player slot, 1-4.</summary>
        public int Player { get; set; }
        public SunshineInputEventType EventType { get; set; }

        /// <summary>Windows virtual-key code. Only valid for KeyDown/KeyUp.</summary>
        public ushort KeyCode { get; set; }

        /// <summary>Relative mouse move deltas. Only valid for MouseMove.</summary>
        public int DeltaX { get; set; }
        public int DeltaY { get; set; }

        /// <summary>Wheel delta. Only valid for MouseWheel (stored in DeltaX).</summary>

        /// <summary>0=Left, 1=Right, 2=Middle, 3=Button4, 4=Button5. Only valid for MouseButtonDown/Up.</summary>
        public int MouseButton { get; set; }

        /// <summary>Only valid for Roster.</summary>
        public bool Connected { get; set; }

        /// <summary>The real Windows XInput user index (0-3) ViGEmBus assigned this player's
        /// virtual controller. Only valid for GamepadSlot.</summary>
        public int XInputIndex { get; set; }
    }

    /// <summary>
    /// Connects to Sunshine's TeknoParrot identity-bridge named pipe
    /// (\\.\pipe\SunshineTeknoParrotInput) and raises <see cref="InputReceived"/> for each
    /// player-tagged event received.
    ///
    /// This exists because SendInput()-based keyboard/mouse injection on Windows carries no
    /// per-client device identity, so RawInput can't natively tell Sunshine's connected
    /// clients apart (they all show up as "Unknown Device"). Sunshine's fork tags every event
    /// with a player slot (1-4) before injecting it and forwards that tag here.
    ///
    /// The wire protocol MUST match Sunshine's src/platform/windows/teknoparrot_pipe.h exactly.
    /// All multi-byte fields are little-endian. Every message starts with a 1-byte type tag:
    ///
    ///   0x01 Roster       [type][player][connected]              3 bytes
    ///   0x02 Key          [type][player][down][vk_lo][vk_hi]     5 bytes
    ///   0x03 MouseMove    [type][player][dx0..3][dy0..3]         10 bytes
    ///   0x04 MouseButton  [type][player][down][button]           4 bytes
    ///   0x05 MouseWheel   [type][player][delta0..3]              6 bytes
    ///   0x06 AbsPosition  [type][player][x0..3][y0..3]           10 bytes
    ///   0x07 GamepadSlot  [type][player][xinputIndex]            3 bytes
    /// </summary>
    public static class SunshinePlayerInput
    {
        /// <summary>
        /// Player slot 1 is reserved for the host machine's own local keyboard/mouse in
        /// TeknoParrotUI's numbering convention, so Moonlight-connected clients are numbered
        /// starting at 2. Matches Sunshine's next_player_slot() round-robin range.
        /// </summary>
        public const int MinPlayer = 2;
        public const int MaxPlayer = 4;

        private const string PipeName = "SunshineTeknoParrotInput";

        private static Thread _thread;
        private static volatile bool _running;
        private static int _refCount;
        private static readonly object Lock = new object();

        private static readonly HashSet<int> ConnectedPlayers = new HashSet<int>();
        private static readonly object RosterLock = new object();

        /// <summary>
        /// Which player owns each real Windows XInput user index (0-3), as reported by
        /// Sunshine's teknoparrot_pipe::send_gamepad_slot(). XInput slots are handed out by
        /// Windows/ViGEmBus in allocation order, not tied to a specific player by design, so
        /// this mapping is the only reliable way to know "index 1 is currently Player 3's
        /// controller" - it can change across sessions/reconnects as controllers are
        /// (re)allocated, so always look it up fresh rather than assuming it's stable.
        /// </summary>
        private static readonly Dictionary<int, int> PlayerByXInputIndex = new Dictionary<int, int>();
        private static readonly object GamepadSlotLock = new object();

        /// <summary>
        /// Raised on a background thread whenever a player-tagged event arrives. Subscribers
        /// must marshal to the UI thread themselves if needed.
        /// </summary>
        public static event EventHandler<SunshineInputEventArgs> InputReceived;

        /// <summary>
        /// The synthetic RawInput-style device path used for a given player's keyboard/mouse
        /// binding. Used as RawInputButton.DevicePath, exactly like a real HID device path.
        /// </summary>
        public static string DevicePathForPlayer(int player) => $"SUNSHINE#PLAYER{player}";

        /// <summary>
        /// Display label shown in the TeknoParrotUI binding UI for a given player slot.
        /// </summary>
        public static string DisplayNameForPlayer(int player) => $"Streaming Device Player {player}";

        /// <summary>
        /// Which player currently owns the given real Windows XInput user index (0-3), or 0
        /// if unknown/unassigned. See <see cref="PlayerByXInputIndex"/> for why this has to be
        /// looked up live rather than assumed.
        /// </summary>
        public static int PlayerForXInputIndex(int xinputIndex)
        {
            lock (GamepadSlotLock)
            {
                return PlayerByXInputIndex.TryGetValue(xinputIndex, out var player) ? player : 0;
            }
        }

        /// <summary>
        /// The reverse of <see cref="DisplayNameForPlayer"/>, used by device-picker dropdowns
        /// (lightgun/trackball movement source selection) to resolve a selected display string
        /// back to a player slot.
        /// </summary>
        public static bool TryParsePlayerFromDisplayName(string displayName, out int player)
        {
            player = 0;
            if (string.IsNullOrEmpty(displayName))
            {
                return false;
            }

            for (int i = MinPlayer; i <= MaxPlayer; i++)
            {
                if (displayName == DisplayNameForPlayer(i))
                {
                    player = i;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The player slots (2-4) that currently have a live Sunshine client connected, sorted
        /// ascending. Used to populate device-picker dropdowns so only actually-connected
        /// players show up as options, rather than always offering all of 2-4. Cleared whenever
        /// the pipe connection to Sunshine is lost, since roster state is no longer known then.
        /// </summary>
        public static IReadOnlyList<int> GetConnectedPlayers()
        {
            lock (RosterLock)
            {
                var players = new List<int>(ConnectedPlayers);
                players.Sort();
                return players;
            }
        }

        /// <summary>
        /// Maps a pipe-protocol mouse button index (0=Left, 1=Right, 2=Middle, 3=Button4,
        /// 4=Button5) to TeknoParrotUI's RawMouseButton enum. Shared by every listener that
        /// consumes SunshineInputEventArgs so the mapping only lives in one place.
        /// </summary>
        public static RawMouseButton MapMouseButton(int button)
        {
            switch (button)
            {
                case 0: return RawMouseButton.LeftButton;
                case 1: return RawMouseButton.RightButton;
                case 2: return RawMouseButton.MiddleButton;
                case 3: return RawMouseButton.Button4;
                case 4: return RawMouseButton.Button5;
                default: return RawMouseButton.None;
            }
        }

        /// <summary>
        /// Starts the background listener if it isn't already running. Reference-counted so
        /// multiple independent consumers (config binding UI, gameplay dispatch) can each call
        /// Start()/Stop() without stepping on each other.
        /// </summary>
        public static void Start()
        {
            lock (Lock)
            {
                _refCount++;
                System.Diagnostics.Trace.WriteLine($"[SunshineDebug] SunshinePlayerInput.Start() called, refCount now {_refCount}, thread already running: {_running}");
                if (_running)
                {
                    return;
                }

                _running = true;
                _thread = new Thread(RunLoop)
                {
                    IsBackground = true,
                    Name = "SunshinePlayerInput"
                };
                _thread.Start();
            }
        }

        /// <summary>
        /// Releases one reference. Only actually stops the listener once every caller that
        /// called Start() has called Stop().
        /// </summary>
        public static void Stop()
        {
            lock (Lock)
            {
                if (_refCount > 0)
                {
                    _refCount--;
                }

                System.Diagnostics.Trace.WriteLine($"[SunshineDebug] SunshinePlayerInput.Stop() called, refCount now {_refCount} (thread intentionally kept alive regardless - see comment on Start())");

                // Deliberately NOT stopping the background thread here, even at refCount 0.
                // This class is called from several independent, short-lived UI screens (the
                // config binding screen, its raw-input listener, the in-game listener), each
                // with their own Start()/Stop() pairing. Balancing that ref count perfectly
                // across screen navigation proved fragile in practice - leaving a config screen
                // could drop the count to zero and kill the pipe connection moments before
                // gameplay needed it, with no automatic recovery. The thread is lightweight and
                // background-only (won't block app exit), so there's no real cost to just
                // keeping it alive for the process lifetime instead of chasing exact balance
                // across every call site.
            }
        }

        private static void RunLoop()
        {
            while (_running)
            {
                try
                {
                    using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.In))
                    {
                        // Sunshine may not be running, or may not have a client connected yet.
                        // Retry quietly rather than surfacing an error.
                        pipe.Connect(1000);

                        System.Diagnostics.Trace.WriteLine("[SunshineDebug] Connected to Sunshine's TeknoParrot pipe.");

                        while (_running && pipe.IsConnected)
                        {
                            if (!TryReadMessage(pipe))
                            {
                                break;
                            }
                        }

                        System.Diagnostics.Trace.WriteLine("[SunshineDebug] Disconnected from Sunshine's TeknoParrot pipe (pipe.IsConnected=" + pipe.IsConnected + ", _running=" + _running + ").");
                    }
                }
                catch (Exception ex)
                {
                    // Pipe not available / connection dropped. Fall through and retry below.
                    System.Diagnostics.Trace.WriteLine($"[SunshineDebug] Pipe connect/read failed: {ex.GetType().Name}: {ex.Message}");
                }
                finally
                {
                    // Roster state is only known while actually connected; don't show stale
                    // "connected" players once we've lost the pipe. Same for gamepad slot
                    // ownership - a reconnect means Sunshine will resend fresh GamepadSlot
                    // messages as controllers re-arrive, so don't trust the old mapping either.
                    lock (RosterLock)
                    {
                        ConnectedPlayers.Clear();
                    }
                    lock (GamepadSlotLock)
                    {
                        PlayerByXInputIndex.Clear();
                    }
                }

                if (_running)
                {
                    Thread.Sleep(1000);
                }
            }
        }

        private static bool TryReadMessage(Stream pipe)
        {
            int type = pipe.ReadByte();
            if (type < 0)
            {
                return false; // pipe closed
            }

            switch (type)
            {
                case 0x01: // Roster: player, connected
                {
                    var rest = ReadExact(pipe, 2);
                    if (rest == null) return false;

                    int player = rest[0];
                    bool connected = rest[1] != 0;

                    lock (RosterLock)
                    {
                        if (connected)
                            ConnectedPlayers.Add(player);
                        else
                            ConnectedPlayers.Remove(player);
                    }

                    Raise(new SunshineInputEventArgs
                    {
                        Player = player,
                        EventType = SunshineInputEventType.Roster,
                        Connected = connected
                    });
                    return true;
                }
                case 0x02: // Key: player, down, vk_lo, vk_hi
                {
                    var rest = ReadExact(pipe, 4);
                    if (rest == null) return false;

                    bool down = rest[1] != 0;
                    ushort vk = (ushort)(rest[2] | (rest[3] << 8));

                    Raise(new SunshineInputEventArgs
                    {
                        Player = rest[0],
                        EventType = down ? SunshineInputEventType.KeyDown : SunshineInputEventType.KeyUp,
                        KeyCode = vk
                    });
                    return true;
                }
                case 0x03: // MouseMove: player, dx(4), dy(4)
                {
                    var rest = ReadExact(pipe, 9);
                    if (rest == null) return false;

                    Raise(new SunshineInputEventArgs
                    {
                        Player = rest[0],
                        EventType = SunshineInputEventType.MouseMove,
                        DeltaX = BitConverter.ToInt32(rest, 1),
                        DeltaY = BitConverter.ToInt32(rest, 5)
                    });
                    return true;
                }
                case 0x04: // MouseButton: player, down, button
                {
                    var rest = ReadExact(pipe, 3);
                    if (rest == null) return false;

                    bool down = rest[1] != 0;

                    Raise(new SunshineInputEventArgs
                    {
                        Player = rest[0],
                        EventType = down ? SunshineInputEventType.MouseButtonDown : SunshineInputEventType.MouseButtonUp,
                        MouseButton = rest[2]
                    });
                    return true;
                }
                case 0x05: // MouseWheel: player, delta(4)
                {
                    var rest = ReadExact(pipe, 5);
                    if (rest == null) return false;

                    Raise(new SunshineInputEventArgs
                    {
                        Player = rest[0],
                        EventType = SunshineInputEventType.MouseWheel,
                        DeltaX = BitConverter.ToInt32(rest, 1)
                    });
                    return true;
                }
                case 0x06: // AbsPosition: player, x(4), y(4)
                {
                    var rest = ReadExact(pipe, 9);
                    if (rest == null)
                    {
                        System.Diagnostics.Trace.WriteLine("[SunshineDebug] AbsPosition: ReadExact returned null - pipe closed mid-message");
                        return false;
                    }

                    int px = rest[0];
                    int vx = BitConverter.ToInt32(rest, 1);
                    int vy = BitConverter.ToInt32(rest, 5);
                    System.Diagnostics.Trace.WriteLine($"[SunshineDebug] AbsPosition received: player={px} x={vx} y={vy}");

                    Raise(new SunshineInputEventArgs
                    {
                        Player = rest[0],
                        EventType = SunshineInputEventType.AbsPosition,
                        DeltaX = BitConverter.ToInt32(rest, 1),
                        DeltaY = BitConverter.ToInt32(rest, 5)
                    });
                    return true;
                }
                case 0x07: // GamepadSlot: player, xinputIndex
                {
                    var rest = ReadExact(pipe, 2);
                    if (rest == null) return false;

                    int player = rest[0];
                    int xinputIndex = rest[1];

                    lock (GamepadSlotLock)
                    {
                        PlayerByXInputIndex[xinputIndex] = player;
                    }

                    Raise(new SunshineInputEventArgs
                    {
                        Player = player,
                        EventType = SunshineInputEventType.GamepadSlot,
                        XInputIndex = xinputIndex
                    });
                    return true;
                }
                default:
                    // Unknown/unsynced message type - we can't safely resume mid-stream.
                    // Drop the connection and reconnect fresh.
                    return false;
            }
        }

        private static void Raise(SunshineInputEventArgs args)
        {
            InputReceived?.Invoke(null, args);
        }

        private static byte[] ReadExact(Stream stream, int count)
        {
            var buf = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buf, offset, count - offset);
                if (read <= 0)
                {
                    return null; // pipe closed mid-message
                }
                offset += read;
            }
            return buf;
        }
    }
}
