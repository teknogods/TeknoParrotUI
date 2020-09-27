using MaterialDesignThemes.Wpf;
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
        private bool _isKeyboardorButtonAxis;
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

                if ((_isKeyboardorButtonAxis) && (_inputApi != InputApi.XInput))
                {
                    //Wheel Axis Right (Keyboard/Button Only) = " "
                    //Joystick Analog X Right (Keyboard/Button Only) = "   "
                    //Joystick Analog Y Up (Keyboard/Button Only) = "    "
                    //Analog X Right (Keyboard/Button Only) = "     "
                    //Analog Y Down (Keyboard/Button Only) = "      "
                    //Throttle Brake (Keyboard/Button Only) = "       "
                    //Wheel Axis Half Turn (Hold Down) = "        "
                    if (t.ButtonName.Equals(" "))
                    {
                        t.ButtonName = "Wheel Axis Right (Keyboard/Button Only)";
                    }
                    if (t.ButtonName.Equals("  "))
                    {
                        t.ButtonName = "Joystick Analog X Right (Keyboard/Button Only)";
                    }
                    if (t.ButtonName.Equals("   "))
                    {
                        t.ButtonName = "Joystick Analog Y Up (Keyboard/Button Only)";
                    }
                    if (t.ButtonName.Equals("    "))
                    {
                        t.ButtonName = "Analog X Right (Keyboard/Button Only)";
                    }
                    if (t.ButtonName.Equals("     "))
                    {
                        t.ButtonName = "Analog Y Down (Keyboard/Button Only)";
                    }
                    if (t.ButtonName.Equals("      "))
                    {
                        t.ButtonName = "Throttle Brake (Keyboard/Button Only)";
                    }
                    if (t.ButtonName.Equals("       "))
                    {
                        t.ButtonName = "Wheel Axis Half Turn (Hold Down)";
                    }
                    if (t.ButtonName.Equals("Wheel Axis"))
                    {
                        t.ButtonName = "Wheel Axis Left";
                    }
                    if (_gameProfile.EmulationProfile == EmulationProfile.NamcoMachStorm)
                    {
                        if (t.ButtonName.Equals("Analog X"))
                        {
                            t.ButtonName = "Analog X Left";
                        }
                        if (t.ButtonName.Equals("Analog Y"))
                        {
                            t.ButtonName = "Analog Y Up";
                        }
                    }
                    if (_gameProfile.EmulationProfile == EmulationProfile.AfterBurnerClimax)
                    {
                        if (t.ButtonName.Equals("Joystick Analog X"))
                        {
                            t.ButtonName = "Joystick Analog X Left";
                        }
                        if (t.ButtonName.Equals("Joystick Analog Y"))
                        {
                            t.ButtonName = "Joystick Analog Y Down";
                        }
                    }
                    if (_gameProfile.EmulationProfile == EmulationProfile.TokyoCop)
                    {
                        if (t.ButtonName.Equals("Leaning Axis"))
                        {
                            t.ButtonName = "Leaning Axis Left";
                        }
                        if (t.ButtonName.Equals("Handlebar Axis"))
                        {
                            t.ButtonName = "Handlebar Axis Left";
                        }
                    }  
                }
                else
                {
                    if ((t.ButtonName.Equals(" ")) || (t.ButtonName.Equals("  ")) || (t.ButtonName.Equals("   ")) || (t.ButtonName.Equals("    ")) || (t.ButtonName.Equals("     ")) || (t.ButtonName.Equals("      ")) || (t.ButtonName.Equals("       ")))
                    {
                        t.BindName = "Hide";
                    }
                    if (t.ButtonName.Equals("Wheel Axis Right (Keyboard/Button Only)"))
                    {
                        t.ButtonName = " ";
                        t.BindName = "Hide";
                    }
                    if (t.ButtonName.Equals("Joystick Analog X Right (Keyboard/Button Only)"))
                    {
                        t.ButtonName = "  ";
                        t.BindName = "Hide";
                    }
                    if (t.ButtonName.Equals("Joystick Analog Y Up (Keyboard/Button Only)"))
                    {
                        t.ButtonName = "   ";
                        t.BindName = "Hide";
                    }
                    if (t.ButtonName.Equals("Analog X Right (Keyboard/Button Only)"))
                    {
                        t.ButtonName = "    ";
                        t.BindName = "Hide";
                    }
                    if (t.ButtonName.Equals("Analog Y Down (Keyboard/Button Only)"))
                    {
                        t.ButtonName = "     ";
                        t.BindName = "Hide";
                    }
                    if (t.ButtonName.Equals("Throttle Brake (Keyboard/Button Only)"))
                    {
                        t.ButtonName = "      ";
                        t.BindName = "Hide";
                    }
                    if (t.ButtonName.Equals("Wheel Axis Half Turn (Hold Down)"))
                    {
                        t.ButtonName = "       ";
                        t.BindName = "Hide";
                    }
                    if (t.ButtonName.Equals("Wheel Axis Left"))
                    {
                        t.ButtonName = "Wheel Axis";
                    }
                    if (_gameProfile.EmulationProfile == EmulationProfile.NamcoMachStorm)
                    {
                        if (t.ButtonName.Equals("Analog X Left"))
                        {
                            t.ButtonName = "Analog X";
                        }
                        if (t.ButtonName.Equals("Analog Y Up"))
                        {
                            t.ButtonName = "Analog Y";
                        }
                    }
                    if (_gameProfile.EmulationProfile == EmulationProfile.AfterBurnerClimax)
                    {
                        if (t.ButtonName.Equals("Joystick Analog X Left"))
                        {
                            t.ButtonName = "Joystick Analog X";
                        }
                        if (t.ButtonName.Equals("Joystick Analog Y Down"))
                        {
                            t.ButtonName = "Joystick Analog Y";
                        }
                    }
                    if (_gameProfile.EmulationProfile == EmulationProfile.TokyoCop)
                    {
                        if (t.ButtonName.Equals("Leaning Axis Left"))
                        {
                            t.ButtonName = "Leaning Axis";
                        }
                        if (t.ButtonName.Equals("Handlebar Axis Left"))
                        {
                            t.ButtonName = "Handlebar Axis";
                        }
                    }
                }
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
            // Save here, also save gamepath.
            if (Lazydata.GamePath != String.Empty)
                _gameProfile.GamePath = Lazydata.GamePath;

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
                    else if (t.InputMapping == InputMapping.P1LightGun || t.InputMapping == InputMapping.P2LightGun)
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

                    break;
                // Dropdown for light gun selection
                case ComboBox txt:
                    if (txt.Tag == null)
                        return;

                    var t3 = txt.Tag as JoystickButtons;

                    if ((t3.InputMapping == InputMapping.P1LightGun || t3.InputMapping == InputMapping.P2LightGun) && _inputApi == InputApi.RawInput)
                    {
                        var deviceList = new List<string>() { "None" };
                        deviceList.AddRange(_joystickControlRawInput.GetDeviceList());

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
            var selectedDevice = _joystickControlRawInput.GetDeviceByName(selectedDeviceName);
            var vid = 0;
            var pid = 0;

            if (selectedDevice == null && selectedDeviceName != "None")
            {
                MessageBoxHelper.ErrorOK("Selected device is currently not available!");
                return;
            }
            else if (selectedDeviceName != "None")
            {
                vid = selectedDevice.VendorId;
                pid = selectedDevice.ProductId;
            }

            var button = new RawInputButton
            {
                DeviceVid = vid,
                DevicePid = pid,
                DeviceType = RawDeviceType.None,
                MouseButton = RawMouseButton.None,
                KeyboardKey = Keys.None
            };

            t.RawInputButton = button;
            t.BindName = selectedDeviceName;
            t.BindNameRi = selectedDeviceName;
        }
    }
}
