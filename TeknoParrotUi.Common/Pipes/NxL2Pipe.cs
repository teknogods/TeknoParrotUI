using System;
using System.IO.Pipes;
using System.Threading;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class NxL2Pipe : ControlSender
    {
        public override void Transmit()
        {
            int buttonData = 0;
            // Player 1
            if (InputCode.PlayerDigitalButtons[0].LeftPressed())
                buttonData |= 0x20;

            if (InputCode.PlayerDigitalButtons[0].RightPressed())
                buttonData |= 0x40;

            if (InputCode.PlayerDigitalButtons[0].DownPressed())
                buttonData |= 0x10;

            if (InputCode.PlayerDigitalButtons[0].UpPressed())
                buttonData |= 0x08;

            if (InputCode.PlayerDigitalButtons[0].Start != null && InputCode.PlayerDigitalButtons[0].Start.Value)
                buttonData |= 0x02;

            if (InputCode.PlayerDigitalButtons[0].Button1 != null && InputCode.PlayerDigitalButtons[0].Button1.Value)
                buttonData |= 0x80;

            if (InputCode.PlayerDigitalButtons[0].Button2 != null && InputCode.PlayerDigitalButtons[0].Button2.Value)
                buttonData |= 0x100;

            if (InputCode.PlayerDigitalButtons[0].Button3 != null && InputCode.PlayerDigitalButtons[0].Button3.Value)
                buttonData |= 0x200;

            if (InputCode.PlayerDigitalButtons[0].Button4 != null && InputCode.PlayerDigitalButtons[0].Button4.Value)
                buttonData |= 0x400;

            if (InputCode.PlayerDigitalButtons[0].Button5 != null && InputCode.PlayerDigitalButtons[0].Button5.Value)
                buttonData |= 0x800;

            if (InputCode.PlayerDigitalButtons[0].Button6 != null && InputCode.PlayerDigitalButtons[0].Button6.Value)
                buttonData |= 0x1000;
            // argh
            if (InputCode.PlayerDigitalButtons[1].Button1 != null && InputCode.PlayerDigitalButtons[1].Button1.Value)
                buttonData |= 0x2000;
            if (InputCode.PlayerDigitalButtons[1].Button2 == true)
                buttonData |= 0x4000;
            if (InputCode.PlayerDigitalButtons[1].Button3 != null && InputCode.PlayerDigitalButtons[1].Button3.Value)
                buttonData |= 0x8000;
            if (InputCode.PlayerDigitalButtons[1].Button4 != null && InputCode.PlayerDigitalButtons[1].Button4.Value)
                buttonData |= 0x10000;
            if (InputCode.PlayerDigitalButtons[1].Button5 != null && InputCode.PlayerDigitalButtons[1].Button5.Value)
                buttonData |= 0x20000;
            if (InputCode.PlayerDigitalButtons[1].Button6 != null && InputCode.PlayerDigitalButtons[1].Button6.Value)
                buttonData |= 0x40000;
            if (InputCode.PlayerDigitalButtons[1].Start != null && InputCode.PlayerDigitalButtons[1].Start.Value)
                buttonData |= 0x80000;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_1 == true)
                buttonData |= 0x100000;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_2 == true)
                buttonData |= 0x200000;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_3 == true)
                buttonData |= 0x400000;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_4 == true)
                buttonData |= 0x800000;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_5 == true)
                buttonData |= 0x1000000;
            if (InputCode.PlayerDigitalButtons[0].Test != null && InputCode.PlayerDigitalButtons[0].Test.Value)
                buttonData |= 0x04;

            if (InputCode.PlayerDigitalButtons[0].Service != null && InputCode.PlayerDigitalButtons[0].Service.Value)
                buttonData |= 0x01;

            JvsHelper.StateView.Write(8, buttonData);
        }

    }
}