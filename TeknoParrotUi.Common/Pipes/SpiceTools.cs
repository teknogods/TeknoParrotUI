using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class SpiceTools : ControlSender
    {
        [Flags]
        enum Buttons
        {
            SERVICE = (1 << 0),
            TEST = (1 << 1),
            BUTTON1 = (1 << 2),
            BUTTON2 = (1 << 3),
            BUTTON3 = (1 << 4),
            BUTTON4 = (1 << 5),
            BUTTON5 = (1 << 6),
            BUTTON6 = (1 << 7),
            BUTTON7 = (1 << 8),
            BUTTON8 = (1 << 9),
            BUTTON9 = (1 << 10),
            BUTTON10 = (1 << 11),
        }
        public override void Transmit()
        {
            // Service
            if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value)
                Control |= (int)Buttons.SERVICE;
            // Test
            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
                Control |= (int)Buttons.TEST;
            // P1 Start
            if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
                Control |= (int)Buttons.BUTTON1;

            JvsHelper.StateView.Write(8, Control);
        }
    }
}
