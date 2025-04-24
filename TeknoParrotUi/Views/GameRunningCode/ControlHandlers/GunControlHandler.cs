using System;
using System.Threading;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Views.GameRunningCode.ControlHandlers
{
    internal static class GunControlHandler
    {
        private static bool _killGunListener;

        public static void SetKillFlag(bool value)
        {
            _killGunListener = value;
        }

        /// <summary>
        /// Handles gun game controls.
        /// </summary>
        public static void HandleRamboControls()
        {
            bool reloaded1 = false;
            bool reloaded2 = false;

            while (true)
            {
                if (_killGunListener)
                    return;

                if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                {
                    // Reload
                    InputCode.AnalogBytes[0] = 0x80;
                    if (!reloaded1)
                        InputCode.AnalogBytes[2] = 0xFF;
                    else
                        InputCode.AnalogBytes[2] = 0xF0;
                    reloaded1 = !reloaded1;
                }

                if (InputCode.PlayerDigitalButtons[1].Button2.HasValue && InputCode.PlayerDigitalButtons[1].Button2.Value)
                {
                    InputCode.AnalogBytes[4] = 0x80;
                    if (!reloaded2)
                        InputCode.AnalogBytes[6] = 0xFF;
                    else
                        InputCode.AnalogBytes[6] = 0xF0;
                    reloaded2 = !reloaded2;
                }

                Thread.Sleep(10);
            }
        }

        public static void HandleGSEvoReload()
        {
            while (true)
            {
                if (_killGunListener)
                    return;

                bool P1ScreenOut = (InputCode.AnalogBytes[0] <= 1 || InputCode.AnalogBytes[0] >= 254 || InputCode.AnalogBytes[2] <= 1 || InputCode.AnalogBytes[2] >= 254);
                bool P2ScreenOut = (InputCode.AnalogBytes[4] <= 1 || InputCode.AnalogBytes[4] >= 254 || InputCode.AnalogBytes[6] <= 1 || InputCode.AnalogBytes[6] >= 254);

                bool P1ReloadPressed = InputCode.PlayerDigitalButtons[0].ExtensionButton1_8.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_8.Value;
                bool P2ReloadPressed = InputCode.PlayerDigitalButtons[1].ExtensionButton1_8.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_8.Value;

                if (P1ScreenOut)
                {
                    InputCode.PlayerDigitalButtons[0].Button2 = true;
                }
                else
                {
                    if (!P1ReloadPressed)
                        InputCode.PlayerDigitalButtons[0].Button2 = false;
                }

                if (P2ScreenOut)
                {
                    InputCode.PlayerDigitalButtons[1].Button2 = true;
                }
                else
                {
                    if (!P2ReloadPressed)
                        InputCode.PlayerDigitalButtons[1].Button2 = false;
                }

                Thread.Sleep(10);
            }
        }
    }
}