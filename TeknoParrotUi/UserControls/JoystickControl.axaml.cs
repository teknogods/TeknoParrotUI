using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using TeknoParrotUi.Views;
using Keys = Avalonia.Input.Key;

namespace TeknoParrotUi.UserControls
{
    /// <summary>
    /// Interaction logic for JoystickControl.axaml
    /// </summary>
    public partial class JoystickControl : UserControl
    {
        // Add this property to fix the binding error
        public string Hint { get; set; }
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
            var textBox = this.FindControl<TextBox>("textBox"); // Replace "textBox" with the actual x:Name of your TextBox
            if (textBox != null)
            {
                textBox.GotFocus += TextBox_GotFocus;
            }
        }

        public void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
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

            if (_gameProfile.ConfigValues.Find(cv => cv.FieldName == "Left Stick Button Mode")?.FieldValue == "1")
            {
                _UseDPadForGUN1Stick = true;
            }

            if (_gameProfile.ConfigValues.Find(cv => cv.FieldName == "Right Stick Button Mode")?.FieldValue == "1")
            {
                _UseDPadForGUN2Stick = true;
            }

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
                else if (_inputApi == InputApi.RawInput || _inputApi == InputApi.RawInputTrackball)
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
            else if (_inputApi == InputApi.RawInput || _inputApi == InputApi.RawInputTrackball)
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
            if (TopLevel.GetTopLevel(this) is MainWindow mainWindow)
            {
                mainWindow.ShowMessage(string.Format(Properties.Resources.SuccessfullySaved, "Joystick Settings"));
            }
            _contentControl.Content = _library;
        }

        private void KeepCursorInTextBox(TextBox box)
        {
            var topLevel = TopLevel.GetTopLevel(box);
            RECT clipRect = new RECT();
            if (topLevel != null)
            {
                Point? relativePoint = box.TranslatePoint(new Point(0, 0), topLevel);
                if (relativePoint.HasValue)
                {
                    // To this:
                    var position = new PixelPoint(0, 0);
                    if (topLevel is Window window)
                    {
                        position = window.Position;
                    }
                    clipRect.Left = (int)(position.X + relativePoint.Value.X + 1);
                    clipRect.Top = (int)(position.Y + relativePoint.Value.Y + 1);
                    clipRect.Right = (int)(position.X + relativePoint.Value.X + box.Bounds.Width);
                    clipRect.Bottom = (int)(position.Y + relativePoint.Value.Y + box.Bounds.Height);
                }
            }

            ClipCursor(ref clipRect);
        }

        private void FreeCursorFromTextBox()
        {
            // TODO: FIX
            // RECT clipRect = new RECT();
            // clipRect.Left = 0;
            // clipRect.Top = 0;

            // // Access screens using the correct Avalonia API for .NET 9.0
            // var screens = Screens.ScreenList.Value;
            // var primaryScreen = screens.FirstOrDefault(s => s.IsPrimary);

            // if (primaryScreen != null)
            // {
            //     clipRect.Right = (int)primaryScreen.Bounds.Width;
            //     clipRect.Bottom = (int)primaryScreen.Bounds.Height;
            // }
            // else
            // {
            //     // Fallback to a reasonable size if no screen information is available
            //     clipRect.Right = 1920;
            //     clipRect.Bottom = 1080;
            // }

            // ClipCursor(ref clipRect);
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
            if (_inputApi == InputApi.RawInput || _inputApi == InputApi.RawInputTrackball)
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
            ToolTip.SetTip(txt, null);

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
                    else if (_inputApi == InputApi.RawInput || _inputApi == InputApi.RawInputTrackball)
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
                        txt.IsVisible = false;
                    else if (_inputApi == InputApi.DirectInput && t.HideWithDirectInput)
                        txt.IsVisible = false;
                    else if (_inputApi == InputApi.XInput && t.HideWithXInput)
                        txt.IsVisible = false;
                    else if (_inputApi == InputApi.RawInput && t.HideWithRawInput)
                        txt.IsVisible = false;
                    else if (_BG4ProMode && t.HideWithProMode)
                        txt.IsVisible = false;
                    else if (!_BG4ProMode && t.HideWithoutProMode)
                        txt.IsVisible = false;
                    else if (t.InputMapping == InputMapping.P1LightGun || t.InputMapping == InputMapping.P2LightGun || t.InputMapping == InputMapping.P3LightGun || t.InputMapping == InputMapping.P4LightGun)
                        txt.IsVisible = false;
                    else if (t.InputMapping == InputMapping.P1Trackball || t.InputMapping == InputMapping.P2Trackball)
                        txt.IsVisible = false;
                    else if (_isKeyboardorButtonAxis && _inputApi != InputApi.XInput && t.HideWithKeyboardForAxis)
                        txt.IsVisible = false;
                    else if (!_isKeyboardorButtonAxis && _inputApi != InputApi.XInput && t.HideWithoutKeyboardForAxis)
                        txt.IsVisible = false;
                    else if (_RelativeAxis && _inputApi != InputApi.RawInput && t.HideWithRelativeAxis)
                        txt.IsVisible = false;
                    else if (!_RelativeAxis && _inputApi != InputApi.RawInput && t.HideWithoutRelativeAxis)
                        txt.IsVisible = false;
                    else if (_UseDPadForGUN1Stick && t.HideWithUseDPadForGUN1Stick)
                        txt.IsVisible = false;
                    else if (!_UseDPadForGUN1Stick && t.HideWithoutUseDPadForGUN1Stick && _inputApi != InputApi.RawInput)
                        txt.IsVisible = false;
                    else if (_UseDPadForGUN2Stick && t.HideWithUseDPadForGUN2Stick)
                        txt.IsVisible = false;
                    else if (!_UseDPadForGUN2Stick && t.HideWithoutUseDPadForGUN2Stick && _inputApi != InputApi.RawInput)
                        txt.IsVisible = false;
                    else if (_UseAnalogAxisToAimGUN1 && t.HideWithUseAnalogAxisToAimGUN1)
                        txt.IsVisible = false;
                    else if (!_UseAnalogAxisToAimGUN1 && t.HideWithoutUseAnalogAxisToAimGUN1)
                        txt.IsVisible = false;
                    else if (_UseAnalogAxisToAimGUN2 && t.HideWithUseAnalogAxisToAimGUN2)
                        txt.IsVisible = false;
                    else if (!_UseAnalogAxisToAimGUN2 && t.HideWithoutUseAnalogAxisToAimGUN2)
                        txt.IsVisible = false;

                    break;
                // Button name label
                case TextBlock txt:
                    if (txt.Tag == null)
                        return;

                    var t2 = txt.Tag as JoystickButtons;

                    if (_inputApi == InputApi.DirectInput && t2.HideWithDirectInput)
                        txt.IsVisible = false;
                    else if (_inputApi == InputApi.XInput && t2.HideWithXInput)
                        txt.IsVisible = false;
                    else if (_inputApi == InputApi.RawInput && t2.HideWithRawInput)
                        txt.IsVisible = false;
                    else if (_BG4ProMode && t2.HideWithProMode)
                        txt.IsVisible = false;
                    else if (!_BG4ProMode && t2.HideWithoutProMode)
                        txt.IsVisible = false;
                    else if (_isKeyboardorButtonAxis && _inputApi != InputApi.XInput && t2.HideWithKeyboardForAxis)
                        txt.IsVisible = false;
                    else if (!_isKeyboardorButtonAxis && _inputApi != InputApi.XInput && t2.HideWithoutKeyboardForAxis)
                        txt.IsVisible = false;
                    else if (_RelativeAxis && _inputApi != InputApi.RawInput && t2.HideWithRelativeAxis)
                        txt.IsVisible = false;
                    else if (!_RelativeAxis && _inputApi != InputApi.RawInput && t2.HideWithoutRelativeAxis)
                        txt.IsVisible = false;
                    else if (_UseDPadForGUN1Stick && t2.HideWithUseDPadForGUN1Stick)
                        txt.IsVisible = false;
                    else if (!_UseDPadForGUN1Stick && t2.HideWithoutUseDPadForGUN1Stick && _inputApi != InputApi.RawInput)
                        txt.IsVisible = false;
                    else if (_UseDPadForGUN2Stick && t2.HideWithUseDPadForGUN2Stick)
                        txt.IsVisible = false;
                    else if (!_UseDPadForGUN2Stick && t2.HideWithoutUseDPadForGUN2Stick && _inputApi != InputApi.RawInput)
                        txt.IsVisible = false;
                    else if (_UseAnalogAxisToAimGUN1 && t2.HideWithUseAnalogAxisToAimGUN1)
                        txt.IsVisible = false;
                    else if (!_UseAnalogAxisToAimGUN1 && t2.HideWithoutUseAnalogAxisToAimGUN1)
                        txt.IsVisible = false;
                    else if (_UseAnalogAxisToAimGUN2 && t2.HideWithUseAnalogAxisToAimGUN2)
                        txt.IsVisible = false;
                    else if (!_UseAnalogAxisToAimGUN2 && t2.HideWithoutUseAnalogAxisToAimGUN2)
                        txt.IsVisible = false;

                    break;
                // Dropdown for light gun selection
                case ComboBox txt:
                    if (txt.Tag == null)
                        return;

                    var t3 = txt.Tag as JoystickButtons;

                    if ((t3.InputMapping == InputMapping.P1LightGun || t3.InputMapping == InputMapping.P2LightGun || t3.InputMapping == InputMapping.P3LightGun || t3.InputMapping == InputMapping.P4LightGun || t3.InputMapping == InputMapping.P1Trackball || t3.InputMapping == InputMapping.P2Trackball) && (_inputApi == InputApi.RawInput || _inputApi == InputApi.RawInputTrackball))
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

                        txt.IsVisible = true;
                        // Restore event
                        txt.SelectionChanged += ComboBox_SelectionChanged;
                    }
                    else
                    {
                        txt.IsVisible = false;
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