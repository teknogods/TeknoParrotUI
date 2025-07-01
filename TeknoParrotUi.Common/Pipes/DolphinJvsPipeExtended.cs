using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    // Alternative extended version if you want to use more extension buttons:
    public class DolphinJvsPipeExtended : ControlSender
    {
        public override void Transmit()
        {
            uint control = 0;

            // === SHARED/SYSTEM BUTTONS ===
            // Test (shared test button for system diagnostics) - Bit 0x01
            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
                control |= 0x01;

            // === PLAYER 1 ===
            // Start - Bit 0x02 (Start button for all games)
            if (InputCode.PlayerDigitalButtons[0].Start.HasValue && InputCode.PlayerDigitalButtons[0].Start.Value)
                control |= 0x02;
            // Service - Bit 0x40 (Service/Settings button)
            if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value)
                control |= 0x40;
            // Button1 - Bit 0x04 (Primary: F-Zero=Boost, Virtua Striker=Long Pass, Mario Kart=Item, Gekitou=A Button)
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                control |= 0x04;
            // Button2 - Bit 0x20 (Secondary: F-Zero=Paddle Right, Virtua Striker=Short Pass, Mario Kart=VS-Cancel, Gekitou=B Button)
            if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                control |= 0x20;
            // Button3 - Bit 0x200 (Tertiary: Virtua Striker=Shoot, Gekitou=Gekitou Button, General=Action Button)
            if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
                control |= 0x200;
            // Left - Bit 0x400 (D-Pad Left / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[0].Left.HasValue && InputCode.PlayerDigitalButtons[0].Left.Value)
                control |= 0x400;
            // Up - Bit 0x800 (D-Pad Up / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[0].Up.HasValue && InputCode.PlayerDigitalButtons[0].Up.Value)
                control |= 0x800;
            // Right - Bit 0x1000 (D-Pad Right / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[0].Right.HasValue && InputCode.PlayerDigitalButtons[0].Right.Value)
                control |= 0x1000;
            // Down - Bit 0x2000 (D-Pad Down / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[0].Down.HasValue && InputCode.PlayerDigitalButtons[0].Down.Value)
                control |= 0x2000;

            // Extended buttons for advanced arcade controls
            // Button4 - Bit 0x80000 (F-Zero=View Change 1, Virtua Striker=Tactics Up, Racing=View Change)
            if (InputCode.PlayerDigitalButtons[0].Button4.HasValue && InputCode.PlayerDigitalButtons[0].Button4.Value)
                control |= 0x80000;
            // Button5 - Bit 0x100000 (F-Zero=View Change 2, Virtua Striker=Tactics Middle, Racing=Horn/Special)
            if (InputCode.PlayerDigitalButtons[0].Button5.HasValue && InputCode.PlayerDigitalButtons[0].Button5.Value)
                control |= 0x100000;
            // Button6 - Bit 0x200000 (F-Zero=View Change 3, Virtua Striker=Tactics Down, Racing=Nitro/Boost)
            if (InputCode.PlayerDigitalButtons[0].Button6.HasValue && InputCode.PlayerDigitalButtons[0].Button6.Value)
                control |= 0x200000;
            // ExtensionButton1_1 - Bit 0x400000 (F-Zero=View Change 4, Virtua Striker=IC-Card Lock, Special Function)
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_1.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_1.Value)
                control |= 0x400000;
            // ExtensionButton1_2 - Bit 0x8000000 (F-Zero=Paddle Left, Special Controls, Extra Action)
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_2.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_2.Value)
                control |= 0x8000000;
            // ExtensionButton1_3 - Bit 0x10000000 (Extra/Custom button, Game-specific functions)
            if (InputCode.PlayerDigitalButtons[0].ExtensionButton1_3.HasValue && InputCode.PlayerDigitalButtons[0].ExtensionButton1_3.Value)
                control |= 0x10000000;

            // === PLAYER 2 ===
            // Start - Bit 0x08 (Player 2 Start button for all games)
            if (InputCode.PlayerDigitalButtons[1].Start.HasValue && InputCode.PlayerDigitalButtons[1].Start.Value)
                control |= 0x08;
            // Service - Bit 0x100 (Player 2 Service/Settings button)
            if (InputCode.PlayerDigitalButtons[1].Service.HasValue && InputCode.PlayerDigitalButtons[1].Service.Value)
                control |= 0x100;
            // Button1 - Bit 0x10 (Primary: F-Zero=Boost, Virtua Striker=Long Pass, Mario Kart=Item, Gekitou=A Button)
            if (InputCode.PlayerDigitalButtons[1].Button1.HasValue && InputCode.PlayerDigitalButtons[1].Button1.Value)
                control |= 0x10;
            // Button2 - Bit 0x80 (Secondary: F-Zero=Paddle Right, Virtua Striker=Short Pass, Mario Kart=VS-Cancel, Gekitou=B Button)
            if (InputCode.PlayerDigitalButtons[1].Button2.HasValue && InputCode.PlayerDigitalButtons[1].Button2.Value)
                control |= 0x80;
            // Button3 - Bit 0x4000 (Tertiary: Virtua Striker=Shoot, Gekitou=Gekitou Button, General=Action Button)
            if (InputCode.PlayerDigitalButtons[1].Button3.HasValue && InputCode.PlayerDigitalButtons[1].Button3.Value)
                control |= 0x4000;
            // Left - Bit 0x8000 (Player 2 D-Pad Left / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[1].Left.HasValue && InputCode.PlayerDigitalButtons[1].Left.Value)
                control |= 0x8000;
            // Up - Bit 0x10000 (Player 2 D-Pad Up / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[1].Up.HasValue && InputCode.PlayerDigitalButtons[1].Up.Value)
                control |= 0x10000;
            // Right - Bit 0x20000 (Player 2 D-Pad Right / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[1].Right.HasValue && InputCode.PlayerDigitalButtons[1].Right.Value)
                control |= 0x20000;
            // Down - Bit 0x40000 (Player 2 D-Pad Down / Menu Navigation)
            if (InputCode.PlayerDigitalButtons[1].Down.HasValue && InputCode.PlayerDigitalButtons[1].Down.Value)
                control |= 0x40000;

            // Extended buttons for Player 2 advanced arcade controls
            // Button4 - Bit 0x800000 (F-Zero=View Change 1, Virtua Striker=Tactics Up, Racing=View Change)
            if (InputCode.PlayerDigitalButtons[1].Button4.HasValue && InputCode.PlayerDigitalButtons[1].Button4.Value)
                control |= 0x800000;
            // Button5 - Bit 0x1000000 (F-Zero=View Change 2, Virtua Striker=Tactics Middle, Racing=Horn/Special)
            if (InputCode.PlayerDigitalButtons[1].Button5.HasValue && InputCode.PlayerDigitalButtons[1].Button5.Value)
                control |= 0x1000000;
            // Button6 - Bit 0x2000000 (F-Zero=View Change 3, Virtua Striker=Tactics Down, Racing=Nitro/Boost)
            if (InputCode.PlayerDigitalButtons[1].Button6.HasValue && InputCode.PlayerDigitalButtons[1].Button6.Value)
                control |= 0x2000000;
            // ExtensionButton1_1 - Bit 0x4000000 (F-Zero=View Change 4, Virtua Striker=IC-Card Lock, Special Function)
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_1.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_1.Value)
                control |= 0x4000000;
            // ExtensionButton1_2 - Bit 0x20000000 (F-Zero=Paddle Left, Special Controls, Extra Action)
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_2.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_2.Value)
                control |= 0x20000000;
            // ExtensionButton1_3 - Bit 0x40000000 (Extra/Custom button, Game-specific functions)
            if (InputCode.PlayerDigitalButtons[1].ExtensionButton1_3.HasValue && InputCode.PlayerDigitalButtons[1].ExtensionButton1_3.Value)
                control |= 0x40000000;

            // Write to shared memory
            JvsHelper.StateView.Write(8, control);
            JvsHelper.StateView.Write(12, InputCode.AnalogBytes[0]);  // P1X / Wheel Left / Right
            JvsHelper.StateView.Write(13, InputCode.AnalogBytes[2]);  // P1Y / Wheel Up / Down
            JvsHelper.StateView.Write(14, InputCode.AnalogBytes[4]);  // P2X / Gas
            JvsHelper.StateView.Write(15, InputCode.AnalogBytes[6]);  // P2Y / Brake

            // Handle Coin separately - write to a different offset for coin counting
            int coinState = 0;
            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
                coinState = 1;

            JvsHelper.StateView.Write(32, coinState); // Coin at separate offset
        }
    }
}
