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
        private readonly Library _library;
        private readonly ContentControl _contentControl;

        public JoystickControl(ContentControl contentControl, Library library)
        {
            InitializeComponent();
            _library = library;
            _contentControl = contentControl;
        }

        public void LoadNewSettings(GameProfile gameProfile, ListBoxItem comboItem, ParrotData parrotData)
        {
            _gameProfile = gameProfile;
            _comboItem = comboItem;
            _isXinput = gameProfile.ConfigValues.Any(x => x.FieldName == "XInput" && x.FieldValue == "1");

            // Hack
            foreach (var t in gameProfile.JoystickButtons)
            {
                t.BindName = _isXinput ? t.BindNameXi : t.BindNameDi;
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
            MessageBox.Show("Save complete");
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
