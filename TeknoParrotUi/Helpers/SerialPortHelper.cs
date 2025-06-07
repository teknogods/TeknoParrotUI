using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Windows;

namespace TeknoParrotUi.Helpers
{
    public class SerialPortHelper
    {
        private static bool _testDone;
        private static bool _testSuccesful;
        private static readonly Stopwatch StopWatch = new Stopwatch();
        public static bool TestComPortEmulation(string gameCom, string emuCom)
        {
            try
            {
                _testDone = false;
                _testSuccesful = false;
                StopWatch.Reset();
                using (var gamePort = new SerialPort(gameCom)
                {
                    BaudRate = 115200,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    ReadTimeout = 0,
                    WriteBufferSize = 516,
                    ReadBufferSize = 516,
                    Handshake = Handshake.None
                })
                using (var emuPort = new SerialPort(emuCom)
                {
                    BaudRate = 115200,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    ReadTimeout = 0,
                    WriteBufferSize = 516,
                    ReadBufferSize = 516,
                    Handshake = Handshake.None
                })
                {
                    StartListening(emuPort);
                    var sendResult = SendMessageToCom(gamePort);
                    if (!sendResult)
                    {
                        emuPort.Close();
                        gamePort.Close();
                        return false;
                    }
                    while (!_testDone)
                    {
                        if (StopWatch.Elapsed <= TimeSpan.FromSeconds(5)) continue;

                        StopWatch.Stop();
                        _testSuccesful = false;
                        _testDone = true;
                        MessageBoxHelper.ErrorOK(Properties.Resources.JVSTestTimeout);
                    }
                    emuPort.Close();
                    gamePort.Close();
                }
            }
            catch (Exception e)
            {
                MessageBoxHelper.ErrorOK(string.Format(Properties.Resources.JVSTestException, e));
                _testSuccesful = false;
                _testDone = true;
            }
            return _testSuccesful;
        }

        private static bool SendMessageToCom(SerialPort serialPort)
        {
            serialPort.Open();
            StopWatch.Start();
            try
            {
                serialPort.WriteLine(Properties.Resources.JVSTestMessage);
            }
            catch (Exception e)
            {
                MessageBoxHelper.ErrorOK(string.Format(Properties.Resources.JVSTestException, e));
                return false;
            }
            return true;
        }
        private static void StartListening(SerialPort serialPort)
        {
            serialPort.DataReceived += delegate (object sender, SerialDataReceivedEventArgs args)
            {
                var sp = (SerialPort)sender;
                var data = sp.ReadLine();
                _testSuccesful = data == Properties.Resources.JVSTestMessage;
                _testDone = true;
            };

            serialPort.Open();
        }
    }
}
