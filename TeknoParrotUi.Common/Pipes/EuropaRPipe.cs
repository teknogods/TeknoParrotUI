using System;
using System.IO.Pipes;
using System.Threading;

namespace TeknoParrotUi.Common.Pipes
{
    public class EuropaRPipe : ControlPipe
    {
        public byte buttons1;
        public virtual void HandleButtons()
        {
            if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
                buttons1 |= 0x04;
            // View Change
            if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
                buttons1 |= 0x08;

            // Shifts
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                buttons1 |= 0x01;
            if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                buttons1 |= 0x02;

            var report = new byte[8];

            report[1] = InputCode.AnalogBytes[0]; // (byte)((s.Gamepad.LeftThumbX / 256) + 128);
            report[2] = InputCode.AnalogBytes[0]; //(byte) ((s.Gamepad.LeftThumbY / 256) + 128);
                                                  //report[3] = s.Gamepad.LeftTrigger;
            byte gas = InputCode.AnalogBytes[2];
            byte brake = InputCode.AnalogBytes[4];
            report[3] = gas;
            report[5] = brake;
            // 1 is gear up
            // 2 is gear down
            // 4 is start
            // 8 is cam switch
            report[6] = buttons1;
            report[7] = 0; //(byte) (((int) s.Gamepad.Buttons >> 8) & 0xFF);

            report[7] |= 4 | 8;

            _npServer.Write(report, 0, 8);
        }

        public override void Transmit(bool runEmuOnly)
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(15);

                    buttons1 = 0;
                    HandleButtons();

                    _npServer.Flush();
                    if (!_isRunning)
                        break;
                }
                catch (Exception ex)
                {
                    // In case pipe is broken
                    if (runEmuOnly)
                    {
						_npServer.Close();
						_npServer = new NamedPipeServerStream(PipeName);
	                    _npServer.WaitForConnection();
                    }
                    else
                    {
                        break;
                    }
                    if (!_isRunning)
                        return;
                }
            }
        }
    }
}
