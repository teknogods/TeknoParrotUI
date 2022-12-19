using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
	public class PointBlankPipe : ControlSender
	{
		public override void Transmit()
		{
			// Coin
			if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
				Control |= 0x01;
			// Service
			if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value)
				Control |= 0x02;
			// Test
			if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
				Control |= 0x04;
			// Select Up
			if (InputCode.PlayerDigitalButtons[0].Up.HasValue && InputCode.PlayerDigitalButtons[0].Up.Value)
				Control |= 0x08;
			// Select Down
			if (InputCode.PlayerDigitalButtons[0].Down.HasValue && InputCode.PlayerDigitalButtons[0].Down.Value)
				Control |= 0x10;
			// Enter
			if (InputCode.PlayerDigitalButtons[0].Right.HasValue && InputCode.PlayerDigitalButtons[0].Right.Value)
				Control |= 0x20;

			// Player 1 Start
			if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
				Control |= 0x40;

			// Player 1 Trigger
			if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
				Control |= 0x80;

			// Player 2 Start
			if (InputCode.PlayerDigitalButtons[1].Start.HasValue && InputCode.PlayerDigitalButtons[1].Start.Value)
				Control |= 0x400;

			// Player 2 Trigger
			if (InputCode.PlayerDigitalButtons[1].Button1.HasValue && InputCode.PlayerDigitalButtons[1].Button1.Value)
				Control |= 0x800;

			JvsHelper.StateView.Write(8, Control);
			JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);  // P1X
			JvsHelper.StateView.Write(16, InputCode.AnalogBytes[2]);  // P1Y
			JvsHelper.StateView.Write(20, InputCode.AnalogBytes[4]);  // P2X
			JvsHelper.StateView.Write(24, InputCode.AnalogBytes[6]);  // P2Y
		}
	}
}
