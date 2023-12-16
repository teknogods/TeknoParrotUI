using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class LuigisMansion : ControlSender
    {
        public override void Transmit()
        {
            uint gunAxis = 0;
            // P1 Start
            if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
                Control |= 0x01;
            // P2 Controller Button
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                Control |= 0x02;
            // P3 Controller Lever
            if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                Control |= 0x04;
            // P2 Start
            if (InputCode.PlayerDigitalButtons[1].Start.HasValue && InputCode.PlayerDigitalButtons[1].Start.Value)
                Control |= 0x08;
            // P2 Controller Button
            if (InputCode.PlayerDigitalButtons[1].Button1.HasValue && InputCode.PlayerDigitalButtons[1].Button1.Value)
                Control |= 0x10;
            // P2 Controller Lever
            if (InputCode.PlayerDigitalButtons[1].Button2.HasValue && InputCode.PlayerDigitalButtons[1].Button2.Value)
                Control |= 0x20;
            // Test SW
            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
                Control |= 0x0100;
            // Select Switch
            if (InputCode.PlayerDigitalButtons[0].Button4.HasValue && InputCode.PlayerDigitalButtons[0].Button4.Value)
                Control |= 0x0200;
            // Service Switch
            if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value)
                Control |= 0x0400;

            // P1 Screen out for vacuuming
            if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
                Control |= 0x010000;
            // P2 Screen out for vacuuming
            if (InputCode.PlayerDigitalButtons[1].Button3.HasValue && InputCode.PlayerDigitalButtons[1].Button3.Value)
                Control |= 0x020000;

            // Analogs
            // P1 Y
            gunAxis = InputCode.AnalogBytes[0];
            // P1 X
            gunAxis += (uint)InputCode.AnalogBytes[2] * 0x100;
            // P2 Y
            gunAxis += (uint)InputCode.AnalogBytes[4] * 0x10000;
            // P2 X
            gunAxis += (uint)InputCode.AnalogBytes[6] * 0x1000000;

            JvsHelper.StateView.Write(8, Control);
            JvsHelper.StateView.Write(12, gunAxis);
        }
    }
}
