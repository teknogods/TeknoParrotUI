using System;
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
            if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value)
                Control |= (int)Buttons.SERVICE;

            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
                Control |= (int)Buttons.TEST;

            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                Control |= (int)Buttons.BUTTON1;

            if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                Control |= (int)Buttons.BUTTON2;

            if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
                Control |= (int)Buttons.BUTTON3;

            if (InputCode.PlayerDigitalButtons[0].Button4.HasValue && InputCode.PlayerDigitalButtons[0].Button4.Value)
                Control |= (int)Buttons.BUTTON4;

            if (InputCode.PlayerDigitalButtons[0].Button5.HasValue && InputCode.PlayerDigitalButtons[0].Button5.Value)
                Control |= (int)Buttons.BUTTON5;

            if (InputCode.PlayerDigitalButtons[0].Button6.HasValue && InputCode.PlayerDigitalButtons[0].Button6.Value)
                Control |= (int)Buttons.BUTTON6;

            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1.Value)
                Control |= (int)Buttons.BUTTON7;

            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_2.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_2.Value)
                Control |= (int)Buttons.BUTTON8;

            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_3.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_3.Value)
                Control |= (int)Buttons.BUTTON9;

            JvsHelper.StateView.Write(8, Control);
            JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);
            JvsHelper.StateView.Write(16, InputCode.AnalogBytes[2]);
            JvsHelper.StateView.Write(20, InputCode.AnalogBytes[4]);
        }
    }
}