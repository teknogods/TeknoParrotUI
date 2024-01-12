using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class SegaRallyCoinPipe : ControlSender
    {
        public override void Transmit()
        {
            // Coin Chute 1
            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
                Control2 |= 0x01;
 
            JvsHelper.StateView.Write(4, Control2);
        }
    }
}
