using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class RawThrills : ControlSender
    {
        private bool _combineGasBrake;
        private new int Control2 = 0;
        
        public RawThrills(bool combineGasBrake)
        {
            _combineGasBrake = combineGasBrake;
        }
        public override void Transmit()
        {
            Control2 = 0;

            // Test
            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
                Control |= 0x0001;
            // Service
            if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value)
                Control |= 0x0002;
            // Coin
            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
                Control |= 0x0004;
            // Start
            if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
                Control |= 0x0008;

            // 
            if (InputCode.PlayerDigitalButtons[0].Button5.HasValue && InputCode.PlayerDigitalButtons[0].Button5.Value)
                Control |= 0x0010;
            // 
            if (InputCode.PlayerDigitalButtons[0].Button6.HasValue && InputCode.PlayerDigitalButtons[0].Button6.Value)
                Control |= 0x0020;
            //
            if (InputCode.PlayerDigitalButtons[1].Up.HasValue && InputCode.PlayerDigitalButtons[1].Up.Value)
                Control |= 0x0040;
            //
            if (InputCode.PlayerDigitalButtons[1].Down.HasValue && InputCode.PlayerDigitalButtons[1].Down.Value)
                Control |= 0x0080;

            // NITRO
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                Control |= 0x0100;
            // View1
            if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                Control |= 0x0200;
            // View2
            if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
                Control |= 0x0400;
            // View3
            if (InputCode.PlayerDigitalButtons[0].Button4.HasValue && InputCode.PlayerDigitalButtons[0].Button4.Value)
                Control |= 0x0800;

            // SHIFT UP
            if (InputCode.PlayerDigitalButtons[0].Up.HasValue && InputCode.PlayerDigitalButtons[0].Up.Value)
                Control |= 0x1000;
            // SHIFT DOWN
            if (InputCode.PlayerDigitalButtons[0].Down.HasValue && InputCode.PlayerDigitalButtons[0].Down.Value)
                Control |= 0x2000;
            // Menu Left
            if (InputCode.PlayerDigitalButtons[0].Left.HasValue && InputCode.PlayerDigitalButtons[0].Left.Value)
                Control |= 0x4000;
            // Menu Right
            if (InputCode.PlayerDigitalButtons[0].Right.HasValue && InputCode.PlayerDigitalButtons[0].Right.Value)
                Control |= 0x8000;

            // Gear 1
            if (InputCode.PlayerDigitalButtons[2].Button1.HasValue && InputCode.PlayerDigitalButtons[2].Button1.Value)
                Control2 |= 0x01;
            // Gear 2
            if (InputCode.PlayerDigitalButtons[2].Button2.HasValue && InputCode.PlayerDigitalButtons[2].Button2.Value)
                Control2 |= 0x02;
            // Gear 3
            if (InputCode.PlayerDigitalButtons[2].Button3.HasValue && InputCode.PlayerDigitalButtons[2].Button3.Value)
                Control2 |= 0x04;
            // Gear 4
            if (InputCode.PlayerDigitalButtons[2].Button4.HasValue && InputCode.PlayerDigitalButtons[2].Button4.Value)
                Control2 |= 0x8;

            JvsHelper.StateView.Write(8, Control);
            JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);
            if (_combineGasBrake)
            {
                if (InputCode.AnalogBytes[4] > 0x00)
                {
                    JvsHelper.StateView.Write(16, 0 - InputCode.AnalogBytes[4]);
                    JvsHelper.StateView.Write(20, 0);
                }
                else if (InputCode.AnalogBytes[2] > 0x00)
                {
                    JvsHelper.StateView.Write(16, 0 + InputCode.AnalogBytes[2]);
                    JvsHelper.StateView.Write(20, 0);
                }
                else
                {
                    JvsHelper.StateView.Write(16, 0);
                    JvsHelper.StateView.Write(20, 0);
                }
            }
            else
            {
                JvsHelper.StateView.Write(16, InputCode.AnalogBytes[2]);
                JvsHelper.StateView.Write(20, InputCode.AnalogBytes[4]);
            }

            JvsHelper.StateView.Write(24, Control2);
        }
    }
}
