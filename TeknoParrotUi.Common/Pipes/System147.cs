using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    // Alternative extended version if you want to use more extension buttons:
    public class System147 : ControlSender
    {
        public override void Transmit()
        {
            Byte control = 0;
            Byte control2 = 0;
            Byte control3 = 0;
            Byte control4 = 0;
            Byte control5 = 0;

            // === SHARED/SYSTEM BUTTONS ===
            // Test (shared test button for system diagnostics) - Bit 0x01
            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
                control |= 0x01;

            // === PLAYER 1 ===
            // Start - Bit 0x02 (Start button for all games)
            if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
                control |= 0x20;
            // Left - Bit 0x400 (D-Pad Left / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[0].Left.HasValue && InputCode.PlayerDigitalButtons[0].Left.Value)
                control |= 0x08;
            // Up - Bit 0x800 (D-Pad Up / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[0].Up.HasValue && InputCode.PlayerDigitalButtons[0].Up.Value)
                control |= 0x02;
            // Right - Bit 0x1000 (D-Pad Right / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[0].Right.HasValue && InputCode.PlayerDigitalButtons[0].Right.Value)
                control |= 0x10;
            // Down - Bit 0x2000 (D-Pad Down / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[0].Down.HasValue && InputCode.PlayerDigitalButtons[0].Down.Value)
                control |= 0x04;
            // Service - Bit 0x40 (Service button for all games)
            if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value)
                control |= 0x40;

            // === PLAYER 2 ===
            // Start - Bit 0x02 (Start button for all games)
            if (InputCode.PlayerDigitalButtons[1].Start.HasValue && InputCode.PlayerDigitalButtons[1].Start.Value)
                control2 |= 0x20;
            // Left - Bit 0x400 (D-Pad Left / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[1].Left.HasValue && InputCode.PlayerDigitalButtons[1].Left.Value)
                control2 |= 0x08;
            // Up - Bit 0x800 (D-Pad Up / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[1].Up.HasValue && InputCode.PlayerDigitalButtons[1].Up.Value)
                control2 |= 0x02;
            // Right - Bit 0x1000 (D-Pad Right / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[1].Right.HasValue && InputCode.PlayerDigitalButtons[1].Right.Value)
                control2 |= 0x10;
            // Down - Bit 0x2000 (D-Pad Down / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[1].Down.HasValue && InputCode.PlayerDigitalButtons[1].Down.Value)
                control2 |= 0x04;

            // === PLAYER 3 ===
            // Start - Bit 0x02 (Start button for all games)
            if (InputCode.PlayerDigitalButtons[2].Start.HasValue && InputCode.PlayerDigitalButtons[2].Start.Value)
                control3 |= 0x20;
            // Left - Bit 0x400 (D-Pad Left / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[2].Left.HasValue && InputCode.PlayerDigitalButtons[2].Left.Value)
                control3 |= 0x08;
            // Up - Bit 0x800 (D-Pad Up / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[2].Up.HasValue && InputCode.PlayerDigitalButtons[2].Up.Value)
                control3 |= 0x02;
            // Right - Bit 0x1000 (D-Pad Right / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[2].Right.HasValue && InputCode.PlayerDigitalButtons[2].Right.Value)
                control3 |= 0x10;
            // Down - Bit 0x2000 (D-Pad Down / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[2].Down.HasValue && InputCode.PlayerDigitalButtons[2].Down.Value)
                control3 |= 0x04;

            // === PLAYER 4 ===
            // Start - Bit 0x02 (Start button for all games)
            if (InputCode.PlayerDigitalButtons[3].Start.HasValue && InputCode.PlayerDigitalButtons[3].Start.Value)
                control4 |= 0x20;
            // Left - Bit 0x400 (D-Pad Left / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[3].Left.HasValue && InputCode.PlayerDigitalButtons[3].Left.Value)
                control4 |= 0x08;
            // Up - Bit 0x800 (D-Pad Up / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[3].Up.HasValue && InputCode.PlayerDigitalButtons[3].Up.Value)
                control4 |= 0x02;
            // Right - Bit 0x1000 (D-Pad Right / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[3].Right.HasValue && InputCode.PlayerDigitalButtons[3].Right.Value)
                control4 |= 0x10;
            // Down - Bit 0x2000 (D-Pad Down / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[3].Down.HasValue && InputCode.PlayerDigitalButtons[3].Down.Value)
                control4 |= 0x04;

            // Handle Coin separately - write to a different offset for coin counting
            int coinState = 0;
            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
                coinState = 1;

            // Actual Buttons for P1
            // Button 1 - Bit 0x01
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                control5 |= 0x01;
            // Button 2 - Bit 0x02
            if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                control5 |= 0x02;
            // Button 3 - Bit 0x04
            if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
                control5 |= 0x04;
            // Button 4 - Bit 0x08
            if (InputCode.PlayerDigitalButtons[0].Button4.HasValue && InputCode.PlayerDigitalButtons[0].Button4.Value)
                control5 |= 0x08;
            // Button 5 - Bit 0x10
            if (InputCode.PlayerDigitalButtons[0].Button5.HasValue && InputCode.PlayerDigitalButtons[0].Button5.Value)
                control5 |= 0x10;
            // Button 6 - Bit 0x20
            if (InputCode.PlayerDigitalButtons[0].Button6.HasValue && InputCode.PlayerDigitalButtons[0].Button6.Value)
                control5 |= 0x20;

            JvsHelper.StateView.Write(8, control);
            JvsHelper.StateView.Write(9, control2);
            JvsHelper.StateView.Write(10, control3);
            JvsHelper.StateView.Write(11, control4);
            JvsHelper.StateView.Write(12, control5);

            JvsHelper.StateView.Write(32, coinState); // Coin at separate offset
        }
    }
}
