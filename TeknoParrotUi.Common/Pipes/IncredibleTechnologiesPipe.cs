using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class IncredibleTechnologiesPipe : ControlSender
    {
        public override void Transmit()
        {
            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
                Control |= 0x0001;
            if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value)
                Control |= 0x0002;
            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
                Control |= 0x0004;
            if (InputCode.PlayerDigitalButtons[1].Coin.HasValue && InputCode.PlayerDigitalButtons[1].Coin.Value)
                Control |= 0x0008;
            if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
                Control |= 0x0010;
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                Control |= 0x0020;
            if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                Control |= 0x0040;
            if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
                Control |= 0x0080;
            if (InputCode.PlayerDigitalButtons[0].Button4.HasValue && InputCode.PlayerDigitalButtons[0].Button4.Value)
                Control |= 0x0100;
            if (InputCode.PlayerDigitalButtons[0].Button5.HasValue && InputCode.PlayerDigitalButtons[0].Button5.Value)
                Control |= 0x0200;
            if (InputCode.PlayerDigitalButtons[0].Button6.HasValue && InputCode.PlayerDigitalButtons[0].Button6.Value)
                Control |= 0x0400;
            if (InputCode.PlayerDigitalButtons[1].Start.HasValue && InputCode.PlayerDigitalButtons[1].Start.Value)
                Control |= 0x0800;
            if (InputCode.PlayerDigitalButtons[0].Up.HasValue && InputCode.PlayerDigitalButtons[0].Up.Value)
                Control |= 0x1000;
            if (InputCode.PlayerDigitalButtons[0].Down.HasValue && InputCode.PlayerDigitalButtons[0].Down.Value)
                Control |= 0x2000;
            if (InputCode.PlayerDigitalButtons[1].Button1.HasValue && InputCode.PlayerDigitalButtons[1].Button1.Value)
                Control |= 0x4000;
            if (InputCode.PlayerDigitalButtons[1].Button2.HasValue && InputCode.PlayerDigitalButtons[1].Button2.Value)
                Control |= 0x8000;

            JvsHelper.StateView.Write(8, Control);
			JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);
			JvsHelper.StateView.Write(13, InputCode.AnalogBytes[1]);
			JvsHelper.StateView.Write(16, InputCode.AnalogBytes[2]);
			JvsHelper.StateView.Write(17, InputCode.AnalogBytes[3]);
        }
    }
}
