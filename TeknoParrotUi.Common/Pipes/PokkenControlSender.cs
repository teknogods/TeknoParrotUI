using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class PokkenControlSender
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
                var control = 0x00;
                if (InputCode.PokkenInputButtons.Up.HasValue && InputCode.PokkenInputButtons.Up.Value)
                    control |= 0x01;
                if (InputCode.PokkenInputButtons.Down.HasValue && InputCode.PokkenInputButtons.Down.Value)
                    control |= 0x02;
                if (InputCode.PokkenInputButtons.Left.HasValue && InputCode.PokkenInputButtons.Left.Value)
                    control |= 0x04;
                if (InputCode.PokkenInputButtons.Right.HasValue && InputCode.PokkenInputButtons.Right.Value)
                    control |= 0x08;
                if (InputCode.PokkenInputButtons.Start.HasValue && InputCode.PokkenInputButtons.Start.Value)
                    control |= 0x10;
                if (InputCode.PokkenInputButtons.ButtonA.HasValue && InputCode.PokkenInputButtons.ButtonA.Value)
                    control |= 0x2000;
                if (InputCode.PokkenInputButtons.ButtonB.HasValue && InputCode.PokkenInputButtons.ButtonB.Value)
                    control |= 0x1000;
                if (InputCode.PokkenInputButtons.ButtonX.HasValue && InputCode.PokkenInputButtons.ButtonX.Value)
                    control |= 0x8000;
                if (InputCode.PokkenInputButtons.ButtonY.HasValue && InputCode.PokkenInputButtons.ButtonY.Value)
                    control |= 0x4000;
                if (InputCode.PokkenInputButtons.ButtonL.HasValue && InputCode.PokkenInputButtons.ButtonL.Value)
                    control |= 0x100;
                if (InputCode.PokkenInputButtons.ButtonR.HasValue && InputCode.PokkenInputButtons.ButtonR.Value)
                    control |= 0x200;

                JvsHelper.StateView.Write(8, control);
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
