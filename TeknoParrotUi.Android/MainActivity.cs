using System;
using System.Linq;
using System.Timers;
using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening;

namespace TeknoParrotUi.Android
{
    /// <summary>
    /// On-device input test harness: exercises AndroidTouchListener +
    /// InputListenersManager end-to-end. Touch the screen (up to two fingers =
    /// two guns) and watch the live JVS analog bytes / trigger states that a
    /// game would receive. This validates the Android input stack without a
    /// game process.
    /// </summary>
    [Activity(Label = "TeknoParrot Input Test", MainLauncher = true,
        Theme = "@android:style/Theme.Material.NoActionBar.Fullscreen")]
    public class MainActivity : Activity
    {
        private readonly InputListenersManager _manager = new InputListenersManager();
        private AndroidTouchListener _touchListener;
        private TextView _stateText;
        private Timer _refreshTimer;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Register the Android head's listener factory with the shared manager.
            InputListenersManager.AndroidTouchListenerFactory = () =>
            {
                _touchListener = new AndroidTouchListener();
                return _touchListener;
            };

            var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
            _stateText = new TextView(this)
            {
                TextSize = 14,
                Typeface = global::Android.Graphics.Typeface.Monospace,
                Text = "Touch the screen (1-2 fingers = P1/P2 light guns)"
            };
            root.AddView(_stateText);
            SetContentView(root);

            // A synthetic gun-game profile: 0-255 axes, standard layout.
            var profile = new GameProfile
            {
                ProfileName = "AndroidInputTest",
                GameNameInternal = "Android Input Test",
                GunGame = true,
                xAxisMin = 0, xAxisMax = 255,
                yAxisMin = 0, yAxisMax = 255,
                ConfigValues = new System.Collections.Generic.List<FieldInformation>(),
                JoystickButtons = new System.Collections.Generic.List<JoystickButtons>()
            };

            _manager.Start(profile, profile.JoystickButtons, InputApi.SDL2);
            if (_touchListener != null)
                root.SetOnTouchListener(_touchListener);

            _refreshTimer = new Timer(100);
            _refreshTimer.Elapsed += (_, _) => RunOnUiThread(UpdateStateText);
            _refreshTimer.Start();
        }

        private void UpdateStateText()
        {
            var analog = string.Join(" ", InputCode.AnalogBytes.Take(16).Select(b => b.ToString("X2")));
            _stateText.Text =
                "TeknoParrot Android input test\n\n" +
                $"AnalogBytes[0-15]: {analog}\n" +
                $"P1 trigger: {InputCode.PlayerDigitalButtons[0].Button1 == true}\n" +
                $"P2 trigger: {InputCode.PlayerDigitalButtons[1].Button1 == true}\n\n" +
                "Touch = aim, press = trigger. Two fingers = two guns.";
        }

        protected override void OnDestroy()
        {
            _refreshTimer?.Stop();
            _manager.Stop();
            base.OnDestroy();
        }
    }
}
