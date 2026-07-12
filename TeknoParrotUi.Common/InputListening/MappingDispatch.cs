using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Common.InputListening
{
    /// <summary>
    /// Shared InputMapping → InputCode dispatch for mouse/keyboard gun-game
    /// listeners (evdev and the X11 fallback). Full parity with
    /// InputListenerRawInput.HandleRawInputButton's switch — including the
    /// JVS board 2, extension, card, TPSystem and relative mappings plus the
    /// per-game special cases (PlayInput Test toggle, EADP coin mirror, GSEVO
    /// Button2 mirror, BattleGear key sensor, Haunted Museum inversion).
    ///
    /// NOT handled here (state-engine mappings owned by other components):
    /// Analog*Positive/Negative (keyboard-axis ramp engine, Windows RawInput
    /// listener only) and Rotary*Left/Right (encoder timer in the SDL2 gamepad
    /// mapper).
    /// </summary>
    public static class MappingDispatch
    {
        /// <summary>Mirrors InputListenerRawInput.DisableTestButton semantics.</summary>
        public static bool DisableTestButton { get; set; }

        // PlayInput/System147 Test-button toggle state (same latch behaviour
        // as the Windows RawInput listener).
        private static bool _testToggleState;
        private static bool _lastTestPressed;
        // TaitoTypeXBattleGear key-sensor toggle.
        private static bool _bg4Key;

        /// <summary>Reset per-session latches. Call from listener Start().</summary>
        public static void ResetSessionState()
        {
            _testToggleState = false;
            _lastTestPressed = false;
            _bg4Key = false;
        }

        public static void Apply(InputMapping mapping, bool pressed, GameProfile profile = null)
        {
            var emulationProfile = profile?.EmulationProfile ?? default;

            switch (mapping)
            {
                case InputMapping.Test:
                    if (DisableTestButton)
                        break;
                    if ((InputCode.ButtonMode == EmulationProfile.PlayInput ||
                         InputCode.ButtonMode == EmulationProfile.System147) && profile?.ProfileName != "superdbz")
                    {
                        if (pressed && !_lastTestPressed)
                            _testToggleState = !_testToggleState;
                        InputCode.PlayerDigitalButtons[0].Test = _testToggleState;
                        _lastTestPressed = pressed;
                    }
                    else
                    {
                        InputCode.PlayerDigitalButtons[0].Test = pressed;
                    }
                    break;
                case InputMapping.Service1:
                    InputCode.PlayerDigitalButtons[0].Service = pressed;
                    break;
                case InputMapping.Service2:
                    InputCode.PlayerDigitalButtons[1].Service = pressed;
                    break;
                case InputMapping.Coin1:
                    InputCode.PlayerDigitalButtons[0].Coin = pressed;
                    JvsPackageEmulator.UpdateCoinCount(0);
                    if (emulationProfile == EmulationProfile.EADP)
                        InputCode.PlayerDigitalButtons[0].ExtensionButton1_7 = pressed;
                    break;
                case InputMapping.Coin2:
                    InputCode.PlayerDigitalButtons[1].Coin = pressed;
                    JvsPackageEmulator.UpdateCoinCount(1);
                    break;
                case InputMapping.P1ButtonStart:
                    InputCode.PlayerDigitalButtons[0].Start = pressed;
                    break;
                case InputMapping.P1Button1:
                    InputCode.PlayerDigitalButtons[0].Button1 = pressed;
                    break;
                case InputMapping.P1Button2:
                    InputCode.PlayerDigitalButtons[0].Button2 = pressed;
                    if (emulationProfile == EmulationProfile.GSEVO)
                        InputCode.PlayerDigitalButtons[0].ExtensionButton1_8 = pressed;
                    break;
                case InputMapping.P1Button3:
                    InputCode.PlayerDigitalButtons[0].Button3 = pressed;
                    break;
                case InputMapping.P1Button4:
                    InputCode.PlayerDigitalButtons[0].Button4 = pressed;
                    break;
                case InputMapping.P1Button5:
                    InputCode.PlayerDigitalButtons[0].Button5 = pressed;
                    break;
                case InputMapping.P1Button6:
                    InputCode.PlayerDigitalButtons[0].Button6 = pressed;
                    break;
                case InputMapping.P1ButtonUp:
                    if (emulationProfile == EmulationProfile.TaitoTypeXBattleGear)
                        InputCode.PlayerDigitalButtons[0].Up = pressed;
                    else
                        InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[0], pressed ? Direction.Up : Direction.VerticalCenter);
                    break;
                case InputMapping.P1ButtonDown:
                    if (emulationProfile == EmulationProfile.TaitoTypeXBattleGear)
                        InputCode.PlayerDigitalButtons[0].Down = pressed;
                    else
                        InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[0], pressed ? Direction.Down : Direction.VerticalCenter);
                    break;
                case InputMapping.P1ButtonLeft:
                    if (emulationProfile == EmulationProfile.TaitoTypeXBattleGear)
                        InputCode.PlayerDigitalButtons[0].Left = pressed;
                    else
                        InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[0], pressed ? Direction.Left : Direction.HorizontalCenter);
                    break;
                case InputMapping.P1ButtonRight:
                    if (emulationProfile == EmulationProfile.TaitoTypeXBattleGear)
                    {
                        // Key sensor: each press toggles the reported state.
                        if (pressed)
                        {
                            InputCode.PlayerDigitalButtons[0].Right = _bg4Key;
                            _bg4Key = !_bg4Key;
                        }
                    }
                    else
                        InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[0], pressed ? Direction.Right : Direction.HorizontalCenter);
                    break;

                // ---------- Relative gun-direction buttons ----------
                case InputMapping.P1RelativeUp:
                    InputCode.PlayerDigitalButtons[0].RelativeUp = pressed;
                    break;
                case InputMapping.P1RelativeDown:
                    InputCode.PlayerDigitalButtons[0].RelativeDown = pressed;
                    break;
                case InputMapping.P1RelativeLeft:
                    InputCode.PlayerDigitalButtons[0].RelativeLeft = pressed;
                    break;
                case InputMapping.P1RelativeRight:
                    InputCode.PlayerDigitalButtons[0].RelativeRight = pressed;
                    break;
                case InputMapping.P2RelativeUp:
                    InputCode.PlayerDigitalButtons[1].RelativeUp = pressed;
                    break;
                case InputMapping.P2RelativeDown:
                    InputCode.PlayerDigitalButtons[1].RelativeDown = pressed;
                    break;
                case InputMapping.P2RelativeLeft:
                    InputCode.PlayerDigitalButtons[1].RelativeLeft = pressed;
                    break;
                case InputMapping.P2RelativeRight:
                    InputCode.PlayerDigitalButtons[1].RelativeRight = pressed;
                    break;
                case InputMapping.P2ButtonStart:
                    InputCode.PlayerDigitalButtons[1].Start = pressed;
                    break;
                case InputMapping.P2Button1:
                    InputCode.PlayerDigitalButtons[1].Button1 = pressed;
                    break;
                case InputMapping.P2Button2:
                    InputCode.PlayerDigitalButtons[1].Button2 = pressed;
                    if (emulationProfile == EmulationProfile.GSEVO)
                        InputCode.PlayerDigitalButtons[1].ExtensionButton1_8 = pressed;
                    break;
                case InputMapping.P2Button3:
                    InputCode.PlayerDigitalButtons[1].Button3 = pressed;
                    break;
                case InputMapping.P2Button4:
                    InputCode.PlayerDigitalButtons[1].Button4 = pressed;
                    break;
                case InputMapping.P2Button5:
                    InputCode.PlayerDigitalButtons[1].Button5 = pressed;
                    break;
                case InputMapping.P2Button6:
                    InputCode.PlayerDigitalButtons[1].Button6 = pressed;
                    break;
                case InputMapping.P2ButtonUp:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[1], pressed ? Direction.Up : Direction.VerticalCenter);
                    break;
                case InputMapping.P2ButtonDown:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[1], pressed ? Direction.Down : Direction.VerticalCenter);
                    break;
                case InputMapping.P2ButtonLeft:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[1], pressed ? Direction.Left : Direction.HorizontalCenter);
                    break;
                case InputMapping.P2ButtonRight:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[1], pressed ? Direction.Right : Direction.HorizontalCenter);
                    break;

                // ---------- JVS board 2 ----------
                // (JvsTwoP2 directions intentionally write to buttons[2] — same
                // as the Windows RawInput listener; kept identical on purpose.)
                case InputMapping.JvsTwoService1:
                    InputCode.PlayerDigitalButtons[2].Service = pressed;
                    break;
                case InputMapping.JvsTwoService2:
                    InputCode.PlayerDigitalButtons[3].Service = pressed;
                    break;
                case InputMapping.JvsTwoCoin1:
                    InputCode.PlayerDigitalButtons[2].Coin = pressed;
                    JvsPackageEmulator.UpdateCoinCount(2);
                    break;
                case InputMapping.JvsTwoCoin2:
                    InputCode.PlayerDigitalButtons[3].Coin = pressed;
                    JvsPackageEmulator.UpdateCoinCount(3);
                    break;
                case InputMapping.JvsTwoP1Button1:
                    InputCode.PlayerDigitalButtons[2].Button1 = pressed;
                    break;
                case InputMapping.JvsTwoP1Button2:
                    InputCode.PlayerDigitalButtons[2].Button2 = pressed;
                    break;
                case InputMapping.JvsTwoP1Button3:
                    InputCode.PlayerDigitalButtons[2].Button3 = pressed;
                    break;
                case InputMapping.JvsTwoP1Button4:
                    InputCode.PlayerDigitalButtons[2].Button4 = pressed;
                    break;
                case InputMapping.JvsTwoP1Button5:
                    InputCode.PlayerDigitalButtons[2].Button5 = pressed;
                    break;
                case InputMapping.JvsTwoP1Button6:
                    InputCode.PlayerDigitalButtons[2].Button6 = pressed;
                    break;
                case InputMapping.JvsTwoP1ButtonUp:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[2], pressed ? Direction.Up : Direction.VerticalCenter);
                    break;
                case InputMapping.JvsTwoP1ButtonDown:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[2], pressed ? Direction.Down : Direction.VerticalCenter);
                    break;
                case InputMapping.JvsTwoP1ButtonLeft:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[2], pressed ? Direction.Left : Direction.HorizontalCenter);
                    break;
                case InputMapping.JvsTwoP1ButtonRight:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[2], pressed ? Direction.Right : Direction.HorizontalCenter);
                    break;
                case InputMapping.JvsTwoP1ButtonStart:
                    InputCode.PlayerDigitalButtons[2].Start = pressed;
                    break;
                case InputMapping.JvsTwoP2Button1:
                    InputCode.PlayerDigitalButtons[3].Button1 = pressed;
                    break;
                case InputMapping.JvsTwoP2Button2:
                    InputCode.PlayerDigitalButtons[3].Button2 = pressed;
                    break;
                case InputMapping.JvsTwoP2Button3:
                    InputCode.PlayerDigitalButtons[3].Button3 = pressed;
                    break;
                case InputMapping.JvsTwoP2Button4:
                    InputCode.PlayerDigitalButtons[3].Button4 = pressed;
                    break;
                case InputMapping.JvsTwoP2Button5:
                    InputCode.PlayerDigitalButtons[3].Button5 = pressed;
                    break;
                case InputMapping.JvsTwoP2Button6:
                    InputCode.PlayerDigitalButtons[3].Button6 = pressed;
                    break;
                case InputMapping.JvsTwoP2ButtonUp:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[2], pressed ? Direction.Up : Direction.VerticalCenter);
                    break;
                case InputMapping.JvsTwoP2ButtonDown:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[2], pressed ? Direction.Down : Direction.VerticalCenter);
                    break;
                case InputMapping.JvsTwoP2ButtonLeft:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[2], pressed ? Direction.Left : Direction.HorizontalCenter);
                    break;
                case InputMapping.JvsTwoP2ButtonRight:
                    InputCode.SetPlayerDirection(InputCode.PlayerDigitalButtons[2], pressed ? Direction.Right : Direction.HorizontalCenter);
                    break;
                case InputMapping.JvsTwoP2ButtonStart:
                    InputCode.PlayerDigitalButtons[3].Start = pressed;
                    break;

                // ---------- Extension board 1, P1 ----------
                case InputMapping.ExtensionOne1:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1 = pressed;
                    break;
                case InputMapping.ExtensionOne2:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton2 = pressed;
                    break;
                case InputMapping.ExtensionOne3:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton3 = pressed;
                    break;
                case InputMapping.ExtensionOne4:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton4 = pressed;
                    break;
                case InputMapping.ExtensionOne11:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_1 = pressed;
                    break;
                case InputMapping.ExtensionOne12:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_2 = pressed;
                    break;
                case InputMapping.ExtensionOne13:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_3 = pressed;
                    break;
                case InputMapping.ExtensionOne14:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_4 = pressed;
                    break;
                case InputMapping.ExtensionOne15:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_5 = pressed;
                    break;
                case InputMapping.ExtensionOne16:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_6 = pressed;
                    break;
                case InputMapping.ExtensionOne17:
                    if (emulationProfile == EmulationProfile.HauntedMuseum || emulationProfile == EmulationProfile.HauntedMuseum2)
                        InputCode.PlayerDigitalButtons[0].ExtensionButton1_7 = !pressed;
                    else
                        InputCode.PlayerDigitalButtons[0].ExtensionButton1_7 = pressed;
                    break;
                case InputMapping.ExtensionOne18:
                    if (emulationProfile == EmulationProfile.HauntedMuseum || emulationProfile == EmulationProfile.HauntedMuseum2)
                        InputCode.PlayerDigitalButtons[0].ExtensionButton1_8 = !pressed;
                    else
                        InputCode.PlayerDigitalButtons[0].ExtensionButton1_8 = pressed;
                    break;

                // ---------- Extension board 1, P2 ----------
                case InputMapping.ExtensionTwo1:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1 = pressed;
                    break;
                case InputMapping.ExtensionTwo2:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton2 = pressed;
                    break;
                case InputMapping.ExtensionTwo3:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton3 = pressed;
                    break;
                case InputMapping.ExtensionTwo4:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton4 = pressed;
                    break;
                case InputMapping.ExtensionTwo11:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_1 = pressed;
                    break;
                case InputMapping.ExtensionTwo12:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_2 = pressed;
                    break;
                case InputMapping.ExtensionTwo13:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_3 = pressed;
                    break;
                case InputMapping.ExtensionTwo14:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_4 = pressed;
                    break;
                case InputMapping.ExtensionTwo15:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_5 = pressed;
                    break;
                case InputMapping.ExtensionTwo16:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_6 = pressed;
                    break;
                case InputMapping.ExtensionTwo17:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_7 = pressed;
                    break;
                case InputMapping.ExtensionTwo18:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton1_8 = pressed;
                    break;

                // ---------- Extension board 2 ----------
                case InputMapping.ExtensionOne21:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton2_1 = pressed;
                    break;
                case InputMapping.ExtensionOne22:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton2_2 = pressed;
                    break;
                case InputMapping.ExtensionOne23:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton2_3 = pressed;
                    break;
                case InputMapping.ExtensionOne24:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton2_4 = pressed;
                    break;
                case InputMapping.ExtensionOne25:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton2_5 = pressed;
                    break;
                case InputMapping.ExtensionOne26:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton2_6 = pressed;
                    break;
                case InputMapping.ExtensionOne27:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton2_7 = pressed;
                    break;
                case InputMapping.ExtensionOne28:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton2_8 = pressed;
                    break;
                case InputMapping.ExtensionTwo21:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton2_1 = pressed;
                    break;
                case InputMapping.ExtensionTwo22:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton2_2 = pressed;
                    break;
                case InputMapping.ExtensionTwo23:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton2_3 = pressed;
                    break;
                case InputMapping.ExtensionTwo24:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton2_4 = pressed;
                    break;
                case InputMapping.ExtensionTwo25:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton2_5 = pressed;
                    break;
                case InputMapping.ExtensionTwo26:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton2_6 = pressed;
                    break;
                case InputMapping.ExtensionTwo27:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton2_7 = pressed;
                    break;
                case InputMapping.ExtensionTwo28:
                    InputCode.PlayerDigitalButtons[1].ExtensionButton2_8 = pressed;
                    break;

                // ---------- cards / system ----------
                case InputMapping.Card1:
                    InputCode.PlayerDigitalButtons[0].Card = pressed;
                    break;
                case InputMapping.Card2:
                    InputCode.PlayerDigitalButtons[1].Card = pressed;
                    break;
                case InputMapping.Card3:
                    InputCode.PlayerDigitalButtons[0].Card2 = pressed;
                    break;
                case InputMapping.Card4:
                    InputCode.PlayerDigitalButtons[1].Card2 = pressed;
                    break;
                case InputMapping.TPSystem1:
                    InputCode.TPSystem1 = pressed;
                    break;
                case InputMapping.TPSystem2:
                    InputCode.TPSystem2 = pressed;
                    break;
                case InputMapping.TPSystem3:
                    InputCode.TPSystem3 = pressed;
                    break;
            }
        }
    }
}
