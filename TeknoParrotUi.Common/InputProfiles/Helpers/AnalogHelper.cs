using SharpDX.DirectInput;
using SharpDX.XInput;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.InputProfiles.Helpers
{
    public static class AnalogHelper
    {
        public static byte CalculateSWThrottleXinput(XInputButton button, State state)
        {
            if (button.IsButton)
            {
                var btnPress = DigitalHelper.GetButtonPressXinput(button, state, 0);
                if (btnPress == true)
                    return 0xFF;
                return 0x00;
            }
            
            if (button.IsLeftThumbY)
            {
                return JvsHelper.CalculateGasPos(32767 + state.Gamepad.LeftThumbY, true, false);
            }

            if (button.IsRightThumbY)
            {
                return JvsHelper.CalculateGasPos(32767 + state.Gamepad.RightThumbY, true, false);
            }

            if (button.IsLeftTrigger)
            {
                int LeftTrigger = 0x80 - ((state.Gamepad.RightTrigger - state.Gamepad.LeftTrigger) / 2);
                return (byte)LeftTrigger;
            }

            if (button.IsRightTrigger)
            {
                int RightTrigger = 0x80 - ((state.Gamepad.LeftTrigger - state.Gamepad.RightTrigger) / 2);
                return (byte)RightTrigger;
            }
            return 0;
        }
        public static byte CalculateAxisOrTriggerGasBrakeXinput(XInputButton button, State state, byte minVal = 0, byte maxVal = 255)
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
                return JvsHelper.CalculateGasPos(state.Gamepad.LeftThumbX, true, false, minVal, maxVal);
            }

            if (button.IsLeftThumbY)
            {
                return JvsHelper.CalculateGasPos(state.Gamepad.LeftThumbY, true, false, minVal, maxVal);
            }

            if (button.IsRightThumbX)
            {
                return JvsHelper.CalculateGasPos(state.Gamepad.RightThumbX, true, false, minVal, maxVal);
            }

            if (button.IsRightThumbY)
            {
                return JvsHelper.CalculateGasPos(state.Gamepad.RightThumbY, true, false, minVal, maxVal);
            }

            int result = 0;
            int divider = maxVal - minVal;

            if (button.IsLeftTrigger)
            {
                result = state.Gamepad.LeftTrigger;
            }

            if (button.IsRightTrigger)
            {
                result = state.Gamepad.RightTrigger;
            }

            result = result / (255 / divider);

            result += minVal;

            if (result < minVal)
                result = minVal;
            if (result > maxVal)
                result = maxVal;

            return (byte)result;
        }

        public static byte CalculateWheelPosXinput(XInputButton button, State state, bool useSto0Z, int stoozPercent,
            GameProfile gameProfile)
        {
            int minValWheel = 0;
            int maxValWheel = 255;
            switch (gameProfile.EmulationProfile)
            {
                case EmulationProfile.SegaInitialD:
                case EmulationProfile.SegaInitialDLindbergh:
                    minValWheel = 0x1F;
                    maxValWheel = 0xE1;
                    break;
                case EmulationProfile.IDZ:
                    minValWheel = 0x36;
                    maxValWheel = 0xCA;
                    break;
                case EmulationProfile.SegaSonicAllStarsRacing:
                    minValWheel = 0x1D;
                    maxValWheel = 0xED;
                    break;
                case EmulationProfile.HummerExtreme:
                    minValWheel = 0x1D;
                    maxValWheel = 0xE0;
                    break;
                case EmulationProfile.HotWheels:
                    minValWheel = 0x00;
                    maxValWheel = 0xFE;
                    break;
            }

            if (button.IsLeftThumbX)
            {
                return useSto0Z ? JvsHelper.CalculateSto0ZWheelPos(state.Gamepad.LeftThumbX, stoozPercent, true) : JvsHelper.CalculateWheelPos(state.Gamepad.LeftThumbX, true, false, minValWheel, maxValWheel);
            }

            if (button.IsLeftThumbY)
            {
                return useSto0Z ? JvsHelper.CalculateSto0ZWheelPos(state.Gamepad.LeftThumbY, stoozPercent, true) : JvsHelper.CalculateWheelPos(state.Gamepad.LeftThumbY, true, false, minValWheel, maxValWheel);
            }

            if (button.IsRightThumbX)
            {
                return useSto0Z ? JvsHelper.CalculateSto0ZWheelPos(state.Gamepad.RightThumbX, stoozPercent, true) : JvsHelper.CalculateWheelPos(state.Gamepad.RightThumbX, true, false, minValWheel, maxValWheel);
            }

            if (button.IsRightThumbY)
            {
                return useSto0Z ? JvsHelper.CalculateSto0ZWheelPos(state.Gamepad.RightThumbY, stoozPercent, true) : JvsHelper.CalculateWheelPos(state.Gamepad.RightThumbY, true, false, minValWheel, maxValWheel);
            }

            return 0x7F;
        }
    }
}
