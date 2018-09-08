using SharpDX.DirectInput;
using SharpDX.XInput;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.InputProfiles.Helpers
{
    public static class AnalogHelper
    {
        public static byte CalculateAxisOrTriggerGasBrakeXinput(XInputButton button, State state)
        {
            if (button.IsButton)
            {
                var btnPress = DigitalHelper.GetButtonPressXinput(button, state, 0);
                if (btnPress == true)
                    return 0xFF;
                return 0x00;
            }

            if (button.IsLeftThumbX)
            {
                return JvsHelper.CalculateGasPos(state.Gamepad.LeftThumbX, true, false);
            }

            if (button.IsLeftThumbY)
            {
                return JvsHelper.CalculateGasPos(state.Gamepad.LeftThumbY, true, false);
            }

            if (button.IsRightThumbX)
            {
                return JvsHelper.CalculateGasPos(state.Gamepad.RightThumbX, true, false);
            }

            if (button.IsRightThumbY)
            {
                return JvsHelper.CalculateGasPos(state.Gamepad.RightThumbY, true, false);
            }

            if (button.IsLeftTrigger)
            {
                return state.Gamepad.LeftTrigger;
            }

            if (button.IsRightTrigger)
            {
                return state.Gamepad.RightTrigger;
            }
            return 0;
        }

        public static byte CalculateWheelPosXinput(XInputButton button, State state, bool useSto0Z, int stoozPercent,
            GameProfile gameProfile)
        {
            int minVal = 0;
            int maxVal = 255;
            switch (gameProfile.EmulationProfile)
            {
                case EmulationProfile.SegaInitialD:
                    minVal = 0x1F;
                    maxVal = 0xE1;
                    break;
                case EmulationProfile.SegaInitialDLindbergh:
                    minVal = 0x1F;
                    maxVal = 0xE1;
                    break;
                case EmulationProfile.SegaSonicAllStarsRacing:
                    minVal = 0x1D;
                    maxVal = 0xED;
                    break;
                default:
                    minVal = 0;
                    maxVal = 255;
                    break;
            }

            if (button.IsLeftThumbX)
            {
                return useSto0Z ? JvsHelper.CalculateSto0ZWheelPos(state.Gamepad.LeftThumbX, stoozPercent, true) : JvsHelper.CalculateWheelPos(state.Gamepad.LeftThumbX, true, false, minVal, maxVal);
            }

            if (button.IsLeftThumbY)
            {
                return useSto0Z ? JvsHelper.CalculateSto0ZWheelPos(state.Gamepad.LeftThumbY, stoozPercent, true) : JvsHelper.CalculateWheelPos(state.Gamepad.LeftThumbY, true, false, minVal, maxVal);
            }

            if (button.IsRightThumbX)
            {
                return useSto0Z ? JvsHelper.CalculateSto0ZWheelPos(state.Gamepad.RightThumbX, stoozPercent, true) : JvsHelper.CalculateWheelPos(state.Gamepad.RightThumbX, true, false, minVal, maxVal);
            }

            if (button.IsRightThumbY)
            {
                return useSto0Z ? JvsHelper.CalculateSto0ZWheelPos(state.Gamepad.RightThumbY, stoozPercent, true) : JvsHelper.CalculateWheelPos(state.Gamepad.RightThumbY, true, false, minVal, maxVal);
            }
            return 0x7F;
        }
    }
}
