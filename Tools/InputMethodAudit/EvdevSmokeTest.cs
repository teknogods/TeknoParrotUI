using System;
using System.Threading;
using TeknoParrotUi.Common.InputListening.Mouse;

namespace InputMethodAudit
{
    /// <summary>
    /// Manual verification for the Linux evdev mouse backend (Phase 2 testing).
    /// Lists mouse devices and streams motion/button events for 10 seconds.
    /// Usage: dotnet run --project Tools/InputMethodAudit -- evdev-test
    /// </summary>
    internal static class EvdevSmokeTest
    {
        public static int Run()
        {
            var mice = EvdevInterop.EnumerateMice();
            Console.WriteLine($"Found {mice.Count} mouse device(s):");
            foreach (var m in mice)
                Console.WriteLine($"  {m.EventNode}  \"{m.Name}\"  path={m.DevicePath}");

            var keyboards = EvdevInterop.EnumerateKeyboards();
            Console.WriteLine($"Found {keyboards.Count} keyboard device(s):");
            foreach (var k in keyboards)
                Console.WriteLine($"  {k.EventNode}  \"{k.Name}\"");
            if (mice.Count == 0)
                return 1;

            Console.WriteLine("\nStreaming events for 10s — move the mouse / click...");
            var device = mice[0];
            int fd = EvdevInterop.open(device.EventNode, EvdevInterop.O_RDONLY | EvdevInterop.O_NONBLOCK);
            if (fd < 0)
            {
                Console.Error.WriteLine($"Cannot open {device.EventNode} — add your user to the 'input' group:");
                Console.Error.WriteLine("  sudo usermod -aG input $USER  (then re-login)");
                return 1;
            }

            bool hasAbs = EvdevInterop.TryGetAbsInfo(fd, EvdevInterop.ABS_X, out var absX);
            Console.WriteLine(hasAbs && absX.Maximum > absX.Minimum
                ? $"Absolute device: X range {absX.Minimum}..{absX.Maximum}"
                : "Relative device");

            int events = 0;
            var end = DateTime.UtcNow.AddSeconds(10);
            try
            {
                while (DateTime.UtcNow < end)
                {
                    while (EvdevInterop.TryReadEvent(fd, out var ev))
                    {
                        if (ev.Type == EvdevInterop.EV_REL || ev.Type == EvdevInterop.EV_ABS || ev.Type == EvdevInterop.EV_KEY)
                        {
                            if (events++ < 30)
                                Console.WriteLine($"  type={ev.Type} code=0x{ev.Code:X} value={ev.Value}");
                        }
                    }
                    Thread.Sleep(2);
                }
            }
            finally
            {
                EvdevInterop.close(fd);
            }
            Console.WriteLine($"Total events: {events}");
            return events > 0 ? 0 : 1;
        }
    }
}
