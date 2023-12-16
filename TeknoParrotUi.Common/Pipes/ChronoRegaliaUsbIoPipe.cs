using System;
using System.IO.Pipes;
using System.Threading;

namespace TeknoParrotUi.Common.Pipes
{
    public class ChronoRegaliaUsbIoPipe : ControlPipe
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

            data[0] = InputCode.AnalogBytes[0];
            data[1] = InputCode.AnalogBytes[1];
            data[2] = InputCode.AnalogBytes[2];
            data[3] = InputCode.AnalogBytes[3];
            data[4] = InputCode.AnalogBytes[4];
            data[5] = InputCode.AnalogBytes[5];
            data[6] = InputCode.AnalogBytes[6];
            data[7] = InputCode.AnalogBytes[7];
            data[8] = InputCode.AnalogBytes[8];
            data[9] = InputCode.AnalogBytes[9];
            data[10] = InputCode.AnalogBytes[10];
            data[11] = InputCode.AnalogBytes[11];
            data[12] = InputCode.AnalogBytes[12];
            data[13] = InputCode.AnalogBytes[13];
            data[14] = InputCode.AnalogBytes[14];
            data[15] = InputCode.AnalogBytes[15];
            data[16] = 0; // Spinner 1
            data[18] = 0; // Spinner 2
            data[20] = 0; // Spinner 3
            data[22] = 0; // Spinner 4
            data[24] = coins[0]; // Chute 1
            data[25] = coins[1]; // Chute 1 byte 2
            data[26] = 0; // Chute 2
            data[28] = 0; // Buttons 1
            data[29] = 0; // Buttons 1
            data[30] = 0; // Buttons 2
            data[31] = 0; // Buttons 2

            if (InputCode.PlayerDigitalButtons[0].Test != null && InputCode.PlayerDigitalButtons[0].Test.Value)
                data[29] |= 0x02;

            if (InputCode.PlayerDigitalButtons[0].Service != null && InputCode.PlayerDigitalButtons[0].Service.Value)
                data[28] |= 0x40;

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