using System;
using System.Diagnostics;
using System.Linq;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.Pipes
{
    public class Pcsx2x6Pipe : ControlSender
    {
        private bool IsTimeCrsCabinetIdSet = false;
        private string cabId = "1";
        public override void Transmit()
        {
            var control1 = JvsPackageEmulator.GetPlayerControls(0);
            var control1ext = JvsPackageEmulator.GetPlayerControlsExt(0);
            var control2 = JvsPackageEmulator.GetPlayerControls(1);
            var control2ext = JvsPackageEmulator.GetPlayerControlsExt(1);
            var testData = JvsPackageEmulator.GetSpecialBits(0);

            if (!IsTimeCrsCabinetIdSet)
            {
                if (InputCode.GameProfile.ProfileName == "timecrs3" || InputCode.GameProfile.ProfileName == "timecrs4")
                {
                    var iD = InputCode.GameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "Cabinet Id");
                    if (iD != null)
                    {
                        IsTimeCrsCabinetIdSet = true;
                        cabId = iD.FieldValue;
                    }
                }
            }

            if (InputCode.GameProfile.ProfileName == "timecrs3" && cabId == "2")
                control1ext |= 0x80;

            if (InputCode.GameProfile.ProfileName == "timecrs4" && cabId == "2")
                control1ext |= 0x40;

            JvsHelper.StateView.Write(8, testData);
            JvsHelper.StateView.Write(9, control1);
            JvsHelper.StateView.Write(10, control1ext);
            JvsHelper.StateView.Write(11, control2);
            JvsHelper.StateView.Write(12, control2ext);

            JvsHelper.StateView.Write(13, InputCode.AnalogBytes[0]);
            JvsHelper.StateView.Write(14, InputCode.AnalogBytes[2]);
            JvsHelper.StateView.Write(15, InputCode.AnalogBytes[4]);
            JvsHelper.StateView.Write(16, InputCode.AnalogBytes[6]);

            int coinState = 0;
            if (InputCode.PlayerDigitalButtons[0].Coin.HasValue && InputCode.PlayerDigitalButtons[0].Coin.Value)
                coinState = 1;

            JvsHelper.StateView.Write(32, coinState);
        }
    }
}
