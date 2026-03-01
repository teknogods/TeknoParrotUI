using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class WonderlandWarsPipe : ControlSender
    {
        public override void Transmit()
        {
            // Pen Button
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                Control |= 0x01;
            // Dodge Button
            if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                Control |= 0x02;

            JvsHelper.StateView.Write(8, Control);
            JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);  // P1X
            JvsHelper.StateView.Write(16, InputCode.AnalogBytes[2]);  // P1Y

            // for future updates? maybe
            int aimeControl = 0;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_3.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_3.Value)
                aimeControl |= 0x01;

            JvsHelper.StateView.Write(32, aimeControl);
        }
    }
}
