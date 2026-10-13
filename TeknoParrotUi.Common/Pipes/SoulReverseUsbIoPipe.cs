using System;
using System.IO.Pipes;
using System.Threading;

namespace TeknoParrotUi.Common.Pipes
{
    /// <summary>
    /// IO4 report for Soul Reverse (SEGA ALLS UX, satellite cabinet): two analog sticks, three buttons
    /// under each hand and a push switch in each stick. The cabinet has no start button; menus are touch.
    /// </summary>
    public class SoulReverseUsbIoPipe : ControlPipe
    {
        // The game's default stick calibration expects a centre slightly off 0x8000 and reaches full
        // deflection about 12000 counts away from it, not at the ends of the 16 bit range.
        private const int StickRange = 12000;
        private const int LeftXCentre = 0x8000 + 1834;
        private const int LeftYCentre = 0x8000 + 1600;
        private const int RightXCentre = 0x8000 + 1344;
        private const int RightYCentre = 0x8000 - 1208;

        public int CoinCount = 0;
        public bool CoinState = false;

        public SoulReverseUsbIoPipe()
        {
            // Sticks rest in the middle until the input listener reports something else.
            InputCode.AnalogBytes[0] = 0x80;
            InputCode.AnalogBytes[2] = 0x80;
            InputCode.AnalogBytes[4] = 0x80;
            InputCode.AnalogBytes[6] = 0x80;
        }

        public override void Transmit(bool runEmuOnly)
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(15);
                    var report = GenButtonsSoulReverse();

                    _npServer.Write(report, 0, 64);
                    _npServer.Flush();
                    if (!_isRunning)
                        break;
                }
                catch (Exception)
                {
                    // In case pipe is broken
                    _npServer.Close();
                    if (runEmuOnly)
                    {
                        _npServer = new NamedPipeServerStream(PipeName);
                        _npServer.WaitForConnection();
                    }
                    else
                    {
                        break;
                    }
                }

                if (!_isRunning)
                    break;
            }
        }

        // 0x00..0xFF (0x7F and 0x80 are both the centre) to -1..1
        private static double StickPosition(byte value)
        {
            if (value == 0x7F || value == 0x80)
                return 0.0;

            return Math.Max(-1.0, Math.Min(1.0, (value - 128) / 127.0));
        }

        private static void WriteChannel(byte[] data, int channel, int centre, double position)
        {
            var raw = (ushort)Math.Max(1, Math.Min(65535, centre + (int)Math.Round(position * StickRange)));
            data[channel * 2] = (byte)(raw & 0xFF);
            data[channel * 2 + 1] = (byte)(raw >> 8);
        }

        private static bool Pressed(bool? button)
        {
            return button != null && button.Value;
        }

        private byte[] GenButtonsSoulReverse()
        {
            byte[] data = new byte[64];
            var player = InputCode.PlayerDigitalButtons[0];

            // Analog bytes follow the screen: 0 is left / up, 255 is right / down. The board reports
            // a higher value when a stick is pushed right or away from the player.
            WriteChannel(data, 0, LeftXCentre, StickPosition(InputCode.AnalogBytes[0]));
            WriteChannel(data, 4, LeftYCentre, -StickPosition(InputCode.AnalogBytes[2]));
            WriteChannel(data, 1, RightXCentre, StickPosition(InputCode.AnalogBytes[4]));
            WriteChannel(data, 5, RightYCentre, -StickPosition(InputCode.AnalogBytes[6]));

            byte[] coins = BitConverter.GetBytes(CoinCount * 256);
            data[24] = coins[0]; // Chute 1
            data[25] = coins[1];

            // Player 1 word: left hand
            if (Pressed(player.Button4))
                data[28] |= 0x80; // Left button 1 (guard / skill shift)

            if (Pressed(player.Button5))
                data[28] |= 0x04; // Left button 2 (dash / dodge)

            if (Pressed(player.Button6))
                data[28] |= 0x08; // Left stick push (action)

            if (Pressed(player.Service))
                data[28] |= 0x40;

            if (Pressed(player.Test))
                data[29] |= 0x02;

            // Player 2 word: right hand
            if (Pressed(player.Button1))
                data[30] |= 0x80; // A (attack)

            if (Pressed(player.Button2))
                data[30] |= 0x04; // B (attack 2)

            if (Pressed(player.Button3))
                data[30] |= 0x08; // C (sub attack)

            if (Pressed(player.ExtensionButton1))
                data[30] |= 0x20; // Right stick push (camera reset)

            if ((player.Coin != null) && (CoinState != player.Coin))
            {
                // update state to match the switch
                CoinState = (bool)player.Coin;
                if (!CoinState)
                {
                    CoinCount++; // increment the coin counter if coin button was released
                }
            }

            return data;
        }
    }
}
