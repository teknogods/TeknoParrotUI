using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
	public class FamilyGuyPipe : ControlSender
	{
		public override void Transmit()
		{
			// Test
			if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
				Control |= 0x0001;
			// Service
			if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value)
				Control |= 0x0002;
			// Coin1
			if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
				Control |= 0x0004;
			// Coin2
			if (InputCode.PlayerDigitalButtons[1].Coin.HasValue && InputCode.PlayerDigitalButtons[1].Coin.Value)
				Control |= 0x0008;

			// START P1
			if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
				Control |= 0x0010;
			// TRIGGER P1
			if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
				Control |= 0x0020;
			// GRENADE P1
			if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
				Control |= 0x0040;
			// RELOAD P1
			if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
				Control |= 0x0080;

			// START P2
			if (InputCode.PlayerDigitalButtons[1].Start.HasValue && InputCode.PlayerDigitalButtons[1].Start.Value)
				Control |= 0x0100;
			// TRIGGER P2
			if (InputCode.PlayerDigitalButtons[1].Button1.HasValue && InputCode.PlayerDigitalButtons[1].Button1.Value)
				Control |= 0x0200;
			// GRENADE P2
			if (InputCode.PlayerDigitalButtons[1].Button2.HasValue && InputCode.PlayerDigitalButtons[1].Button2.Value)
				Control |= 0x0400;
			// RELOAD P2
			if (InputCode.PlayerDigitalButtons[1].Button3.HasValue && InputCode.PlayerDigitalButtons[1].Button3.Value)
				Control |= 0x0800;

			// VOLUME UP
			if (InputCode.PlayerDigitalButtons[0].Up.HasValue && InputCode.PlayerDigitalButtons[0].Up.Value)
				Control |= 0x1000;
			// VOLUME DOWN
			if (InputCode.PlayerDigitalButtons[0].Down.HasValue && InputCode.PlayerDigitalButtons[0].Down.Value)
				Control |= 0x2000;
			// free
			if (InputCode.PlayerDigitalButtons[0].Left.HasValue && InputCode.PlayerDigitalButtons[0].Left.Value)
				Control |= 0x4000;
			// free
			if (InputCode.PlayerDigitalButtons[0].Right.HasValue && InputCode.PlayerDigitalButtons[0].Right.Value)
				Control |= 0x8000;

			// P2
            if (InputCode.StreamingPlayerDigitalButtons[0].Start.HasValue && InputCode.StreamingPlayerDigitalButtons[0].Start.Value)
                Control2 |= 0x0010;
            if (InputCode.StreamingPlayerDigitalButtons[0].Button1.HasValue && InputCode.StreamingPlayerDigitalButtons[0].Button1.Value)
                Control2 |= 0x0020;
            if (InputCode.StreamingPlayerDigitalButtons[0].Button2.HasValue && InputCode.StreamingPlayerDigitalButtons[0].Button2.Value)
                Control2 |= 0x0040;
            if (InputCode.StreamingPlayerDigitalButtons[0].Left.HasValue && InputCode.StreamingPlayerDigitalButtons[0].Left.Value)
                Control2 |= 0x4000;
            if (InputCode.StreamingPlayerDigitalButtons[0].Right.HasValue && InputCode.StreamingPlayerDigitalButtons[0].Right.Value)
                Control2 |= 0x8000;

            // P3
            if (InputCode.StreamingPlayerDigitalButtons[2].Start.HasValue && InputCode.StreamingPlayerDigitalButtons[2].Start.Value)
                Control3 |= 0x0010;
            if (InputCode.StreamingPlayerDigitalButtons[2].Button1.HasValue && InputCode.StreamingPlayerDigitalButtons[2].Button1.Value)
                Control3 |= 0x0020;
            if (InputCode.StreamingPlayerDigitalButtons[2].Button2.HasValue && InputCode.StreamingPlayerDigitalButtons[2].Button2.Value)
                Control3 |= 0x0040;
            if (InputCode.StreamingPlayerDigitalButtons[2].Left.HasValue && InputCode.StreamingPlayerDigitalButtons[2].Left.Value)
                Control3 |= 0x4000;
            if (InputCode.StreamingPlayerDigitalButtons[2].Right.HasValue && InputCode.StreamingPlayerDigitalButtons[2].Right.Value)
                Control3 |= 0x8000;

            // P4
            if (InputCode.StreamingPlayerDigitalButtons[4].Start.HasValue && InputCode.StreamingPlayerDigitalButtons[4].Start.Value)
                Control4 |= 0x0010;
            if (InputCode.StreamingPlayerDigitalButtons[4].Button1.HasValue && InputCode.StreamingPlayerDigitalButtons[4].Button1.Value)
                Control4 |= 0x0020;
            if (InputCode.StreamingPlayerDigitalButtons[4].Button2.HasValue && InputCode.StreamingPlayerDigitalButtons[4].Button2.Value)
                Control4 |= 0x0040;
            if (InputCode.StreamingPlayerDigitalButtons[4].Left.HasValue && InputCode.StreamingPlayerDigitalButtons[4].Left.Value)
                Control4 |= 0x4000;
            if (InputCode.StreamingPlayerDigitalButtons[4].Right.HasValue && InputCode.StreamingPlayerDigitalButtons[4].Right.Value)
                Control4 |= 0x8000;

            // Host
            if (InputCode.StreamingPlayerDigitalButtons[6].Start.HasValue && InputCode.StreamingPlayerDigitalButtons[6].Start.Value)
                ControlHost |= 0x0010;
            if (InputCode.StreamingPlayerDigitalButtons[6].Button1.HasValue && InputCode.StreamingPlayerDigitalButtons[6].Button1.Value)
                ControlHost |= 0x0020;
            if (InputCode.StreamingPlayerDigitalButtons[6].Button2.HasValue && InputCode.StreamingPlayerDigitalButtons[6].Button2.Value)
                ControlHost |= 0x0040;


            JvsHelper.StateView.Write(8, Control);
            JvsHelper.StateView.Write(28, Control2); // P2
            JvsHelper.StateView.Write(32, Control3); // P3
            JvsHelper.StateView.Write(36, Control4); // P4
            JvsHelper.StateView.Write(4, ControlHost); // Host

            JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);
			JvsHelper.StateView.Write(16, InputCode.AnalogBytes[2]);
			JvsHelper.StateView.Write(20, InputCode.AnalogBytes[4]);
			JvsHelper.StateView.Write(24, InputCode.AnalogBytes[6]);
		}
	}
}