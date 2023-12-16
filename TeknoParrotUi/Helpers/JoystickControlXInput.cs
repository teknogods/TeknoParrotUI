using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SharpDX.XInput;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    public class JoystickControlXInput
    {
        private static bool _stopListening;

        /// <summary>
        /// Listens given joystick.
        /// </summary>
        public void Listen()
        {
            _stopListening = false;
            SpawnXInputListener(UserIndex.One);
            SpawnXInputListener(UserIndex.Two);
            SpawnXInputListener(UserIndex.Three);
            SpawnXInputListener(UserIndex.Four);
        }

        public void StopListening()
        {
            _stopListening = true;
        }

        public void SpawnXInputListener(UserIndex index)
        {
            var controller = new Controller(index);
            if (!controller.IsConnected)
                return;
            new Thread(() =>
                {
                    try
                    {
                        var previousState = controller.GetState();
                        while (!_stopListening)
                        {
                            var state = controller.GetState();
                            if (previousState.PacketNumber != state.PacketNumber)
                            {
                                SetTextBoxText(state, previousState, (int)index);
                            }
                            Thread.Sleep(10);
                            previousState = state;
                        }
                    }
                    catch (Exception)
                    {

                    }
                }
            ).Start();
        }

        /// <summary>
        /// Sets text box text and tag.
        /// </summary>
        /// <param name="newState">New state.</param>
        /// <param name="oldState">Previous state.</param>
        /// <param name="index">XInput index.</param>
        private void SetTextBoxText(State newState, State oldState, int index)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    var txt = GetActiveTextBox();
                    if (txt == null) return;

                    if (newState.Gamepad.Buttons != oldState.Gamepad.Buttons)
                    {
                        if (newState.Gamepad.Buttons == GamepadButtonFlags.A)
                            HandleButton(GamepadButtonFlags.A, txt, index);
                        if (newState.Gamepad.Buttons == GamepadButtonFlags.B)
                            HandleButton(GamepadButtonFlags.B, txt, index);
                        if (newState.Gamepad.Buttons == GamepadButtonFlags.X)
                            HandleButton(GamepadButtonFlags.X, txt, index);
                        if (newState.Gamepad.Buttons == GamepadButtonFlags.Y)
                            HandleButton(GamepadButtonFlags.Y, txt, index);
                        if (newState.Gamepad.Buttons == GamepadButtonFlags.Start)
                            HandleButton(GamepadButtonFlags.Start, txt, index);
                        if (newState.Gamepad.Buttons == GamepadButtonFlags.Back)
                            HandleButton(GamepadButtonFlags.Back, txt, index);
                        if (newState.Gamepad.Buttons == GamepadButtonFlags.LeftShoulder)
                            HandleButton(GamepadButtonFlags.LeftShoulder, txt, index);
                        if (newState.Gamepad.Buttons == GamepadButtonFlags.RightShoulder)
                            HandleButton(GamepadButtonFlags.RightShoulder, txt, index);
                        if (newState.Gamepad.Buttons == GamepadButtonFlags.LeftThumb)
                            HandleButton(GamepadButtonFlags.LeftThumb, txt, index);
                        if (newState.Gamepad.Buttons == GamepadButtonFlags.RightThumb)
                            HandleButton(GamepadButtonFlags.RightThumb, txt, index);
                        if (newState.Gamepad.Buttons == GamepadButtonFlags.DPadDown)
                            HandleButton(GamepadButtonFlags.DPadDown, txt, index);
                        if (newState.Gamepad.Buttons == GamepadButtonFlags.DPadUp)
                            HandleButton(GamepadButtonFlags.DPadUp, txt, index);
                        if (newState.Gamepad.Buttons == GamepadButtonFlags.DPadLeft)
                            HandleButton(GamepadButtonFlags.DPadLeft, txt, index);
                        if (newState.Gamepad.Buttons == GamepadButtonFlags.DPadRight)
                            HandleButton(GamepadButtonFlags.DPadRight, txt, index);
                        return;
                    }

                    if (newState.Gamepad.LeftThumbX != oldState.Gamepad.LeftThumbX)
                    {
                        GetAnalogXInput(newState.Gamepad.LeftThumbX, true, txt, false, index);
                        return;
                    }

                    if (newState.Gamepad.RightThumbX != oldState.Gamepad.RightThumbX)
                    {
                        GetAnalogXInput(newState.Gamepad.RightThumbX, false, txt, false, index);
                        return;
                    }

                    if (newState.Gamepad.LeftThumbY != oldState.Gamepad.LeftThumbY)
                    {
                        GetAnalogXInput(newState.Gamepad.LeftThumbY, true, txt, true, index);
                        return;
                    }

                    if (newState.Gamepad.RightThumbY != oldState.Gamepad.RightThumbY)
                    {
                        GetAnalogXInput(newState.Gamepad.RightThumbY, false, txt, true, index);
                        return;
                    }

                    if (newState.Gamepad.LeftTrigger != oldState.Gamepad.LeftTrigger)
                    {
                        var t = txt.Tag as JoystickButtons;
                        var button = new XInputButton { IsLeftTrigger = true, XInputIndex = index };
                        txt.Text = $"Input Device {index} " + "LeftTrigger";
                        t.XInputButton = button;
                        t.BindNameXi = txt.Text;
                        return;
                    }

                    if (newState.Gamepad.RightTrigger != oldState.Gamepad.RightTrigger)
                    {
                        var t = txt.Tag as JoystickButtons;
                        var button = new XInputButton { IsRightTrigger = true, XInputIndex = index };
                        txt.Text = $"Input Device {index} " + "RightTrigger";
                        t.BindNameXi = txt.Text;
                        t.XInputButton = button;
                    }
                }));
        }

        private void HandleButton(GamepadButtonFlags buttonFlag, TextBox txt, int index)
        {
            var button = new XInputButton
            {
                IsButton = true,
                ButtonCode = (short)buttonFlag,
                XInputIndex = index
            };
            var t = txt.Tag as JoystickButtons;
            txt.Text = $"Input Device {index} " + buttonFlag;
            t.BindNameXi = txt.Text;
            t.XInputButton = button;
        }

        /// <summary>
        /// Gets active text box.
        /// </summary>
        /// <returns></returns>
        private TextBox GetActiveTextBox()
        {
            IInputElement focusedControl = FocusManager.GetFocusedElement(Application.Current.Windows[0]);
            if (focusedControl == null)
                return null;
            if (focusedControl.GetType() == typeof(TextBox))
            {
                var txt = (TextBox)focusedControl;
                var tag = txt.Tag as string;
                if (tag != "SettingsTxt")
                    return txt;
            }
            return null;
        }

        private void GetAnalogXInput(short value, bool isLeftThumb, TextBox txt, bool isY, int index)
        {
            var deadZone = isLeftThumb ? Gamepad.LeftThumbDeadZone : Gamepad.RightThumbDeadZone;
            var indexTxt = $"Input Device {index} ";
            XInputButton button = new XInputButton { IsButton = false, XInputIndex = index };
            if (value > 0 + deadZone)
            {
                txt.Text = indexTxt + (isLeftThumb ? "LeftThumb" : "RightThumb");
                if (isY)
                {
                    if (isLeftThumb)
                        button.IsLeftThumbY = true;
                    else
                        button.IsRightThumbY = true;
                    txt.Text += indexTxt + "Y+";
                }
                else
                {
                    if (isLeftThumb)
                        button.IsLeftThumbX = true;
                    else
                        button.IsRightThumbX = true;
                    button.IsAxisMinus = false;
                    txt.Text += indexTxt + "X+";
                }
                var t = txt.Tag as JoystickButtons;
                t.BindNameXi = txt.Text;
                t.XInputButton = button;
            }
            else if (value < 0 - deadZone)
            {
                txt.Text = indexTxt + (isLeftThumb ? "LeftThumb" : "RightThumb");
                button.IsAxisMinus = true;
                if (isY)
                {
                    if (isLeftThumb)
                        button.IsLeftThumbY = true;
                    else
                        button.IsRightThumbY = true;
                    txt.Text += indexTxt + "Y-";
                }
                else
                {
                    if (isLeftThumb)
                        button.IsLeftThumbX = true;
                    else
                        button.IsRightThumbX = true;
                    txt.Text += indexTxt + "X-";
                }
                var t = txt.Tag as JoystickButtons;
                t.BindNameXi = txt.Text;
                t.XInputButton = button;
            }
        }
    }
}
