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
            if (InputCode.PlayerDigitalButtons[1].Button3.HasValue && InputCode.PlayerDigitalButtons[1].Button3.Value)
                Control |= 0x10000;

            //P2
            if (InputCode.StreamingPlayerDigitalButtons[0].Start.HasValue && InputCode.StreamingPlayerDigitalButtons[0].Start.Value)
                Control2 |= 0x0010;
            if (InputCode.StreamingPlayerDigitalButtons[0].Button1.HasValue && InputCode.StreamingPlayerDigitalButtons[0].Button1.Value)
                Control2 |= 0x0020;
            if (InputCode.StreamingPlayerDigitalButtons[0].Button2.HasValue && InputCode.StreamingPlayerDigitalButtons[0].Button2.Value)
                Control2 |= 0x0040;
            if (InputCode.StreamingPlayerDigitalButtons[0].Button3.HasValue && InputCode.StreamingPlayerDigitalButtons[0].Button3.Value)
                Control2 |= 0x0080;
            if (InputCode.StreamingPlayerDigitalButtons[0].Button4.HasValue && InputCode.StreamingPlayerDigitalButtons[0].Button4.Value)
                Control2 |= 0x0100;
            if (InputCode.StreamingPlayerDigitalButtons[0].Button6.HasValue && InputCode.StreamingPlayerDigitalButtons[0].Button6.Value)
                Control2 |= 0x0400;
            if (InputCode.StreamingPlayerDigitalButtons[1].Button1.HasValue && InputCode.StreamingPlayerDigitalButtons[1].Button1.Value)
                Control2 |= 0x4000;
            if (InputCode.StreamingPlayerDigitalButtons[1].Button2.HasValue && InputCode.StreamingPlayerDigitalButtons[1].Button2.Value)
                Control2 |= 0x8000;
            if (InputCode.StreamingPlayerDigitalButtons[1].Start.HasValue && InputCode.StreamingPlayerDigitalButtons[1].Start.Value)
                Control2 |= 0x0800;

            //P3
            if (InputCode.StreamingPlayerDigitalButtons[2].Start.HasValue && InputCode.StreamingPlayerDigitalButtons[2].Start.Value)
                Control3 |= 0x0010;
            if (InputCode.StreamingPlayerDigitalButtons[2].Button1.HasValue && InputCode.StreamingPlayerDigitalButtons[2].Button1.Value)
                Control3 |= 0x0020;
            if (InputCode.StreamingPlayerDigitalButtons[2].Button2.HasValue && InputCode.StreamingPlayerDigitalButtons[2].Button2.Value)
                Control3 |= 0x0040;
            if (InputCode.StreamingPlayerDigitalButtons[2].Button3.HasValue && InputCode.StreamingPlayerDigitalButtons[2].Button3.Value)
                Control3 |= 0x0080;
            if (InputCode.StreamingPlayerDigitalButtons[2].Button4.HasValue && InputCode.StreamingPlayerDigitalButtons[2].Button4.Value)
                Control3 |= 0x0100;
            if (InputCode.StreamingPlayerDigitalButtons[2].Button6.HasValue && InputCode.StreamingPlayerDigitalButtons[2].Button6.Value)
                Control3 |= 0x0400;
            if (InputCode.StreamingPlayerDigitalButtons[3].Button1.HasValue && InputCode.StreamingPlayerDigitalButtons[3].Button1.Value)
                Control3 |= 0x4000;
            if (InputCode.StreamingPlayerDigitalButtons[3].Button2.HasValue && InputCode.StreamingPlayerDigitalButtons[3].Button2.Value)
                Control3 |= 0x8000;
            if (InputCode.StreamingPlayerDigitalButtons[3].Start.HasValue && InputCode.StreamingPlayerDigitalButtons[3].Start.Value)
                Control3 |= 0x0800;

            //P4
            if (InputCode.StreamingPlayerDigitalButtons[4].Start.HasValue && InputCode.StreamingPlayerDigitalButtons[4].Start.Value)
                Control4 |= 0x0010;
            if (InputCode.StreamingPlayerDigitalButtons[4].Button1.HasValue && InputCode.StreamingPlayerDigitalButtons[4].Button1.Value)
                Control4 |= 0x0020;
            if (InputCode.StreamingPlayerDigitalButtons[4].Button2.HasValue && InputCode.StreamingPlayerDigitalButtons[4].Button2.Value)
                Control4 |= 0x0040;
            if (InputCode.StreamingPlayerDigitalButtons[4].Button3.HasValue && InputCode.StreamingPlayerDigitalButtons[4].Button3.Value)
                Control4 |= 0x0080;
            if (InputCode.StreamingPlayerDigitalButtons[4].Button4.HasValue && InputCode.StreamingPlayerDigitalButtons[4].Button4.Value)
                Control4 |= 0x0100;
            if (InputCode.StreamingPlayerDigitalButtons[4].Button6.HasValue && InputCode.StreamingPlayerDigitalButtons[4].Button6.Value)
                Control4 |= 0x0400;
            if (InputCode.StreamingPlayerDigitalButtons[5].Button1.HasValue && InputCode.StreamingPlayerDigitalButtons[5].Button1.Value)
                Control4 |= 0x4000;
            if (InputCode.StreamingPlayerDigitalButtons[5].Button2.HasValue && InputCode.StreamingPlayerDigitalButtons[5].Button2.Value)
                Control4 |= 0x8000;
            if (InputCode.StreamingPlayerDigitalButtons[5].Start.HasValue && InputCode.StreamingPlayerDigitalButtons[5].Start.Value)
                Control4 |= 0x0800;

            JvsHelper.StateView.Write(8, Control); 
            JvsHelper.StateView.Write(24, Control2); // P2  
            JvsHelper.StateView.Write(40, Control3); // P3
            JvsHelper.StateView.Write(56, Control4); // P4

            JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);
            JvsHelper.StateView.Write(13, InputCode.AnalogBytes[1]);
            JvsHelper.StateView.Write(16, InputCode.AnalogBytes[2]);
            JvsHelper.StateView.Write(17, InputCode.AnalogBytes[3]);
        }
    }
}