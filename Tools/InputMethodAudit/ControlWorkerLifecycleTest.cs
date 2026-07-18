using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening;
using TeknoParrotUi.Common.Pipes;
using TeknoParrotUi.Views.GameRunningCode.ControlHandlers;

namespace InputMethodAudit
{
    internal static class ControlWorkerLifecycleTest
    {
        private sealed class ProbeSender : ControlSender
        {
            public readonly ManualResetEventSlim Entered = new(false);
            public int Calls;

            public override void Transmit()
            {
                Interlocked.Increment(ref Calls);
                Entered.Set();
            }
        }

        public static int Run()
        {
            var failures = new List<string>();
            var checks = 0;

            void Check(string name, bool condition)
            {
                checks++;
                if (!condition)
                    failures.Add(name);
            }

            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                var stopwatch = Stopwatch.StartNew();
                GunControlHandler.HandleRamboControls(cancelled.Token);
                GunControlHandler.HandleGSEvoReload(cancelled.Token);
                OlympicControlHandler.HandleOlympicControls(cancelled.Token);
                OlympicControlHandler.Handle2020OlympicControls(cancelled.Token);
                Check("Pre-cancelled special handlers return immediately",
                    stopwatch.Elapsed < TimeSpan.FromMilliseconds(100));
            }

            GunControlHandler.SetKillFlag(false);
            OlympicControlHandler.SetKillFlag(false);
            using (var cancellation = new CancellationTokenSource())
            {
                var worker = new Thread(
                    () => OlympicControlHandler.Handle2020OlympicControls(
                        cancellation.Token))
                {
                    IsBackground = true
                };
                worker.Start();
                Thread.Sleep(30);
                cancellation.Cancel();
                Check("Active Olympic handler observes its session cancellation",
                    worker.Join(TimeSpan.FromMilliseconds(500)));
            }

            var sender = new ProbeSender { SleepTime = 250 };
            sender.Start();
            Check("ControlSender worker starts", sender.Entered.Wait(500));
            var stopWatch = Stopwatch.StartNew();
            Check("ControlSender StopAndWait joins the worker",
                sender.StopAndWait(TimeSpan.FromMilliseconds(500)));
            Check("ControlSender stop wakes the timed wait promptly",
                stopWatch.Elapsed < TimeSpan.FromMilliseconds(200));
            Check("ControlSender reports stopped after join", !sender.Running);

            sender.Entered.Reset();
            sender.Start();
            Check("ControlSender can be reused for a later session",
                sender.Entered.Wait(500));
            Check("Reused ControlSender also stops cleanly",
                sender.StopAndWait(TimeSpan.FromMilliseconds(500)));

            var settingsPipe = new SettingsSyncPipe(
                new GameProfile(),
                _ => { });
            settingsPipe.Start();
            Thread.Sleep(30);
            var settingsStopWatch = Stopwatch.StartNew();
            Check("SettingsSync waiting server stops and joins",
                settingsPipe.StopAndWait(TimeSpan.FromMilliseconds(500)));
            Check("SettingsSync stop unblocks promptly",
                settingsStopWatch.Elapsed < TimeSpan.FromMilliseconds(250));
            settingsPipe.Start();
            Thread.Sleep(30);
            Check("SettingsSync worker can be reused",
                settingsPipe.StopAndWait(TimeSpan.FromMilliseconds(500)));

            if (OperatingSystem.IsWindows())
            {
                var trackball = new InputListenerRawInputTrackball();
                Check("RawInput trackball owns shared-memory handles before dispose",
                    trackball.HasOpenSharedMemory);
                trackball.Dispose();
                Check("RawInput trackball releases shared-memory handles",
                    !trackball.HasOpenSharedMemory);
                trackball.Dispose();
                Check("RawInput trackball dispose is idempotent",
                    !trackball.HasOpenSharedMemory);
            }

            GunControlHandler.SetKillFlag(true);
            OlympicControlHandler.SetKillFlag(true);

            if (failures.Count == 0)
            {
                Console.WriteLine($"Control worker lifecycle: PASS ({checks}/{checks})");
                return 0;
            }

            foreach (var failure in failures)
                Console.Error.WriteLine("FAIL: " + failure);
            Console.Error.WriteLine(
                $"Control worker lifecycle: FAIL ({checks - failures.Count}/{checks})");
            return 1;
        }
    }
}
