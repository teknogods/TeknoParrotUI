using System;
using System.IO.Pipes;
using System.Threading;

namespace TeknoParrotUi.Common.Pipes
{
    public class EuropaRPipeHandler
    {
        private static bool _isRunning = false;
        private static bool _isSegaRally3 = false;
        private static NamedPipeServerStream _npServer;
        private static Thread _pipeThread;
        public void StartListening(bool isRally3)
        {
            if (_isRunning)
                return;
            _isRunning = true;
            _isSegaRally3 = isRally3;
            _pipeThread = new Thread(TransmitPipeInformation);
            _pipeThread.Start();
        }

        public void TransmitPipeInformation()
        {
            _npServer?.Close();
            _npServer = new NamedPipeServerStream("TeknoParrotPipe");

            _npServer.WaitForConnection();

            while (true)
            {
                try
                {
                Thread.Sleep(15);
                    byte buttons1 = 0;
                    if (_isSegaRally3)
                    {
                        if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
                            buttons1 |= 0x01;
                        // Handbrake
                        if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
                            buttons1 |= 0x02;
                        // View Change
                        if (InputCode.PlayerDigitalButtons[0].Button4.HasValue && InputCode.PlayerDigitalButtons[0].Button4.Value)
                            buttons1 |= 0x04;

                        // Shifts
                        if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                            buttons1 |= 0x08;
                        if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                            buttons1 |= 0x10;

                        var report = new byte[15];

                        report[1] = InputCode.AnalogBytes[0]; // (byte)((s.Gamepad.LeftThumbX / 256) + 128);
                        report[2] = InputCode.AnalogBytes[0]; // (byte)((s.Gamepad.LeftThumbY / 256) + 128);
                        report[3] = InputCode.AnalogBytes[4]; //s.Gamepad.LeftTrigger;
                        report[4] = InputCode.AnalogBytes[2]; //s.Gamepad.RightTrigger;

                        report[6] = buttons1; //(byte)((int)s.Gamepad.Buttons & 0xFF);
                        report[7] = 0; //(byte)(((int)s.Gamepad.Buttons >> 8) & 0xFF);

                        report[7] |= 4 | 8;

                        _npServer.Write(report, 0, 15);

                    }
                    else
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
                    _npServer.Flush();
                    if (!_isRunning)
                        break;
                }
                catch (Exception)
                {
                    // In case pipe is broken
                    _npServer.Close();
                    if (!_isRunning)
                        return;
                    return;
                }
            }
            _npServer.Close();
            _npServer?.Dispose();
        }

        public void StopListening()
        {
            try
            {
                _isRunning = false;
                using (NamedPipeClientStream npcs = new NamedPipeClientStream("TeknoParrotPipe"))
                {
                    npcs.Connect(100);
                }
                Thread.Sleep(100);
                _npServer?.Close();
                _npServer?.Dispose();
            }
            catch (Exception)
            {
                // ignored
            }
        }


    }
}
