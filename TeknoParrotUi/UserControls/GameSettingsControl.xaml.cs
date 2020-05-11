using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using TeknoParrotUi.Views;

namespace TeknoParrotUi.UserControls
{
    /// <summary>
    /// Interaction logic for GameSettingsControl.xaml
    /// </summary>
    public partial class GameSettingsControl : UserControl
    {
        public GameSettingsControl()
        {
            InitializeComponent();
        }
        
        private GameProfile _gameProfile;
        private ListBoxItem _comboItem;
        private ContentControl _contentControl;
        public string GamePath;
        private Library _library;
        private bool _isXinput;
        private bool _isKeyboardorButtonAxis;

        public void LoadNewSettings(GameProfile gameProfile, ListBoxItem comboItem, ContentControl contentControl,
            Library library)
        {
            _gameProfile = gameProfile;
            _comboItem = comboItem;
            GamePathBox.Text = _gameProfile.GamePath;
            GameSettingsList.ItemsSource = gameProfile.ConfigValues;
            Lazydata.GamePath = string.Empty;
            _contentControl = contentControl;
            _library = library;
        }

        private void SelectExecutableForTextBox(object sender, MouseButtonEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = false,
                CheckFileExists = true,
                Title = Properties.Resources.GameSettingsSelectGameExecutable
            };
            if (!string.IsNullOrEmpty(_gameProfile.ExecutableName))
            {
                openFileDialog.Filter = $"{Properties.Resources.GameSettingsGameExecutableFilter} ({_gameProfile.ExecutableName})|{_gameProfile.ExecutableName}|All files (*.*)|*.*";
            }
            if (openFileDialog.ShowDialog() == true)
            {
                ((TextBox)sender).Text = openFileDialog.FileName;
                Lazydata.GamePath = openFileDialog.FileName;
            }
        }

        private void BtnSaveSettings(object sender, RoutedEventArgs e)
        {
            _isXinput = _gameProfile.ConfigValues.Any(x => x.FieldName == "XInput" && x.FieldValue == "1");
            _isKeyboardorButtonAxis = _gameProfile.ConfigValues.Any(x => x.FieldName == "Use Keyboard/Button For Axis" && x.FieldValue == "1");

            foreach (var t in _gameProfile.JoystickButtons)
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
            JoystickHelper.SerializeGameProfile(_gameProfile);
            _gameProfile.GamePath = GamePathBox.Text;
            Lazydata.GamePath = GamePathBox.Text;
            JoystickHelper.SerializeGameProfile(_gameProfile);
            _comboItem.Tag = _gameProfile;
            Application.Current.Windows.OfType<MainWindow>().Single().ShowMessage(string.Format(Properties.Resources.SuccessfullySaved, System.IO.Path.GetFileName(_gameProfile.FileName)));
            _contentControl.Content = _library;

        }

        private void BtnGoBack(object sender, RoutedEventArgs e)
        {
            _contentControl.Content = _library;
        }
    }
}
