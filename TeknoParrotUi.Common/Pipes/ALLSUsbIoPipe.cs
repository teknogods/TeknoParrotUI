using System;
using System.IO.Pipes;
using System.Threading;

namespace TeknoParrotUi.Common.Pipes
{
    public class ALLSUsbIoPipe : ControlPipe
    {
        public int CoinCount = 0;
        public bool CoinState = false;
        public override void Transmit(bool runEmuOnly)
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(15);
                    var report = GenButtonsALLSUsbIo();

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

        private byte[] GenButtonsALLSUsbIo()
        {
            byte[] data = new byte[64];
            byte[] coins = BitConverter.GetBytes(CoinCount * 256);

            // hm, don't like it but what can ya do
            // i'm not about to rewrite the entire xinput/dinput/rawinput handlers... ._.
            short analog0 = (short)Math.Min(InputCode.AnalogBytes[0] * (65535.0 / 255.0) + 1, 65534);
            byte[] analog0Bytes = BitConverter.GetBytes(analog0);
            short analog1 = (short)Math.Min(InputCode.AnalogBytes[1] * (65535.0 / 255.0) + 1, 65534);
            byte[] analog1Bytes = BitConverter.GetBytes(analog1);
            short analog2 = (short)Math.Min(InputCode.AnalogBytes[2] * (65535.0 / 255.0) + 1, 65534);
            byte[] analog2Bytes = BitConverter.GetBytes(analog2);
            short analog3 = (short)Math.Min(InputCode.AnalogBytes[3] * (65535.0 / 255.0) + 1, 65534);
            byte[] analog3Bytes = BitConverter.GetBytes(analog3);
            short analog4 = (short)Math.Min(InputCode.AnalogBytes[4] * (65535.0 / 255.0) + 1, 65534);
            byte[] analog4Bytes = BitConverter.GetBytes(analog4);
            short analog5 = (short)Math.Min(InputCode.AnalogBytes[5] * (65535.0 / 255.0) + 1, 65534);
            byte[] analog5Bytes = BitConverter.GetBytes(analog5);
            short analog6 = (short)Math.Min(InputCode.AnalogBytes[6] * (65535.0 / 255.0) + 1, 65534);
            byte[] analog6Bytes = BitConverter.GetBytes(analog6);
            short analog7 = (short)Math.Min(InputCode.AnalogBytes[7] * (65535.0 / 255.0) + 1, 65534);
            byte[] analog7Bytes = BitConverter.GetBytes(analog7);

            data[0] = analog0Bytes[0];
            data[1] = analog0Bytes[1];
            data[2] = analog1Bytes[0];
            data[3] = analog1Bytes[1];
            data[4] = analog2Bytes[0];
            data[5] = analog2Bytes[1];
            data[6] = analog3Bytes[0];
            data[7] = analog3Bytes[1];
            data[8] = analog4Bytes[0];
            data[9] = analog4Bytes[1];
            data[10] = analog5Bytes[0];
            data[11] = analog5Bytes[1];
            data[12] = analog6Bytes[0];
            data[13] = analog6Bytes[1];
            data[14] = analog7Bytes[0];
            data[15] = analog7Bytes[1];
            data[16] = 0; // Spinner 1
            data[18] = 0; // Spinner 2
            data[20] = 0; // Spinner 3
            data[22] = 0; // Spinner 4
            data[24] = coins[0]; // Chute 1
            data[25] = coins[1]; // Chute 1 byte 2
            data[28] = 0; // Buttons 1
            data[29] = 0; // Buttons 1
            data[30] = 0; // Buttons 2
            data[31] = 0; // Buttons 2

            // Buttons 1
            if (InputCode.PlayerDigitalButtons[0].LeftPressed())
                data[28] |= 0x08;

            if (InputCode.PlayerDigitalButtons[0].RightPressed())
                data[28] |= 0x04;

            if (InputCode.PlayerDigitalButtons[0].DownPressed())
                data[28] |= 0x10;

            if (InputCode.PlayerDigitalButtons[0].UpPressed())
                data[28] |= 0x20;

            if (InputCode.PlayerDigitalButtons[0].Start != null && InputCode.PlayerDigitalButtons[0].Start.Value)
                data[28] |= 0x80;

            if (InputCode.PlayerDigitalButtons[0].Button1 != null && InputCode.PlayerDigitalButtons[0].Button1.Value)
                data[28] |= 0x02;

            if (InputCode.PlayerDigitalButtons[0].Button2 != null && InputCode.PlayerDigitalButtons[0].Button2.Value)
                data[28] |= 0x01;

            if (InputCode.PlayerDigitalButtons[0].Button3 != null && InputCode.PlayerDigitalButtons[0].Button3.Value)
                data[29] |= 0x80;

            if (InputCode.PlayerDigitalButtons[0].Button4 != null && InputCode.PlayerDigitalButtons[0].Button4.Value)
                data[29] |= 0x40;

            if (InputCode.PlayerDigitalButtons[0].Button5 != null && InputCode.PlayerDigitalButtons[0].Button5.Value)
                data[29] |= 0x20;

            if (InputCode.PlayerDigitalButtons[0].Button6 != null && InputCode.PlayerDigitalButtons[0].Button6.Value)
                data[29] |= 0x10;

            if (InputCode.PlayerDigitalButtons[0].Test != null && InputCode.PlayerDigitalButtons[0].Test.Value)
                data[29] |= 0x02;

            if (InputCode.PlayerDigitalButtons[0].Service != null && InputCode.PlayerDigitalButtons[0].Service.Value)
                data[28] |= 0x40;

            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1 != null && InputCode.PlayerDigitalButtons[0].ExtensionButton1.Value)
                data[29] |= 0x08;

            if (InputCode.PlayerDigitalButtons[0].ExtensionButton2 != null && InputCode.PlayerDigitalButtons[0].ExtensionButton2.Value)
                data[29] |= 0x04;

            if (InputCode.PlayerDigitalButtons[0].ExtensionButton3 != null && InputCode.PlayerDigitalButtons[0].ExtensionButton3.Value)
                data[29] |= 0x01;

            // Buttons 2
            if (InputCode.PlayerDigitalButtons[1].LeftPressed())
                data[30] |= 0x08;

            if (InputCode.PlayerDigitalButtons[1].RightPressed())
                data[30] |= 0x04;

            if (InputCode.PlayerDigitalButtons[1].DownPressed())
                data[30] |= 0x10;

            if (InputCode.PlayerDigitalButtons[1].UpPressed())
                data[30] |= 0x20;

            if (InputCode.PlayerDigitalButtons[1].Start != null && InputCode.PlayerDigitalButtons[1].Start.Value)
                data[30] |= 0x80;

            if (InputCode.PlayerDigitalButtons[1].Button1 != null && InputCode.PlayerDigitalButtons[1].Button1.Value)
                data[30] |= 0x02;

            if (InputCode.PlayerDigitalButtons[1].Button2 != null && InputCode.PlayerDigitalButtons[1].Button2.Value)
                data[30] |= 0x01;

            if (InputCode.PlayerDigitalButtons[1].Button3 != null && InputCode.PlayerDigitalButtons[1].Button3.Value)
                data[31] |= 0x80;

            if (InputCode.PlayerDigitalButtons[1].Button4 != null && InputCode.PlayerDigitalButtons[1].Button4.Value)
                data[31] |= 0x40;

            if (InputCode.PlayerDigitalButtons[1].Button5 != null && InputCode.PlayerDigitalButtons[1].Button5.Value)
                data[31] |= 0x20;

            if (InputCode.PlayerDigitalButtons[1].Button6 != null && InputCode.PlayerDigitalButtons[1].Button6.Value)
                data[31] |= 0x10;

            if (InputCode.PlayerDigitalButtons[1].Test != null && InputCode.PlayerDigitalButtons[1].Test.Value)
                data[31] |= 0x02;

            if (InputCode.PlayerDigitalButtons[1].Service != null && InputCode.PlayerDigitalButtons[1].Service.Value)
                data[30] |= 0x40;

            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1 != null && InputCode.PlayerDigitalButtons[1].ExtensionButton1.Value)
                data[31] |= 0x08;

            if (InputCode.PlayerDigitalButtons[1].ExtensionButton2 != null && InputCode.PlayerDigitalButtons[1].ExtensionButton2.Value)
                data[31] |= 0x04;

            if (InputCode.PlayerDigitalButtons[1].ExtensionButton3 != null && InputCode.PlayerDigitalButtons[1].ExtensionButton3.Value)
                data[31] |= 0x01;

            if ((InputCode.PlayerDigitalButtons[0].Coin != null) && (CoinState != InputCode.PlayerDigitalButtons[0].Coin))
            {
                // update state to match the switch
                CoinState = (bool)InputCode.PlayerDigitalButtons[0].Coin;
                if (!CoinState)
                {
                    CoinCount++; // increment the coin counter if coin button was released
                }
            }

            return data;
        }
    }
}