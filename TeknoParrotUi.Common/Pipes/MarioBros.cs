using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class MarioBrosPipe : ControlSender
    {
        public override void Transmit()
        {
            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value) //P1 Test
                Control |= 0x01;
            if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value) //P1 Cancel
                Control |= 0x02;
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value) //P1 Selection
                Control |= 0x04;
            if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value) //P1 Start
                Control |= 0x08;
            if (InputCode.PlayerDigitalButtons[0].Up.HasValue && InputCode.PlayerDigitalButtons[0].Up.Value) //P1 Up
                Control |= 0x10;
            if (InputCode.PlayerDigitalButtons[0].Down.HasValue && InputCode.PlayerDigitalButtons[0].Down.Value) //P1 Down
                Control |= 0x20;
            if (InputCode.PlayerDigitalButtons[0].Left.HasValue && InputCode.PlayerDigitalButtons[0].Left.Value) //P1 Left
                Control |= 0x40;
            if (InputCode.PlayerDigitalButtons[0].Right.HasValue && InputCode.PlayerDigitalButtons[0].Right.Value) //P1 Right
                Control |= 0x80;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_4.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_4.Value) //P1 Bet
                Control |= 0x10000;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_5.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_5.Value) //P1 Payout
                Control |= 0x20000;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1.Value) //P1 Medal
                Control |= 0x100000;

            if (InputCode.PlayerDigitalButtons[1].Test.HasValue && InputCode.PlayerDigitalButtons[1].Test.Value) //P2 Test
                Control |= 0x100;
            if (InputCode.PlayerDigitalButtons[1].Service.HasValue && InputCode.PlayerDigitalButtons[1].Service.Value) //P2 Cancel
                Control |= 0x200;
            if (InputCode.PlayerDigitalButtons[1].Button1.HasValue && InputCode.PlayerDigitalButtons[1].Button1.Value) //P2 Selection
                Control |= 0x400;
            if (InputCode.PlayerDigitalButtons[1].Start.HasValue && InputCode.PlayerDigitalButtons[1].Start.Value) //P2 Start
                Control |= 0x800;
            if (InputCode.PlayerDigitalButtons[1].Up.HasValue && InputCode.PlayerDigitalButtons[1].Up.Value) //P2 Up
                Control |= 0x1000;
            if (InputCode.PlayerDigitalButtons[1].Down.HasValue && InputCode.PlayerDigitalButtons[1].Down.Value) //P2 Down
                Control |= 0x2000;
            if (InputCode.PlayerDigitalButtons[1].Left.HasValue && InputCode.PlayerDigitalButtons[1].Left.Value) //P2 Left
                Control |= 0x4000;
            if (InputCode.PlayerDigitalButtons[1].Right.HasValue && InputCode.PlayerDigitalButtons[1].Right.Value) //P2 Right
                Control |= 0x8000;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_4.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_4.Value) //P2 Bet
                Control |= 0x40000;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_5.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_5.Value) //P2 Payout
                Control |= 0x80000;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton2.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton2.Value) //P2 Medal
                Control |= 0x200000;

            if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value) //P3 Test
                Control2 |= 0x01;
            if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value) //P3 Cancel
                Control2 |= 0x02;
            if (InputCode.PlayerDigitalButtons[0].Button4.HasValue && InputCode.PlayerDigitalButtons[0].Button4.Value) //P3 Selection
                Control2 |= 0x04;
            if (InputCode.PlayerDigitalButtons[0].Button5.HasValue && InputCode.PlayerDigitalButtons[0].Button5.Value) //P3 Start
                Control2 |= 0x08;
            if (InputCode.PlayerDigitalButtons[0].Button6.HasValue && InputCode.PlayerDigitalButtons[0].Button6.Value) //P3 Up
                Control2 |= 0x10;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_1.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_1.Value) //P3 Down
                Control2 |= 0x20;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_2.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_2.Value) //P3 Left
                Control2 |= 0x40;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_3.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_3.Value) //P3 Right
                Control2 |= 0x80;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_6.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_6.Value) //P3 Bet
                Control2 |= 0x10000;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_7.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_7.Value) //P3 Payout
                Control2 |= 0x20000;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton3.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton3.Value) //P3 Medal
                Control2 |= 0x100000;

            if (InputCode.PlayerDigitalButtons[1].Button2.HasValue && InputCode.PlayerDigitalButtons[1].Button2.Value) //P4 Test
                Control2 |= 0x100;
            if (InputCode.PlayerDigitalButtons[1].Button3.HasValue && InputCode.PlayerDigitalButtons[1].Button3.Value) //P4 Cancel
                Control2 |= 0x200;
            if (InputCode.PlayerDigitalButtons[1].Button4.HasValue && InputCode.PlayerDigitalButtons[1].Button4.Value) //P4 Selection
                Control2 |= 0x400;
            if (InputCode.PlayerDigitalButtons[1].Button5.HasValue && InputCode.PlayerDigitalButtons[1].Button5.Value) //P4 Start
                Control2 |= 0x800;
            if (InputCode.PlayerDigitalButtons[1].Button6.HasValue && InputCode.PlayerDigitalButtons[1].Button6.Value) //P4 Up
                Control2 |= 0x1000;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_1.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_1.Value) //P4 Down
                Control2 |= 0x2000;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_2.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_2.Value) //P4 Left
                Control2 |= 0x4000;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_3.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_3.Value) //P4 Right
                Control2 |= 0x8000;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_6.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_6.Value) //P4 Bet
                Control2 |= 0x40000;
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_7.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_7.Value) //P4 Payout
                Control2 |= 0x80000;
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton4.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton4.Value) //P4 Medal
                Control2 |= 0x200000;

            JvsHelper.StateView.Write(4, Control2);
            JvsHelper.StateView.Write(8, Control);
        }
    }
}
