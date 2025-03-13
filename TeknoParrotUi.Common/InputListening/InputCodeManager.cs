using System;

namespace TeknoParrotUi.Common.InputListening
{
    public static class InputCodeManager
    {
        private static readonly object InputCodeLock = new object();

        // Safely update digital button state
        public static void UpdateDigitalButton(int player, int buttonIndex, bool value)
        {
            lock (InputCodeLock)
            {
                switch (buttonIndex)
                {
                    case 0: InputCode.PlayerDigitalButtons[player].Button1 = value; break;
                    case 1: InputCode.PlayerDigitalButtons[player].Button2 = value; break;
                    case 2: InputCode.PlayerDigitalButtons[player].Button3 = value; break;
                        // Add cases for other buttons
                }
            }
        }

        // Safely update analog inputs
        public static void UpdateAnalogValue(int player, int axis, byte value)
        {
            lock (InputCodeLock)
            {
                //InputCode.PlayerAnalogBytes[player][axis] = value;
            }
        }

        // Clear all inputs (for shutdown)
        public static void ResetAllInputs()
        {
            lock (InputCodeLock)
            {
                // Reset all digital and analog inputs
                // for (int i = 0; i < InputCode.PlayerDigitalButtons.Length; i++)
                // {
                //     InputCode.PlayerDigitalButtons[i] = new DigitalButtons();
                // }

                // for (int i = 0; i < InputCode.PlayerAnalogBytes.Length; i++)
                // {
                //     for (int j = 0; j < InputCode.PlayerAnalogBytes[i].Length; j++)
                //     {
                //         InputCode.PlayerAnalogBytes[i][j] = 0;
                //     }
                // }
            }
        }
    }
}