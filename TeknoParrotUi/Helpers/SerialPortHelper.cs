using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading.Tasks;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    public class SerialPortHelper
    {
        private static bool _testDone;
        private static bool _testSuccesful;
        private static readonly Stopwatch StopWatch = new Stopwatch();

        public static async Task<bool> TestComPortEmulationAsync(string gameCom, string emuCom)
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

                    // Use Task.Run to allow non-blocking wait
                    await Task.Run(async () =>
                    {
                        while (!_testDone)
                        {
                            if (StopWatch.Elapsed > TimeSpan.FromSeconds(5))
                            {
                                StopWatch.Stop();
                                _testSuccesful = false;
                                _testDone = true;

                                // Show error message on UI thread
                                await Dispatcher.UIThread.InvokeAsync(async () =>
                                {
                                    await MessageBoxHelper.ErrorOK("JVS test timed out");
                                });
                                break;
                            }

                            await Task.Delay(100); // Small delay to prevent CPU hogging
                        }
                    });

                    emuPort.Close();
                    gamePort.Close();
                }
            }
            catch (Exception e)
            {
                await MessageBoxHelper.ErrorOK($"Exception happened during JVS test!{Environment.NewLine}{Environment.NewLine}{e}");
                _testSuccesful = false;
                _testDone = true;
            }

            return _testSuccesful;
        }

        // Non-async version for backward compatibility
        public static bool TestComPortEmulation(string gameCom, string emuCom)
        {
            return TestComPortEmulationAsync(gameCom, emuCom).GetAwaiter().GetResult();
        }

        private static bool SendMessageToCom(SerialPort serialPort)
        {
            try
            {
                serialPort.Open();
                StopWatch.Start();
                serialPort.WriteLine("TeknoGodsTest Lol");
                return true;
            }
            catch (Exception e)
            {
                // Use MessageBoxHelper instead of System.Windows.MessageBox
                Dispatcher.UIThread.Post(() =>
                {
                    MessageBoxHelper.ErrorOK($"Exception happened during JVS test!{Environment.NewLine}{Environment.NewLine}{e}").GetAwaiter().GetResult();
                });
                return false;
            }
        }

        private static void StartListening(SerialPort serialPort)
        {
            serialPort.DataReceived += delegate (object sender, SerialDataReceivedEventArgs args)
            {
                var sp = (SerialPort)sender;
                var data = sp.ReadLine();
                _testSuccesful = data == "TeknoGodsTest Lol";
                _testDone = true;
            };

            serialPort.Open();
        }
    }
}