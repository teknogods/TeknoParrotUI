using SharpDX;
using SharpDX.DirectInput;
using SharpDX.Multimedia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TeknoParrotUi.Common;
using DeviceType = SharpDX.DirectInput.DeviceType;
using Key = SharpDX.DirectInput.Key;

namespace TeknoParrotUi.Helpers
{
    public class JoystickControlDirectInput
    {
        private List<Joystick> _joystickCollection = new List<Joystick>();
        private List<Thread> _joystickThreads = new List<Thread>();
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

        private volatile bool _stopListening;
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
                var allDevices = _directInput.GetDevices().ToList();
                Trace.WriteLine($"Total devices found: {allDevices.Count}");

                foreach (var device in allDevices)
                {
                    Trace.WriteLine($"Device: {device.InstanceName}");
                    Trace.WriteLine($"  Type: {device.Type}");
                    Trace.WriteLine($"  UsagePage: {device.UsagePage:X}");
                    Trace.WriteLine($"  Usage: {device.Usage:X}");
                    Trace.WriteLine($"  ProductName: {device.ProductName}");
                    Trace.WriteLine($"  IsHumanInterfaceDevice: {device.IsHumanInterfaceDevice}");
                    Trace.WriteLine("  ------------------");
                }
                devices.AddRange(_directInput.GetDevices().Where(x => x.Type != DeviceType.Mouse && x.UsagePage != UsagePage.VendorDefinedBegin && x.Usage != UsageId.AlphanumericBitmapSizeX && x.Usage != UsageId.AlphanumericAlphanumericDisplay && x.UsagePage != unchecked((UsagePage)0xffffff43) && x.UsagePage != UsagePage.Vr).ToList());
            }

            foreach (var t in devices)
            {
                var joystick = new Joystick(new DirectInput(), t.InstanceGuid);
                Trace.WriteLine($"Listening to {t.InstanceName}");
                var thread = new Thread(() => SpawnDirectInputListener(joystick, t));
                thread.Start();
                Thread.Sleep(10);
                _joystickThreads.Add(thread);
            }
        }

        private void SpawnDirectInputListener(Joystick joystick, DeviceInstance deviceInstance)
        {
            _joystickCollection.Add(joystick);
            joystick.Properties.BufferSize = 512;

            try { joystick.Acquire(); }
            catch (SharpDXException ex)
            {
                Trace.WriteLine($"Failed to acquire joystick: {ex.Message}");
            }
            try
            {
                while (!_stopListening)
                {
                    try
                    {
                        joystick.Poll();

                        if (_stopListening) break;

                        var datas = joystick.GetBufferedData();

                        if (_stopListening) break;

                        foreach (var state in datas)
                        {
                            SetTextBoxText(state, deviceInstance);
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Error: {ex.Message}");
                    }

                    Thread.Sleep(10);
                }
                Trace.Write($"Exiting thread for {joystick.Properties.InstanceName}");
                try { joystick.Unacquire(); } catch { }
            }
            catch (Exception)
            {
                Thread.Sleep(10);
            }
            Trace.WriteLine("Unacquired joystick");
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
                        txt.ToolTip = deviceInstance.InstanceName;
                        txt.Text = deviceInstance.Type + " " + inputText;

                        var t = txt.Tag as JoystickButtons;
                        t.DirectInputButton = button;
                        t.BindNameDi = txt.Text;
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
            _stopListening = true;
            Thread.Sleep(100);
            _joystickCollection.Clear();
            _joystickThreads.Clear();
        }
    }
}
