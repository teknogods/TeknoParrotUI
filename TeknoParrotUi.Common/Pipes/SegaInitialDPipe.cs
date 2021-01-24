using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{ 
    public class SegaInitialDPipe : ControlSender
    {
        private static bool Gear1 = false;
        private static bool Gear2 = false;
        private static bool Gear3 = false;
        private static bool Gear4 = false;
        private static bool Gear5 = false;
        private static bool Gear6 = false;
        public override void Transmit()
        {

            if (Gear1)
            {
                Gear1 = false;
                InputCode.PlayerDigitalButtons[1].Down = true;
            }

            if (Gear2)
            {
                Gear2 = false;
                InputCode.PlayerDigitalButtons[1].Down = true;
            }

            if (Gear3)
            {
                Gear3 = false;
                InputCode.PlayerDigitalButtons[1].Down = true;
            }

            if (Gear4)
            {
                Gear4 = false;
                InputCode.PlayerDigitalButtons[1].Down = true;
            }

            if (Gear5)
            {
                Gear5 = false;
                InputCode.PlayerDigitalButtons[1].Down = true;
            }

            if (Gear6)
            {
                Gear6 = false;
                InputCode.PlayerDigitalButtons[1].Down = true;
            }

            // 1st Gear
            if (InputCode.PlayerDigitalButtons[1].Button1.HasValue && InputCode.PlayerDigitalButtons[1].Button1.Value)
            {
                Gear1 = true;
                Control2 |= 0x0400;
            }
            // 2nd Gear
            else if (InputCode.PlayerDigitalButtons[1].Button2.HasValue && InputCode.PlayerDigitalButtons[1].Button2.Value)
            {
                Gear2 = true;
                Control2 |= 0x0800;
            }
            // 3rd Gear
            else if (InputCode.PlayerDigitalButtons[1].Button3.HasValue && InputCode.PlayerDigitalButtons[1].Button3.Value)
            {
                Gear3 = true;
                Control2 |= 0x1000;
            }
            // 4th Gear
            else if (InputCode.PlayerDigitalButtons[1].Button4.HasValue && InputCode.PlayerDigitalButtons[1].Button4.Value)
            {
                Gear4 = true;
                Control2 |= 0x2000;
            }
            // 5th Gear
            else if (InputCode.PlayerDigitalButtons[1].Button5.HasValue && InputCode.PlayerDigitalButtons[1].Button5.Value)
            {
                Gear5 = true;
                Control2 |= 0x4000;
            }
            // 6th Gear
            else if (InputCode.PlayerDigitalButtons[1].Button6.HasValue && InputCode.PlayerDigitalButtons[1].Button6.Value)
            {
                Gear6 = true;
                Control2 |= 0x8000;
            }
            else
            {
                InputCode.PlayerDigitalButtons[1].Down = false;
            }

            JvsHelper.StateView.Write(20, Control2);
        }
    }
}
