using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using SharpDX.DirectInput;
using TeknoParrotUi.Common.InputProfiles.Helpers;
using TeknoParrotUi.Common.Jvs;
using DeviceType = SharpDX.DirectInput.DeviceType;

namespace TeknoParrotUi.Common.InputListening
{
    public class InputListenerDirectInput
    {
        private static GameProfile _gameProfile;
        public static bool KillMe;
        private static readonly DirectInput _diInput = new DirectInput();
        private bool mkdxTest = false;
        private bool changeWmmt5GearUp = false;
        private bool changeWmmt5GearDown = false;
        private bool changeSrcGearUp = false;
        private bool changeSrcGearDown = false;
        private bool KeyboardGasDown = false;
        private bool KeyboardGasUp = false;
        private bool KeyboardBrakeDown = false;
        private bool KeyboardBrakeUp = false;
        private bool KeyboardWheelLeft = false;
        private bool KeyboardWheelCenter = false;
        private bool KeyboardWheelRight = false;
        private bool KeyboardWheelHalfVal = false;
        private bool KeyboardAnalogLeft = false;
        private bool KeyboardAnalogCenter = false;
        private bool KeyboardAnalogRight = false;
        private bool KeyboardAnalogReverseDown = false;
        private bool KeyboardAnalogReverseCenter = false;
        private bool KeyboardAnalogReverseUp = false;
        private bool KeyboardSWThrottleDown = false;
        private bool KeyboardSWThrottleCenter = false;
        private bool KeyboardSWThrottleUp = false;
        private bool KeyboardorButtonAxis = false;
        private bool ReverseYAxis = false;
        private bool ReverseSWThrottleAxis = false;

        /// <summary>
        /// Checks if joystick or gamepad GUID is found.
        /// </summary>
        /// <param name="joystickGuid">Joystick GUID;:</param>
        /// <returns></returns>
        private bool DoesJoystickExist(Guid joystickGuid)
        {
            if (File.Exists("DirectInputOverride.txt"))
            {
                // Don't care about filters when using override!
                return _diInput.GetDevices().Any(x => x.InstanceGuid == joystickGuid);
            }

            return _diInput.GetDevices()
                .Any(
                    x => x.InstanceGuid == joystickGuid && x.Type != DeviceType.Device);
        }

        public void ListenDirectInput(List<JoystickButtons> joystickButtons, GameProfile gameProfile)
        {
            _gameProfile = gameProfile;
            var guids = new List<Guid>();
            changeWmmt5GearUp = false;
            changeWmmt5GearDown = false;
            changeSrcGearDown = false;
            changeSrcGearUp = false;
            mkdxTest = false;

            KeyboardorButtonAxis = gameProfile.ConfigValues.Any(x => x.FieldName == "Use Keyboard/Button For Axis" && x.FieldValue == "1");
            ReverseYAxis = gameProfile.ConfigValues.Any(x => x.FieldName == "Reverse Y Axis" && x.FieldValue == "1");
            ReverseSWThrottleAxis = gameProfile.ConfigValues.Any(x => x.FieldName == "Reverse Throttle Axis" && x.FieldValue == "1");

            //Center values upon startup
            if (_gameProfile.EmulationProfile == EmulationProfile.AfterBurnerClimax)
            {
                InputCode.AnalogBytes[0] = 0x80;
                InputCode.AnalogBytes[2] = 0x80;
            }
            if (_gameProfile.EmulationProfile == EmulationProfile.NamcoMachStorm)
            {
                InputCode.AnalogBytes[2] = 0x80;
                InputCode.AnalogBytes[4] = 0x80;
                InputCode.AnalogBytes[6] = 0x80;
            }
            if (_gameProfile.EmulationProfile == EmulationProfile.StarTrekVoyager)
            {
                InputCode.AnalogBytes[0] = 0x80;
                InputCode.AnalogBytes[2] = 0x80;
                InputCode.AnalogBytes[4] = 0x80;
                InputCode.AnalogBytes[6] = 0x80;
            }
            if (_gameProfile.EmulationProfile == EmulationProfile.TokyoCop)
            {
                InputCode.AnalogBytes[0] = 0x80;
                InputCode.AnalogBytes[6] = 0x80;
            }
            if (_gameProfile.EmulationProfile == EmulationProfile.TaitoTypeXBattleGear || _gameProfile.EmulationProfile == EmulationProfile.VirtuaRLimit)
            {
                JvsHelper.StateView.Write(4, 0x80);
            }
            if (_gameProfile.EmulationProfile == EmulationProfile.ChaseHq2 || _gameProfile.EmulationProfile == EmulationProfile.WackyRaces)
            {
                InputCode.AnalogBytes[4] = 0x80;
            }
            if (_gameProfile.EmulationProfile == EmulationProfile.Daytona3 || _gameProfile.EmulationProfile == EmulationProfile.EuropaRFordRacing || _gameProfile.EmulationProfile == EmulationProfile.EuropaRSegaRally3 || _gameProfile.EmulationProfile == EmulationProfile.FNFDrift || _gameProfile.EmulationProfile == EmulationProfile.GRID ||
                _gameProfile.EmulationProfile == EmulationProfile.GtiClub3 || _gameProfile.EmulationProfile == EmulationProfile.NamcoMkdx || _gameProfile.EmulationProfile == EmulationProfile.NamcoWmmt5 || _gameProfile.EmulationProfile == EmulationProfile.Outrun2SPX || _gameProfile.EmulationProfile == EmulationProfile.RawThrillsFNF || _gameProfile.EmulationProfile == EmulationProfile.RawThrillsFNFH2O ||
                _gameProfile.EmulationProfile == EmulationProfile.SegaInitialD || _gameProfile.EmulationProfile == EmulationProfile.SegaInitialDLindbergh || _gameProfile.EmulationProfile == EmulationProfile.SegaRTuned || _gameProfile.EmulationProfile == EmulationProfile.SegaRacingClassic || _gameProfile.EmulationProfile == EmulationProfile.SegaRtv || _gameProfile.EmulationProfile == EmulationProfile.SegaSonicAllStarsRacing || _gameProfile.EmulationProfile == EmulationProfile.SegaToolsIDZ)
            {
                InputCode.AnalogBytes[0] = 0x80;
            }

            // Find individual guis so we can listen.

            var nonNullButtons = joystickButtons.Where(x => x?.DirectInputButton != null).ToList();

            foreach (var t in nonNullButtons)
            {
                if (guids.All(x => x != t.DirectInputButton?.JoystickGuid))
                {
                    guids.Add(t.DirectInputButton.JoystickGuid);
                }
            }

            // Remove guids that we cannot listen!
            for (int i = 0; i < guids.Count; i++)
            {
                if (!DoesJoystickExist(guids[i]))
                {
                    guids.RemoveAt(i);
                    i = 0;
                }
            }

            // Spawn listeners
            foreach (var guid in guids)
            {
                var joystick = new Joystick(_diInput, guid);
                // Set BufferSize in order to use buffered data.
                joystick.Properties.BufferSize = 512;
                joystick.Acquire();
                var thread = new Thread(() => ListenJoystick(nonNullButtons.Where(x => x.DirectInputButton.JoystickGuid == guid).ToList(), joystick));
                thread.Start();
            }

            while (!KillMe)
                Thread.Sleep(5000);

        }

        private void ListenJoystick(List<JoystickButtons> joystickButtons, Joystick joystick)
        {
            // Poll events from joystick
            try
            {
                Lazydata.Joystick = joystick;
                while (!KillMe)
                {
                    joystick.Poll();
                    foreach (var state in joystick.GetBufferedData())
                    {
                        foreach (var t in joystickButtons)
                        {
                            HandleDirectInput(t, state);
                        }
                    }
                    Thread.Sleep(10);
                }
                joystick.Unacquire();
            }
            catch (Exception)
            {
                // ignored
                joystick.Unacquire();
            }
        }

        private void HandleDirectInput(JoystickButtons joystickButtons, JoystickUpdate state)
        {
            var button = joystickButtons.DirectInputButton;
            switch (joystickButtons.InputMapping)
            {
                case InputMapping.Test:
                {
                    if (InputCode.ButtonMode == EmulationProfile.NamcoMkdx || 
                            InputCode.ButtonMode == EmulationProfile.NamcoMachStorm || 
                            InputCode.ButtonMode == EmulationProfile.NamcoWmmt5)
                    {
                        var result = DigitalHelper.GetButtonPressDirectInput(button, state);
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
                        InputCode.PlayerDigitalButtons[0].Test = DigitalHelper.GetButtonPressDirectInput(button, state);
                    }
                    break;
                }
                case InputMapping.Service1:
                    InputCode.PlayerDigitalButtons[0].Service = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.Service2:
                    InputCode.PlayerDigitalButtons[1].Service = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.Coin1:
                    InputCode.PlayerDigitalButtons[0].Coin = DigitalHelper.GetButtonPressDirectInput(button, state);
                    JvsPackageEmulator.UpdateCoinCount(0);
                    break;
                case InputMapping.Coin2:
                    InputCode.PlayerDigitalButtons[1].Coin = DigitalHelper.GetButtonPressDirectInput(button, state);
                    JvsPackageEmulator.UpdateCoinCount(1);
                    break;
                case InputMapping.P1Button1:
                    if (_gameProfile.EmulationProfile == EmulationProfile.Theatrhythm)
                    {
                        DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[0], button, state, Direction.FFUp);
                    }
                    else
                    {
                        InputCode.PlayerDigitalButtons[0].Button1 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    }
                    break;
                case InputMapping.P1Button2:
                    if (_gameProfile.EmulationProfile == EmulationProfile.Theatrhythm)
                    {
                        DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[0], button, state, Direction.FFDown);
                    }
                    else
                    {
                        InputCode.PlayerDigitalButtons[0].Button2 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    }
                    break;
                case InputMapping.P1Button3:
                    if (_gameProfile.EmulationProfile == EmulationProfile.Theatrhythm)
                    {
                        DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[0], button, state, Direction.FFLeft);
                    }
                    else
                    {
                        InputCode.PlayerDigitalButtons[0].Button3 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    }
                    break;
                case InputMapping.P1Button4:
                    if (_gameProfile.EmulationProfile == EmulationProfile.Theatrhythm)
                    {
                        DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[0], button, state, Direction.FFRight);
                    }
                    else
                    {
                        InputCode.PlayerDigitalButtons[0].Button4 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    }
                    break;
                case InputMapping.P1Button5:
                    InputCode.PlayerDigitalButtons[0].Button5 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.P1Button6:
                    InputCode.PlayerDigitalButtons[0].Button6 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.P1ButtonUp:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[0], button, state, Direction.Up);
                    break;
                case InputMapping.P1ButtonDown:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[0], button, state,
                        Direction.Down);
                    break;
                case InputMapping.P1ButtonLeft:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[0], button, state,
                        Direction.Left);
                    break;
                case InputMapping.P1ButtonRight:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[0], button, state,
                        Direction.Right);
                    break;
                case InputMapping.P1ButtonStart:
                    InputCode.PlayerDigitalButtons[0].Start = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.P2Button1:
                    InputCode.PlayerDigitalButtons[1].Button1 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.P2Button2:
                    InputCode.PlayerDigitalButtons[1].Button2 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.P2Button3:
                    InputCode.PlayerDigitalButtons[1].Button3 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.P2Button4:
                    InputCode.PlayerDigitalButtons[1].Button4 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.P2Button5:
                    InputCode.PlayerDigitalButtons[1].Button5 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.P2Button6:
                    InputCode.PlayerDigitalButtons[1].Button6 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.P2ButtonUp:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[1], button, state, Direction.Up);
                    break;
                case InputMapping.P2ButtonDown:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[1], button, state,
                        Direction.Down);
                    break;
                case InputMapping.P2ButtonLeft:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[1], button, state,
                        Direction.Left);
                    break;
                case InputMapping.P2ButtonRight:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[1], button, state,
                        Direction.Right);
                    break;
                case InputMapping.P2ButtonStart:
                    InputCode.PlayerDigitalButtons[1].Start = DigitalHelper.GetButtonPressDirectInput(button, state);
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
                    if (DigitalHelper.GetButtonPressDirectInput(joystickButtons.DirectInputButton, state) == true)
                    {
                        DigitalHelper.ChangeSrcGear(1);
                    }
                }
                    break;
                case InputMapping.SrcGearChange2:
                {
                    if (DigitalHelper.GetButtonPressDirectInput(joystickButtons.DirectInputButton, state) == true)
                    {
                        DigitalHelper.ChangeSrcGear(2);
                    }
                }
                    break;
                case InputMapping.SrcGearChange3:
                {
                    if (DigitalHelper.GetButtonPressDirectInput(joystickButtons.DirectInputButton, state) == true)
                    {
                        DigitalHelper.ChangeSrcGear(3);
                    }
                }
                    break;
                case InputMapping.SrcGearChange4:
                {
                    if (DigitalHelper.GetButtonPressDirectInput(joystickButtons.DirectInputButton, state) == true)
                    {
                        DigitalHelper.ChangeSrcGear(4);
                    }
                }
                    break;
                case InputMapping.ExtensionOne1:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionOne2:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton2 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionOne3:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton3 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionOne4:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton4 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionOne11:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_1 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionOne12:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_2 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionOne13:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_3 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionOne14:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_4 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionOne15:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_5 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionOne16:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_6 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionOne17:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_7 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionOne18:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_8 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionTwo1:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionTwo2:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton2 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionTwo3:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton3 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionTwo4:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton4 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionTwo11:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_1 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionTwo12:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_2 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionTwo13:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_3 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionTwo14:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_4 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionTwo15:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_5 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionTwo16:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_6 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionTwo17:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_7 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.ExtensionTwo18:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_8 =
                        DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.Analog0Special1:
                case InputMapping.Analog0Special2:
                    InputCode.SetAnalogByte(0, ModifyAnalog(joystickButtons, state));
                    break;
                case InputMapping.Analog2Special1:
                case InputMapping.Analog2Special2:
                    InputCode.SetAnalogByte(2, ModifyAnalog(joystickButtons, state));
                    break;

                // Jvs Board 2

                case InputMapping.JvsTwoService1:
                    InputCode.PlayerDigitalButtons[2].Service = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoService2:
                    InputCode.PlayerDigitalButtons[3].Service = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoCoin1:
                    InputCode.PlayerDigitalButtons[2].Coin = DigitalHelper.GetButtonPressDirectInput(button, state);
                    JvsPackageEmulator.UpdateCoinCount(2);
                    break;
                case InputMapping.JvsTwoCoin2:
                    InputCode.PlayerDigitalButtons[3].Coin = DigitalHelper.GetButtonPressDirectInput(button, state);
                    JvsPackageEmulator.UpdateCoinCount(3);
                    break;
                case InputMapping.JvsTwoP1Button1:
                    InputCode.PlayerDigitalButtons[2].Button1 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoP1Button2:
                    InputCode.PlayerDigitalButtons[2].Button2 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoP1Button3:
                    InputCode.PlayerDigitalButtons[2].Button3 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoP1Button4:
                    InputCode.PlayerDigitalButtons[2].Button4 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoP1Button5:
                    InputCode.PlayerDigitalButtons[2].Button5 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoP1Button6:
                    InputCode.PlayerDigitalButtons[2].Button6 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoP1ButtonUp:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[2], button, state, Direction.Up);
                    break;
                case InputMapping.JvsTwoP1ButtonDown:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[2], button, state, Direction.Down);
                    break;
                case InputMapping.JvsTwoP1ButtonLeft:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[2], button, state, Direction.Left);
                    break;
                case InputMapping.JvsTwoP1ButtonRight:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[2], button, state, Direction.Right);
                    break;
                case InputMapping.JvsTwoP1ButtonStart:
                    InputCode.PlayerDigitalButtons[2].Start = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoP2Button1:
                    InputCode.PlayerDigitalButtons[3].Button1 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoP2Button2:
                    InputCode.PlayerDigitalButtons[3].Button2 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoP2Button3:
                    InputCode.PlayerDigitalButtons[3].Button3 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoP2Button4:
                    InputCode.PlayerDigitalButtons[3].Button4 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoP2Button5:
                    InputCode.PlayerDigitalButtons[3].Button5 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoP2Button6:
                    InputCode.PlayerDigitalButtons[3].Button6 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoP2ButtonUp:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[3], button, state, Direction.Up);
                    break;
                case InputMapping.JvsTwoP2ButtonDown:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[3], button, state, Direction.Down);
                    break;
                case InputMapping.JvsTwoP2ButtonLeft:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[3], button, state, Direction.Left);
                    break;
                case InputMapping.JvsTwoP2ButtonRight:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PlayerDigitalButtons[3], button, state, Direction.Right);
                    break;
                case InputMapping.JvsTwoP2ButtonStart:
                    InputCode.PlayerDigitalButtons[3].Start = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;

                case InputMapping.JvsTwoExtensionOne1:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionOne2:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton2 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionOne3:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton3 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionOne4:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton4 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionOne11:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1_1 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionOne12:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1_2 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionOne13:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1_3 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionOne14:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1_4 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionOne15:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1_5 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionOne16:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1_6 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionOne17:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1_7 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionOne18:
                    InputCode.PlayerDigitalButtons[2].ExtensionButton1_8 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionTwo1:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionTwo2:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton2 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionTwo3:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton3 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionTwo4:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton4 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionTwo11:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1_1 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionTwo12:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1_2 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionTwo13:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1_3 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionTwo14:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1_4 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionTwo15:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1_5 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionTwo16:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1_6 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionTwo17:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1_7 = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.JvsTwoExtensionTwo18:
                    InputCode.PlayerDigitalButtons[3].ExtensionButton1_8 = DigitalHelper.GetButtonPressDirectInput(button, state);
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

                case InputMapping.Wmmt5GearChange1:
                {
                    var pressed = DigitalHelper.GetButtonPressDirectInput(joystickButtons.DirectInputButton, state);
                    if (pressed != null)
                    {
                        DigitalHelper.ChangeWmmt5Gear((bool)pressed ? 1 : 0);
                    }
                }
                    break;
                case InputMapping.Wmmt5GearChange2:
                {
                    var pressed = DigitalHelper.GetButtonPressDirectInput(joystickButtons.DirectInputButton, state);
                    if (pressed != null)
                    {
                        DigitalHelper.ChangeWmmt5Gear((bool)pressed ? 2 : 0);
                    }
                }
                    break;
                case InputMapping.Wmmt5GearChange3:
                {
                    var pressed = DigitalHelper.GetButtonPressDirectInput(joystickButtons.DirectInputButton, state);
                    if (pressed != null)
                    {
                        DigitalHelper.ChangeWmmt5Gear((bool)pressed ? 3 : 0);
                    }
                }
                    break;
                case InputMapping.Wmmt5GearChange4:
                {
                    var pressed = DigitalHelper.GetButtonPressDirectInput(joystickButtons.DirectInputButton, state);
                    if (pressed != null)
                    {
                        DigitalHelper.ChangeWmmt5Gear((bool)pressed ? 4 : 0);
                    }
                }
                    break;
                case InputMapping.Wmmt5GearChange5:
                {
                    var pressed = DigitalHelper.GetButtonPressDirectInput(joystickButtons.DirectInputButton, state);
                    if (pressed != null)
                    {
                        DigitalHelper.ChangeWmmt5Gear((bool)pressed ? 5 : 0);
                    }
                }
                    break;
                case InputMapping.Wmmt5GearChange6:
                {
                    var pressed = DigitalHelper.GetButtonPressDirectInput(joystickButtons.DirectInputButton, state);
                    if (pressed != null)
                    {
                        DigitalHelper.ChangeWmmt5Gear((bool)pressed ? 6 : 0);
                    }
                }
                    break;
                case InputMapping.Wmmt5GearChangeUp:
                {
                    if (DigitalHelper.GetButtonPressDirectInput(joystickButtons.DirectInputButton, state) == true)
                    {
                        if(!changeWmmt5GearUp)
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
                    if (DigitalHelper.GetButtonPressDirectInput(joystickButtons.DirectInputButton, state) == true)
                    {
                        if(!changeWmmt5GearDown)
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
                    if (DigitalHelper.GetButtonPressDirectInput(joystickButtons.DirectInputButton, state) == true)
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
                    if (DigitalHelper.GetButtonPressDirectInput(joystickButtons.DirectInputButton, state) == true)
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
                case InputMapping.PokkenButtonUp:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PokkenInputButtons, button, state, Direction.Up);
                    break;
                case InputMapping.PokkenButtonDown:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PokkenInputButtons, button, state, Direction.Down);
                    break;
                case InputMapping.PokkenButtonLeft:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PokkenInputButtons, button, state, Direction.Left);
                    break;
                case InputMapping.PokkenButtonRight:
                    DigitalHelper.GetDirectionPressDirectInput(InputCode.PokkenInputButtons, button, state, Direction.Right);
                    break;
                case InputMapping.PokkenButtonStart:
                    InputCode.PokkenInputButtons.Start = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.PokkenButtonA:
                    InputCode.PokkenInputButtons.ButtonA = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.PokkenButtonB:
                    InputCode.PokkenInputButtons.ButtonB = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.PokkenButtonX:
                    InputCode.PokkenInputButtons.ButtonX = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.PokkenButtonY:
                    InputCode.PokkenInputButtons.ButtonY = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.PokkenButtonL:
                    InputCode.PokkenInputButtons.ButtonL = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                case InputMapping.PokkenButtonR:
                    InputCode.PokkenInputButtons.ButtonR = DigitalHelper.GetButtonPressDirectInput(button, state);
                    break;
                default:
                    break;
                    //throw new ArgumentOutOfRangeException();
            }
        }

        private byte? ModifyAnalog(JoystickButtons joystickButtons, JoystickUpdate state)
        {
            if (joystickButtons.DirectInputButton == null)
                return null;
            if ((JoystickOffset) joystickButtons.DirectInputButton.Button != state.Offset)
                return null;

            switch (joystickButtons.AnalogType)
            {
                case AnalogType.None:
                    break;
                case AnalogType.AnalogJoystick:
                {
                    var analogPos = JvsHelper.CalculateWheelPos(state.Value);

                    if (_gameProfile.EmulationProfile == EmulationProfile.Mballblitz)
                    {
                        if (joystickButtons.InputMapping == InputMapping.Analog0)
                            JvsHelper.StateView.Write(8, analogPos);
                        if (joystickButtons.InputMapping == InputMapping.Analog2)
                            JvsHelper.StateView.Write(12, analogPos);
                    }

                        if (KeyboardorButtonAxis)
                        {
                            if ((joystickButtons.BindNameDi.Contains("Keyboard")) || (joystickButtons.BindNameDi.Contains("Buttons")))
                            {
                                if (joystickButtons.ButtonName.Contains("Keyboard/Button"))
                                {
                                    if (!KeyboardAnalogRight)
                                    {
                                        KeyboardAnalogRight = true;
                                        if (KeyboardAnalogLeft)
                                        {
                                            analogPos = 0x80;
                                        }
                                        else
                                        {
                                            analogPos = 0xFF;
                                        }
                                    }
                                    else
                                    {
                                        KeyboardAnalogRight = false;
                                        KeyboardAnalogCenter = true;
                                    }
                                }
                                else
                                {
                                    if (!KeyboardAnalogLeft)
                                    {
                                        KeyboardAnalogLeft = true;
                                        if (KeyboardAnalogRight)
                                        {
                                            analogPos = 0x80;
                                        }
                                        else
                                        {
                                            analogPos = 0x00;
                                        }
                                    }
                                    else
                                    {
                                        KeyboardAnalogLeft = false;
                                        KeyboardAnalogCenter = true;
                                    }
                                }

                                if (KeyboardAnalogCenter)
                                {
                                    KeyboardAnalogCenter = false;
                                    if ((KeyboardAnalogLeft) && (!KeyboardAnalogRight))
                                    {
                                        analogPos = 0x00;
                                    }
                                    else if ((KeyboardAnalogRight) && (!KeyboardAnalogLeft))
                                    {
                                        analogPos = 0xFF;
                                    }
                                    else
                                    {
                                        analogPos = 0x80;
                                    }
                                }
                            }
                        }

                        return analogPos;
                }
                case AnalogType.AnalogJoystickReverse:
                {
                        byte analogReversePos = 0;
                        if (ReverseYAxis)
                        {
                            analogReversePos = JvsHelper.CalculateWheelPos(state.Value);
                        }
                        else
                        {
                            analogReversePos = (byte)~JvsHelper.CalculateWheelPos(state.Value);
                        }

                        if (KeyboardorButtonAxis)
                        {
                            if ((joystickButtons.BindNameDi.Contains("Keyboard")) || (joystickButtons.BindNameDi.Contains("Buttons")))
                            {
                                if (joystickButtons.ButtonName.Contains("Keyboard/Button"))
                                {
                                    if (!KeyboardAnalogReverseDown)
                                    {
                                        KeyboardAnalogReverseDown = true;
                                        if (KeyboardAnalogReverseUp)
                                        {
                                            analogReversePos = 0x80;
                                        }
                                        else
                                        {
                                            analogReversePos = 0xFF;
                                        }
                                    }
                                    else
                                    {
                                        KeyboardAnalogReverseDown = false;
                                        KeyboardAnalogReverseCenter = true;
                                    }
                                }
                                else
                                {
                                    if (!KeyboardAnalogReverseUp)
                                    {
                                        KeyboardAnalogReverseUp = true;
                                        if (KeyboardAnalogReverseDown)
                                        {
                                            analogReversePos = 0x80;
                                        }
                                        else
                                        {
                                            analogReversePos = 0x00;
                                        }
                                    }
                                    else
                                    {
                                        KeyboardAnalogReverseUp = false;
                                        KeyboardAnalogReverseCenter = true;
                                    }
                                }

                                if (KeyboardAnalogReverseCenter)
                                {
                                    KeyboardAnalogReverseCenter = false;
                                    if ((KeyboardAnalogReverseUp) && (!KeyboardAnalogReverseDown))
                                    {
                                        analogReversePos = 0x00;
                                    }
                                    else if ((KeyboardAnalogReverseDown) && (!KeyboardAnalogReverseUp))
                                    {
                                        analogReversePos = 0xFF;
                                    }
                                    else
                                    {
                                        analogReversePos = 0x80;
                                    }
                                }
                            }
                        }
                        return analogReversePos;
                }
                case AnalogType.Gas:
                {
                    var gas = HandleGasBrakeForJvs(state.Value, joystickButtons.DirectInputButton?.IsAxisMinus, Lazydata.ParrotData.ReverseAxisGas, Lazydata.ParrotData.FullAxisGas, true);
                    //Console.WriteLine("Gas: " + gas.ToString("X2"));
                    if (InputCode.ButtonMode == EmulationProfile.NamcoWmmt5)
                    {
                        gas /= 3;
                    }
                        if (KeyboardorButtonAxis)
                        {
                            if ((joystickButtons.BindNameDi.Contains("Keyboard")) || (joystickButtons.BindNameDi.Contains("Buttons")))
                            {
                                if (!KeyboardGasDown)
                                {
                                    KeyboardGasDown = true;
                                    gas = 0xFF;
                                }
                                else
                                {
                                    KeyboardGasDown = false;
                                    KeyboardGasUp = true;
                                }

                                if (KeyboardGasUp)
                                {
                                    KeyboardGasUp = false;
                                    gas = 0x00;
                                }
                            }
                        }                  
                    return gas;
                }
                case AnalogType.SWThrottle:
                {
                        byte gas = 0;
                        if (ReverseSWThrottleAxis)
                        {
                            gas = HandleGasBrakeForJvs(state.Value, joystickButtons.DirectInputButton?.IsAxisMinus, false, true, false);
                        }
                        else
                        {
                            gas = HandleGasBrakeForJvs(state.Value, joystickButtons.DirectInputButton?.IsAxisMinus, true, true, false);
                        }

                        if (KeyboardorButtonAxis)
                        {
                            if ((joystickButtons.BindNameDi.Contains("Keyboard")) || (joystickButtons.BindNameDi.Contains("Buttons")))
                            {
                                if (joystickButtons.ButtonName.Contains("Keyboard/Button"))
                                {
                                    if (!KeyboardSWThrottleUp)
                                    {
                                        KeyboardSWThrottleUp = true;
                                        if (KeyboardSWThrottleDown)
                                        {
                                            gas = 0x80;
                                        }
                                        else
                                        {
                                            gas = 0x00;
                                        }
                                    }
                                    else
                                    {
                                        KeyboardSWThrottleUp = false;
                                        KeyboardSWThrottleCenter = true;
                                    }
                                }
                                else
                                {
                                    if (!KeyboardSWThrottleDown)
                                    {
                                        KeyboardSWThrottleDown = true;
                                        if (KeyboardSWThrottleUp)
                                        {
                                            gas = 0x80;
                                        }
                                        else
                                        {
                                            gas = 0xFF;
                                        }
                                    }
                                    else
                                    {
                                        KeyboardSWThrottleDown = false;
                                        KeyboardSWThrottleCenter = true;
                                    }
                                }

                                if (KeyboardSWThrottleCenter)
                                {
                                    KeyboardSWThrottleCenter = false;
                                    if ((KeyboardSWThrottleDown) && (!KeyboardSWThrottleUp))
                                    {
                                        gas = 0xFF;
                                    }
                                    else if ((KeyboardSWThrottleUp) && (!KeyboardSWThrottleDown))
                                    {
                                        gas = 0x00;
                                    }
                                    else
                                    {
                                        gas = 0x80;
                                    }
                                }
                            }
                        }
                        return gas;
                }
                case AnalogType.SWThrottleReverse:
                {
                        byte gas = 0;
                        if (ReverseSWThrottleAxis)
                        {
                            gas = HandleGasBrakeForJvs(state.Value, joystickButtons.DirectInputButton?.IsAxisMinus, true, true, false);                            
                        }
                        else
                        {
                            gas = HandleGasBrakeForJvs(state.Value, joystickButtons.DirectInputButton?.IsAxisMinus, false, true, false);
                        }
                        if (KeyboardorButtonAxis)
                        {
                            if ((joystickButtons.BindNameDi.Contains("Keyboard")) || (joystickButtons.BindNameDi.Contains("Buttons")))
                            {
                                if (joystickButtons.ButtonName.Contains("Keyboard/Button"))
                                {
                                    if (!KeyboardSWThrottleUp)
                                    {
                                        KeyboardSWThrottleUp = true;
                                        if (KeyboardSWThrottleDown)
                                        {
                                            gas = 0x80;
                                        }
                                        else
                                        {
                                            gas = 0xFF;
                                        }
                                    }
                                    else
                                    {
                                        KeyboardSWThrottleUp = false;
                                        KeyboardSWThrottleCenter = true;
                                    }
                                }
                                else
                                {
                                    if (!KeyboardSWThrottleDown)
                                    {
                                        KeyboardSWThrottleDown = true;
                                        if (KeyboardSWThrottleUp)
                                        {
                                            gas = 0x80;
                                        }
                                        else
                                        {
                                            gas = 0x00;
                                        }
                                    }
                                    else
                                    {
                                        KeyboardSWThrottleDown = false;
                                        KeyboardSWThrottleCenter = true;
                                    }
                                }

                                if (KeyboardSWThrottleCenter)
                                {
                                    KeyboardSWThrottleCenter = false;
                                    if ((KeyboardSWThrottleDown) && (!KeyboardSWThrottleUp))
                                    {
                                        gas = 0x00;
                                    }
                                    else if ((KeyboardSWThrottleUp) && (!KeyboardSWThrottleDown))
                                    {
                                        gas = 0xFF;
                                    }
                                    else
                                    {
                                        gas = 0x80;
                                    }
                                }
                            }
                        }
                        return gas;
                }
                case AnalogType.Brake:
                {
                    var brake = HandleGasBrakeForJvs(state.Value, joystickButtons.DirectInputButton?.IsAxisMinus, Lazydata.ParrotData.ReverseAxisGas, Lazydata.ParrotData.FullAxisGas, false);
                    if (InputCode.ButtonMode == EmulationProfile.NamcoWmmt5)
                    {
                        brake /= 3;
                    }
                        //Console.WriteLine("Brake: " + brake.ToString("X2"));
                        if (KeyboardorButtonAxis)
                        {
                            if ((joystickButtons.BindNameDi.Contains("Keyboard")) || (joystickButtons.BindNameDi.Contains("Buttons")))
                            {
                                if (!KeyboardBrakeDown)
                                {
                                    KeyboardBrakeDown = true;
                                    brake = 0xFF;
                                }
                                else
                                {
                                    KeyboardBrakeDown = false;
                                    KeyboardBrakeUp = true;
                                }

                                if (KeyboardBrakeUp)
                                {
                                    KeyboardBrakeUp = false;
                                    brake = 0x00;
                                }
                            }
                        }  
                        return brake;
                }
                case AnalogType.KeyboardWheelHalfValue:
                    {
                        int minValA = 0;
                        int maxValA = 0xFF;
                        switch (_gameProfile.EmulationProfile)
                        {
                            case EmulationProfile.SegaInitialD:
                            case EmulationProfile.SegaInitialDLindbergh:
                                minValA = 0x1F;
                                maxValA = 0xE1;
                                break;
                            case EmulationProfile.SegaSonicAllStarsRacing:
                                minValA = 0x1D;
                                maxValA = 0xED;
                                break;
                        }
                        if (!KeyboardWheelHalfVal)
                        {
                            KeyboardWheelHalfVal = true;
                            if (_gameProfile.EmulationProfile == EmulationProfile.TokyoCop || _gameProfile.EmulationProfile == EmulationProfile.Daytona3 || _gameProfile.EmulationProfile == EmulationProfile.EuropaRFordRacing || _gameProfile.EmulationProfile == EmulationProfile.EuropaRSegaRally3 || _gameProfile.EmulationProfile == EmulationProfile.FNFDrift || _gameProfile.EmulationProfile == EmulationProfile.GRID ||
                    _gameProfile.EmulationProfile == EmulationProfile.GtiClub3 || _gameProfile.EmulationProfile == EmulationProfile.NamcoMkdx || _gameProfile.EmulationProfile == EmulationProfile.NamcoWmmt5 || _gameProfile.EmulationProfile == EmulationProfile.Outrun2SPX || _gameProfile.EmulationProfile == EmulationProfile.RawThrillsFNF || _gameProfile.EmulationProfile == EmulationProfile.RawThrillsFNFH2O ||
                    _gameProfile.EmulationProfile == EmulationProfile.SegaInitialD || _gameProfile.EmulationProfile == EmulationProfile.SegaInitialDLindbergh || _gameProfile.EmulationProfile == EmulationProfile.SegaRTuned || _gameProfile.EmulationProfile == EmulationProfile.SegaRacingClassic || _gameProfile.EmulationProfile == EmulationProfile.SegaRtv || _gameProfile.EmulationProfile == EmulationProfile.SegaSonicAllStarsRacing || _gameProfile.EmulationProfile == EmulationProfile.SegaToolsIDZ)
                            {
                                if (InputCode.AnalogBytes[0] == 0xFF)
                                {
                                    int Half = (byte)maxValA - 0x40;
                                    InputCode.AnalogBytes[0] = (byte)Half;
                                }
                                if (InputCode.AnalogBytes[0] == 0x00)
                                {
                                    int Half = (byte)maxValA + 0x40;
                                    InputCode.AnalogBytes[0] = (byte)Half;
                                }
                            }
                            if (_gameProfile.EmulationProfile == EmulationProfile.ChaseHq2 || _gameProfile.EmulationProfile == EmulationProfile.WackyRaces)
                            {
                                if (InputCode.AnalogBytes[4] == 0xFF)
                                {
                                    int Half = (byte)maxValA - 0x40;
                                    InputCode.AnalogBytes[4] = (byte)Half;
                                }
                                if (InputCode.AnalogBytes[4] == 0x00)
                                {
                                    int Half = (byte)maxValA + 0x40;
                                    InputCode.AnalogBytes[4] = (byte)Half;
                                }
                            }
                            if (_gameProfile.EmulationProfile == EmulationProfile.VirtuaRLimit)
                            {
                                if (InputCode.AnalogBytes[0] == 0xFF)
                                {
                                    int Half = (byte)maxValA - 0x40;
                                    JvsHelper.StateView.Write(4, (byte)Half);
                                }
                                if (InputCode.AnalogBytes[0] == 0x00)
                                {
                                    int Half = (byte)maxValA + 0x40;
                                    JvsHelper.StateView.Write(4, (byte)Half);
                                }
                            }
                            if (_gameProfile.EmulationProfile == EmulationProfile.TaitoTypeXBattleGear)
                            {
                                if (InputCode.AnalogBytes[20] == 0xFF)
                                {
                                    int Half = (byte)maxValA - 0x40;
                                    JvsHelper.StateView.Write(4, (byte)Half);
                                }
                                if (InputCode.AnalogBytes[20] == 0x00)
                                {
                                    int Half = (byte)maxValA + 0x40;
                                    JvsHelper.StateView.Write(4, (byte)Half);
                                }
                            }
                        }
                        else
                        {
                            KeyboardWheelHalfVal = false;
                            if (_gameProfile.EmulationProfile == EmulationProfile.TokyoCop || _gameProfile.EmulationProfile == EmulationProfile.Daytona3 || _gameProfile.EmulationProfile == EmulationProfile.EuropaRFordRacing || _gameProfile.EmulationProfile == EmulationProfile.EuropaRSegaRally3 || _gameProfile.EmulationProfile == EmulationProfile.FNFDrift || _gameProfile.EmulationProfile == EmulationProfile.GRID ||
                    _gameProfile.EmulationProfile == EmulationProfile.GtiClub3 || _gameProfile.EmulationProfile == EmulationProfile.NamcoMkdx || _gameProfile.EmulationProfile == EmulationProfile.NamcoWmmt5 || _gameProfile.EmulationProfile == EmulationProfile.Outrun2SPX || _gameProfile.EmulationProfile == EmulationProfile.RawThrillsFNF || _gameProfile.EmulationProfile == EmulationProfile.RawThrillsFNFH2O ||
                    _gameProfile.EmulationProfile == EmulationProfile.SegaInitialD || _gameProfile.EmulationProfile == EmulationProfile.SegaInitialDLindbergh || _gameProfile.EmulationProfile == EmulationProfile.SegaRTuned || _gameProfile.EmulationProfile == EmulationProfile.SegaRacingClassic || _gameProfile.EmulationProfile == EmulationProfile.SegaRtv || _gameProfile.EmulationProfile == EmulationProfile.SegaSonicAllStarsRacing || _gameProfile.EmulationProfile == EmulationProfile.SegaToolsIDZ)
                            {
                                if (InputCode.AnalogBytes[0] > 0x80)
                                {
                                    InputCode.AnalogBytes[0] = (byte)maxValA;
                                }
                                if (InputCode.AnalogBytes[0] < 0x80)
                                {
                                    InputCode.AnalogBytes[0] = (byte)minValA;
                                }
                            }
                            if (_gameProfile.EmulationProfile == EmulationProfile.ChaseHq2 || _gameProfile.EmulationProfile == EmulationProfile.WackyRaces)
                            {
                                if (InputCode.AnalogBytes[4] > 0x80)
                                {
                                    InputCode.AnalogBytes[4] = (byte)maxValA;
                                }
                                if (InputCode.AnalogBytes[4] < 0x80)
                                {
                                    InputCode.AnalogBytes[4] = (byte)minValA;
                                }
                            }
                            if (_gameProfile.EmulationProfile == EmulationProfile.VirtuaRLimit)
                            {
                                if (InputCode.AnalogBytes[0] > 0x80)
                                {
                                    JvsHelper.StateView.Write(4, (byte)maxValA);
                                }
                                if (InputCode.AnalogBytes[0] < 0x80)
                                {
                                    JvsHelper.StateView.Write(4, (byte)minValA);
                                }
                            }
                            if (_gameProfile.EmulationProfile == EmulationProfile.TaitoTypeXBattleGear)
                            {
                                if (InputCode.AnalogBytes[20] > 0x80) 
                                {
                                    JvsHelper.StateView.Write(4, (byte)maxValA);
                                }
                                if (InputCode.AnalogBytes[20] < 0x80)
                                {
                                    JvsHelper.StateView.Write(4, (byte)minValA);
                                }
                            }
                        }
                        return 0;
                    }
                case AnalogType.Wheel:
                    {
                        int minVal = 0;
                        int maxVal = 0xFF;
                        switch (_gameProfile.EmulationProfile)
                        {
                            case EmulationProfile.SegaInitialD:
                            case EmulationProfile.SegaInitialDLindbergh:
                                minVal = 0x1F;
                                maxVal = 0xE1;
                                break;
                            case EmulationProfile.SegaSonicAllStarsRacing:
                                minVal = 0x1D;
                                maxVal = 0xED;
                                break;
                        }
                        var wheelPos = Lazydata.ParrotData.UseSto0ZDrivingHack
                            ? JvsHelper.CalculateSto0ZWheelPos(state.Value, Lazydata.ParrotData.StoozPercent)
                            : JvsHelper.CalculateWheelPos(state.Value, false, false, minVal, maxVal);

                        if (KeyboardorButtonAxis)
                        {
                            if ((joystickButtons.BindNameDi.Contains("Keyboard")) || (joystickButtons.BindNameDi.Contains("Buttons")))
                            {
                                if (joystickButtons.ButtonName.Contains("Keyboard/Button"))
                                {
                                    if (!KeyboardWheelRight)
                                    {
                                        KeyboardWheelRight = true;
                                        if (KeyboardWheelLeft)
                                        {
                                            wheelPos = 0x80;
                                        }
                                        else
                                        {
                                            if (KeyboardWheelHalfVal)
                                            {
                                                int Half = (byte)maxVal - 0x40;
                                                wheelPos = (byte)Half;
                                            }
                                            else
                                            {
                                                wheelPos = (byte)maxVal;
                                            }   
                                        }  
                                    }
                                    else
                                    { 
                                        KeyboardWheelRight = false;
                                        KeyboardWheelCenter = true;
                                    }
                                }
                                else
                                {
                                    if (!KeyboardWheelLeft)
                                    {
                                        KeyboardWheelLeft = true;
                                        if (KeyboardWheelRight)
                                        {
                                            wheelPos = 0x80;
                                        }
                                        else
                                        {
                                            if (KeyboardWheelHalfVal)
                                            {
                                                int Half = (byte)minVal + 0x40;
                                                wheelPos = (byte)Half;
                                            }
                                            else
                                            {
                                                wheelPos = (byte)minVal;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        KeyboardWheelLeft = false;
                                        KeyboardWheelCenter = true;
                                    }
                                }

                                if (KeyboardWheelCenter)
                                {
                                    KeyboardWheelCenter = false;
                                    if ((KeyboardWheelLeft) && (!KeyboardWheelRight))
                                    {
                                        if (KeyboardWheelHalfVal)
                                        {
                                            int Half = (byte)minVal + 0x40;
                                            wheelPos = (byte)Half;
                                        }
                                        else
                                        {
                                            wheelPos = (byte)minVal;
                                        }
                                    }
                                    else if ((KeyboardWheelRight) && (!KeyboardWheelLeft))
                                    {
                                        if (KeyboardWheelHalfVal)
                                        {
                                            int Half = (byte)maxVal - 0x40;
                                            wheelPos = (byte)Half;
                                        }
                                        else
                                        {
                                            wheelPos = (byte)maxVal;
                                        }
                                    }
                                    else
                                    {
                                        wheelPos = 0x80;
                                    }
                                }
                            }
                        }

                        if (_gameProfile.EmulationProfile == EmulationProfile.TaitoTypeXBattleGear ||
                            _gameProfile.EmulationProfile == EmulationProfile.VirtuaRLimit)
                            JvsHelper.StateView.Write(4, wheelPos);

                        return wheelPos;
                    }
                case AnalogType.Minimum:
                    if (state.Value == 0x80)
                    {
                        return 0x00;
                    }
                    else
                    {
                        return 0x7F;
                    }
                case AnalogType.Maximum:
                    if (state.Value == 0x80)
                    {
                        return 0xFF;
                    }
                    else
                    {
                        return 0x7F;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return null;
        }

        public byte HandleGasBrakeForJvs(int value, bool? isAxisMinus, bool isReverseAxis, bool isFullAxis, bool isGas)
        {
            if (isFullAxis)
            {
                return JvsHelper.CalculateGasPos(value, true, isReverseAxis);
            }

            // Dual Axis
            if (isAxisMinus.HasValue && isAxisMinus.Value)
            {
                if (value <= short.MaxValue)
                {
                    if (isGas)
                    {
                        return JvsHelper.CalculateGasPos(-value + short.MaxValue, false, isReverseAxis);
                    }
                    return JvsHelper.CalculateGasPos(-value + short.MaxValue, false, isReverseAxis);
                }
                return 0;
            }

            if (value <= short.MaxValue)
            {
                return 0;
            }

            return JvsHelper.CalculateGasPos(value + short.MaxValue, false, isReverseAxis);
        }
    }
}
