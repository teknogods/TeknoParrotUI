using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class GRIDControlSender
    {
        private static bool _isRunning = false;
        private static Thread _pipeThread;

        public void StartListening()
        {
            if (_isRunning)
                return;
            _isRunning = true;
            _pipeThread = new Thread(TransmitControls);
            _pipeThread.Start();
        }

        public void TransmitControls()
        {
            while (_isRunning)
            {
                int control = 0x00;
                int control2 = 0x00;
                // Start
                if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
                    control |= 0x01;

                // Shift Up
                if (InputCode.PlayerDigitalButtons[1].Up.HasValue && InputCode.PlayerDigitalButtons[1].Up.Value)
                    control |= 0x02;

                // Shift Down
                if (InputCode.PlayerDigitalButtons[1].Down.HasValue && InputCode.PlayerDigitalButtons[1].Down.Value)
                    control |= 0x04;

                // View Change
                if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                    control2 |= 0x01;

                // Reset Button
                if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                    control2 |= 0x02;

                // Look behind (Hack)
                if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
                    control2 |= 0x04;

                // Look left (Hack)
                if (InputCode.PlayerDigitalButtons[0].Left.HasValue && InputCode.PlayerDigitalButtons[0].Left.Value)
                    control2 |= 0x08;

                // Look Right (Hack)
                if (InputCode.PlayerDigitalButtons[0].Right.HasValue && InputCode.PlayerDigitalButtons[0].Right.Value)
                    control2 |= 0x10;

                // Pause menu (Hack)
                if (InputCode.PlayerDigitalButtons[0].Button4.HasValue && InputCode.PlayerDigitalButtons[0].Button4.Value)
                    control2 |= 0x20;

                // Replay Button  (Hack)
                if (InputCode.PlayerDigitalButtons[0].Up.HasValue && InputCode.PlayerDigitalButtons[0].Up.Value)
                    control2 |= 0x40;

                // Look behind 2 (Hack)
                if (InputCode.PlayerDigitalButtons[0].Down.HasValue && InputCode.PlayerDigitalButtons[0].Down.Value)
                    control2 |= 0x80;

                JvsHelper.StateView.Write(4, control2);
                JvsHelper.StateView.Write(8, control);
                JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);
                JvsHelper.StateView.Write(16, InputCode.AnalogBytes[2]);
                JvsHelper.StateView.Write(20, InputCode.AnalogBytes[4]);
                Thread.Sleep(15);
            }
        }

        public void StopListening()
        {
            try
            {
                _isRunning = false;
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}
