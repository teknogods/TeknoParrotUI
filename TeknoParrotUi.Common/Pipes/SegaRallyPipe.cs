using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeknoParrotUi.Common.Pipes
{
    public class SegaRallyPipe : EuropaRPipe
    {
        public override void HandleButtons()
        {
            if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
                buttons1 |= 0x01;
            // Handbrake
            if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
                buttons1 |= 0x02;
            // View Change
            if (InputCode.PlayerDigitalButtons[0].Button4.HasValue && InputCode.PlayerDigitalButtons[0].Button4.Value)
                buttons1 |= 0x04;

            // Shifts
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                buttons1 |= 0x08;
            if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                buttons1 |= 0x10;

            var report = new byte[15];

            report[1] = InputCode.AnalogBytes[0]; // (byte)((s.Gamepad.LeftThumbX / 256) + 128);
            report[2] = InputCode.AnalogBytes[0]; // (byte)((s.Gamepad.LeftThumbY / 256) + 128);
            report[3] = InputCode.AnalogBytes[4]; //s.Gamepad.LeftTrigger;
            report[4] = InputCode.AnalogBytes[2]; //s.Gamepad.RightTrigger;

            report[6] = buttons1; //(byte)((int)s.Gamepad.Buttons & 0xFF);
            report[7] = 0; //(byte)(((int)s.Gamepad.Buttons >> 8) & 0xFF);

            report[7] |= 4 | 8;

            _npServer.Write(report, 0, 15);
        }
    }
}
