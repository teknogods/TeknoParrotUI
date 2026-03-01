using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
	public class RawThrillsGUN : ControlSender
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


			JvsHelper.StateView.Write(8, Control);
			JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);
			JvsHelper.StateView.Write(16, InputCode.AnalogBytes[2]);
			JvsHelper.StateView.Write(20, InputCode.AnalogBytes[4]);
			JvsHelper.StateView.Write(24, InputCode.AnalogBytes[6]);
		}
	}
}
