using System;
using System.IO.Pipes;
using System.Threading;

namespace TeknoParrotUi.Common.Pipes
{
    public class APM3Pipe : ControlPipe
    {
        public override void Transmit(bool runEmuOnly)
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(15);
                    var report = GenButtonsAPM3();

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

        private byte[] GenButtonsAPM3()
        {
            byte[] data = new byte[64];

            // Player 1
            if (InputCode.PlayerDigitalButtons[0].Test != null && InputCode.PlayerDigitalButtons[0].Test.Value)
                data[0] = 1;

            if (InputCode.PlayerDigitalButtons[0].Service != null && InputCode.PlayerDigitalButtons[0].Service.Value)
                data[1] = 1;

            if (InputCode.PlayerDigitalButtons[0].LeftPressed())
                data[4] = 1;

            if (InputCode.PlayerDigitalButtons[0].RightPressed())
                data[5] = 1;

            if (InputCode.PlayerDigitalButtons[0].DownPressed())
                data[3] = 1;

            if (InputCode.PlayerDigitalButtons[0].UpPressed())
                data[2] = 1;

            if (InputCode.PlayerDigitalButtons[0].Start != null && InputCode.PlayerDigitalButtons[0].Start.Value)
                data[6] = 1;

            if (InputCode.PlayerDigitalButtons[0].Button1 != null && InputCode.PlayerDigitalButtons[0].Button1.Value)
                data[7] = 1;

            if (InputCode.PlayerDigitalButtons[0].Button2 != null && InputCode.PlayerDigitalButtons[0].Button2.Value)
                data[8] = 1;

            if (InputCode.PlayerDigitalButtons[0].Button3 != null && InputCode.PlayerDigitalButtons[0].Button3.Value)
                data[9] = 1;

            if (InputCode.PlayerDigitalButtons[0].Button4 != null && InputCode.PlayerDigitalButtons[0].Button4.Value)
                data[10] = 1;

            if (InputCode.PlayerDigitalButtons[0].Button5 != null && InputCode.PlayerDigitalButtons[0].Button5.Value)
                data[11] = 1;

            if (InputCode.PlayerDigitalButtons[0].Button6 != null && InputCode.PlayerDigitalButtons[0].Button6.Value)
                data[12] = 1;

            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1 != null && InputCode.PlayerDigitalButtons[0].ExtensionButton1.Value)
                data[13] = 1;

            if (InputCode.PlayerDigitalButtons[0].ExtensionButton2 != null && InputCode.PlayerDigitalButtons[0].ExtensionButton2.Value)
                data[14] = 1;

            return data;
        }
    }
}