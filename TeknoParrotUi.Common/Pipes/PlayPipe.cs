using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    // Alternative extended version if you want to use more extension buttons:
    public class PlayPipe : ControlSender
    {
        public override void Transmit()
        {
            var control1 = JvsPackageEmulator.GetPlayerControls(0);
            var control1ext = JvsPackageEmulator.GetPlayerControlsExt(0);
            var control2 = JvsPackageEmulator.GetPlayerControls(1);
            var control2ext = JvsPackageEmulator.GetPlayerControlsExt(1);
            var testData = JvsPackageEmulator.GetSpecialBits(0);

            JvsHelper.StateView.Write(8, testData);
            JvsHelper.StateView.Write(9, control1);
            JvsHelper.StateView.Write(10, control1ext);
            JvsHelper.StateView.Write(11, control2);
            JvsHelper.StateView.Write(12, control2ext);

            JvsHelper.StateView.Write(13, InputCode.AnalogBytes[0]);
            JvsHelper.StateView.Write(14, InputCode.AnalogBytes[2]);
            JvsHelper.StateView.Write(15, InputCode.AnalogBytes[4]);
            JvsHelper.StateView.Write(16, InputCode.AnalogBytes[6]);

            // Handle Coin separately - write to a different offset for coin counting
            int coinState = 0;
            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
                coinState = 1;

            JvsHelper.StateView.Write(32, coinState); // Coin at separate offset
        }
    }
}
