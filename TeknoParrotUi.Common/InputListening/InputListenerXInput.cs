using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using SharpDX.XInput;
using System.Timers;
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
        public static bool DisableTestButton;
        private static short _minX;
        private static short _maxX;
        private static short _minY;
        private static short _maxY;
        private static double _DivideX;
        private static double _DivideY;
        private static bool GunGame = false;
        private static bool _invertedMouseAxis = false;
        private static bool mkdxTest = false;
        private static bool changeWmmt5GearUp = false;
        private static bool changeWmmt5GearDown = false;
        private static bool changeFnfGearUp = false;
        private static bool changeFnfGearDown = false;
        private static bool changeSrcGearUp = false;
        private static bool changeSrcGearDown = false;
        private static bool changeIDZGearUp = false;
        private static bool changeIDZGearDown = false;
        private static bool ReverseYAxis = false;
        private static bool ReverseSWThrottleAxis = false;
        private static bool StartButtonInitialD = false;
        private static bool TestButtonInitialD = false;
        private static bool RelativeInput = false;
        private static bool RelativeTimer = false;
        private static int RelativeAnalogXValue1p;
        private static int RelativeAnalogYValue1p;
        private static int RelativeAnalogXValue2p;
        private static int RelativeAnalogYValue2p;
        private static int RelativeAnalogXValue3p;
        private static int RelativeAnalogYValue3p;
        private static int RelativeAnalogXValue4p;
        private static int RelativeAnalogYValue4p;
        private static int AnalogXByteValue1p = -1;
        private static int AnalogYByteValue1p = -1;
        private static int AnalogXByteValue2p = -1;
        private static int AnalogYByteValue2p = -1;
        private static int AnalogXByteValue3p = -1;
        private static int AnalogYByteValue3p = -1;
        private static int AnalogXByteValue4p = -1;
        private static int AnalogYByteValue4p = -1;
        private static int RelativeP1Sensitivity;
        private static int RelativeP2Sensitivity;
        private static int RelativeP3Sensitivity;
        private static int RelativeP4Sensitivity;
        private static System.Timers.Timer Relativetimer = new System.Timers.Timer(32);

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
                changeFnfGearUp = false;
                changeFnfGearDown = false;
                changeSrcGearDown = false;
                changeSrcGearUp = false;
                changeIDZGearUp = false;
                changeIDZGearDown = false;
                mkdxTest = false;

                ReverseYAxis = gameProfile.ConfigValues.Any(x => x.FieldName == "Reverse Y Axis" && x.FieldValue == "1");
                ReverseSWThrottleAxis = gameProfile.ConfigValues.Any(x => x.FieldName == "Reverse Throttle Axis" && x.FieldValue == "1");
                RelativeInput = gameProfile.ConfigValues.Any(x => x.FieldName == "Use Relative Input" && x.FieldValue == "1");
                GunGame = gameProfile.GunGame;

                //Center values upon startup
                if (_gameProfile.EmulationProfile == EmulationProfile.AfterBurnerClimax || _gameProfile.EmulationProfile == EmulationProfile.BlazingAngels)
                {
                    InputCode.AnalogBytes[0] = 0x80;
                    InputCode.AnalogBytes[2] = 0x80;
                    InputCode.AnalogBytes[4] = 0x80;
                }

                if (_gameProfile.EmulationProfile == EmulationProfile.NamcoMachStorm)
                {
                    InputCode.AnalogBytes[2] = 0x80;
                    InputCode.AnalogBytes[4] = 0x80;
                    InputCode.AnalogBytes[6] = 0x80;
                }

                if (_gameProfile.EmulationProfile == EmulationProfile.TokyoCop)
                {
                    InputCode.AnalogBytes[0] = 0x80;
                    InputCode.AnalogBytes[6] = 0x80;
                }

                if (_gameProfile.EmulationProfile == EmulationProfile.NamcoGundamPod)
                {
                    InputCode.AnalogBytes[0] = 0x80;
                    InputCode.AnalogBytes[2] = 0x80;
                    InputCode.AnalogBytes[4] = 0x80;
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
                    _gameProfile.EmulationProfile == EmulationProfile.GtiClub3 || _gameProfile.EmulationProfile == EmulationProfile.NamcoMkdx || _gameProfile.EmulationProfile == EmulationProfile.NamcoMkdxUsa || _gameProfile.EmulationProfile == EmulationProfile.NamcoWmmt5 || _gameProfile.EmulationProfile == EmulationProfile.Outrun2SPX || _gameProfile.EmulationProfile == EmulationProfile.RawThrillsFNF || _gameProfile.EmulationProfile == EmulationProfile.RawThrillsFNFH2O ||
                    _gameProfile.EmulationProfile == EmulationProfile.SegaInitialD || _gameProfile.EmulationProfile == EmulationProfile.SegaInitialDLindbergh || _gameProfile.EmulationProfile == EmulationProfile.SegaRTuned || _gameProfile.EmulationProfile == EmulationProfile.SegaRacingClassic || _gameProfile.EmulationProfile == EmulationProfile.SegaRtv || _gameProfile.EmulationProfile == EmulationProfile.SegaSonicAllStarsRacing ||
                    _gameProfile.EmulationProfile == EmulationProfile.SegaToolsIDZ || _gameProfile.EmulationProfile == EmulationProfile.NamcoWmmt3 || _gameProfile.EmulationProfile == EmulationProfile.IDZ)
                {
                    InputCode.AnalogBytes[0] = 0x80;
                }

                if (_gameProfile.EmulationProfile == EmulationProfile.HummerExtreme)
                {
                    InputCode.AnalogBytes[0] = 0x62;
                    InputCode.AnalogBytes[2] = 0x20;
                    InputCode.AnalogBytes[4] = 0x20;
                }

                if (_gameProfile.EmulationProfile == EmulationProfile.HotWheels)
                {
                    InputCode.AnalogBytes[0] = 0x7F;
                    InputCode.AnalogBytes[4] = 0x7F;
                    InputCode.AnalogBytes[8] = 0x7F;
                    InputCode.AnalogBytes[12] = 0x7F;
                    InputCode.AnalogBytes[16] = 0x7F;
                    InputCode.AnalogBytes[20] = 0x7F;
                }

                if (GunGame)
                {
                    _minX = gameProfile.xAxisMin;
                    _maxX = gameProfile.xAxisMax;
                    _minY = gameProfile.yAxisMin;
                    _maxY = gameProfile.yAxisMax;
                    _invertedMouseAxis = gameProfile.InvertedMouseAxis;

                    _DivideX = 255.0 / (_maxX - _minX);
                    _DivideY = 255.0 / (_maxY - _minY);

                    if (_gameProfile.EmulationProfile == EmulationProfile.LuigisMansion || (!_invertedMouseAxis))
                    {
                        InputCode.AnalogBytes[0] = (byte)((_maxY + _minY) / 2.0);
                        InputCode.AnalogBytes[2] = (byte)((_maxX + _minX) / 2.0);
                        InputCode.AnalogBytes[4] = (byte)((_maxY + _minY) / 2.0);
                        InputCode.AnalogBytes[6] = (byte)((_maxX + _minX) / 2.0);
                        InputCode.AnalogBytes[8] = (byte)((_maxY + _minY) / 2.0);
                        InputCode.AnalogBytes[10] = (byte)((_maxX + _minX) / 2.0);
                        InputCode.AnalogBytes[12] = (byte)((_maxY + _minY) / 2.0);
                        InputCode.AnalogBytes[14] = (byte)((_maxX + _minX) / 2.0);

                        if (RelativeInput)
                        {
                            AnalogXByteValue1p = 2;
                            AnalogYByteValue1p = 0;
                            AnalogXByteValue2p = 6;
                            AnalogYByteValue2p = 4;
                            AnalogXByteValue3p = 10;
                            AnalogYByteValue3p = 8;
                            AnalogXByteValue4p = 14;
                            AnalogYByteValue4p = 12;
                        }
                    }
                    else
                    {
                        InputCode.AnalogBytes[0] = (byte)((_maxX + _minX) / 2.0);
                        InputCode.AnalogBytes[2] = (byte)((_maxY + _minY) / 2.0);
                        InputCode.AnalogBytes[4] = (byte)((_maxX + _minX) / 2.0);
                        InputCode.AnalogBytes[6] = (byte)((_maxY + _minY) / 2.0);
                        InputCode.AnalogBytes[8] = (byte)((_maxY + _minY) / 2.0);
                        InputCode.AnalogBytes[10] = (byte)((_maxX + _minX) / 2.0);
                        InputCode.AnalogBytes[12] = (byte)((_maxY + _minY) / 2.0);
                        InputCode.AnalogBytes[14] = (byte)((_maxX + _minX) / 2.0);

                        if (RelativeInput)
                        {
                            AnalogXByteValue1p = 0;
                            AnalogYByteValue1p = 2;
                            AnalogXByteValue2p = 4;
                            AnalogYByteValue2p = 6;
                            AnalogXByteValue3p = 8;
                            AnalogYByteValue3p = 10;
                            AnalogXByteValue4p = 12;
                            AnalogYByteValue4p = 14;
                        }
                    }

                    if (RelativeInput)
                    {
                        RelativeAnalogXValue1p = (byte)((_maxX + _minX) / 2.0);
                        RelativeAnalogYValue1p = (byte)((_maxY + _minY) / 2.0);
                        RelativeAnalogXValue2p = (byte)((_maxX + _minX) / 2.0);
                        RelativeAnalogYValue2p = (byte)((_maxY + _minY) / 2.0);
                        RelativeAnalogXValue3p = (byte)((_maxX + _minX) / 2.0);
                        RelativeAnalogYValue3p = (byte)((_maxY + _minY) / 2.0);
                        RelativeAnalogXValue4p = (byte)((_maxX + _minX) / 2.0);
                        RelativeAnalogYValue4p = (byte)((_maxY + _minY) / 2.0);


                        var P1SensitivityA = gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "Player 1 Relative Sensitivity");
                        if (P1SensitivityA != null)
                        {
                            RelativeP1Sensitivity = System.Convert.ToInt32(P1SensitivityA.FieldValue);
                        }

                        var P2SensitivityA = gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "Player 2 Relative Sensitivity");
                        if (P2SensitivityA != null)
                        {
                            RelativeP2Sensitivity = System.Convert.ToInt32(P2SensitivityA.FieldValue);
                        }

                        var P3SensitivityA = gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "Player 3 Relative Sensitivity");
                        if (P3SensitivityA != null)
                        {
                            RelativeP3Sensitivity = System.Convert.ToInt32(P3SensitivityA.FieldValue);
                        }

                        var P4SensitivityA = gameProfile.ConfigValues.FirstOrDefault(x => x.FieldName == "Player 4 Relative Sensitivity");
                        if (P4SensitivityA != null)
                        {
                            RelativeP4Sensitivity = System.Convert.ToInt32(P4SensitivityA.FieldValue);
                        }

                        if (!RelativeTimer)
                        {
                            RelativeTimer = true;
                            Relativetimer.Elapsed += ListenRelativeAnalog;
                        }

                        Relativetimer.Start();
                    }
                }

                var previousState = controller.GetState();
                while (!KillMe)
                {
                    var state = controller.GetState();
                    if (previousState.PacketNumber != state.PacketNumber)
                    {
                        for (int i = 0; i < joystickButtons.Count; i++)
                        {
                            HandleXinput(joystickButtons[i], state, previousState, (int)index);
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

        private void ListenRelativeAnalog(object sender, ElapsedEventArgs e)
        {
            // P1
            if (AnalogXByteValue1p >= 0)
            {
                if (InputCode.PlayerDigitalButtons[0].RelativeLeftPressed())
                {
                    RelativeAnalogXValue1p = (byte)Math.Max(_minX, RelativeAnalogXValue1p - RelativeP1Sensitivity);
                }
                else if (InputCode.PlayerDigitalButtons[0].RelativeRightPressed())
                {
                    RelativeAnalogXValue1p = (byte)Math.Min(_maxX, RelativeAnalogXValue1p + RelativeP1Sensitivity);
                }

                if (_invertedMouseAxis)
                {
                    InputCode.AnalogBytes[AnalogXByteValue1p] = (byte)RelativeAnalogXValue1p;
                }
                else
                {
                    InputCode.AnalogBytes[AnalogXByteValue1p] = (byte)~RelativeAnalogXValue1p;
                }
            }

            if (AnalogYByteValue1p >= 0)
            {
                if (InputCode.PlayerDigitalButtons[0].RelativeUpPressed())
                {
                    RelativeAnalogYValue1p = (byte)Math.Max(_minY, RelativeAnalogYValue1p - RelativeP1Sensitivity);
                }
                else if (InputCode.PlayerDigitalButtons[0].RelativeDownPressed())
                {
                    RelativeAnalogYValue1p = (byte)Math.Min(_maxY, RelativeAnalogYValue1p + RelativeP1Sensitivity);
                }

                if (_invertedMouseAxis)
                {
                    InputCode.AnalogBytes[AnalogYByteValue1p] = (byte)RelativeAnalogYValue1p;
                }
                else
                {
                    InputCode.AnalogBytes[AnalogYByteValue1p] = (byte)~RelativeAnalogYValue1p;
                }
            }

            // P2
            if (AnalogXByteValue2p >= 0)
            {
                if (InputCode.PlayerDigitalButtons[1].RelativeLeftPressed())
                {
                    RelativeAnalogXValue2p = (byte)Math.Max(_minX, RelativeAnalogXValue2p - RelativeP2Sensitivity);
                }
                else if (InputCode.PlayerDigitalButtons[1].RelativeRightPressed())
                {
                    RelativeAnalogXValue2p = (byte)Math.Min(_maxX, RelativeAnalogXValue2p + RelativeP2Sensitivity);
                }

                if (_invertedMouseAxis)
                {
                    InputCode.AnalogBytes[AnalogXByteValue2p] = (byte)RelativeAnalogXValue2p;
                }
                else
                {
                    InputCode.AnalogBytes[AnalogXByteValue2p] = (byte)~RelativeAnalogXValue2p;
                }
            }

            if (AnalogYByteValue2p >= 0)
            {
                if (InputCode.PlayerDigitalButtons[1].RelativeUpPressed())
                {
                    RelativeAnalogYValue2p = (byte)Math.Max(_minY, RelativeAnalogYValue2p - RelativeP2Sensitivity);
                }
                else if (InputCode.PlayerDigitalButtons[1].RelativeDownPressed())
                {
                    RelativeAnalogYValue2p = (byte)Math.Min(_maxY, RelativeAnalogYValue2p + RelativeP2Sensitivity);
                }

                if (_invertedMouseAxis)
                {
                    InputCode.AnalogBytes[AnalogYByteValue2p] = (byte)RelativeAnalogYValue2p;
                }
                else
                {
                    InputCode.AnalogBytes[AnalogYByteValue2p] = (byte)~RelativeAnalogYValue2p;
                }
            }

            // P3
            if (AnalogXByteValue3p >= 0)
            {
                if (InputCode.PlayerDigitalButtons[2].RelativeLeftPressed())
                {
                    RelativeAnalogXValue3p = (byte)Math.Max(_minX, RelativeAnalogXValue3p - RelativeP3Sensitivity);
                }
                else if (InputCode.PlayerDigitalButtons[2].RelativeRightPressed())
                {
                    RelativeAnalogXValue3p = (byte)Math.Min(_maxX, RelativeAnalogXValue3p + RelativeP3Sensitivity);
                }

                if (_invertedMouseAxis)
                {
                    InputCode.AnalogBytes[AnalogXByteValue3p] = (byte)RelativeAnalogXValue3p;
                }
                else
                {
                    InputCode.AnalogBytes[AnalogXByteValue3p] = (byte)~RelativeAnalogXValue3p;
                }
            }

            if (AnalogYByteValue3p >= 0)
            {
                if (InputCode.PlayerDigitalButtons[2].RelativeUpPressed())
                {
                    RelativeAnalogYValue3p = (byte)Math.Max(_minY, RelativeAnalogYValue3p - RelativeP3Sensitivity);
                }
                else if (InputCode.PlayerDigitalButtons[2].RelativeDownPressed())
                {
                    RelativeAnalogYValue3p = (byte)Math.Min(_maxY, RelativeAnalogYValue3p + RelativeP3Sensitivity);
                }

                if (_invertedMouseAxis)
                {
                    InputCode.AnalogBytes[AnalogYByteValue3p] = (byte)RelativeAnalogYValue3p;
                }
                else
                {
                    InputCode.AnalogBytes[AnalogYByteValue3p] = (byte)~RelativeAnalogYValue3p;
                }
            }

            // P4
            if (AnalogXByteValue4p >= 0)
            {
                if (InputCode.PlayerDigitalButtons[3].RelativeLeftPressed())
                {
                    RelativeAnalogXValue4p = (byte)Math.Max(_minX, RelativeAnalogXValue4p - RelativeP4Sensitivity);
                }
                else if (InputCode.PlayerDigitalButtons[3].RelativeRightPressed())
                {
                    RelativeAnalogXValue4p = (byte)Math.Min(_maxX, RelativeAnalogXValue4p + RelativeP4Sensitivity);
                }

                if (_invertedMouseAxis)
                {
                    InputCode.AnalogBytes[AnalogXByteValue4p] = (byte)RelativeAnalogXValue4p;
                }
                else
                {
                    InputCode.AnalogBytes[AnalogXByteValue4p] = (byte)~RelativeAnalogXValue4p;
                }
            }

            if (AnalogYByteValue4p >= 0)
            {
                if (InputCode.PlayerDigitalButtons[3].RelativeUpPressed())
                {
                    RelativeAnalogYValue4p = (byte)Math.Max(_minY, RelativeAnalogYValue4p - RelativeP4Sensitivity);
                }
                else if (InputCode.PlayerDigitalButtons[3].RelativeDownPressed())
                {
                    RelativeAnalogYValue4p = (byte)Math.Min(_maxY, RelativeAnalogYValue4p + RelativeP4Sensitivity);
                }

                if (_invertedMouseAxis)
                {
                    InputCode.AnalogBytes[AnalogYByteValue4p] = (byte)RelativeAnalogYValue4p;
                }
                else
                {
                    InputCode.AnalogBytes[AnalogYByteValue4p] = (byte)~RelativeAnalogYValue4p;
                }
            }

            if (KillMe)
            {
                Relativetimer.Enabled = false;
            }
        }

        private void HandleXinput(JoystickButtons joystickButtons, State state, State previousState, int index)
        {
            var button = joystickButtons.XInputButton;
            switch (joystickButtons.InputMapping)
            {
                case InputMapping.Test:
                    {
                        if (DisableTestButton)
                        {
                            if (_gameProfile.EmulationProfile == EmulationProfile.SegaInitialD || _gameProfile.EmulationProfile == EmulationProfile.SegaInitialDLindbergh)
                            {
                                if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                                {
                                    if (!TestButtonInitialD)
                                    {
                                        TestButtonInitialD = true;
                                    }
                                }
                                else
                                {
                                    if (TestButtonInitialD)
                                    {
                                        TestButtonInitialD = false;
                                    }
                                }
                                if ((StartButtonInitialD) && (TestButtonInitialD))
                                {
                                    InputCode.PlayerDigitalButtons[0].Test = true;
                                }
                                else
                                {
                                    InputCode.PlayerDigitalButtons[0].Test = false;
                                }
                            }
                            break;
                        }

                        if (InputCode.ButtonMode == EmulationProfile.NamcoMkdx ||
                            InputCode.ButtonMode == EmulationProfile.NamcoMkdxUsa ||
                            InputCode.ButtonMode == EmulationProfile.NamcoMachStorm ||
                            InputCode.ButtonMode == EmulationProfile.NamcoWmmt5 ||
                            InputCode.ButtonMode == EmulationProfile.DeadHeatRiders ||
                            InputCode.ButtonMode == EmulationProfile.NamcoGundamPod)
                        {
                            var result = DigitalHelper.GetButtonPressXinput(button, state, index);
                            var prevResult = DigitalHelper.GetButtonPressXinput(button, previousState, index);
                            if ((result != null && result.Value) && ((prevResult == null) || (!prevResult.Value)))
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
                    JvsPackageEmulator.UpdateCoinCount(0);
                    if (_gameProfile.EmulationProfile == EmulationProfile.EADP)
                    {
                        if (InputCode.PlayerDigitalButtons[0].Coin.Value)
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_7 = true;
                        else
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_7 = false;
                    }
                    break;
                case InputMapping.Coin2:
                    InputCode.PlayerDigitalButtons[1].Coin = DigitalHelper.GetButtonPressXinput(button, state, index);
                    JvsPackageEmulator.UpdateCoinCount(1);
                    break;
                case InputMapping.P1Button1:
                    if (_gameProfile.EmulationProfile == EmulationProfile.Theatrhythm || _gameProfile.EmulationProfile == EmulationProfile.SegaOlympic2016)
                    {
                        DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[0], button, state, Direction.FFUp, index);
                    }
                    else
                    {
                        InputCode.PlayerDigitalButtons[0].Button1 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    }
                    break;
                case InputMapping.P1Button2:
                    if (_gameProfile.EmulationProfile == EmulationProfile.Theatrhythm)
                    {
                        DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[0], button, state, Direction.FFDown, index);
                    }
                    else
                    {
                        InputCode.PlayerDigitalButtons[0].Button2 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    }
                    break;
                case InputMapping.P1Button3:
                    if (_gameProfile.EmulationProfile == EmulationProfile.Theatrhythm || _gameProfile.EmulationProfile == EmulationProfile.SegaOlympic2016)
                    {
                        DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[0], button, state, Direction.FFLeft, index);
                    }
                    else
                    {
                        InputCode.PlayerDigitalButtons[0].Button3 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    }
                    break;
                case InputMapping.P1Button4:
                    if (_gameProfile.EmulationProfile == EmulationProfile.Theatrhythm)
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
                case InputMapping.P1RelativeUp:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[0], button, state, Direction.RelativeUp, index);
                    break;
                case InputMapping.P1RelativeDown:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[0], button, state, Direction.RelativeDown, index);
                    break;
                case InputMapping.P1RelativeLeft:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[0], button, state, Direction.RelativeLeft, index);
                    break;
                case InputMapping.P1RelativeRight:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[0], button, state, Direction.RelativeRight, index);
                    break;
                case InputMapping.P1ButtonStart:
                    if (DisableTestButton)
                    {
                        if (_gameProfile.EmulationProfile == EmulationProfile.SegaInitialD || _gameProfile.EmulationProfile == EmulationProfile.SegaInitialDLindbergh)
                        {
                            if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                            {
                                if (!StartButtonInitialD)
                                {
                                    StartButtonInitialD = true;
                                }
                            }
                            else
                            {
                                if (StartButtonInitialD)
                                {
                                    StartButtonInitialD = false;
                                }
                            }
                        }
                    }
                    InputCode.PlayerDigitalButtons[0].Start = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.P2Button1:
                    if (_gameProfile.EmulationProfile == EmulationProfile.SegaOlympic2016)
                    {
                        DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[1], button, state, Direction.FFUp, index);
                    }
                    else
                    {
                        InputCode.PlayerDigitalButtons[1].Button1 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    }
                    break;
                case InputMapping.P2Button2:
                    InputCode.PlayerDigitalButtons[1].Button2 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.P2Button3:
                    if (_gameProfile.EmulationProfile == EmulationProfile.SegaOlympic2016)
                    {
                        DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[1], button, state, Direction.FFLeft, index);
                    }
                    else
                    {
                        InputCode.PlayerDigitalButtons[1].Button3 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    }
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
                case InputMapping.P2RelativeUp:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[1], button, state, Direction.RelativeUp, index);
                    break;
                case InputMapping.P2RelativeDown:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[1], button, state, Direction.RelativeDown, index);
                    break;
                case InputMapping.P2RelativeLeft:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[1], button, state, Direction.RelativeLeft, index);
                    break;
                case InputMapping.P2RelativeRight:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[1], button, state, Direction.RelativeRight, index);
                    break;
                case InputMapping.P2ButtonStart:
                    InputCode.PlayerDigitalButtons[1].Start = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.Analog0:
                    InputCode.SetAnalogByte(0, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog1:
                    InputCode.SetAnalogByte(1, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog2:
                    InputCode.SetAnalogByte(2, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog3:
                    InputCode.SetAnalogByte(3, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog4:
                    InputCode.SetAnalogByte(4, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog5:
                    InputCode.SetAnalogByte(5, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog6:
                    InputCode.SetAnalogByte(6, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog7:
                    InputCode.SetAnalogByte(7, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog8:
                    InputCode.SetAnalogByte(8, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog9:
                    InputCode.SetAnalogByte(9, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog10:
                    InputCode.SetAnalogByte(10, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog11:
                    InputCode.SetAnalogByte(11, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog12:
                    InputCode.SetAnalogByte(12, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog13:
                    InputCode.SetAnalogByte(13, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog14:
                    InputCode.SetAnalogByte(14, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog15:
                    InputCode.SetAnalogByte(15, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog16:
                    InputCode.SetAnalogByte(16, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog17:
                    InputCode.SetAnalogByte(17, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog18:
                    InputCode.SetAnalogByte(18, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog19:
                    InputCode.SetAnalogByte(19, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog20:
                    InputCode.SetAnalogByte(20, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog22:
                    InputCode.SetAnalogByte(22, ModifyAnalog(joystickButtons, state, index));
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
                    {
                        var result = DigitalHelper.GetButtonPressXinput(button, state, index);
                        if (_gameProfile.EmulationProfile == EmulationProfile.NamcoGundamPod)
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1 = !result;
                        }
                        else
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1 = result;
                        }
                    }
                    break;
                case InputMapping.ExtensionOne2:
                    {
                        var result = DigitalHelper.GetButtonPressXinput(button, state, index);
                        if (_gameProfile.EmulationProfile == EmulationProfile.NamcoGundamPod)
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton2 = !result;
                        }
                        else
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton2 = result;
                        }
                    }
                    break;
                case InputMapping.ExtensionOne3:
                    {
                        var result = DigitalHelper.GetButtonPressXinput(button, state, index);
                        if (_gameProfile.EmulationProfile == EmulationProfile.NamcoGundamPod)
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton3 = !result;
                        }
                        else
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton3 = result;
                        }
                    }
                    break;
                case InputMapping.ExtensionOne4:
                    {
                        var result = DigitalHelper.GetButtonPressXinput(button, state, index);
                        if (_gameProfile.EmulationProfile == EmulationProfile.NamcoGundamPod)
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton4 = !result;
                        }
                        else
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton4 = result;
                        }
                    }
                    break;
                case InputMapping.ExtensionOne11:
                    {
                        var result = DigitalHelper.GetButtonPressXinput(button, state, index);
                        if (_gameProfile.EmulationProfile == EmulationProfile.NamcoGundamPod)
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_1 = !result;
                        }
                        else
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_1 = result;
                        }
                    }
                    break;
                case InputMapping.ExtensionOne12:
                    {
                        var result = DigitalHelper.GetButtonPressXinput(button, state, index);
                        if (_gameProfile.EmulationProfile == EmulationProfile.NamcoGundamPod)
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_2 = !result;
                        }
                        else
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_2 = result;
                        }
                    }
                    break;
                case InputMapping.ExtensionOne13:
                    {
                        var result = DigitalHelper.GetButtonPressXinput(button, state, index);
                        if (_gameProfile.EmulationProfile == EmulationProfile.NamcoGundamPod)
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_3 = !result;
                        }
                        else
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_3 = result;
                        }
                    }
                    break;
                case InputMapping.ExtensionOne14:
                    {
                        var result = DigitalHelper.GetButtonPressXinput(button, state, index);
                        if (_gameProfile.EmulationProfile == EmulationProfile.NamcoGundamPod)
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_4 = !result;
                        }
                        else
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_4 = result;
                        }
                    }
                    break;
                case InputMapping.ExtensionOne15:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_5 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionOne16:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_6 = DigitalHelper.GetButtonPressXinput(button, state, index);
                    break;
                case InputMapping.ExtensionOne17:
                    {
                        var result = DigitalHelper.GetButtonPressXinput(button, state, index);
                        if (_gameProfile.EmulationProfile == EmulationProfile.HauntedMuseum || _gameProfile.EmulationProfile == EmulationProfile.HauntedMuseum2)
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_7 = !result;
                        }
                        else
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_7 = result;
                        }
                    }
                    break;
                case InputMapping.ExtensionOne18:
                    {
                        var result = DigitalHelper.GetButtonPressXinput(button, state, index);
                        if (_gameProfile.EmulationProfile == EmulationProfile.HauntedMuseum || _gameProfile.EmulationProfile == EmulationProfile.HauntedMuseum2)
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_8 = !result;
                        }
                        else
                        {
                            InputCode.PlayerDigitalButtons[0].ExtensionButton1_8 = result;
                        }
                    }
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
                    JvsPackageEmulator.UpdateCoinCount(2);
                    break;
                case InputMapping.JvsTwoCoin2:
                    InputCode.PlayerDigitalButtons[3].Coin = DigitalHelper.GetButtonPressXinput(button, state, index);
                    JvsPackageEmulator.UpdateCoinCount(3);
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
                    InputCode.SetAnalogByte(0, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog1:
                    InputCode.SetAnalogByte(1, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog2:
                    InputCode.SetAnalogByte(2, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog3:
                    InputCode.SetAnalogByte(3, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog4:
                    InputCode.SetAnalogByte(4, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog5:
                    InputCode.SetAnalogByte(5, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog6:
                    InputCode.SetAnalogByte(6, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog7:
                    InputCode.SetAnalogByte(7, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog8:
                    InputCode.SetAnalogByte(8, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog9:
                    InputCode.SetAnalogByte(9, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog10:
                    InputCode.SetAnalogByte(10, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog11:
                    InputCode.SetAnalogByte(11, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog12:
                    InputCode.SetAnalogByte(12, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog13:
                    InputCode.SetAnalogByte(13, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog14:
                    InputCode.SetAnalogByte(14, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog15:
                    InputCode.SetAnalogByte(15, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog16:
                    InputCode.SetAnalogByte(16, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog17:
                    InputCode.SetAnalogByte(17, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog18:
                    InputCode.SetAnalogByte(18, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog19:
                    InputCode.SetAnalogByte(19, ModifyAnalog(joystickButtons, state, index), true);
                    break;
                case InputMapping.JvsTwoAnalog20:
                    InputCode.SetAnalogByte(20, ModifyAnalog(joystickButtons, state, index), true);
                    break;

                case InputMapping.Analog0Special1:
                case InputMapping.Analog0Special2:
                    InputCode.SetAnalogByte(0, ModifyAnalog(joystickButtons, state, index));
                    break;
                case InputMapping.Analog2Special1:
                case InputMapping.Analog2Special2:
                    InputCode.SetAnalogByte(2, ModifyAnalog(joystickButtons, state, index));
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
                case InputMapping.FnfGearChange1:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeFnfGear(1);
                        }
                    }
                    break;
                case InputMapping.FnfGearChange2:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeFnfGear(2);
                        }
                    }
                    break;
                case InputMapping.FnfGearChange3:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeFnfGear(3);
                        }
                    }
                    break;
                case InputMapping.FnfGearChange4:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeFnfGear(4);
                        }
                    }
                    break;
                case InputMapping.FnfGearChangeUp:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            if (!changeFnfGearUp)
                                DigitalHelper.ChangeFnfGearUp();
                            changeFnfGearUp = true;
                        }
                        else
                        {
                            changeFnfGearUp = false;
                        }
                    }
                    break;
                case InputMapping.FnfGearChangeDown:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            if (!changeFnfGearDown)
                                DigitalHelper.ChangeFnfGearDown();
                            changeFnfGearDown = true;
                        }
                        else
                        {
                            changeFnfGearDown = false;
                        }
                    }
                    break;
                case InputMapping.IDZGearChange1:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeIDZGear(1);
                        }
                    }
                    break;
                case InputMapping.IDZGearChange2:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeIDZGear(2);
                        }
                    }
                    break;
                case InputMapping.IDZGearChange3:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeIDZGear(3);
                        }
                    }
                    break;
                case InputMapping.IDZGearChange4:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeIDZGear(4);
                        }
                    }
                    break;
                case InputMapping.IDZGearChange5:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeIDZGear(5);
                        }
                    }
                    break;
                case InputMapping.IDZGearChange6:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            DigitalHelper.ChangeIDZGear(6);
                        }
                    }
                    break;
                case InputMapping.IDZGearChangeUp:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            if (!changeIDZGearUp)
                                DigitalHelper.ChangeIDZGearUp();
                            changeIDZGearUp = true;
                        }
                        else
                        {
                            changeIDZGearUp = false;
                        }
                    }
                    break;
                case InputMapping.IDZGearChangeDown:
                    {
                        if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                        {
                            if (!changeIDZGearDown)
                                DigitalHelper.ChangeIDZGearDown();
                            changeIDZGearDown = true;
                        }
                        else
                        {
                            changeIDZGearDown = false;
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
                case InputMapping.P3RelativeUp:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[2], button, state, Direction.RelativeUp, index);
                    break;
                case InputMapping.P3RelativeDown:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[2], button, state, Direction.RelativeDown, index);
                    break;
                case InputMapping.P3RelativeLeft:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[2], button, state, Direction.RelativeLeft, index);
                    break;
                case InputMapping.P3RelativeRight:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[2], button, state, Direction.RelativeRight, index);
                    break;
                case InputMapping.P4RelativeUp:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[3], button, state, Direction.RelativeUp, index);
                    break;
                case InputMapping.P4RelativeDown:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[3], button, state, Direction.RelativeDown, index);
                    break;
                case InputMapping.P4RelativeLeft:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[3], button, state, Direction.RelativeLeft, index);
                    break;
                case InputMapping.P4RelativeRight:
                    DigitalHelper.GetDirectionPressXinput(InputCode.PlayerDigitalButtons[3], button, state, Direction.RelativeRight, index);
                    break;
                case InputMapping.Wmmt3InsertCard:
                    if (DigitalHelper.GetButtonPressXinput(joystickButtons.XInputButton, state, index) == true)
                    {
                        WMMT3Cards.InsertCard();
                    }
                    break;
                default:
                    break;
                    //throw new ArgumentOutOfRangeException();
            }
        }

        private byte? ModifyAnalog(JoystickButtons joystickButtons, State state, int index)
        {
            if (joystickButtons.XInputButton?.XInputIndex != index)
                return null;
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
                            if (joystickButtons.InputMapping == InputMapping.Analog2)
                                JvsHelper.StateView.Write(12, analogPos);
                        }

                        if (GunGame)
                        {
                            if (RelativeInput)
                            {
                                break;
                            }

                            if (analogPos == 254) //Due to nature of Xinput (-32768 to 32767), Value can't reach 255 otherwise here.
                            {
                                analogPos = 255;
                            }

                            analogPos = (byte)(_minX + analogPos / _DivideX);

                            if (!_invertedMouseAxis)
                            {
                                analogPos = (byte)~analogPos;
                            }
                        }

                        return analogPos;
                    }
                case AnalogType.AnalogJoystickReverse:
                    {
                        byte analogReversePos = 0;
                        if (ReverseYAxis)
                        {
                            analogReversePos = AnalogHelper.CalculateWheelPosXinput(joystickButtons.XInputButton, state, false, 0, _gameProfile);
                        }
                        else
                        {
                            analogReversePos = (byte)~AnalogHelper.CalculateWheelPosXinput(joystickButtons.XInputButton, state, false, 0, _gameProfile);

                            if (GunGame)
                            {
                                if (RelativeInput)
                                {
                                    break;
                                }

                                if (analogReversePos == 1) //Due to nature of Xinput (-32768 to 32767), Value can't reach 0 otherwise here.
                                {
                                    analogReversePos = 0;
                                }

                                analogReversePos = (byte)(_minY + analogReversePos / _DivideY);

                                if (!_invertedMouseAxis)
                                {
                                    analogReversePos = (byte)~analogReversePos;
                                }
                            }
                        }
                        return analogReversePos;
                    }
                case AnalogType.Gas:
                case AnalogType.Brake:
                    return AnalogHelper.CalculateAxisOrTriggerGasBrakeXinput(joystickButtons.XInputButton, state, (byte)_gameProfile.GasAxisMin, (byte)_gameProfile.GasAxisMax);
                case AnalogType.SWThrottle:
                    byte SWThrottlePos = 0;
                    if (ReverseSWThrottleAxis)
                    {
                        SWThrottlePos = (byte)~AnalogHelper.CalculateSWThrottleXinput(joystickButtons.XInputButton, state);
                    }
                    else
                    {
                        SWThrottlePos = AnalogHelper.CalculateSWThrottleXinput(joystickButtons.XInputButton, state);
                    }
                    return SWThrottlePos;
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
