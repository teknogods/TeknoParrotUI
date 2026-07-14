using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class amJvsPipe : ControlPipe
    {
        public override void Transmit(bool runEmuOnly)
        {
            var server = Server;
            while (true)
            {
                try
                {
                    Thread.Sleep(15);
                    var report = GenButtonsJvs();

                    server.Write(report, 0, 256);
                    server.Flush();
                    if (!IsRunning)
                        break;
                }
                catch (Exception)
                {
                    // In case pipe is broken
                    try { server?.Close(); } catch { /* ignored */ }
                    server = runEmuOnly ? RecreatePipe() : null;
                    if (server == null)
                        break;
                    server.WaitForConnection();
                }

                if (!IsRunning)
                    break;
            }
        }

        private byte[] GenButtonsJvs()
        {
            // byte for now, unless we need to feed more data later.
            byte[] data = new byte[256];
            data[0] = JvsPackageEmulator.GetPlayerControls(0);
            data[1] = JvsPackageEmulator.GetPlayerControlsExt(0);
            data[2] = JvsPackageEmulator.GetPlayerControlsExt2(0);
            data[3] = JvsPackageEmulator.GetPlayerControls(1);
            data[4] = JvsPackageEmulator.GetPlayerControlsExt(1);
            data[5] = JvsPackageEmulator.GetPlayerControlsExt2(1);
            data[6] = JvsPackageEmulator.GetSpecialBits(0);
            data[7] = JvsPackageEmulator.GetSpecialBits(1);

            // TODO: Add 2nd JVS buttons and way to send JvsSwitchCount etc.

            // Copy analogs
            for (int i = 0; i < 16; i++)
            {
                data[i+8] = InputCode.AnalogBytes[i];
            }
            for (int i = 0; i < 16; i++)
            {
                data[i + 16+8] = InputCode.AnalogBytes2[i];
            }

            data[200] = (byte)JvsPackageEmulator.Coins[0];
            data[201] = (byte)JvsPackageEmulator.Coins[1];

            return data;
        }
    }
}
