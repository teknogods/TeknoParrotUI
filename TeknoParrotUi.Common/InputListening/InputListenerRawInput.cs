using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using TeknoParrotUi.Common.Jvs;
using Keys = System.Windows.Forms.Keys;

namespace TeknoParrotUi.Common.InputListening
{
    public class InputListenerRawInput
    {
        public static bool KillMe;
        private List<JoystickButtons> _joystickButtons;
        private float _minX;
        private float _maxX;
        private float _minY;
        private float _maxY;
        private bool _fullScreen;
        private bool _invertedMouseAxis;
        private bool _isLuigisMansion = false;

        public void ListenRawInput(List<JoystickButtons> joystickButtons, GameProfile gameProfile)
        {
            Console.WriteLine("ListenRawInput");

            _minX = gameProfile.xAxisMin;
            _maxX = gameProfile.xAxisMax;
            _minY = gameProfile.yAxisMin;
            _maxY = gameProfile.yAxisMax;
            _fullScreen = gameProfile.ConfigValues.Any(x => x.FieldName == "Windowed" && x.FieldValue == "0");
            _invertedMouseAxis = gameProfile.InvertedMouseAxis;

            if (gameProfile.EmulationProfile == EmulationProfile.LuigisMansion)
                _isLuigisMansion = true;

            if (!joystickButtons.Any())
                return;

            // Only configured buttons
            _joystickButtons = joystickButtons.Where(x => x?.RawInputButton != null).ToList();

            while (!KillMe)
            {
                Thread.Sleep(100);
            }
        }

        public void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_INPUT = 0x00FF;

            if (msg == WM_INPUT)
            {
                var data = RawInputData.FromHandle(lParam);

                int vid = 0;
                int pid = 0;

                if (data != null && data.Device != null)
                {
                    vid = data.Device.VendorId;
                    pid = data.Device.ProductId;
                }

                switch (data)
                {
                    case RawInputMouseData mouse:
                        // Handle mouse button presses
                        if (mouse.Mouse.Buttons != RawMouseButtonFlags.None)
                        {
                            Debug.WriteLine(mouse.Mouse);
                            RawMouseButtonFlags flags = mouse.Mouse.Buttons;

                            // Multiple buttons can be pressed/released in single event so check them all
                            if (flags.HasFlag(RawMouseButtonFlags.LeftButtonDown) || flags.HasFlag(RawMouseButtonFlags.LeftButtonUp))
                            {
                                foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DeviceVid == vid && btn.RawInputButton.DevicePid == pid && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && btn.RawInputButton.MouseButton == RawMouseButton.LeftButton))
                                    HandleRawInputButton(jsButton, flags.HasFlag(RawMouseButtonFlags.LeftButtonDown));
                            }

                            if (flags.HasFlag(RawMouseButtonFlags.RightButtonDown) || flags.HasFlag(RawMouseButtonFlags.RightButtonUp))
                            {
                                foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DeviceVid == vid && btn.RawInputButton.DevicePid == pid && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && btn.RawInputButton.MouseButton == RawMouseButton.RightButton))
                                    HandleRawInputButton(jsButton, flags.HasFlag(RawMouseButtonFlags.RightButtonDown));
                            }

                            if (flags.HasFlag(RawMouseButtonFlags.MiddleButtonDown) || flags.HasFlag(RawMouseButtonFlags.MiddleButtonUp))
                            {
                                foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DeviceVid == vid && btn.RawInputButton.DevicePid == pid && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && btn.RawInputButton.MouseButton == RawMouseButton.MiddleButton))
                                    HandleRawInputButton(jsButton, flags.HasFlag(RawMouseButtonFlags.MiddleButtonDown));
                            }

                            if (flags.HasFlag(RawMouseButtonFlags.Button4Down) || flags.HasFlag(RawMouseButtonFlags.Button4Up))
                            {
                                foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DeviceVid == vid && btn.RawInputButton.DevicePid == pid && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && btn.RawInputButton.MouseButton == RawMouseButton.Button4))
                                    HandleRawInputButton(jsButton, flags.HasFlag(RawMouseButtonFlags.Button4Down));
                            }

                            if (flags.HasFlag(RawMouseButtonFlags.Button5Down) || flags.HasFlag(RawMouseButtonFlags.Button5Up))
                            {
                                foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DeviceVid == vid && btn.RawInputButton.DevicePid == pid && btn.RawInputButton.DeviceType == RawDeviceType.Mouse && btn.RawInputButton.MouseButton == RawMouseButton.Button5))
                                    HandleRawInputButton(jsButton, flags.HasFlag(RawMouseButtonFlags.Button5Down));
                            }
                        }

                        // Handle position
                        if (mouse.Mouse.Flags.HasFlag(RawMouseFlags.MoveAbsolute))
                        {
                            var gun = _joystickButtons.Find(btn => btn.RawInputButton.DeviceVid == vid && btn.RawInputButton.DevicePid == pid && (btn.InputMapping == InputMapping.P1LightGun || btn.InputMapping == InputMapping.P2LightGun));

                            if (gun != null)
                                HandleRawInputGun(gun, mouse.Mouse.LastX, mouse.Mouse.LastY);
                        }

                        break;
                    case RawInputKeyboardData keyboard:
                        Debug.WriteLine(keyboard.Keyboard);

                        foreach (var jsButton in _joystickButtons.Where(btn => btn.RawInputButton.DeviceVid == vid && btn.RawInputButton.DevicePid == pid && btn.RawInputButton.DeviceType == RawDeviceType.Keyboard && btn.RawInputButton.KeyboardKey == (Keys)keyboard.Keyboard.VirutalKey))
                            HandleRawInputButton(jsButton, keyboard.Keyboard.Flags == RawKeyboardFlags.Down);

                        break;
                }
            }
        }

        private void HandleRawInputButton(JoystickButtons joystickButton, bool pressed)
        {
            Console.WriteLine(String.Format("HandleRawInput {0} {1}", joystickButton.InputMapping, pressed));

            switch (joystickButton.InputMapping)
            {
                case InputMapping.Test:
                    InputCode.PlayerDigitalButtons[0].Test = pressed;
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
                    break;
                case InputMapping.Coin2:
                    InputCode.PlayerDigitalButtons[1].Coin = pressed;
                    JvsPackageEmulator.UpdateCoinCount(1);
                    break;
                // P1
                case InputMapping.P1ButtonStart:
                    InputCode.PlayerDigitalButtons[0].Start = pressed;
                    break;
                case InputMapping.P1Button1:
                    InputCode.PlayerDigitalButtons[0].Button1 = pressed;
                    break;
                case InputMapping.P1Button2:
                    InputCode.PlayerDigitalButtons[0].Button2 = pressed;
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
                // P2
                case InputMapping.P2ButtonStart:
                    InputCode.PlayerDigitalButtons[1].Start = pressed;
                    break;
                case InputMapping.P2Button1:
                    InputCode.PlayerDigitalButtons[1].Button1 = pressed;
                    break;
                case InputMapping.P2Button2:
                    InputCode.PlayerDigitalButtons[1].Button2 = pressed;
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
                // Ext1
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
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_7 = pressed;
                    break;
                case InputMapping.ExtensionOne18:
                    InputCode.PlayerDigitalButtons[0].ExtensionButton1_8 = pressed;
                    break;
                // Ext2
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
                default:
                    break;
            }
        }

        private void HandleRawInputGun(JoystickButtons joystickButton, int inputX, int inputY)
        {
            float factorX = (float)inputX / (float)0xFFFF;
            float factorY = (float)inputY / (float)0xFFFF;

            ushort x = (ushort)Math.Round(_minX + factorX * (_maxX - _minX));
            ushort y = (ushort)Math.Round(_minY + factorY * (_maxY - _minY));

            /*
             * InvertedMouseAxis:
             * AnalogBytes[0] = X, Left = 0, Right = 255
             * AnalogBytes[2] = Y, Top = 0,  Bottom = 255
             * 
             * NOT InvertedMouseAxis:
             * AnalogBytes[2] = X, Left = 255, Right = 0
             * AnalogBytes[0] = Y, Top = 255,  Bottom = 0
             * 
             * Luigi is different ofcourse:
             * AnalogBytes[2] = X, Left = 0, Right = 255
             * AnalogBytes[0] = Y, Top = 0,  Bottom = 255
             */

            if (joystickButton.InputMapping == InputMapping.P1LightGun)
            {
                if (_invertedMouseAxis)
                {
                    InputCode.AnalogBytes[0] = (byte)x;
                    InputCode.AnalogBytes[2] = (byte)y;
                }
                else if (_isLuigisMansion)
                {
                    InputCode.AnalogBytes[2] = (byte)x;
                    InputCode.AnalogBytes[0] = (byte)y;
                }
                else
                {
                    InputCode.AnalogBytes[2] = (byte)~x;
                    InputCode.AnalogBytes[0] = (byte)~y;
                }
            }
            else if (joystickButton.InputMapping == InputMapping.P2LightGun)
            {
                if (_invertedMouseAxis)
                {
                    InputCode.AnalogBytes[4] = (byte)x;
                    InputCode.AnalogBytes[6] = (byte)y;
                }
                else if (_isLuigisMansion)
                {
                    InputCode.AnalogBytes[6] = (byte)x;
                    InputCode.AnalogBytes[4] = (byte)y;
                }
                else
                {
                    InputCode.AnalogBytes[6] = (byte)~x;
                    InputCode.AnalogBytes[4] = (byte)~y;
                }
            }
        }
    }
}
