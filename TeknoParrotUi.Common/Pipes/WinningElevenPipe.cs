using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
	public class WinningElevenPipe : ControlSender
	{
		public override void Transmit()
		{
			// Test
			if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
				Control |= 0x01;
			// Service
			if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value)
				Control |= 0x02;
			// Coin1
			if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
				Control |= 0x04;
			// Coin2
			if (InputCode.PlayerDigitalButtons[1].Coin.HasValue && InputCode.PlayerDigitalButtons[1].Coin.Value)
				Control |= 0x08;
			// Start 1
			if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
				Control |= 0x10;
			// Start 2 / Double Deal
			if (InputCode.PlayerDigitalButtons[1].Start.HasValue && InputCode.PlayerDigitalButtons[1].Start.Value)
				Control |= 0x20;
			// Bill
			if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
				Control |= 0x40;
			// Volume Up
			if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
				Control |= 0x80;
			// Volume Down
			if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
				Control |= 0x100;
			// Case 1
			if (InputCode.PlayerDigitalButtons[0].Button4.HasValue && InputCode.PlayerDigitalButtons[0].Button4.Value)
				Control |= 0x200;
			// Case 2
			if (InputCode.PlayerDigitalButtons[0].Button5.HasValue && InputCode.PlayerDigitalButtons[0].Button5.Value)
				Control |= 0x400;
			// Case 3
			if (InputCode.PlayerDigitalButtons[0].Button6.HasValue && InputCode.PlayerDigitalButtons[0].Button6.Value)
				Control |= 0x800;
			// Case 4
			if (InputCode.PlayerDigitalButtons[0].Up.HasValue && InputCode.PlayerDigitalButtons[0].Up.Value)
				Control |= 0x1000;
			// Case 5
			if (InputCode.PlayerDigitalButtons[0].Down.HasValue && InputCode.PlayerDigitalButtons[0].Down.Value)
				Control |= 0x2000;
			// Case 6
			if (InputCode.PlayerDigitalButtons[0].Left.HasValue && InputCode.PlayerDigitalButtons[0].Left.Value)
				Control |= 0x4000;
			// Case 7
			if (InputCode.PlayerDigitalButtons[0].Right.HasValue && InputCode.PlayerDigitalButtons[0].Right.Value)
				Control |= 0x8000;
			// Case 8
			if (InputCode.PlayerDigitalButtons[1].Button1.HasValue && InputCode.PlayerDigitalButtons[1].Button1.Value)
				Control |= 0x10000;
			// Case 9
			if (InputCode.PlayerDigitalButtons[1].Button2.HasValue && InputCode.PlayerDigitalButtons[1].Button2.Value)
				Control |= 0x20000;
			// Case 10
			if (InputCode.PlayerDigitalButtons[1].Button3.HasValue && InputCode.PlayerDigitalButtons[1].Button3.Value)
				Control |= 0x40000;
			// Case 11
			if (InputCode.PlayerDigitalButtons[1].Button4.HasValue && InputCode.PlayerDigitalButtons[1].Button4.Value)
				Control |= 0x80000;
			// Case 12
			if (InputCode.PlayerDigitalButtons[1].Button5.HasValue && InputCode.PlayerDigitalButtons[1].Button5.Value)
				Control |= 0x100000;
			// Case 13
			if (InputCode.PlayerDigitalButtons[1].Button6.HasValue && InputCode.PlayerDigitalButtons[1].Button6.Value)
				Control |= 0x200000;
			// Case 14
			if (InputCode.PlayerDigitalButtons[1].Up.HasValue && InputCode.PlayerDigitalButtons[1].Up.Value)
				Control |= 0x400000;
			// Case 15
			if (InputCode.PlayerDigitalButtons[1].Down.HasValue && InputCode.PlayerDigitalButtons[1].Down.Value)
				Control |= 0x800000;
			// Case 16
			if (InputCode.PlayerDigitalButtons[1].Left.HasValue && InputCode.PlayerDigitalButtons[1].Left.Value)
				Control |= 0x1000000;
			// Deal
			if (InputCode.PlayerDigitalButtons[1].Right.HasValue && InputCode.PlayerDigitalButtons[1].Right.Value)
				Control |= 0x2000000;
			// No Deal
			if (InputCode.PlayerDigitalButtons[1].Service.HasValue && InputCode.PlayerDigitalButtons[1].Service.Value)
				Control |= 0x4000000;
			// Modifier Up
			if (InputCode.PlayerDigitalButtons[0].RelativeUp.HasValue && InputCode.PlayerDigitalButtons[0].RelativeUp.Value)
				Control |= 0x8000000;
			// Modifier Down
			if (InputCode.PlayerDigitalButtons[0].RelativeDown.HasValue && InputCode.PlayerDigitalButtons[0].RelativeDown.Value)
				Control |= 0x10000000;
			// Modifier Left
			if (InputCode.PlayerDigitalButtons[0].RelativeLeft.HasValue && InputCode.PlayerDigitalButtons[0].RelativeLeft.Value)
				Control |= 0x20000000;
			// Modifier Right
			if (InputCode.PlayerDigitalButtons[0].RelativeRight.HasValue && InputCode.PlayerDigitalButtons[0].RelativeRight.Value)
				Control |= 0x40000000;
			// Modifier Button 1
			if (InputCode.PlayerDigitalButtons[1].ExtensionButton1.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1.Value)
				Control |= unchecked((int)0x80000000);

			JvsHelper.StateView.Write(8, Control);
			JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);
			JvsHelper.StateView.Write(13, InputCode.AnalogBytes[2]);
			JvsHelper.StateView.Write(14, InputCode.AnalogBytes[4]);
			JvsHelper.StateView.Write(15, InputCode.AnalogBytes[6]);
			JvsHelper.StateView.Write(16, InputCode.AnalogBytes[8]);
			JvsHelper.StateView.Write(17, InputCode.AnalogBytes[10]);
		}
	}
}
