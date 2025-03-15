using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening;
using TeknoParrotUi.Common.InputListening.Plugins;
using TeknoParrotUi.Helpers;
using TeknoParrotUi.Views;

namespace TeknoParrotUi.UserControls
{
    public partial class JoystickControl : UserControl
    {
        public string Hint { get; set; }
        private GameProfile _gameProfile;

        // Legacy controls - kept for backward compatibility
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
        private ItemsControl _joystickMappingItems;

        // Plugin system
        private InputPluginManager _pluginManager;

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
            _joystickMappingItems = this.FindControl<ItemsControl>("JoystickMappingItems");
            _library = library;
            _contentControl = contentControl;

            // Initialize the plugin manager
            _pluginManager = new InputPluginManager();
            _pluginManager.DiscoverPlugins();

            // Initialize legacy components (for backward compatibility)
            _joystickControlDirectInput = new JoystickControlDirectInput();
            _joystickControlXInput = new JoystickControlXInput();
            _joystickControlRawInput = new JoystickControlRawInput();

            var textBox = this.FindControl<TextBox>("textBox");
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

            // Initialize game-specific settings
            _isKeyboardorButtonAxis = gameProfile.ConfigValues.Any(x => x.FieldName == "Use Keyboard/Button For Axis" && x.FieldValue == "1");
            _RelativeAxis = gameProfile.ConfigValues.Any(x => x.FieldName == "Use Relative Input" && x.FieldValue == "1");
            _BG4ProMode = gameProfile.ConfigValues.Any(x => x.FieldName == "Professional Edition Enable" && x.FieldValue == "1");

            // Handle GUN settings
            string UseDPadForGUN1Stick_String = gameProfile.ConfigValues.Find(cv => cv.FieldName == "GUN1StickAxisInputStyle")?.FieldValue;
            _UseDPadForGUN1Stick = UseDPadForGUN1Stick_String == "UseDPadForGUN1Stick";

            string UseDPadForGUN2Stick_String = gameProfile.ConfigValues.Find(cv => cv.FieldName == "GUN2StickAxisInputStyle")?.FieldValue;
            _UseDPadForGUN2Stick = UseDPadForGUN2Stick_String == "UseDPadForGUN2Stick";

            string UseAnalogAxisToAimGUN1_String = gameProfile.ConfigValues.Find(cv => cv.FieldName == "GUN1AimingInputStyle")?.FieldValue;
            _UseAnalogAxisToAimGUN1 = UseAnalogAxisToAimGUN1_String == "UseAnalogAxisToAim";

            string UseAnalogAxisToAimGUN2_String = gameProfile.ConfigValues.Find(cv => cv.FieldName == "GUN2AimingInputStyle")?.FieldValue;
            _UseAnalogAxisToAimGUN2 = UseAnalogAxisToAimGUN2_String == "UseAnalogAxisToAim";

            // Initialize stick button modes
            if (_gameProfile.ConfigValues.Find(cv => cv.FieldName == "Left Stick Button Mode")?.FieldValue == "1")
            {
                _UseDPadForGUN1Stick = true;
            }

            if (_gameProfile.ConfigValues.Find(cv => cv.FieldName == "Right Stick Button Mode")?.FieldValue == "1")
            {
                _UseDPadForGUN2Stick = true;
            }

            // Initialize plugins for this game
            _pluginManager.InitializeAll(_gameProfile);

            // Set up the joystick buttons UI
            if (_joystickMappingItems == null)
            {
                _joystickMappingItems = this.FindControl<ItemsControl>("JoystickMappingItems");
            }

            if (_joystickMappingItems != null)
            {
                _joystickMappingItems.ItemsSource = gameProfile.JoystickButtons;
            }
            else
            {
                Debug.WriteLine("JoystickMappingItems control not found!");
            }

            // Update button display names based on configured plugins
            UpdateButtonDisplayNames();
        }

        private void UpdateButtonDisplayNames()
        {
            if (_gameProfile == null) return;

            // For each button, set its display name based on the default plugin
            foreach (var button in _gameProfile.JoystickButtons)
            {
                var binding = button.GetBinding<IInputBinding>(_gameProfile.DefaultInputPlugin);
                if (binding != null)
                {
                    button.BindName = binding.DisplayName;
                }
                else
                {
                    // No binding found for this plugin
                    button.BindName = "";
                }
            }
        }

        public void Listen()
        {
            // Start listening on all enabled plugins
            _pluginManager.StartListeningAll(_gameProfile.JoystickButtons, _gameProfile);

            // Legacy listening support (will be removed in future)
            string defaultPlugin = _gameProfile.DefaultInputPlugin;
            if (defaultPlugin == "DirectInput")
            {
                _inputListener = new Thread(() => _joystickControlDirectInput.Listen());
                _inputListener.Start();
            }
            else if (defaultPlugin == "XInput")
            {
                _inputListener = new Thread(() => _joystickControlXInput.Listen());
                _inputListener.Start();
            }
            else if (defaultPlugin == "RawInput" || defaultPlugin == "RawInputTrackball")
            {
                _joystickControlRawInput.Listen();
            }
        }

        public void StopListening()
        {
            // Stop all plugins
            _pluginManager.StopListeningAll();

            // Legacy stop listening (will be removed in future)
            _joystickControlDirectInput?.StopListening();
            _joystickControlXInput?.StopListening();
            _joystickControlRawInput?.StopListening();

            if (_inputListener != null && _inputListener.IsAlive)
            {
                _inputListener.Join(100);
            }
        }

        private void Control_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender == null) return;

            switch (sender)
            {
                case TextBox txt:
                    HandleTextBoxVisibility(txt);
                    break;

                case TextBlock txt:
                    HandleTextBlockVisibility(txt);
                    break;

                case ComboBox txt:
                    HandleComboBoxVisibility(txt);
                    break;

                default:
                    break;
            }
        }

        private void HandleTextBoxVisibility(TextBox txt)
        {
            if (txt.Tag == null) return;
            var t = txt.Tag as JoystickButtons;
            if (t == null) return;

            // Basic visibility
            if ("Hide".Equals(txt.Text))
            {
                txt.IsVisible = false;
                return;
            }

            // Handle different visibility conditions based on active plugin
            bool shouldHide = false;
            string activePlugin = _gameProfile.DefaultInputPlugin;

            // Plugin-specific hiding
            if (activePlugin == "DirectInput" && t.HideWithDirectInput)
                shouldHide = true;
            else if (activePlugin == "XInput" && t.HideWithXInput)
                shouldHide = true;
            else if (activePlugin == "RawInput" && t.HideWithRawInput)
                shouldHide = true;

            // Game mode specific hiding
            else if (_BG4ProMode && t.HideWithProMode)
                shouldHide = true;
            else if (!_BG4ProMode && t.HideWithoutProMode)
                shouldHide = true;

            // Special input mappings
            else if (t.InputMapping == InputMapping.P1LightGun || t.InputMapping == InputMapping.P2LightGun ||
                     t.InputMapping == InputMapping.P3LightGun || t.InputMapping == InputMapping.P4LightGun)
                shouldHide = true;
            else if (t.InputMapping == InputMapping.P1Trackball || t.InputMapping == InputMapping.P2Trackball)
                shouldHide = true;

            // Feature-specific hiding
            else if (_isKeyboardorButtonAxis && activePlugin != "XInput" && t.HideWithKeyboardForAxis)
                shouldHide = true;
            else if (!_isKeyboardorButtonAxis && activePlugin != "XInput" && t.HideWithoutKeyboardForAxis)
                shouldHide = true;
            else if (_RelativeAxis && activePlugin != "RawInput" && t.HideWithRelativeAxis)
                shouldHide = true;
            else if (!_RelativeAxis && activePlugin != "RawInput" && t.HideWithoutRelativeAxis)
                shouldHide = true;

            // Gun-specific hiding
            else if (_UseDPadForGUN1Stick && t.HideWithUseDPadForGUN1Stick)
                shouldHide = true;
            else if (!_UseDPadForGUN1Stick && t.HideWithoutUseDPadForGUN1Stick && activePlugin != "RawInput")
                shouldHide = true;
            else if (_UseDPadForGUN2Stick && t.HideWithUseDPadForGUN2Stick)
                shouldHide = true;
            else if (!_UseDPadForGUN2Stick && t.HideWithoutUseDPadForGUN2Stick && activePlugin != "RawInput")
                shouldHide = true;
            else if (_UseAnalogAxisToAimGUN1 && t.HideWithUseAnalogAxisToAimGUN1)
                shouldHide = true;
            else if (!_UseAnalogAxisToAimGUN1 && t.HideWithoutUseAnalogAxisToAimGUN1)
                shouldHide = true;
            else if (_UseAnalogAxisToAimGUN2 && t.HideWithUseAnalogAxisToAimGUN2)
                shouldHide = true;
            else if (!_UseAnalogAxisToAimGUN2 && t.HideWithoutUseAnalogAxisToAimGUN2)
                shouldHide = true;

            txt.IsVisible = !shouldHide;
        }

        private void HandleTextBlockVisibility(TextBlock txt)
        {
            // Same logic as TextBox visibility but for TextBlocks
            if (txt.Tag == null) return;
            var t2 = txt.Tag as JoystickButtons;
            if (t2 == null) return;

            bool shouldHide = false;
            string activePlugin = _gameProfile.DefaultInputPlugin;

            if (activePlugin == "DirectInput" && t2.HideWithDirectInput)
                shouldHide = true;
            else if (activePlugin == "XInput" && t2.HideWithXInput)
                shouldHide = true;
            else if (activePlugin == "RawInput" && t2.HideWithRawInput)
                shouldHide = true;
            // ... rest of the visibility logic similar to TextBox ...

            txt.IsVisible = !shouldHide;
        }

        private void HandleComboBoxVisibility(ComboBox txt)
        {
            if (txt.Tag == null) return;
            var t3 = txt.Tag as JoystickButtons;
            if (t3 == null) return;

            // Special handling for light gun/trackball with RawInput
            bool isGunMapping = t3.InputMapping == InputMapping.P1LightGun ||
                               t3.InputMapping == InputMapping.P2LightGun ||
                               t3.InputMapping == InputMapping.P3LightGun ||
                               t3.InputMapping == InputMapping.P4LightGun ||
                               t3.InputMapping == InputMapping.P1Trackball ||
                               t3.InputMapping == InputMapping.P2Trackball;

            bool isRawInputPlugin = _gameProfile.DefaultInputPlugin == "RawInput" ||
                                   _gameProfile.DefaultInputPlugin == "RawInputTrackball";

            txt.IsVisible = isGunMapping && isRawInputPlugin;

            if (txt.IsVisible)
            {
                // Set up the device list for raw input
                SetupRawInputDeviceComboBox(txt, t3);
            }
        }

        private void SetupRawInputDeviceComboBox(ComboBox comboBox, JoystickButtons button)
        {
            var deviceList = new List<string> { "None", "Windows Mouse Cursor" };
            deviceList.AddRange(_joystickControlRawInput.GetMouseDeviceList());

            // Set up the ComboBox
            comboBox.SelectionChanged -= ComboBox_SelectionChanged;
            comboBox.ItemsSource = deviceList;

            // Try to select current item
            var currentBinding = button.GetBinding<RawInputButton>(_gameProfile.DefaultInputPlugin);
            if (currentBinding != null)
            {
                if (currentBinding.DevicePath == "Windows Mouse Cursor")
                    comboBox.SelectedItem = "Windows Mouse Cursor";
                else if (currentBinding.DevicePath == "None")
                    comboBox.SelectedItem = "None";
                else
                {
                    // Try to find by device path
                    var matchingDevice = deviceList.FirstOrDefault(d => d == button.BindName);
                    comboBox.SelectedItem = matchingDevice ?? "None";
                }
            }
            else
            {
                comboBox.SelectedItem = "None";
            }

            comboBox.SelectionChanged += ComboBox_SelectionChanged;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Handle raw input device selection
            var comboBox = sender as ComboBox;
            if (comboBox == null || comboBox.SelectedItem == null) return;

            var button = comboBox.Tag as JoystickButtons;
            if (button == null) return;

            string selectedDeviceName = comboBox.SelectedItem.ToString();

            // Create the appropriate binding for the selected device
            if (selectedDeviceName == "None")
            {
                // Clear the binding
                button.SetBinding(_gameProfile.DefaultInputPlugin, null);
                button.BindName = "";
            }
            else if (selectedDeviceName == "Windows Mouse Cursor")
            {
                // Create Windows mouse binding
                var rawBinding = new RawInputButton
                {
                    DevicePath = "Windows Mouse Cursor",
                    DeviceType = RawDeviceType.Mouse,
                    MouseButton = RawMouseButton.None
                };

                button.SetBinding(_gameProfile.DefaultInputPlugin, rawBinding);
                button.BindName = "Windows Mouse Cursor";
            }
            else
            {
                // Get the selected device
                var device = _joystickControlRawInput.GetMouseDeviceByName(selectedDeviceName);
                if (device != null)
                {
                    var rawBinding = new RawInputButton
                    {
                        DevicePath = device.DevicePath,
                        DeviceType = RawDeviceType.Mouse,
                        MouseButton = RawMouseButton.None
                    };

                    button.SetBinding(_gameProfile.DefaultInputPlugin, rawBinding);
                    button.BindName = selectedDeviceName;
                }
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var txt = sender as TextBox;
            if (txt == null) return;

            txt.Text = "Press a key/button...";

            if (txt.Tag is JoystickButtons button)
            {
                // Clear the current binding for this button
                button.SetBinding(_gameProfile.DefaultInputPlugin, null);
                button.BindName = "";

                // Start listening for input to create a new binding
                // This would be handled by the appropriate plugin
                Dispatcher.UIThread.Post(() =>
                {
                    txt.Text = "Press a key/button...";
                    button.BindName = "";
                }, DispatcherPriority.Normal);
            }
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
            if (_gameProfile.DefaultInputPlugin == "RawInput" || _gameProfile.DefaultInputPlugin == "RawInputTrackball")
            {
                Task.Delay(150).ContinueWith(t => FreeCursorFromTextBox());

                var txt = (TextBox)sender;

                if (txt != null && txt.Text == "Press button or cancel with ESC...")
                    txt.Text = "";
            }
        }
    }
}