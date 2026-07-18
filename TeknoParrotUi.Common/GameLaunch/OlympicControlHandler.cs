using System;
using System.Diagnostics;
using System.Threading;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views.GameRunningCode.ControlHandlers
{
    public static class OlympicControlHandler
    {
        private static volatile bool _killGunListener;

        public static void SetKillFlag(bool value)
        {
            _killGunListener = value;
        }

        public static void HandleOlympicControls() =>
            HandleOlympicControls(CancellationToken.None, observeLegacyKillFlag: true);

        internal static void HandleOlympicControls(CancellationToken cancellationToken) =>
            HandleOlympicControls(cancellationToken, observeLegacyKillFlag: false);

        private static void HandleOlympicControls(
            CancellationToken cancellationToken,
            bool observeLegacyKillFlag)
        {
            while (true)
            {
                if ((observeLegacyKillFlag && _killGunListener) ||
                    cancellationToken.IsCancellationRequested)
                    return;

                // Handle jump sensors
                if (InputCode.PlayerDigitalButtons[0].Button6.HasValue && InputCode.PlayerDigitalButtons[0].Button6.Value)
                {
                    InputCode.PlayerDigitalButtons[1].Button6 = true;
                    InputCode.PlayerDigitalButtons[0].ExtensionButton3 = true;
                    InputCode.PlayerDigitalButtons[0].ExtensionButton4 = true;
                    InputCode.PlayerDigitalButtons[1].ExtensionButton3 = true;
                    InputCode.PlayerDigitalButtons[1].ExtensionButton4 = true;
                }
                else
                {
                    InputCode.PlayerDigitalButtons[1].Button6 = false;
                    InputCode.PlayerDigitalButtons[0].ExtensionButton3 = false;
                    InputCode.PlayerDigitalButtons[0].ExtensionButton4 = false;
                    InputCode.PlayerDigitalButtons[1].ExtensionButton3 = false;
                    InputCode.PlayerDigitalButtons[1].ExtensionButton4 = false;
                }

                // Joy1 Right Up
                if (InputCode.PlayerDigitalButtons[0].Up.HasValue && InputCode.PlayerDigitalButtons[0].Up.Value
                                                                  && InputCode.PlayerDigitalButtons[0].Right.HasValue &&
                                                                  InputCode.PlayerDigitalButtons[0].Right.Value)
                {
                    InputCode.PlayerDigitalButtons[0].Left = true;
                }
                else
                {
                    InputCode.PlayerDigitalButtons[0].Left = false;
                }

                // Add rest of Olympic control handling logic...
                // (abbreviated for space)

                if (cancellationToken.WaitHandle.WaitOne(10))
                    return;
            }
        }

        public static void Handle2020OlympicControls() =>
            Handle2020OlympicControls(CancellationToken.None, observeLegacyKillFlag: true);

        internal static void Handle2020OlympicControls(CancellationToken cancellationToken) =>
            Handle2020OlympicControls(cancellationToken, observeLegacyKillFlag: false);

        private static void Handle2020OlympicControls(
            CancellationToken cancellationToken,
            bool observeLegacyKillFlag)
        {
            const int targetElapsedMilliseconds = 10;
            Stopwatch stopwatch = new Stopwatch();
            SpinWait spinWait = new SpinWait();
            while (true)
            {
                if ((observeLegacyKillFlag && _killGunListener) ||
                    cancellationToken.IsCancellationRequested)
                    return;
                stopwatch.Restart();
                // Handle jump sensors
                if (InputCode.PlayerDigitalButtons[1].Button6.HasValue && InputCode.PlayerDigitalButtons[1].Button6.Value)
                {
                    InputCode.PlayerDigitalButtons[1].Button6 = true;
                    InputCode.PlayerDigitalButtons[0].Service = true;
                    InputCode.PlayerDigitalButtons[0].Test = true;
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1 = true;
                    InputCode.PlayerDigitalButtons[0].Button6 = true;
                    InputCode.PlayerDigitalButtons[1].ExtensionButton2 = true;
                }
                else
                {
                    InputCode.PlayerDigitalButtons[1].Button6 = false;
                    InputCode.PlayerDigitalButtons[0].Service = false;
                    InputCode.PlayerDigitalButtons[0].Test = false;
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1 = false;
                    InputCode.PlayerDigitalButtons[0].Button6 = false;
                    InputCode.PlayerDigitalButtons[1].ExtensionButton2 = false;
                }

                if (SerialPortHandler.UsesBusySpinForTiming(OperatingSystem.IsWindows()))
                {
                    while (stopwatch.ElapsedMilliseconds < targetElapsedMilliseconds)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;
                        spinWait.SpinOnce();
                    }
                }
                else
                {
                    var remaining =
                        TimeSpan.FromMilliseconds(targetElapsedMilliseconds) -
                        stopwatch.Elapsed;
                    if (remaining > TimeSpan.Zero &&
                        cancellationToken.WaitHandle.WaitOne(remaining))
                        return;
                }
            }
        }
    }
}
