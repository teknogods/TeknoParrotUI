using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{ 
    public class SegaInitialDLindberghPipe : ControlSender
    {
        public override void Transmit()
        {
            // 1st Gear
            if (InputCode.PlayerDigitalButtons[1].Button1.HasValue && InputCode.PlayerDigitalButtons[1].Button1.Value)
            {
                Control2 |= 0x0400;
            }
            // 2nd Gear
            if (InputCode.PlayerDigitalButtons[1].Button2.HasValue && InputCode.PlayerDigitalButtons[1].Button2.Value)
            {
                Control2 |= 0x0800;
            }
            // 3rd Gear
            if (InputCode.PlayerDigitalButtons[1].Button3.HasValue && InputCode.PlayerDigitalButtons[1].Button3.Value)
            {
                Control2 |= 0x1000;
            }
            // 4th Gear
            if (InputCode.PlayerDigitalButtons[1].Button4.HasValue && InputCode.PlayerDigitalButtons[1].Button4.Value)
            {
                Control2 |= 0x2000;
            }
            // 5th Gear
            if (InputCode.PlayerDigitalButtons[1].Button5.HasValue && InputCode.PlayerDigitalButtons[1].Button5.Value)
            {
                Control2 |= 0x4000;
            }
            // 6th Gear
            if (InputCode.PlayerDigitalButtons[1].Button6.HasValue && InputCode.PlayerDigitalButtons[1].Button6.Value)
            {
                Control2 |= 0x8000;
            }

            JvsHelper.StateView.Write(20, Control2);
        }
    }
}
