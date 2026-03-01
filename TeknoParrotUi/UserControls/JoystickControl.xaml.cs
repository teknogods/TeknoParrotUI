using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using TeknoParrotUi.Views;
using Keys = System.Windows.Forms.Keys;

namespace TeknoParrotUi.UserControls
{
    /// <summary>
    /// Interaction logic for JoystickControl.xaml
    /// </summary>
    public partial class JoystickControl : UserControl
    {
        private GameProfile _gameProfile;
        private JoystickControlXInput _joystickControlXInput;
        private JoystickControlDirectInput _joystickControlDirectInput;
        private JoystickControlRawInput _joystickControlRawInput;
        private ListBoxItem _comboItem;
        private static Thread _inputListener;
        private bool _BG4ProMode;
        private bool _isKeyboardorButtonAxis;
        private bool _RelativeAxis;
        private bool _UseDPadForGUN1Stick;
        private bool _UseDPadForGUN2Stick;
        private bool _UseAnalogAxisToAimGUN1;
        private bool _UseAnalogAxisToAimGUN2;
        private readonly Library _library;
        private readonly ContentControl _contentControl;
        private InputApi _inputApi = InputApi.DirectInput;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ClipCursor(ref RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("User32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        public JoystickControl(ContentControl contentControl, Library library)
        {
            InitializeComponent();
            _library = library;
            _contentControl = contentControl;
        }

        public void LoadNewSettings(GameProfile gameProfile, ListBoxItem comboItem)
        {
            _gameProfile = gameProfile;
            _comboItem = comboItem;
            _isKeyboardorButtonAxis = gameProfile.ConfigValues.Any(x => x.FieldName == "Use Keyboard/Button For Axis" && x.FieldValue == "1");
            _RelativeAxis = gameProfile.ConfigValues.Any(x => x.FieldName == "Use Relative Input" && x.FieldValue == "1");
            _BG4ProMode = gameProfile.ConfigValues.Any(x => x.FieldName == "Professional Edition Enable" && x.FieldValue == "1");

            string UseDPadForGUN1Stick_String = _gameProfile.ConfigValues.Find(cv => cv.FieldName == "GUN1StickAxisInputStyle")?.FieldValue;
            if (UseDPadForGUN1Stick_String == "UseDPadForGUN1Stick")
                _UseDPadForGUN1Stick = true;
            else _UseDPadForGUN1Stick = false;
            string UseDPadForGUN2Stick_String = _gameProfile.ConfigValues.Find(cv => cv.FieldName == "GUN2StickAxisInputStyle")?.FieldValue;
            if (UseDPadForGUN2Stick_String == "UseDPadForGUN2Stick")
                _UseDPadForGUN2Stick = true;
            else _UseDPadForGUN2Stick = false;
            string UseAnalogAxisToAimGUN1_String = _gameProfile.ConfigValues.Find(cv => cv.FieldName == "GUN1AimingInputStyle")?.FieldValue;
            if (UseAnalogAxisToAimGUN1_String == "UseAnalogAxisToAim")
                _UseAnalogAxisToAimGUN1 = true;
            else _UseAnalogAxisToAimGUN1 = false;
            string UseAnalogAxisToAimGUN2_String = _gameProfile.ConfigValues.Find(cv => cv.FieldName == "GUN2AimingInputStyle")?.FieldValue;
            if (UseAnalogAxisToAimGUN2_String == "UseAnalogAxisToAim")
                _UseAnalogAxisToAimGUN2 = true;
            else _UseAnalogAxisToAimGUN2 = false;

            string inputApiString = _gameProfile.ConfigValues.Find(cv => cv.FieldName == "Input API")?.FieldValue;

            if (inputApiString != null)
                _inputApi = (InputApi)Enum.Parse(typeof(InputApi), inputApiString);

            // Hack
            foreach (var t in gameProfile.JoystickButtons)
            {
                if (_inputApi == InputApi.DirectInput)
                    t.BindName = t.BindNameDi;
                else if (_inputApi == InputApi.XInput)
                    t.BindName = t.BindNameXi;
                else if (_inputApi == InputApi.RawInput)
                    t.BindName = t.BindNameRi;
            }

            JoystickMappingItems.ItemsSource = gameProfile.JoystickButtons;
            if (_joystickControlRawInput == null)
                _joystickControlRawInput = new JoystickControlRawInput();
            if (_joystickControlXInput == null)
                _joystickControlXInput = new JoystickControlXInput();
            if (_joystickControlDirectInput == null)
                _joystickControlDirectInput = new JoystickControlDirectInput();
        }

        public void Listen()
        {
            if (_inputApi == InputApi.DirectInput)
            {
                _inputListener = new Thread(() => _joystickControlDirectInput.Listen());
                _inputListener.Start();
            }
            else if (_inputApi == InputApi.XInput)
            {
                _inputListener = new Thread(() => _joystickControlXInput.Listen());
                _inputListener.Start();
            }
            else if (_inputApi == InputApi.RawInput)
            {
                _joystickControlRawInput.Listen();
            }
        }

        public void StopListening()
        {
            if (_inputApi == InputApi.DirectInput)
                _joystickControlDirectInput?.StopListening();
            else if (_inputApi == InputApi.XInput)
                _joystickControlXInput?.StopListening();
            else if (_inputApi == InputApi.RawInput)
                _joystickControlRawInput?.StopListening();
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            JoystickHelper.SerializeGameProfile(_gameProfile);
            _comboItem.Tag = _gameProfile;
            Application.Current.Windows.OfType<MainWindow>().Single().ShowMessage(string.Format(Properties.Resources.SuccessfullySaved, "Joystick Settings"));
            _contentControl.Content = _library;
        }

        private void KeepCursorInTextBox(TextBox box)
        {
            Point relativePoint = box.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(0, 0));

            RECT clipRect = new RECT();
            clipRect.Left = (int)(Application.Current.MainWindow.Left + relativePoint.X + 1);
            clipRect.Top = (int)(Application.Current.MainWindow.Top + relativePoint.Y + 1);
            clipRect.Right = (int)(Application.Current.MainWindow.Left + relativePoint.X + box.ActualWidth);
            clipRect.Bottom = (int)(Application.Current.MainWindow.Top + relativePoint.Y + box.ActualHeight);

            ClipCursor(ref clipRect);
        }

        private void FreeCursorFromTextBox()
        {
            RECT clipRect = new RECT();
            clipRect.Left = 0;
            clipRect.Top = 0;
            clipRect.Right = (int)SystemParameters.VirtualScreenWidth;
            clipRect.Bottom = (int)SystemParameters.VirtualScreenHeight;

            ClipCursor(ref clipRect);
        }

        private void TextBox_Unloaded(object sender, RoutedEventArgs e)
        {
            StopListening();
        }

        private void JoystickGoBack_OnClick(object sender, RoutedEventArgs e)
        {
            // Reload library to discard changes
            _library.ListUpdate(_gameProfile.GameNameInternal);

            _contentControl.Content = _library;
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_inputApi == InputApi.RawInput)
            {
                Task.Delay(150).ContinueWith(t => FreeCursorFromTextBox());

                var txt = (TextBox)sender;

                if (txt != null && txt.Text == "Press button or cancel with ESC...")
                    txt.Text = "";
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var txt = (TextBox)sender;

            if (txt == null)
                return;

            txt.Text = "";
            txt.ToolTip = null;

            if (txt.Tag != null)
            {
                var t = txt.Tag as JoystickButtons;

                if (t != null)
                {
                    if (_inputApi == InputApi.DirectInput)
                    {
                        t.DirectInputButton = null;
                        t.BindNameDi = "";
                    }
                    else if (_inputApi == InputApi.XInput)
                    {
                        t.XInputButton = null;
                        t.BindNameXi = "";
                    }
                    else if (_inputApi == InputApi.RawInput)
                    {
                        t.RawInputButton = null;
                        t.BindNameRi = "";
                        txt.Text = "Press button or cancel with ESC...";
                        KeepCursorInTextBox(txt);
                    }

                    t.BindName = "";
                }
            }
        }

        private void Control_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender == null)
                return;

            switch (sender)
            {
                // Normal button setup box
                case TextBox txt:
                    if (txt.Tag == null)
                        return;

                    var t = txt.Tag as JoystickButtons;

                    if (txt.Text.Equals("Hide"))
                        txt.Visibility = Visibility.Collapsed;
                    else if (_inputApi == InputApi.DirectInput && t.HideWithDirectInput)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_inputApi == InputApi.XInput && t.HideWithXInput)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_inputApi == InputApi.RawInput && t.HideWithRawInput)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_BG4ProMode && t.HideWithProMode)
                        txt.Visibility = Visibility.Collapsed;
                    else if (!_BG4ProMode && t.HideWithoutProMode)
                        txt.Visibility = Visibility.Collapsed;
                    else if (t.InputMapping == InputMapping.P1LightGun || t.InputMapping == InputMapping.P2LightGun || t.InputMapping == InputMapping.P3LightGun || t.InputMapping == InputMapping.P4LightGun)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_isKeyboardorButtonAxis && _inputApi != InputApi.XInput && t.HideWithKeyboardForAxis)
                        txt.Visibility = Visibility.Collapsed;
                    else if (!_isKeyboardorButtonAxis && _inputApi != InputApi.XInput && t.HideWithoutKeyboardForAxis)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_RelativeAxis && _inputApi != InputApi.RawInput && t.HideWithRelativeAxis)
                        txt.Visibility = Visibility.Collapsed;
                    else if (!_RelativeAxis && _inputApi != InputApi.RawInput && t.HideWithoutRelativeAxis)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_UseDPadForGUN1Stick && t.HideWithUseDPadForGUN1Stick)
                        txt.Visibility = Visibility.Collapsed;
                    else if (!_UseDPadForGUN1Stick && t.HideWithoutUseDPadForGUN1Stick)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_UseDPadForGUN2Stick && t.HideWithUseDPadForGUN2Stick)
                        txt.Visibility = Visibility.Collapsed;
                    else if (!_UseDPadForGUN2Stick && t.HideWithoutUseDPadForGUN2Stick)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_UseAnalogAxisToAimGUN1 && t.HideWithUseAnalogAxisToAimGUN1)
                        txt.Visibility = Visibility.Collapsed;
                    else if (!_UseAnalogAxisToAimGUN1 && t.HideWithoutUseAnalogAxisToAimGUN1)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_UseAnalogAxisToAimGUN2 && t.HideWithUseAnalogAxisToAimGUN2)
                        txt.Visibility = Visibility.Collapsed;
                    else if (!_UseAnalogAxisToAimGUN2 && t.HideWithoutUseAnalogAxisToAimGUN2)
                        txt.Visibility = Visibility.Collapsed;

                    break;
                // Button name label
                case TextBlock txt:
                    if (txt.Tag == null)
                        return;

                    var t2 = txt.Tag as JoystickButtons;

                    if (_inputApi == InputApi.DirectInput && t2.HideWithDirectInput)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_inputApi == InputApi.XInput && t2.HideWithXInput)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_inputApi == InputApi.RawInput && t2.HideWithRawInput)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_BG4ProMode && t2.HideWithProMode)
                        txt.Visibility = Visibility.Collapsed;
                    else if (!_BG4ProMode && t2.HideWithoutProMode)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_isKeyboardorButtonAxis && _inputApi != InputApi.XInput && t2.HideWithKeyboardForAxis)
                        txt.Visibility = Visibility.Collapsed;
                    else if (!_isKeyboardorButtonAxis && _inputApi != InputApi.XInput && t2.HideWithoutKeyboardForAxis)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_RelativeAxis && _inputApi != InputApi.RawInput && t2.HideWithRelativeAxis)
                        txt.Visibility = Visibility.Collapsed;
                    else if (!_RelativeAxis && _inputApi != InputApi.RawInput && t2.HideWithoutRelativeAxis)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_UseDPadForGUN1Stick && t2.HideWithUseDPadForGUN1Stick)
                        txt.Visibility = Visibility.Collapsed;
                    else if (!_UseDPadForGUN1Stick && t2.HideWithoutUseDPadForGUN1Stick)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_UseDPadForGUN2Stick && t2.HideWithUseDPadForGUN2Stick)
                        txt.Visibility = Visibility.Collapsed;
                    else if (!_UseDPadForGUN2Stick && t2.HideWithoutUseDPadForGUN2Stick)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_UseAnalogAxisToAimGUN1 && t2.HideWithUseAnalogAxisToAimGUN1)
                        txt.Visibility = Visibility.Collapsed;
                    else if (!_UseAnalogAxisToAimGUN1 && t2.HideWithoutUseAnalogAxisToAimGUN1)
                        txt.Visibility = Visibility.Collapsed;
                    else if (_UseAnalogAxisToAimGUN2 && t2.HideWithUseAnalogAxisToAimGUN2)
                        txt.Visibility = Visibility.Collapsed;
                    else if (!_UseAnalogAxisToAimGUN2 && t2.HideWithoutUseAnalogAxisToAimGUN2)
                        txt.Visibility = Visibility.Collapsed;

                    break;
                // Dropdown for light gun selection
                case ComboBox txt:
                    if (txt.Tag == null)
                        return;

                    var t3 = txt.Tag as JoystickButtons;

                    if ((t3.InputMapping == InputMapping.P1LightGun || t3.InputMapping == InputMapping.P2LightGun || t3.InputMapping == InputMapping.P3LightGun || t3.InputMapping == InputMapping.P4LightGun) && _inputApi == InputApi.RawInput)
                    {
                        var deviceList = new List<string>() { "None", "Windows Mouse Cursor", "Unknown Device" };
                        deviceList.AddRange(_joystickControlRawInput.GetMouseDeviceList());

                        // Add current selection even though it isnt currently available
                        if (t3.BindNameRi != null && !deviceList.Contains(t3.BindNameRi))
                            deviceList.Add(t3.BindNameRi);

                        // Temporary remove event to prevent triggering it
                        txt.SelectionChanged -= ComboBox_SelectionChanged;
                        txt.ItemsSource = deviceList;

                        if (t3.BindNameRi == null)
                            txt.SelectedItem = "None";
                        else
                            txt.SelectedItem = t3.BindNameRi;

                        txt.Visibility = Visibility.Visible;
                        // Restore event
                        txt.SelectionChanged += ComboBox_SelectionChanged;
                    }
                    else
                    {
                        txt.Visibility = Visibility.Collapsed;
                    }
                    break;
                default:
                    break;
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var txt = (ComboBox)sender;
            var t = txt.Tag as JoystickButtons;
            var selectedDeviceName = txt.SelectedValue.ToString();
            var selectedDevice = _joystickControlRawInput.GetMouseDeviceByName(selectedDeviceName);
            string path = "null";
            var type = RawDeviceType.None;

            if (selectedDeviceName == "Windows Mouse Cursor")
            {
                path = "Windows Mouse Cursor";
                type = RawDeviceType.Mouse;
            }
            else if (selectedDeviceName == "None")
            {
                path = "None";
                type = RawDeviceType.None;
            }
            else if (selectedDeviceName == "Unknown Device")
            {
                path = "null";
                type = RawDeviceType.Mouse;
            }
            else if (selectedDevice == null)
            {
                MessageBoxHelper.ErrorOK("Selected device is currently not available!");
                return;
            }
            else
            {
                path = selectedDevice.DevicePath;
                type = RawDeviceType.Mouse;
            }

            var button = new RawInputButton
            {
                DevicePath = path,
                DeviceType = type,
                MouseButton = RawMouseButton.None,
                KeyboardKey = Keys.None
            };

            t.RawInputButton = button;
            t.BindName = selectedDeviceName;
            t.BindNameRi = selectedDeviceName;
        }
    }
}
