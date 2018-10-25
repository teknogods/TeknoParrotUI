using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SharpDX.DirectInput;
using SharpDX.Multimedia;
using TeknoParrotUi.Common;
using DeviceType = SharpDX.DirectInput.DeviceType;

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
                    MessageBox.Show($"Cannot parse GUID: {line}. Please check that data has the GUID value only and nothing else!");
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
                devices.AddRange(_directInput.GetDevices().Where(x => x.Type != DeviceType.Mouse && x.UsagePage != UsagePage.VendorDefinedBegin && x.Usage != UsageId.AlphanumericBitmapSizeX && x.Usage != UsageId.AlphanumericAlphanumericDisplay && x.UsagePage != unchecked((UsagePage) 0xffffff43) && x.UsagePage != UsagePage.Vr).ToList());
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

            }
            joystick.Unacquire();
        }

        /// <summary>
        /// Sets text box text and tag.
        /// </summary>
        /// <param name="key"></param>
        private void SetTextBoxText(JoystickUpdate key, DeviceInstance deviceInstance)
        {
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    var txt = GetActiveTextBox();
                    if (txt == null) return;
                    if (key.Offset == JoystickOffset.PointOfViewControllers0 ||
                        key.Offset == JoystickOffset.PointOfViewControllers1 ||
                        key.Offset == JoystickOffset.PointOfViewControllers2 ||
                        key.Offset == JoystickOffset.PointOfViewControllers3)
                    {
                        if (key.Value != -1)
                        {
                            if (key.Value == 0)
                                txt.Text = key.Offset + " UP";
                            if (key.Value == 9000)
                                txt.Text = key.Offset + " RIGHT";
                            if (key.Value == 18000)
                                txt.Text = key.Offset + " DOWN";
                            if (key.Value == 27000)
                                txt.Text = key.Offset + " LEFT";
                            JoystickButton button = new JoystickButton
                            {
                                Button = (int)key.Offset,
                                IsAxis = false,
                                PovDirection = key.Value,
                                JoystickGuid = deviceInstance.InstanceGuid
                            };
                            var t = txt.Tag as JoystickButtons;
                            txt.ToolTip = deviceInstance.InstanceName;
                            t.BindNameDi = txt.Text;
                            t.DirectInputButton = button;
                        }
                    }
                    else if (key.Offset == JoystickOffset.X || key.Offset == JoystickOffset.Y ||
                        key.Offset == JoystickOffset.Z
                        || key.Offset == JoystickOffset.RotationX ||
                        key.Offset == JoystickOffset.RotationY || key.Offset == JoystickOffset.RotationZ ||
                        key.Offset == JoystickOffset.Sliders0 || key.Offset == JoystickOffset.Sliders1 ||
                        key.Offset == JoystickOffset.AccelerationX || key.Offset == JoystickOffset.AccelerationY ||
                        key.Offset == JoystickOffset.AccelerationZ)
                {
                        if (key.Value > short.MaxValue + 15000)
                        {
                            txt.Text = key.Offset + "+";
                            JoystickButton button = new JoystickButton
                            {
                                Button = (int)key.Offset,
                                IsAxis = true,
                                IsAxisMinus = false,
                                JoystickGuid = deviceInstance.InstanceGuid
                            };
                            var t = txt.Tag as JoystickButtons;
                            txt.ToolTip = deviceInstance.InstanceName;
                            t.DirectInputButton = button;
                            t.BindNameDi = txt.Text;
                        }
                        else if (key.Value < short.MaxValue - 15000)
                        {
                            txt.Text = key.Offset + "-";
                            JoystickButton button = new JoystickButton
                            {
                                Button = (int)key.Offset,
                                IsAxis = true,
                                IsAxisMinus = true,
                                JoystickGuid = deviceInstance.InstanceGuid
                            };
                            var t = txt.Tag as JoystickButtons;
                            txt.ToolTip = deviceInstance.InstanceName;
                            t.DirectInputButton = button;
                            t.BindNameDi = txt.Text;
                        }
                    }
                    else //if (key.Offset >= (JoystickOffset)48 && key.Offset <= (JoystickOffset)175)
                    {
                        if (key.Value == 128)
                        {
                            txt.Text = key.Offset.ToString();
                            JoystickButton button = new JoystickButton
                            {
                                Button = (int)key.Offset,
                                IsAxis = false,
                                JoystickGuid = deviceInstance.InstanceGuid
                            };
                            var t = txt.Tag as JoystickButtons;
                            txt.ToolTip = deviceInstance.InstanceName;
                            t.DirectInputButton = button;
                            t.BindNameDi = txt.Text;
                        }
                    }
                }));
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

        public void StopListening()
        {
            try
            {
                foreach (Joystick t in _joystickCollection)
                {
                    if(!t.IsDisposed)
                        t.Unacquire();
                }
            }
            catch (Exception)
            {
            }
            _joystickCollection.Clear();
            _stopListening = true;
        }
    }
}
