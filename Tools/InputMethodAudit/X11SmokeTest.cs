using System;
using System.Runtime.InteropServices;
using System.Threading;
using TeknoParrotUi.Common.InputListening.Mouse;

namespace InputMethodAudit
{
    /// <summary>
    /// Manual verification for the permission-free X11 input fallback.
    /// First runs an automated warp/read-back self-test (no interaction), then
    /// polls cursor/buttons/keys for 10 seconds — no /dev/input access needed.
    /// Usage: dotnet run --project Tools/InputMethodAudit -- x11-test
    /// </summary>
    internal static class X11SmokeTest
    {
        [DllImport("libX11.so.6")]
        private static extern int XWarpPointer(IntPtr display, IntPtr src, IntPtr dst,
            int srcX, int srcY, uint srcW, uint srcH, int dstX, int dstY);

        [DllImport("libX11.so.6")]
        private static extern int XFlush(IntPtr display);

        public static int Run()
        {
            Console.WriteLine($"DISPLAY={Environment.GetEnvironmentVariable("DISPLAY") ?? "(unset)"}");
            if (!X11Interop.IsAvailable())
            {
                Console.Error.WriteLine("No X display available (DISPLAY unset, libX11 missing, or connection refused).");
                return 1;
            }
            Console.WriteLine("X display: OK");
            Console.WriteLine($"Evdev fallback decision: mouseReadable={EvdevInterop.AnyReadableMouse()} keyboardReadable={EvdevInterop.AnyReadableKeyboard()}");

            var display = X11Interop.XOpenDisplay(null);
            var root = X11Interop.XDefaultRootWindow(display);
            int screen = X11Interop.XDefaultScreen(display);
            int width = X11Interop.XDisplayWidth(display, screen);
            int height = X11Interop.XDisplayHeight(display, screen);
            Console.WriteLine($"Screen: {width}x{height}");

            // XInput2 raw-event support (the primary rootless path).
            int opcode = X11Interop.SelectRawEvents(display);
            if (opcode >= 0)
            {
                Console.WriteLine($"XInput2: OK (opcode {opcode}) — raw events active (event-driven, per-device)");
                var pointers = X11Interop.EnumeratePointerDevices(display);
                Console.WriteLine($"Physical pointer devices ({pointers.Count}):");
                for (int i = 0; i < pointers.Count; i++)
                    Console.WriteLine($"  [{i}] XI id {pointers[i].SourceId}  \"{pointers[i].Name}\"" +
                                      (i < 4 ? $"  -> Player {i + 1}" : "  (unassigned)"));
                if (pointers.Count > 1)
                    Console.WriteLine("  Multi-gun capable WITHOUT root on this server (per-device raw events).");
            }
            else
            {
                Console.WriteLine("XInput2: unavailable — will use XQueryPointer polling (single pointer).");
            }

            // Automated self-test: warp the pointer through the X server and read
            // it back with the exact call the fallback listener uses.
            int failures = 0;
            foreach (var (tx, ty) in new[] { (width / 2, height / 2), (100, 100), (width - 50, height - 50) })
            {
                XWarpPointer(display, IntPtr.Zero, root, 0, 0, 0, 0, tx, ty);
                XFlush(display);
                Thread.Sleep(50);
                X11Interop.XQueryPointer(display, root, out _, out _, out int rx, out int ry, out _, out _, out _);
                bool ok = Math.Abs(rx - tx) <= 1 && Math.Abs(ry - ty) <= 1;
                Console.WriteLine($"  warp → ({tx},{ty})  read ← ({rx},{ry})  {(ok ? "OK" : "FAIL")}");
                if (!ok) failures++;
            }
            Console.WriteLine(failures == 0
                ? "Self-test PASSED: pointer round-trip via X server works."
                : $"Self-test FAILED: {failures} mismatch(es).");

            Console.WriteLine("\nPolling for 10s — move the mouse / click / press keys (focus an X11 window on Wayland)...");

            uint prevMask = 0;
            int lastX = int.MinValue, lastY = int.MinValue;
            var keymap = new byte[32];
            var prevKeymap = new byte[32];
            int motionLines = 0;
            var end = DateTime.UtcNow.AddSeconds(10);

            try
            {
                while (DateTime.UtcNow < end)
                {
                    if (X11Interop.XQueryPointer(display, root, out _, out _,
                            out int x, out int y, out _, out _, out uint mask))
                    {
                        if ((x != lastX || y != lastY) && motionLines < 20)
                        {
                            Console.WriteLine($"  pointer {x},{y}");
                            motionLines++;
                            lastX = x; lastY = y;
                        }
                        foreach (var (bit, name) in new[]
                                 {
                                     (X11Interop.Button1Mask, "Left"),
                                     (X11Interop.Button2Mask, "Middle"),
                                     (X11Interop.Button3Mask, "Right")
                                 })
                        {
                            bool now = (mask & bit) != 0;
                            bool before = (prevMask & bit) != 0;
                            if (now != before)
                                Console.WriteLine($"  button {name} {(now ? "DOWN" : "UP")}");
                        }
                        prevMask = mask;
                    }

                    X11Interop.XQueryKeymap(display, keymap);
                    for (int keycode = 8; keycode < 256; keycode++)
                    {
                        bool now = (keymap[keycode >> 3] & (1 << (keycode & 7))) != 0;
                        bool before = (prevKeymap[keycode >> 3] & (1 << (keycode & 7))) != 0;
                        if (now == before)
                            continue;
                        var key = TeknoParrotUi.Common.InputListening.Keyboard.EvdevKeyMap.ToKeys((ushort)(keycode - 8));
                        Console.WriteLine($"  key code={keycode} ({key}) {(now ? "DOWN" : "UP")}");
                    }
                    Buffer.BlockCopy(keymap, 0, prevKeymap, 0, 32);

                    Thread.Sleep(4);
                }
            }
            finally
            {
                X11Interop.XCloseDisplay(display);
            }

            Console.WriteLine("Done.");
            return failures == 0 ? 0 : 1;
        }
    }
}
