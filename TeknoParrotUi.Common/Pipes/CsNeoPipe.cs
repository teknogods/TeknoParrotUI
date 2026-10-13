using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    /// <summary>
    /// Counter-Strike NEO (ElfLdr2): the cabinet's switches as one bit each in the 32-bit word at offset 8 of
    /// TeknoParrot_JvsState, set while the button is held. The DLL turns them into the cabinet's hardware (the I/O
    /// board's test and service switches, the two coin counters, the player card on the reader); the loader turns the
    /// panel's YES and NO into the keys the game reads for them (right Alt, right Ctrl). Nothing else is sent: moving,
    /// aiming and firing are the PC's keyboard and mouse, which the game reads itself.
    /// </summary>
    public class CsNeoPipe : ControlSender
    {
        public override void Start()
        {
            // InputCode keeps its state between the games of one TeknoParrotUI session: a button that was still held when
            // the last game ended must not arrive here as a coin, a card or a flip of the test switch.
            InputCode.PlayerDigitalButtons[0].Test = false;
            InputCode.PlayerDigitalButtons[0].Service = false;
            InputCode.PlayerDigitalButtons[0].Coin = false;
            InputCode.PlayerDigitalButtons[1].Coin = false;
            InputCode.PlayerDigitalButtons[0].Button1 = false;
            InputCode.PlayerDigitalButtons[0].Button2 = false;
            InputCode.PlayerDigitalButtons[0].Button3 = false;
            base.Start();
        }

        public override void Transmit()
        {
            // Test
            if (InputCode.PlayerDigitalButtons[0].Test.HasValue && InputCode.PlayerDigitalButtons[0].Test.Value)
                Control |= 0x0001;
            // Service
            if (InputCode.PlayerDigitalButtons[0].Service.HasValue && InputCode.PlayerDigitalButtons[0].Service.Value)
                Control |= 0x0002;
            // Coin 1 (100 yen)
            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
                Control |= 0x0004;
            // Coin 2 (500 yen)
            if (InputCode.PlayerDigitalButtons[1].Coin.HasValue && InputCode.PlayerDigitalButtons[1].Coin.Value)
                Control |= 0x0008;
            // Insert Card (the DLL holds the profile's Card ID on the reader)
            if (InputCode.PlayerDigitalButtons[0].Button1.HasValue && InputCode.PlayerDigitalButtons[0].Button1.Value)
                Control |= 0x0010;
            // YES and NO, the two keys of the cabinet's panel the game reads from its keyboard: the loader turns these
            // bits into the right Alt and right Ctrl key (ElfLoader csneo_hooks.cpp), the DLL leaves them alone
            if (InputCode.PlayerDigitalButtons[0].Button2.HasValue && InputCode.PlayerDigitalButtons[0].Button2.Value)
                Control |= 0x0020;
            if (InputCode.PlayerDigitalButtons[0].Button3.HasValue && InputCode.PlayerDigitalButtons[0].Button3.Value)
                Control |= 0x0040;

            JvsHelper.StateView.Write(8, Control);
        }
    }
}
