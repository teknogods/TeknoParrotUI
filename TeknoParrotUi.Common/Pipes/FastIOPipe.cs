using System;
using System.IO.Pipes;
using System.Threading;

namespace TeknoParrotUi.Common.Pipes
{
    public class FastIOPipe : ControlPipe
    {
        public override void Transmit(bool runEmuOnly)
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(15);
                    var report = GenButtonsFastIo();

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

        private byte[] GenButtonsFastIo()
        {
            byte[] data = new byte[64];

            // Player 1
            if (InputCode.PlayerDigitalButtons[0].LeftPressed())
                data[1] |= 0x10;

            if (InputCode.PlayerDigitalButtons[0].RightPressed())
                data[1] |= 0x40;

            if (InputCode.PlayerDigitalButtons[0].DownPressed())
                data[1] |= 0x4;

            if (InputCode.PlayerDigitalButtons[0].UpPressed())
                data[1] |= 0x1;

            if (InputCode.PlayerDigitalButtons[0].Start != null && InputCode.PlayerDigitalButtons[0].Start.Value)
                data[0] |= 0x10;

            if (InputCode.PlayerDigitalButtons[0].Button1 != null && InputCode.PlayerDigitalButtons[0].Button1.Value)
                data[2] |= 0x1;

            if (InputCode.PlayerDigitalButtons[0].Button2 != null && InputCode.PlayerDigitalButtons[0].Button2.Value)
                data[2] |= 0x4;

            if (InputCode.PlayerDigitalButtons[0].Button3 != null && InputCode.PlayerDigitalButtons[0].Button3.Value)
                data[2] |= 0x10;

            if (InputCode.PlayerDigitalButtons[0].Button4 != null && InputCode.PlayerDigitalButtons[0].Button4.Value)
                data[2] |= 0x40;

            if (InputCode.PlayerDigitalButtons[0].Button5 != null && InputCode.PlayerDigitalButtons[0].Button5.Value)
                data[3] |= 0x1;

            if (InputCode.PlayerDigitalButtons[0].Button6 != null && InputCode.PlayerDigitalButtons[0].Button6.Value)
                data[3] |= 0x4;

            if (InputCode.PlayerDigitalButtons[0].Test != null && InputCode.PlayerDigitalButtons[0].Test.Value)
                data[0] |= 0x40;

            if (InputCode.PlayerDigitalButtons[0].Service != null && InputCode.PlayerDigitalButtons[0].Service.Value)
                data[0] |= 0x04;

            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1 != null && InputCode.PlayerDigitalButtons[0].ExtensionButton1.Value)
                data[0] |= 0x01;

            if (InputCode.PlayerDigitalButtons[0].ExtensionButton2 != null && InputCode.PlayerDigitalButtons[0].ExtensionButton2.Value)
                data[0] |= 0x02;

            if (InputCode.PlayerDigitalButtons[0].ExtensionButton3 != null && InputCode.PlayerDigitalButtons[0].ExtensionButton3.Value)
                data[0] |= 0x80;

            // Player 2
            if (InputCode.PlayerDigitalButtons[1].LeftPressed())
                data[1] |= 0x20;

            if (InputCode.PlayerDigitalButtons[1].RightPressed())
                data[1] |= 0x80;

            if (InputCode.PlayerDigitalButtons[1].DownPressed())
                data[1] |= 0x8;

            if (InputCode.PlayerDigitalButtons[1].UpPressed())
                data[1] |= 0x2;

            if (InputCode.PlayerDigitalButtons[1].Start != null && InputCode.PlayerDigitalButtons[1].Start.Value)
                data[0] |= 0x20;

            if (InputCode.PlayerDigitalButtons[1].Button1 != null && InputCode.PlayerDigitalButtons[1].Button1.Value)
                data[2] |= 0x2;

            if (InputCode.PlayerDigitalButtons[1].Button2 != null && InputCode.PlayerDigitalButtons[1].Button2.Value)
                data[2] |= 0x8;

            if (InputCode.PlayerDigitalButtons[1].Button3 != null && InputCode.PlayerDigitalButtons[1].Button3.Value)
                data[2] |= 0x20;

            if (InputCode.PlayerDigitalButtons[1].Button4 != null && InputCode.PlayerDigitalButtons[1].Button4.Value)
                data[2] |= 0x80;

            if (InputCode.PlayerDigitalButtons[1].Button5 != null && InputCode.PlayerDigitalButtons[1].Button5.Value)
                data[3] |= 0x2;

            if (InputCode.PlayerDigitalButtons[1].Button6 != null && InputCode.PlayerDigitalButtons[1].Button6.Value)
                data[3] |= 0x8;

            if (InputCode.PlayerDigitalButtons[1].Service != null && InputCode.PlayerDigitalButtons[1].Service.Value)
                data[0] |= 0x08;

            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
                data[4] = 1;

            data[8] = InputCode.AnalogBytes[0];
            data[9] = InputCode.AnalogBytes[2];

            // Player 3
            if (InputCode.PlayerDigitalButtons[2].LeftPressed())
                data[11] |= 0x10;

            if (InputCode.PlayerDigitalButtons[2].RightPressed())
                data[11] |= 0x40;

            if (InputCode.PlayerDigitalButtons[2].DownPressed())
                data[11] |= 0x4;

            if (InputCode.PlayerDigitalButtons[2].UpPressed())
                data[11] |= 0x1;

            if (InputCode.PlayerDigitalButtons[2].Start != null && InputCode.PlayerDigitalButtons[2].Start.Value)
                data[10] |= 0x10;

            if (InputCode.PlayerDigitalButtons[2].Button1 != null && InputCode.PlayerDigitalButtons[2].Button1.Value)
                data[12] |= 0x1;

            if (InputCode.PlayerDigitalButtons[2].Button2 != null && InputCode.PlayerDigitalButtons[2].Button2.Value)
                data[12] |= 0x4;

            if (InputCode.PlayerDigitalButtons[2].Button3 != null && InputCode.PlayerDigitalButtons[2].Button3.Value)
                data[12] |= 0x10;

            if (InputCode.PlayerDigitalButtons[2].Button4 != null && InputCode.PlayerDigitalButtons[2].Button4.Value)
                data[12] |= 0x40;

            if (InputCode.PlayerDigitalButtons[2].Button5 != null && InputCode.PlayerDigitalButtons[2].Button5.Value)
                data[13] |= 0x1;

            if (InputCode.PlayerDigitalButtons[2].Button6 != null && InputCode.PlayerDigitalButtons[2].Button6.Value)
                data[13] |= 0x4;

            if (InputCode.PlayerDigitalButtons[2].Test != null && InputCode.PlayerDigitalButtons[2].Test.Value)
                data[10] |= 0x40;

            if (InputCode.PlayerDigitalButtons[2].Service != null && InputCode.PlayerDigitalButtons[2].Service.Value)
                data[10] |= 0x04;

            // Player 4
            if (InputCode.PlayerDigitalButtons[3].LeftPressed())
                data[11] |= 0x20;

            if (InputCode.PlayerDigitalButtons[3].RightPressed())
                data[11] |= 0x80;

            if (InputCode.PlayerDigitalButtons[3].DownPressed())
                data[11] |= 0x8;

            if (InputCode.PlayerDigitalButtons[3].UpPressed())
                data[11] |= 0x2;

            if (InputCode.PlayerDigitalButtons[3].Start != null && InputCode.PlayerDigitalButtons[3].Start.Value)
                data[10] |= 0x20;

            if (InputCode.PlayerDigitalButtons[3].Button1 != null && InputCode.PlayerDigitalButtons[3].Button1.Value)
                data[12] |= 0x2;

            if (InputCode.PlayerDigitalButtons[3].Button2 != null && InputCode.PlayerDigitalButtons[3].Button2.Value)
                data[12] |= 0x8;

            if (InputCode.PlayerDigitalButtons[3].Button3 != null && InputCode.PlayerDigitalButtons[3].Button3.Value)
                data[12] |= 0x20;

            if (InputCode.PlayerDigitalButtons[3].Button4 != null && InputCode.PlayerDigitalButtons[3].Button4.Value)
                data[12] |= 0x80;

            if (InputCode.PlayerDigitalButtons[3].Button5 != null && InputCode.PlayerDigitalButtons[3].Button5.Value)
                data[13] |= 0x2;

            if (InputCode.PlayerDigitalButtons[3].Button6 != null && InputCode.PlayerDigitalButtons[3].Button6.Value)
                data[13] |= 0x8;

            if (InputCode.PlayerDigitalButtons[3].Service != null && InputCode.PlayerDigitalButtons[3].Service.Value)
                data[10] |= 0x08;

            if (InputCode.PlayerDigitalButtons[2].Coin.HasValue && InputCode.PlayerDigitalButtons[2].Coin.Value)
                data[14] = 1;

            data[15] = InputCode.AnalogBytes[8];
            data[16] = InputCode.AnalogBytes[10];
            data[17] = InputCode.AnalogBytes[4];
            data[18] = InputCode.AnalogBytes[6];
            data[19] = InputCode.AnalogBytes[12];
            data[20] = InputCode.AnalogBytes[14];

            return data;
        }
    }
}