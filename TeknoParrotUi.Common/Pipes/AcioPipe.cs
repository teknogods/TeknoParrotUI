using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    // For use with our libacio code
    // Once again the magic question is, how to assign buttons in a way that makes sense to use in profiles
    // My least favorite activity...
    public class AcioPipe : ControlSender
    {
        public override void Transmit()
        {
            ulong control = 0;
            // How many buttons do we need...
            if (InputCode.PlayerDigitalButtons[0].Test == true)
                control |= 0x1UL;
            if (InputCode.PlayerDigitalButtons[0].Service == true)
                control |= 0x2UL;
            if (InputCode.PlayerDigitalButtons[0].Coin == true)
                control |= 0x4UL;
            if (InputCode.PlayerDigitalButtons[0].Start == true)
                control |= 0x8UL;

            if (InputCode.PlayerDigitalButtons[0].Button1 == true)
                control |= 0x10UL;
            if (InputCode.PlayerDigitalButtons[0].Button2 == true)
                control |= 0x20UL;
            if (InputCode.PlayerDigitalButtons[0].Button3 == true)
                control |= 0x40UL;
            if (InputCode.PlayerDigitalButtons[0].Button4 == true)
                control |= 0x80UL;
            if (InputCode.PlayerDigitalButtons[0].Button5 == true)
                control |= 0x100UL;
            if (InputCode.PlayerDigitalButtons[0].Button6 == true)
                control |= 0x200UL;

            if (InputCode.PlayerDigitalButtons[1].Button1 == true)
                control |= 0x400UL;
            if (InputCode.PlayerDigitalButtons[1].Button2 == true)
                control |= 0x800UL;
            if (InputCode.PlayerDigitalButtons[1].Button3 == true)
                control |= 0x1000UL;
            if (InputCode.PlayerDigitalButtons[1].Button4 == true)
                control |= 0x2000UL;
            if (InputCode.PlayerDigitalButtons[1].Button5 == true)
                control |= 0x4000UL;
            if (InputCode.PlayerDigitalButtons[1].Button6 == true)
                control |= 0x8000UL;

            if (InputCode.PlayerDigitalButtons[2].Button1 == true)
                control |= 0x10000UL;
            if (InputCode.PlayerDigitalButtons[2].Button2 == true)
                control |= 0x20000UL;
            if (InputCode.PlayerDigitalButtons[2].Button3 == true)
                control |= 0x40000UL;
            if (InputCode.PlayerDigitalButtons[2].Button4 == true)
                control |= 0x80000UL;
            if (InputCode.PlayerDigitalButtons[2].Button5 == true)
                control |= 0x100000UL;
            if (InputCode.PlayerDigitalButtons[2].Button6 == true)
                control |= 0x200000UL;

            // TODO: figure out what other buttons we might need later on
            // Do any of the games that will use this have a joystick? Then we gotta map p1/p2 etc UP/DOWN/LEFT/RIGHT so we 
            // can use the conversion stuff or whatever 

            JvsHelper.StateView.Write(8, control);
            
            // Big endian
            ushort analog0 = (ushort)((InputCode.AnalogBytes[0] << 8) | InputCode.AnalogBytes[1]);
            ushort analog1 = (ushort)((InputCode.AnalogBytes[2] << 8) | InputCode.AnalogBytes[3]);
            ushort analog2 = (ushort)((InputCode.AnalogBytes[4] << 8) | InputCode.AnalogBytes[5]);
            ushort analog3 = (ushort)((InputCode.AnalogBytes[6] << 8) | InputCode.AnalogBytes[7]);
            ushort analog4 = (ushort)((InputCode.AnalogBytes[8] << 8) | InputCode.AnalogBytes[9]);
            
            JvsHelper.StateView.Write(16, analog0);
            JvsHelper.StateView.Write(20, analog1);
            JvsHelper.StateView.Write(24, analog2);
            JvsHelper.StateView.Write(28, analog3);
            JvsHelper.StateView.Write(32, analog4);
        }
    }
}
