using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class BanapassButtonEXVS2 : ControlSender
    {
        public override void Transmit()
        {
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1.Value)
                Control |= 0x01;

            JvsHelper.StateView.Write(8, Control);
        }
    }
}
