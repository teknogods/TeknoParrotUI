using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SharpDX.XInput;
using TeknoParrotUi.Common.InputProfiles.Helpers;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.InputListening
{
    public class InputListenerXInput
    {
        private static GameProfile _gameProfile;
        private static bool _useSto0Z;
        private static int _stoozPercent;
        public static bool KillMe;
        private bool mkdxTest = false;
        private bool changeWmmt5GearUp = false;
        private bool changeWmmt5GearDown = false;
        private bool changeSrcGearUp = false;
        private bool changeSrcGearDown = false;
        public void ListenXInput(bool useSto0Z, int stoozPercent, List<JoystickButtons> joystickButtons, UserIndex index, GameProfile gameProfile)
        {
            _useSto0Z = useSto0Z;
            _stoozPercent = stoozPercent;
            _gameProfile = gameProfile;
            if (!joystickButtons.Any())
                return;
            try
            {
                var controller = new Controller(index);
                if (!controller.IsConnected)
                    return;
                changeWmmt5GearUp = false;
                changeWmmt5GearDown = false;
                changeSrcGearDown = false;
                changeSrcGearUp = false;
                mkdxTest = false;
                var previousState = controller.GetState();
                while (!KillMe)
                {
                    var state = controller.GetState();
                    if (previousState.PacketNumber != state.PacketNumber)
                    {
                        for (int i = 0; i < joystickButtons.Count; i++)
                        {
                            HandleXinput(joystickButtons[i], state, (int)index);
                        }
                    }
                    Thread.Sleep(10);
                    previousState = state;
                }
            }
            catch (Exception)
            {

            }
        }

        private void HandleXinput(JoystickButtons joystickButtons, State state, int index)
        {
            var button = joystickButtons.XInputButton;
            switch (joystickButtons.InputMapping)
            {
                case InputMapping.Test:
                    {
                        if (InputCode.ButtonMode == EmulationProfile.NamcoMkdx || 
                            InputCode.ButtonMode == EmulationProfile.NamcoMachStorm || 
                            InputCode.ButtonMode == EmulationProfile.NamcoWmmt5)
                        {
                            var result = DigitalHelper.GetButtonPressXinput(button, state, index);
                            if (result != null && result.Value)
                            {
                                if (mkdxTest)
                                {
                                    InputCode.PlayerDigitalButtons[0].Test = false;
                                    mkdxTest = false;
                                }
                                else
                                {
                                    InputCode.PlayerDigitalButtons[0].Test = true;
                                    mkdxTest = true;
                                }
                            }
                        }
                        else
                        {
                            InputCode.PlayerDigitalButtons[0].Test = DigitalHelper.GetButtonPressXinput(button, state, index);
                        }
                        break;
                    }
                case InputMapping.Service1:
                    InputCode.PlayerDigitalButtons[0].Service = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.Service2:
                    InputCode.PlayerDigitalButtons[1].Service = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.Coin1:
                    InputCode.PlayerDigitalButtons[0].Coin = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.Coin2:
                    InputCode.PlayerDigitalButtons[1].Coin = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.P1Button1:
                    if (_gameProfile.GameName == "Theatrhythm Final Fantasy All-star Carnival")
                    {
                        DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[0], button, state, Direction.FFUp, index);
                    }
                    else
                    {
                        InputCode.PlayerDigitalButtons[0].Button1 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    }
                    break;
                case InputMapping.P1Button2:
                    if (_gameProfile.GameName == "Theatrhythm Final Fantasy All-star Carnival")
                    {
                        DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[0], button, state, Direction.FFDown, index);
                    }
                    else
                    {
                        InputCode.PlayerDigitalButtons[0].Button2 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    }
                    break;
                case InputMapping.P1Button3:
                    if (_gameProfile.GameName == "Theatrhythm Final Fantasy All-star Carnival")
                    {
                        DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[0], button, state, Direction.FFLeft, index);
                    }
                    else
                    {
                        InputCode.PlayerDigitalButtons[0].Button3 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    }
                    break;
                case InputMapping.P1Button4:
                    if (_gameProfile.GameName == "Theatrhythm Final Fantasy All-star Carnival")
                    {
                        DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[0], button, state, Direction.FFRight, index);
                    }
                    else
                    {
                        InputCode.PlayerDigitalButtons[0].Button4 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    }
                    break;
                case InputMapping.P1Button5:
                    InputCode.PlayerDigitalButtons[0].Button5 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.P1Button6:
                    InputCode.PlayerDigitalButtons[0].Button6 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.P1ButtonUp:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[0], button, state, Direction.Up, index);
                    break;
                case InputMapping.P1ButtonDown:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[0], button, state, Direction.Down, index);
                    break;
                case InputMapping.P1ButtonLeft:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[0], button, state, Direction.Left, index);
                    break;
                case InputMapping.P1ButtonRight:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[0], button, state, Direction.Right, index);
                    break;
                case InputMapping.P1ButtonStart:
                    InputCode.PlayerDigitalButtons[0].Start = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.P2Button1:
                    InputCode.PlayerDigitalButtons[1].Button1 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.P2Button2:
                    InputCode.PlayerDigitalButtons[1].Button2 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.P2Button3:
                    InputCode.PlayerDigitalButtons[1].Button3 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.P2Button4:
                    InputCode.PlayerDigitalButtons[1].Button4 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.P2Button5:
                    InputCode.PlayerDigitalButtons[1].Button5 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.P2Button6:
                    InputCode.PlayerDigitalButtons[1].Button6 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.P2ButtonUp:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[1], button, state, Direction.Up, index);
                    break;
                case InputMapping.P2ButtonDown:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[1], button, state, Direction.Down, index);
                    break;
                case InputMapping.P2ButtonLeft:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[1], button, state, Direction.Left, index);
                    break;
                case InputMapping.P2ButtonRight:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[1], button, state, Direction.Right, index);
                    break;
                case InputMapping.P2ButtonStart:
                    InputCode.PlayerDigitalButtons[1].Start = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.Analog0:
                    InputCode.SetAnalogByte(0, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog1:
                    InputCode.SetAnalogByte(1, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog2:
                    InputCode.SetAnalogByte(2, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog3:
                    InputCode.SetAnalogByte(3, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog4:
                    InputCode.SetAnalogByte(4, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog5:
                    InputCode.SetAnalogByte(5, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog6:
                    InputCode.SetAnalogByte(6, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog7:
                    InputCode.SetAnalogByte(7, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog8:
                    InputCode.SetAnalogByte(8, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog9:
                    InputCode.SetAnalogByte(9, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog10:
                    InputCode.SetAnalogByte(10, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog11:
                    InputCode.SetAnalogByte(11, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog12:
                    InputCode.SetAnalogByte(12, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog13:
                    InputCode.SetAnalogByte(13, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog14:
                    InputCode.SetAnalogByte(14, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog15:
                    InputCode.SetAnalogByte(15, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog16:
                    InputCode.SetAnalogByte(16, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog17:
                    InputCode.SetAnalogByte(17, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog18:
                    InputCode.SetAnalogByte(18, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog19:
                    InputCode.SetAnalogByte(19, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog20:
                    InputCode.SetAnalogByte(20, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.SrcGearChange1:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeSrcGear(1);
                        }
                    }
                    break;
                case InputMapping.SrcGearChange2:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeSrcGear(2);
                        }
                    }
                    break;
                case InputMapping.SrcGearChange3:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeSrcGear(3);
                        }
                    }
                    break;
                case InputMapping.SrcGearChange4:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeSrcGear(4);
                        }
                    }
                    break;
                case InputMapping.ExtensionOne1:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionOne2:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton2 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionOne3:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton3 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionOne4:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton4 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionOne11:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_1 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionOne12:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_2 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionOne13:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_3 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionOne14:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_4 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionOne15:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_5 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionOne16:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_6 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionOne17:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_7 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionOne18:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_8 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionTwo1:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionTwo2:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton2 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionTwo3:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton3 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionTwo4:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton4 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionTwo11:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_1 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionTwo12:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_2 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionTwo13:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_3 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionTwo14:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_4 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionTwo15:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_5 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionTwo16:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_6 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionTwo17:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_7 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionTwo18:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_8 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;

                // Jvs Board 2

                case InputMapping.JvsTwoService1:
                    InputCode.PlayerDigitalButtons[2].Service = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoService2:
                    InputCode.PlayerDigitalButtons[3].Service = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoCoin1:
                    InputCode.PlayerDigitalButtons[2].Coin = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoCoin2:
                    InputCode.PlayerDigitalButtons[3].Coin = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoP1Button1:
                    InputCode.PlayerDigitalButtons[2].Button1 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoP1Button2:
                    InputCode.PlayerDigitalButtons[2].Button2 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoP1Button3:
                    InputCode.PlayerDigitalButtons[2].Button3 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoP1Button4:
                    InputCode.PlayerDigitalButtons[2].Button4 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoP1Button5:
                    InputCode.PlayerDigitalButtons[2].Button5 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoP1Button6:
                    InputCode.PlayerDigitalButtons[2].Button6 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoP1ButtonUp:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[2], button, state, Direction.Up, index);
                    break;
                case InputMapping.JvsTwoP1ButtonDown:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[2], button, state, Direction.Down, index);
                    break;
                case InputMapping.JvsTwoP1ButtonLeft:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[2], button, state, Direction.Left, index);
                    break;
                case InputMapping.JvsTwoP1ButtonRight:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[2], button, state, Direction.Right, index);
                    break;
                case InputMapping.JvsTwoP1ButtonStart:
                    InputCode.PlayerDigitalButtons[2].Start = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoP2Button1:
                    InputCode.PlayerDigitalButtons[3].Button1 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoP2Button2:
                    InputCode.PlayerDigitalButtons[3].Button2 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoP2Button3:
                    InputCode.PlayerDigitalButtons[3].Button3 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoP2Button4:
                    InputCode.PlayerDigitalButtons[3].Button4 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoP2Button5:
                    InputCode.PlayerDigitalButtons[3].Button5 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoP2Button6:
                    InputCode.PlayerDigitalButtons[3].Button6 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoP2ButtonUp:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[3], button, state, Direction.Up, index);
                    break;
                case InputMapping.JvsTwoP2ButtonDown:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[3], button, state, Direction.Down, index);
                    break;
                case InputMapping.JvsTwoP2ButtonLeft:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[3], button, state, Direction.Left, index);
                    break;
                case InputMapping.JvsTwoP2ButtonRight:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[3], button, state, Direction.Right, index);
                    break;
                case InputMapping.JvsTwoP2ButtonStart:
                    InputCode.PlayerDigitalButtons[3].Start = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;

                case InputMapping.JvsTwoExtensionOne1:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionOne2:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton2 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionOne3:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton3 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionOne4:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton4 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionOne11:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1_1 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionOne12:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1_2 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionOne13:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1_3 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionOne14:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1_4 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionOne15:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1_5 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionOne16:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1_6 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionOne17:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1_7 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionOne18:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1_8 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionTwo1:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionTwo2:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton2 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionTwo3:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton3 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionTwo4:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton4 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionTwo11:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1_1 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionTwo12:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1_2 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionTwo13:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1_3 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionTwo14:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1_4 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionTwo15:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1_5 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionTwo16:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1_6 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionTwo17:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1_7 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.JvsTwoExtensionTwo18:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1_8 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;

                case InputMapping.JvsTwoAnalog0:
                    InputCode.SetAnalogByte(0, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog1:
                    InputCode.SetAnalogByte(1, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog2:
                    InputCode.SetAnalogByte(2, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog3:
                    InputCode.SetAnalogByte(3, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog4:
                    InputCode.SetAnalogByte(4, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog5:
                    InputCode.SetAnalogByte(5, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog6:
                    InputCode.SetAnalogByte(6, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog7:
                    InputCode.SetAnalogByte(7, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog8:
                    InputCode.SetAnalogByte(8, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog9:
                    InputCode.SetAnalogByte(9, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog10:
                    InputCode.SetAnalogByte(10, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog11:
                    InputCode.SetAnalogByte(11, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog12:
                    InputCode.SetAnalogByte(12, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog13:
                    InputCode.SetAnalogByte(13, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog14:
                    InputCode.SetAnalogByte(14, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog15:
                    InputCode.SetAnalogByte(15, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog16:
                    InputCode.SetAnalogByte(16, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog17:
                    InputCode.SetAnalogByte(17, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog18:
                    InputCode.SetAnalogByte(18, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog19:
                    InputCode.SetAnalogByte(19, ModifyAnalog(joystickButtons, state), true);
                    break;
                case InputMapping.JvsTwoAnalog20:
                    InputCode.SetAnalogByte(20, ModifyAnalog(joystickButtons, state), true);
                    break;


                case InputMapping.Analog0Special1:
                case InputMapping.Analog0Special2:
                    InputCode.SetAnalogByte(0, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog2Special1:
                case InputMapping.Analog2Special2:
                    InputCode.SetAnalogByte(2, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Wmmt5GearChange1:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeWmmt5Gear(1);
                        }
                    }
                    break;
                case InputMapping.Wmmt5GearChange2:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeWmmt5Gear(2);
                        }
                    }
                    break;
                case InputMapping.Wmmt5GearChange3:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeWmmt5Gear(3);
                        }
                    }
                    break;
                case InputMapping.Wmmt5GearChange4:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeWmmt5Gear(4);
                        }
                    }
                    break;
                case InputMapping.Wmmt5GearChange5:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeWmmt5Gear(5);
                        }
                    }
                    break;
                case InputMapping.Wmmt5GearChange6:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeWmmt5Gear(6);
                        }
                    }
                    break;
                case InputMapping.Wmmt5GearChangeUp:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            if (!changeWmmt5GearUp)
                                DigitalHelper.ChangeWmmt5GearUp();
                            changeWmmt5GearUp = true;
                        }
                        else
                        {
                            changeWmmt5GearUp = false;
                        }
                    }
                    break;
                case InputMapping.Wmmt5GearChangeDown:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            if (!changeWmmt5GearDown)
                                DigitalHelper.ChangeWmmt5GearDown();
                            changeWmmt5GearDown = true;
                        }
                        else
                        {
                            changeWmmt5GearDown = false;
                        }
                    }
                    break;
                case InputMapping.SrcGearChangeUp:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            if (!changeSrcGearUp)
                                DigitalHelper.ChangeSrcGearUp();
                            changeSrcGearUp = true;
                        }
                        else
                        {
                            changeSrcGearUp = false;
                        }
                    }
                    break;
                case InputMapping.SrcGearChangeDown:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            if (!changeSrcGearDown)
                                DigitalHelper.ChangeSrcGearDown();
                            changeSrcGearDown = true;
                        }
                        else
                        {
                            changeSrcGearDown = false;
                        }
                    }
                    break;
                case InputMapping.InitialDRealGear1:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeIdGear(1);
                        }
                    }
                    break;
                case InputMapping.InitialDRealGear2:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeIdGear(2);
                        }
                    }
                    break;
                case InputMapping.InitialDRealGear3:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeIdGear(3);
                        }
                    }
                    break;
                case InputMapping.InitialDRealGear4:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeIdGear(4);
                        }
                    }
                    break;
                case InputMapping.InitialDRealGear5:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeIdGear(5);
                        }
                    }
                    break;
                case InputMapping.InitialDRealGear6:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeIdGear(6);
                        }
                    }
                    break;
                case InputMapping.PokkenButtonUp:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PokkenInputButtons, button, state, Direction.Up, index);
                    break;
                case InputMapping.PokkenButtonDown:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PokkenInputButtons, button, state, Direction.Down, index);
                    break;
                case InputMapping.PokkenButtonLeft:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PokkenInputButtons, button, state, Direction.Left, index);
                    break;
                case InputMapping.PokkenButtonRight:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PokkenInputButtons, button, state, Direction.Right, index);
                    break;
                case InputMapping.PokkenButtonStart:
                    InputCode.PokkenInputButtons.Start = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.PokkenButtonA:
                    InputCode.PokkenInputButtons.ButtonA = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.PokkenButtonB:
                    InputCode.PokkenInputButtons.ButtonB = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.PokkenButtonX:
                    InputCode.PokkenInputButtons.ButtonX = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.PokkenButtonY:
                    InputCode.PokkenInputButtons.ButtonY = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.PokkenButtonL:
                    InputCode.PokkenInputButtons.ButtonL = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.PokkenButtonR:
                    InputCode.PokkenInputButtons.ButtonR = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                default:
                    break;
                    //throw new ArgumentOutOfRangeException();
            }
        }

        private byte? ModifyAnalog(JoystickButtons joystickButtons, State state)
        {
            switch (joystickButtons.AnalogType)
            {
                case AnalogType.None:
                    break;
                case AnalogType.AnalogJoystick:
                {
                    var analogPos = AnalogHelper.CalculateWheelPosXinput(joystickButtons.XInputButton, state, false, 0, _gameProfile);
                    if (_gameProfile.EmulationProfile == EmulationProfile.Mballblitz)
                    {
                        if (joystickButtons.InputMapping == InputMapping.Analog0)
                            JvsHelper.StateView.Write(8, analogPos);
                        if(joystickButtons.InputMapping == InputMapping.Analog2)
                            JvsHelper.StateView.Write(12, analogPos);
                    }
                    return analogPos;
                }
                case AnalogType.AnalogJoystickReverse:
                {
                    return (byte)~AnalogHelper.CalculateWheelPosXinput(joystickButtons.XInputButton, state, false, 0, _gameProfile);
                }
                case AnalogType.Gas:
                case AnalogType.Brake:
                    return AnalogHelper.CalculateAxisOrTriggerGasBrakeXinput(joystickButtons.XInputButton, state);
                case AnalogType.Wheel:
                {
                    var wheelPos = AnalogHelper.CalculateWheelPosXinput(joystickButtons.XInputButton, state, _useSto0Z, _stoozPercent, _gameProfile);
                    if (_gameProfile.EmulationProfile == EmulationProfile.TaitoTypeXBattleGear || _gameProfile.EmulationProfile == EmulationProfile.VirtuaRLimit)
                            JvsHelper.StateView.Write(4, wheelPos);

                    return wheelPos;
                }
            }
            return null;
        }
    }
}
