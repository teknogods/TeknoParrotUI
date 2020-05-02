using MaterialDesignThemes.Wpf;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using TeknoParrotUi.Views;

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
        private ListBoxItem _comboItem;
        private static Thread _inputListener;
        private bool _isXinput;
        private bool _isKeyboardorButtonAxis;
        private readonly Library _library;
        private readonly ContentControl _contentControl;

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
            _isXinput = gameProfile.ConfigValues.Any(x => x.FieldName == "XInput" && x.FieldValue == "1");
            _isKeyboardorButtonAxis = gameProfile.ConfigValues.Any(x => x.FieldName == "Use Keyboard/Button For Axis" && x.FieldValue == "1");

            // Hack
            foreach (var t in gameProfile.JoystickButtons)
            {
                t.BindName = _isXinput ? t.BindNameXi : t.BindNameDi;
                if ((_isKeyboardorButtonAxis) && (!_isXinput))
                {
                    //Wheel Axis Right (Keyboard/Button Only) = " "
                    //Joystick Analog X Right (Keyboard/Button Only) = "   "
                    //Joystick Analog Y Up (Keyboard/Button Only) = "    "
                    //Analog X Right (Keyboard/Button Only) = "     "
                    //Analog Y Down (Keyboard/Button Only) = "      "
                    //Throttle Brake (Keyboard/Button Only) = "       "
                    if (t.ButtonName.Equals("Wheel Axis"))
                    {
                        t.ButtonName = "Wheel Axis Left";
                    }
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
                    if ((t.ButtonName.Equals(" ")) || (t.ButtonName.Equals("  ")) || (t.ButtonName.Equals("   ")) || (t.ButtonName.Equals("    ")) || (t.ButtonName.Equals("     ")) || (t.ButtonName.Equals("      ")))
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
            if(_joystickControlXInput == null)
                _joystickControlXInput = new JoystickControlXInput();
            if(_joystickControlDirectInput == null)
                _joystickControlDirectInput = new JoystickControlDirectInput();
        }

        public void Listen()
        {
            if (_isXinput)
            {
                _inputListener = new Thread(() => _joystickControlXInput.Listen());
                _inputListener.Start();
            }
            else
            {
                _inputListener = new Thread(() => _joystickControlDirectInput.Listen());
                _inputListener.Start();
            }
        }

        public void StopListening()
        {
            if (_isXinput)
            {
                _joystickControlXInput?.StopListening();
            }
            else
            {
                _joystickControlDirectInput?.StopListening();
            }
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

        private void TextBox_loaded(object sender, RoutedEventArgs e)
        {  
            var txt = (TextBox)sender;
            if (txt == null)
                return;
            if (txt.Tag != null)
            {
                var t = txt.Tag as JoystickButtons;
                Thickness m = txt.Margin;
                m.Left = 10000;
                if (txt.Text.Equals("Hide"))
                {
                    txt.Margin = m;
                }          
            }
        }

        private void UIElement_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var txt = (TextBox) sender;
            if (txt == null)
                return;
            txt.Text = "";
            txt.ToolTip = null;
            if (txt.Tag != null)
            {
                var t = txt.Tag as JoystickButtons;
                if (t != null)
                {
                    if (_isXinput)
                    {
                        t.XInputButton = null;
                        t.BindNameXi = "";
                    }
                    else
                    {
                        t.DirectInputButton = null;
                        t.BindNameDi = "";
                    }
                    t.BindName = "";
                }
            }
        }

        private void TextBox_Unloaded(object sender, RoutedEventArgs e)
        {
            StopListening();
        }

        private void JoystickGoBack_OnClick(object sender, RoutedEventArgs e)
        {
            _contentControl.Content = _library;
        }
    }
}
