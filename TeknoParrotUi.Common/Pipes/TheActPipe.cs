using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class TheActPipe : ControlSender
    {
        public override void Transmit()
        {
            // Coin
            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
                Control |= 0x01;
            // Test
            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
                Control |= 0x02;
            // Start
            if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
                Control |= 0x04;
            // Left
            if (InputCode.PlayerDigitalButtons[0].Left.HasValue && InputCode.PlayerDigitalButtons[0].Left.Value)
                Control |= 0x08;
            // Right
            if (InputCode.PlayerDigitalButtons[0].Right.HasValue && InputCode.PlayerDigitalButtons[0].Right.Value)
                Control |= 0x10;

            JvsHelper.StateView.Write(8, Control);
        }
    }
}
