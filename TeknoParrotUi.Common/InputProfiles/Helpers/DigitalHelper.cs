using TeknoParrotUi.Common.InputListening.Gamepad;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.InputProfiles.Helpers
{
    public static class DigitalHelper
    {
        public static int CurrentWmmt5Gear = 0;
        public static int CurrentSrcGear = 1;
        public static int CurrentIDZGear = 0;
        public static int CurrentFnfGear = 1;

        public static void ChangeWmmt5GearUp()
        {
            if (CurrentWmmt5Gear == 6)
                return;
            ChangeWmmt5Gear(CurrentWmmt5Gear + 1);
        }

        public static void ChangeWmmt5GearDown()
        {
            if (CurrentWmmt5Gear == 0)
                return;
            ChangeWmmt5Gear(CurrentWmmt5Gear - 1);
        }

        public static void ChangeSrcGearUp()
        {
            if (CurrentSrcGear == 4)
                return;
            ChangeSrcGear(CurrentSrcGear + 1);
        }

        public static void ChangeSrcGearDown()
        {
            if (CurrentSrcGear == 1)
                return;
            ChangeSrcGear(CurrentSrcGear - 1);
        }

        public static void ChangeIDZGearUp()
        {
            if (CurrentIDZGear == 6)
                return;
            ChangeIDZGear(CurrentIDZGear + 1);
        }

        public static void ChangeIDZGearDown()
        {
            if (CurrentIDZGear == 0)
                return;
            ChangeIDZGear(CurrentIDZGear - 1);
        }
        public static void ChangeFnfGearUp()
        {
            if (CurrentFnfGear == 4)
                return;
            ChangeFnfGear(CurrentFnfGear + 1);
        }

        public static void ChangeFnfGearDown()
        {
            if (CurrentFnfGear == 1)
                return;
            ChangeFnfGear(CurrentFnfGear - 1);
        }

        public static void ChangeSrcGear(int gear)
        {
            switch (gear)
            {
                case 2:
                    {
                        InputCode.PlayerDigitalButtons[1].Up = false;
                        InputCode.PlayerDigitalButtons[1].Left = true;
                        InputCode.PlayerDigitalButtons[1].Right = false;
                        InputCode.PlayerDigitalButtons[1].Down = true;
                        CurrentSrcGear = 2;
                    }
                    break;
                case 3:
                    {
                        InputCode.PlayerDigitalButtons[1].Up = true;
                        InputCode.PlayerDigitalButtons[1].Left = false;
                        InputCode.PlayerDigitalButtons[1].Right = false;
                        InputCode.PlayerDigitalButtons[1].Down = false;
                        CurrentSrcGear = 3;
                    }
                    break;
                case 4:
                    {
                        InputCode.PlayerDigitalButtons[1].Up = false;
                        InputCode.PlayerDigitalButtons[1].Left = false;
                        InputCode.PlayerDigitalButtons[1].Right = false;
                        InputCode.PlayerDigitalButtons[1].Down = true;
                        CurrentSrcGear = 4;
                    }
                    break;
                default:
                    {
                        InputCode.PlayerDigitalButtons[1].Up = true;
                        InputCode.PlayerDigitalButtons[1].Left = true;
                        InputCode.PlayerDigitalButtons[1].Right = false;
                        InputCode.PlayerDigitalButtons[1].Down = false;
                        CurrentSrcGear = 1;
                    }
                    break;
            }
        }

        public static void ChangeFnfGear(int gear)
        {
            switch (gear)
            {
                case 2:
                    {
                        InputCode.PlayerDigitalButtons[2].Button1 = false;
                        InputCode.PlayerDigitalButtons[2].Button2 = true;
                        InputCode.PlayerDigitalButtons[2].Button3 = false;
                        InputCode.PlayerDigitalButtons[2].Button4 = false;
                        CurrentFnfGear = 2;
                    }
                    break;
                case 3:
                    {
                        InputCode.PlayerDigitalButtons[2].Button1 = false;
                        InputCode.PlayerDigitalButtons[2].Button2 = false;
                        InputCode.PlayerDigitalButtons[2].Button3 = true;
                        InputCode.PlayerDigitalButtons[2].Button4 = false;
                        CurrentFnfGear = 3;
                    }
                    break;
                case 4:
                    {
                        InputCode.PlayerDigitalButtons[2].Button1 = false;
                        InputCode.PlayerDigitalButtons[2].Button2 = false;
                        InputCode.PlayerDigitalButtons[2].Button3 = false;
                        InputCode.PlayerDigitalButtons[2].Button4 = true;
                        CurrentFnfGear = 4;
                    }
                    break;
                default:
                    {
                        InputCode.PlayerDigitalButtons[2].Button1 = true;
                        InputCode.PlayerDigitalButtons[2].Button2 = false;
                        InputCode.PlayerDigitalButtons[2].Button3 = false;
                        InputCode.PlayerDigitalButtons[2].Button4 = false;
                        CurrentFnfGear = 1;
                    }
                    break;
            }
        }
        public static void ChangeWmmt5Gear(int gear)
        {
            switch (gear)
            {
                case 2:
                    {
                        InputCode.PlayerDigitalButtons[0].Button1 = false;
                        InputCode.PlayerDigitalButtons[0].Button2 = false;
                        InputCode.PlayerDigitalButtons[0].Button3 = false;
                        InputCode.PlayerDigitalButtons[0].Button4 = true;
                        InputCode.PlayerDigitalButtons[0].Button5 = true;
                        InputCode.PlayerDigitalButtons[0].Button6 = false;
                        CurrentWmmt5Gear = 2;
                    }
                    break;
                case 3:
                    {
                        InputCode.PlayerDigitalButtons[0].Button1 = false;
                        InputCode.PlayerDigitalButtons[0].Button2 = false;
                        InputCode.PlayerDigitalButtons[0].Button3 = true;
                        InputCode.PlayerDigitalButtons[0].Button4 = false;
                        InputCode.PlayerDigitalButtons[0].Button5 = false;
                        InputCode.PlayerDigitalButtons[0].Button6 = false;
                        CurrentWmmt5Gear = 3;
                    }
                    break;
                case 4:
                    {
                        InputCode.PlayerDigitalButtons[0].Button1 = false;
                        InputCode.PlayerDigitalButtons[0].Button2 = false;
                        InputCode.PlayerDigitalButtons[0].Button3 = false;
                        InputCode.PlayerDigitalButtons[0].Button4 = true;
                        InputCode.PlayerDigitalButtons[0].Button5 = false;
                        InputCode.PlayerDigitalButtons[0].Button6 = false;
                        CurrentWmmt5Gear = 4;
                    }
                    break;
                case 5:
                    {
                        InputCode.PlayerDigitalButtons[0].Button1 = false;
                        InputCode.PlayerDigitalButtons[0].Button2 = false;
                        InputCode.PlayerDigitalButtons[0].Button3 = true;
                        InputCode.PlayerDigitalButtons[0].Button4 = false;
                        InputCode.PlayerDigitalButtons[0].Button5 = false;
                        InputCode.PlayerDigitalButtons[0].Button6 = true;
                        CurrentWmmt5Gear = 5;
                    }
                    break;
                case 6:
                    {
                        InputCode.PlayerDigitalButtons[0].Button1 = false;
                        InputCode.PlayerDigitalButtons[0].Button2 = false;
                        InputCode.PlayerDigitalButtons[0].Button3 = false;
                        InputCode.PlayerDigitalButtons[0].Button4 = true;
                        InputCode.PlayerDigitalButtons[0].Button5 = false;
                        InputCode.PlayerDigitalButtons[0].Button6 = true;
                        CurrentWmmt5Gear = 6;
                    }
                    break;
                case 1:
                    {
                        InputCode.PlayerDigitalButtons[0].Button1 = false;
                        InputCode.PlayerDigitalButtons[0].Button2 = false;
                        InputCode.PlayerDigitalButtons[0].Button3 = true;
                        InputCode.PlayerDigitalButtons[0].Button4 = false;
                        InputCode.PlayerDigitalButtons[0].Button5 = true;
                        InputCode.PlayerDigitalButtons[0].Button6 = false;
                        CurrentWmmt5Gear = 1;
                    }
                    break;
                case 0:
                    {
                        InputCode.PlayerDigitalButtons[0].Button1 = false;
                        InputCode.PlayerDigitalButtons[0].Button2 = false;
                        InputCode.PlayerDigitalButtons[0].Button3 = false;
                        InputCode.PlayerDigitalButtons[0].Button4 = false;
                        InputCode.PlayerDigitalButtons[0].Button5 = false;
                        InputCode.PlayerDigitalButtons[0].Button6 = false;
                        CurrentWmmt5Gear = 0;
                    }
                    break;
            }
        }

        public static void ChangeIDZGear(int gear)
        {
            switch (gear)
            {
                case 0:
                    {
                        InputCode.PlayerDigitalButtons[1].Up = false;
                        InputCode.PlayerDigitalButtons[1].Down = false;
                        InputCode.PlayerDigitalButtons[1].Left = false;
                        InputCode.PlayerDigitalButtons[1].Right = false;
                        CurrentIDZGear = 0;
                    }
                    break;
                case 1:
                    {
                        InputCode.PlayerDigitalButtons[1].Up = true;
                        InputCode.PlayerDigitalButtons[1].Down = false;
                        InputCode.PlayerDigitalButtons[1].Left = true;
                        InputCode.PlayerDigitalButtons[1].Right = false;
                        CurrentIDZGear = 1;
                    }
                    break;
                case 2:
                    {
                        InputCode.PlayerDigitalButtons[1].Up = false;
                        InputCode.PlayerDigitalButtons[1].Down = true;
                        InputCode.PlayerDigitalButtons[1].Left = true;
                        InputCode.PlayerDigitalButtons[1].Right = false;
                        CurrentIDZGear = 2;
                    }
                    break;
                case 3:
                    {
                        InputCode.PlayerDigitalButtons[1].Up = true;
                        InputCode.PlayerDigitalButtons[1].Down = false;
                        InputCode.PlayerDigitalButtons[1].Left = false;
                        InputCode.PlayerDigitalButtons[1].Right = false;
                        CurrentIDZGear = 3;
                    }
                    break;
                case 4:
                    {
                        InputCode.PlayerDigitalButtons[1].Up = false;
                        InputCode.PlayerDigitalButtons[1].Down = true;
                        InputCode.PlayerDigitalButtons[1].Left = false;
                        InputCode.PlayerDigitalButtons[1].Right = false;
                        CurrentIDZGear = 4;
                    }
                    break;
                case 5:
                    {
                        InputCode.PlayerDigitalButtons[1].Up = true;
                        InputCode.PlayerDigitalButtons[1].Down = false;
                        InputCode.PlayerDigitalButtons[1].Left = false;
                        InputCode.PlayerDigitalButtons[1].Right = true;
                        CurrentIDZGear = 5;
                    }
                    break;
                case 6:
                    {
                        InputCode.PlayerDigitalButtons[1].Up = false;
                        InputCode.PlayerDigitalButtons[1].Down = true;
                        InputCode.PlayerDigitalButtons[1].Left = false;
                        InputCode.PlayerDigitalButtons[1].Right = true;
                        CurrentIDZGear = 6;
                    }
                    break;
            }
        }

        public static void ChangeIdGear(int gear)
        {
            JvsHelper.StateView.Write(4, gear);
        }

        /// <summary>
        /// Gets if button is pressed. Null if not the same as requested.
        /// </summary>
        /// <param name="button"></param>
        /// <param name="state"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static bool? GetButtonPressXinput(XInputButton button, State state, int index)
        {
            if (button?.XInputIndex != index)
                return null;

            if (button.IsLeftTrigger)
                return state.Gamepad.LeftTrigger != 0;

            if (button.IsRightTrigger)
                return state.Gamepad.RightTrigger != 0;

            // Handle analog stick as button press
            if (button.IsLeftThumbX || button.IsLeftThumbY || button.IsRightThumbX || button.IsRightThumbY)
            {
                var calcVal = 0;
                if (button.IsLeftThumbY) calcVal = state.Gamepad.LeftThumbY;
                if (button.IsLeftThumbX) calcVal = state.Gamepad.LeftThumbX;
                if (button.IsRightThumbX) calcVal = state.Gamepad.RightThumbX;
                if (button.IsRightThumbY) calcVal = state.Gamepad.RightThumbY;

                if (button.IsAxisMinus)
                {
                    // Negative direction (down/left)
                    return calcVal <= -15000;
                }
                else
                {
                    // Positive direction (up/right)
                    return calcVal >= 15000;
                }
            }

            var buttonButtonCode = (short)state.Gamepad.Buttons;
            return (buttonButtonCode & button.ButtonCode) != 0;
        }

        /// <summary>
        /// Get directional press from button, POV and analog.
        /// </summary>
        /// <param name="playerButtons"></param>
        /// <param name="button"></param>
        /// <param name="state"></param>
        /// <param name="direction"></param>
        public static void GetDirectionPressXinput(PlayerButtons playerButtons, XInputButton button, State state, Direction direction, int index)
        {
            if (button?.XInputIndex != index)
                return;

            // Analog Axis, we expect that the both direction are on same axis!!!!
            if (button.IsLeftThumbX || button.IsLeftThumbY || button.IsRightThumbX || button.IsRightThumbY)
            {
                var calcVal = 0;
                if (button.IsLeftThumbY) calcVal = state.Gamepad.LeftThumbY;
                if (button.IsLeftThumbX) calcVal = state.Gamepad.LeftThumbX;
                if (button.IsRightThumbX) calcVal = state.Gamepad.RightThumbX;
                if (button.IsRightThumbY) calcVal = state.Gamepad.RightThumbY;
                if (button.IsAxisMinus)
                {
                    if (calcVal >= 0 + 15000)
                    {
                    }
                    else if (calcVal <= 0 - 15000)
                    {
                        InputCode.SetPlayerDirection(playerButtons, direction);
                    }
                    else
                    {
                        if (direction == Direction.Left || direction == Direction.Right)
                            InputCode.SetPlayerDirection(playerButtons, Direction.HorizontalCenter);
                        if (direction == Direction.Up || direction == Direction.Down)
                            InputCode.SetPlayerDirection(playerButtons, Direction.VerticalCenter);
                        if (direction == Direction.FFLeft || direction == Direction.FFRight)
                            InputCode.SetPlayerDirection(playerButtons, Direction.FFHoriCenter);
                        if (direction == Direction.FFUp || direction == Direction.FFDown)
                            InputCode.SetPlayerDirection(playerButtons, Direction.FFVertCenter);
                        if (direction == Direction.RelativeLeft || direction == Direction.RelativeRight)
                            InputCode.SetPlayerDirection(playerButtons, Direction.RelativeHoriCenter);
                        if (direction == Direction.RelativeUp || direction == Direction.RelativeDown)
                            InputCode.SetPlayerDirection(playerButtons, Direction.RelativeVertCenter);

                    }
                }
                else
                {
                    if (calcVal >= 0 + 15000)
                    {
                        InputCode.SetPlayerDirection(playerButtons, direction);
                    }
                    else if (calcVal <= 0 - 15000)
                    {
                    }
                    else
                    {
                        if (direction == Direction.Left || direction == Direction.Right)
                            InputCode.SetPlayerDirection(playerButtons, Direction.HorizontalCenter);
                        if (direction == Direction.Up || direction == Direction.Down)
                            InputCode.SetPlayerDirection(playerButtons, Direction.VerticalCenter);
                        if (direction == Direction.FFLeft || direction == Direction.FFRight)
                            InputCode.SetPlayerDirection(playerButtons, Direction.FFHoriCenter);
                        if (direction == Direction.FFUp || direction == Direction.FFDown)
                            InputCode.SetPlayerDirection(playerButtons, Direction.FFVertCenter);
                        if (direction == Direction.RelativeLeft || direction == Direction.RelativeRight)
                            InputCode.SetPlayerDirection(playerButtons, Direction.RelativeHoriCenter);
                        if (direction == Direction.RelativeUp || direction == Direction.RelativeDown)
                            InputCode.SetPlayerDirection(playerButtons, Direction.RelativeVertCenter);
                    }
                }
            }

            // Normal button
            if (button.IsButton)
            {
                if ((button.ButtonCode & (short)state.Gamepad.Buttons) != 0)
                {
                    InputCode.SetPlayerDirection(playerButtons, direction);
                }
                else
                {
                    if (direction == Direction.Left && !playerButtons.RightPressed())
                        InputCode.SetPlayerDirection(playerButtons, Direction.HorizontalCenter);
                    if (direction == Direction.Right && !playerButtons.LeftPressed())
                        InputCode.SetPlayerDirection(playerButtons, Direction.HorizontalCenter);
                    if (direction == Direction.Up && !playerButtons.DownPressed())
                        InputCode.SetPlayerDirection(playerButtons, Direction.VerticalCenter);
                    if (direction == Direction.Down && !playerButtons.UpPressed())
                        InputCode.SetPlayerDirection(playerButtons, Direction.VerticalCenter);
                    if (direction == Direction.FFLeft && !playerButtons.FFRightPressed())
                        InputCode.SetPlayerDirection(playerButtons, Direction.FFHoriCenter);
                    if (direction == Direction.FFRight && !playerButtons.FFLeftPressed())
                        InputCode.SetPlayerDirection(playerButtons, Direction.FFHoriCenter);
                    if (direction == Direction.FFUp && !playerButtons.FFDownPressed())
                        InputCode.SetPlayerDirection(playerButtons, Direction.FFVertCenter);
                    if (direction == Direction.FFDown && !playerButtons.FFUpPressed())
                        InputCode.SetPlayerDirection(playerButtons, Direction.FFVertCenter);
                    if (direction == Direction.RelativeLeft && !playerButtons.RelativeRightPressed())
                        InputCode.SetPlayerDirection(playerButtons, Direction.RelativeHoriCenter);
                    if (direction == Direction.RelativeRight && !playerButtons.RelativeLeftPressed())
                        InputCode.SetPlayerDirection(playerButtons, Direction.RelativeHoriCenter);
                    if (direction == Direction.RelativeUp && !playerButtons.RelativeDownPressed())
                        InputCode.SetPlayerDirection(playerButtons, Direction.RelativeVertCenter);
                    if (direction == Direction.RelativeDown && !playerButtons.RelativeUpPressed())
                        InputCode.SetPlayerDirection(playerButtons, Direction.RelativeVertCenter);
                }
            }

            if (button.IsLeftTrigger && state.Gamepad.LeftTrigger != 0)
            {
                InputCode.SetPlayerDirection(playerButtons, direction);
            }
            else if (button.IsLeftTrigger && state.Gamepad.LeftTrigger == 0)
            {
                if (direction == Direction.Left || direction == Direction.Right)
                    InputCode.SetPlayerDirection(playerButtons, Direction.HorizontalCenter);
                if (direction == Direction.Up || direction == Direction.Down)
                    InputCode.SetPlayerDirection(playerButtons, Direction.VerticalCenter);
                if (direction == Direction.RelativeLeft || direction == Direction.RelativeRight)
                    InputCode.SetPlayerDirection(playerButtons, Direction.RelativeHoriCenter);
                if (direction == Direction.RelativeUp || direction == Direction.RelativeDown)
                    InputCode.SetPlayerDirection(playerButtons, Direction.RelativeVertCenter);
            }

            if (button.IsRightTrigger && state.Gamepad.RightTrigger != 0)
            {
                InputCode.SetPlayerDirection(playerButtons, direction);
            }
            else if (button.IsRightTrigger && state.Gamepad.RightTrigger == 0)
            {
                if (direction == Direction.Left || direction == Direction.Right)
                    InputCode.SetPlayerDirection(playerButtons, Direction.HorizontalCenter);
                if (direction == Direction.Up || direction == Direction.Down)
                    InputCode.SetPlayerDirection(playerButtons, Direction.VerticalCenter);
                if (direction == Direction.RelativeLeft || direction == Direction.RelativeRight)
                    InputCode.SetPlayerDirection(playerButtons, Direction.RelativeHoriCenter);
                if (direction == Direction.RelativeUp || direction == Direction.RelativeDown)
                    InputCode.SetPlayerDirection(playerButtons, Direction.RelativeVertCenter);
            }
        }

        /// <summary>
        /// Get directional press from button, POV and analog.
        /// </summary>
        /// <param name="playerButtons"></param>
        /// <param name="button"></param>
        /// <param name="state"></param>
        /// <param name="direction"></param>
        public static void GetDirectionPressXinput(PokkenButtons pokkenButtons, XInputButton button, State state, Direction direction, int index)
        {
            if (button?.XInputIndex != index)
                return;

            // Analog Axis, we expect that the both direction are on same axis!!!!
            if (button.IsLeftThumbX || button.IsLeftThumbY || button.IsRightThumbX || button.IsRightThumbY)
            {
                var calcVal = 0;
                if (button.IsLeftThumbY) calcVal = state.Gamepad.LeftThumbY;
                if (button.IsLeftThumbX) calcVal = state.Gamepad.LeftThumbX;
                if (button.IsRightThumbX) calcVal = state.Gamepad.RightThumbX;
                if (button.IsRightThumbY) calcVal = state.Gamepad.RightThumbY;
                if (button.IsAxisMinus)
                {
                    if (calcVal >= 0 + 15000)
                    {
                    }
                    else if (calcVal <= 0 - 15000)
                    {
                        InputCode.SetPlayerDirection(pokkenButtons, direction);
                    }
                    else
                    {
                        if (direction == Direction.Left || direction == Direction.Right)
                            InputCode.SetPlayerDirection(pokkenButtons, Direction.HorizontalCenter);
                        if (direction == Direction.Up || direction == Direction.Down)
                            InputCode.SetPlayerDirection(pokkenButtons, Direction.VerticalCenter);
                    }
                }
                else
                {
                    if (calcVal >= 0 + 15000)
                    {
                        InputCode.SetPlayerDirection(pokkenButtons, direction);
                    }
                    else if (calcVal <= 0 - 15000)
                    {
                    }
                    else
                    {
                        if (direction == Direction.Left || direction == Direction.Right)
                            InputCode.SetPlayerDirection(pokkenButtons, Direction.HorizontalCenter);
                        if (direction == Direction.Up || direction == Direction.Down)
                            InputCode.SetPlayerDirection(pokkenButtons, Direction.VerticalCenter);
                    }
                }
            }

            // Normal button
            if (button.IsButton)
            {
                if ((button.ButtonCode & (short)state.Gamepad.Buttons) != 0)
                {
                    InputCode.SetPlayerDirection(pokkenButtons, direction);
                }
                else
                {
                    if (direction == Direction.Left && !pokkenButtons.RightPressed())
                        InputCode.SetPlayerDirection(pokkenButtons, Direction.HorizontalCenter);
                    if (direction == Direction.Right && !pokkenButtons.LeftPressed())
                        InputCode.SetPlayerDirection(pokkenButtons, Direction.HorizontalCenter);
                    if (direction == Direction.Up && !pokkenButtons.DownPressed())
                        InputCode.SetPlayerDirection(pokkenButtons, Direction.VerticalCenter);
                    if (direction == Direction.Down && !pokkenButtons.UpPressed())
                        InputCode.SetPlayerDirection(pokkenButtons, Direction.VerticalCenter);
                }
            }
        }
    }
}
