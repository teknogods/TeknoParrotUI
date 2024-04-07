using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class AimeButton : ControlSender
    {
        public override void Transmit()
        {
            int aimeControl = 0;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_3.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_3.Value)
            {
                aimeControl |= 0x01;
            }

            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_4.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_4.Value)
            {
                aimeControl |= 0x02;
            }

            JvsHelper.StateView.Write(32, aimeControl);
        }
    }
}
