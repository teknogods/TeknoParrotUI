using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives; // For ToolTip
using Avalonia.Input;
using Avalonia.Threading;
using SharpDX.DirectInput;
using SharpDX.Multimedia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TeknoParrotUi.Common;
using DeviceType = SharpDX.DirectInput.DeviceType;
using Key = SharpDX.DirectInput.Key;

namespace TeknoParrotUi.Helpers
{
    public class JoystickControlDirectInput
    {
        private List<Joystick> _joystickCollection = new List<Joystick>();
        private readonly DirectInput _directInput = new DirectInput();
        private List<Guid> FetchValidGuids()
        {
            var guids = new List<Guid>();
            var lines = File.ReadAllLines("DirectInputOverride.txt");
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (Guid.TryParse(line, out var result))
                {
                    guids.Add(result);
                }
                else
                {
                    MessageBoxHelper.ErrorOK(string.Format(Properties.Resources.ErrorCannotParseGUID, line));
                }
            }
            return guids;
        }

        private static bool _stopListening;
        /// <summary>
        /// Listens given joystick.
        /// </summary>
        public void Listen()
        {
            _joystickCollection.Clear();
            var devices = new List<DeviceInstance>();
            _stopListening = false;
            if (File.Exists("DirectInputOverride.txt"))
            {
                var devs = _directInput.GetDevices();
                var guids = FetchValidGuids();
                foreach (var guid in guids)
                {
                    var result = devs.FirstOrDefault(x => x.InstanceGuid == guid);
                    if (result != null)
                    {
                        devices.Add(result);
                    }
                }
            }
            else
            {
                devices.AddRange(_directInput.GetDevices().Where(x => x.Type != DeviceType.Mouse && x.UsagePage != UsagePage.VendorDefinedBegin && x.Usage != UsageId.AlphanumericBitmapSizeX && x.Usage != UsageId.AlphanumericAlphanumericDisplay && x.UsagePage != unchecked((UsagePage)0xffffff43) && x.UsagePage != UsagePage.Vr).ToList());
            }

            foreach (var t in devices)
            {
                var joystick = new Joystick(new DirectInput(), t.InstanceGuid);
                joystick.Properties.BufferSize = 512;
                joystick.Acquire();
                new Thread(() => SpawnDirectInputListener(joystick, t)).Start();
            }
        }

        private void SpawnDirectInputListener(Joystick joystick, DeviceInstance deviceInstance)
        {
            _joystickCollection.Add(joystick);
            // Acquire the joystick
            try
            {
                while (!_stopListening)
                {
                    joystick.Poll();
                    var datas = joystick.GetBufferedData();
                    foreach (var state in datas)
                    {
                        SetTextBoxText(state, deviceInstance);
                    }
                    Thread.Sleep(10);
                }
            }
            catch (Exception)
            {
                // Handle exception or just continue
            }
            joystick.Unacquire();
        }

        /// <summary>
        /// Sets text box text and tag.
        /// </summary>
        /// <param name="key"></param>
        private void SetTextBoxText(JoystickUpdate key, DeviceInstance deviceInstance)
        {
            // Replace WPF dispatcher with Avalonia dispatcher
            Dispatcher.UIThread.Post(() =>
            {
                var txt = GetActiveTextBox();
                if (txt == null) return;
                JoystickButton button = null;
                string inputText = "";

                // 4 Direction input
                if (key.Offset == JoystickOffset.PointOfViewControllers0 ||
                    key.Offset == JoystickOffset.PointOfViewControllers1 ||
                    key.Offset == JoystickOffset.PointOfViewControllers2 ||
                    key.Offset == JoystickOffset.PointOfViewControllers3)
                {
                    // Not neutral
                    if (key.Value != -1)
                    {
                        if (key.Value == 0)
                            inputText = key.Offset + " Up";
                        else if (key.Value == 9000)
                            inputText = key.Offset + " Right";
                        else if (key.Value == 18000)
                            inputText = key.Offset + " Down";
                        else if (key.Value == 27000)
                            inputText = key.Offset + " Left";

                        button = new JoystickButton
                        {
                            Button = (int)key.Offset,
                            IsAxis = false,
                            PovDirection = key.Value,
                            JoystickGuid = deviceInstance.InstanceGuid
                        };
                    }
                }
                // 2 Direction input
                else if (key.Offset == JoystickOffset.X ||
                        key.Offset == JoystickOffset.Y ||
                        key.Offset == JoystickOffset.Z ||
                        key.Offset == JoystickOffset.RotationX ||
                        key.Offset == JoystickOffset.RotationY ||
                        key.Offset == JoystickOffset.RotationZ ||
                        key.Offset == JoystickOffset.Sliders0 ||
                        key.Offset == JoystickOffset.Sliders1 ||
                        key.Offset == JoystickOffset.AccelerationX ||
                        key.Offset == JoystickOffset.AccelerationY ||
                        key.Offset == JoystickOffset.AccelerationZ)
                {
                    // Positive direction
                    if (key.Value > short.MaxValue + 15000)
                    {
                        inputText = key.Offset + " +";

                        button = new JoystickButton
                        {
                            Button = (int)key.Offset,
                            IsAxis = true,
                            IsAxisMinus = false,
                            JoystickGuid = deviceInstance.InstanceGuid
                        };
                    }
                    // Negative direction
                    else if (key.Value < short.MaxValue - 15000)
                    {
                        inputText = key.Offset + " -";

                        button = new JoystickButton
                        {
                            Button = (int)key.Offset,
                            IsAxis = true,
                            IsAxisMinus = true,
                            JoystickGuid = deviceInstance.InstanceGuid
                        };
                    }
                }
                // Digital input
                else
                {
                    if (key.Value == 128)
                    {
                        if (deviceInstance.Type == DeviceType.Keyboard)
                            inputText = "Button " + ((Key)key.Offset - 47).ToString();
                        else
                            inputText = key.Offset.ToString();

                        button = new JoystickButton
                        {
                            Button = (int)key.Offset,
                            IsAxis = false,
                            JoystickGuid = deviceInstance.InstanceGuid
                        };
                    }
                }

                // Save input
                if (button != null)
                {
                    // Use ToolTip.SetTip instead of direct property
                    ToolTip.SetTip(txt, deviceInstance.InstanceName);
                    txt.Text = deviceInstance.Type + " " + inputText;

                    var t = txt.Tag as JoystickButtons;
                    t.DirectInputButton = button;
                    //t.BindNameDi = txt.Text;
                }
            });
        }

        /// <summary>
        /// Gets active text box.
        /// </summary>
        /// <returns></returns>
        private TextBox GetActiveTextBox()
        {
            // Use Avalonia's approach to get focused element
            var topLevel = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var focusedControl = topLevel?.FocusManager?.GetFocusedElement();

            if (focusedControl == null)
                return null;

            // Use 'is' pattern matching instead of GetType() comparison
            if (focusedControl is TextBox txt)
            {
                var tag = txt.Tag as string;
                if (tag != "SettingsTxt")
                    return txt;
            }
            return null;
        }

        public void StopListening()
        {
            try
            {
                foreach (Joystick t in _joystickCollection)
                {
                    if (!t.IsDisposed)
                        t.Unacquire();
                }
            }
            catch (Exception)
            {
                // Handle exception or just continue
            }
            _joystickCollection.Clear();
            _stopListening = true;
        }
    }
}