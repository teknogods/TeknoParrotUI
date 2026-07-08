using System;
using System.Collections.Generic;
using System.Linq;
using Android.Views;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.InputListening;
using TeknoParrotUi.Common.InputListening.Mouse;

namespace TeknoParrotUi.Android
{
    /// <summary>
    /// Android gun-game touch listener (Phase 3 of the cross-platform input
    /// refactor). Converts touch positions on the game surface into the same
    /// analog byte layouts as the Windows RawInput listener, via the shared
    /// (oracle-verified) <see cref="GunAnalogMath"/>.
    ///
    /// Multi-touch: the first two active pointers map to Player 1 and Player 2
    /// light guns. A pointer's press state drives that player's Button1
    /// (trigger), matching the evdev default map on Linux.
    ///
    /// The hosting Activity/View forwards MotionEvents via <see cref="OnTouch"/>.
    /// </summary>
    public class AndroidTouchListener : Java.Lang.Object, IInputListener, View.IOnTouchListener
    {
        public string Name => "AndroidTouch";
        public bool IsSupported => OperatingSystem.IsAndroid();

        private GunAnalogMath.GunConfig _config;
        private bool _started;

        // pointer id -> player slot (0/1), first-come first-served
        private readonly Dictionary<int, int> _pointerPlayers = new Dictionary<int, int>();

        public void Start(GameProfile gameProfile, List<JoystickButtons> joystickButtons)
        {
            bool luigi = gameProfile.EmulationProfile == EmulationProfile.LuigisMansion;
            bool gunslinger = gameProfile.EmulationProfile == EmulationProfile.GunslingerStratos3;
            _config = new GunAnalogMath.GunConfig(
                gameProfile.xAxisMin, gameProfile.xAxisMax,
                gameProfile.yAxisMin, gameProfile.yAxisMax,
                gameProfile.Use16BitAnalog, gameProfile.InvertedMouseAxis,
                luigi || gunslinger, gunslinger);

            _pointerPlayers.Clear();
            _started = true;

            // Center crosshairs on start (fullscreen semantics, same as evdev listener)
            for (int player = 0; player < 4; player++)
                GunAnalogMath.Write(InputCode.AnalogBytes, player, 0.5f, 0.5f, _config);
        }

        /// <summary>View.IOnTouchListener entry point — attach with view.SetOnTouchListener(listener).</summary>
        public bool OnTouch(View v, MotionEvent e)
        {
            if (!_started || v == null || e == null || v.Width == 0 || v.Height == 0)
                return false;

            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                case MotionEventActions.PointerDown:
                {
                    int index = e.ActionIndex;
                    int pointerId = e.GetPointerId(index);
                    int player = AssignPlayer(pointerId);
                    if (player >= 0)
                    {
                        UpdateAim(v, e, index, player);
                        SetTrigger(player, true);
                    }
                    break;
                }
                case MotionEventActions.Move:
                {
                    for (int index = 0; index < e.PointerCount; index++)
                    {
                        if (_pointerPlayers.TryGetValue(e.GetPointerId(index), out int player))
                            UpdateAim(v, e, index, player);
                    }
                    break;
                }
                case MotionEventActions.Up:
                case MotionEventActions.PointerUp:
                {
                    int pointerId = e.GetPointerId(e.ActionIndex);
                    if (_pointerPlayers.TryGetValue(pointerId, out int player))
                    {
                        SetTrigger(player, false);
                        _pointerPlayers.Remove(pointerId);
                    }
                    break;
                }
                case MotionEventActions.Cancel:
                {
                    foreach (var player in _pointerPlayers.Values)
                        SetTrigger(player, false);
                    _pointerPlayers.Clear();
                    break;
                }
            }
            return true;
        }

        private int AssignPlayer(int pointerId)
        {
            if (_pointerPlayers.TryGetValue(pointerId, out int existing))
                return existing;
            for (int player = 0; player < 2; player++)
            {
                if (!_pointerPlayers.ContainsValue(player))
                {
                    _pointerPlayers[pointerId] = player;
                    return player;
                }
            }
            return -1; // more than 2 simultaneous pointers — ignored
        }

        private void UpdateAim(View v, MotionEvent e, int pointerIndex, int player)
        {
            float factorX = Math.Clamp(e.GetX(pointerIndex) / v.Width, 0f, 1f);
            float factorY = Math.Clamp(e.GetY(pointerIndex) / v.Height, 0f, 1f);
            GunAnalogMath.Write(InputCode.AnalogBytes, player, factorX, factorY, _config);
        }

        private static void SetTrigger(int player, bool pressed)
        {
            if (player == 0)
                InputCode.PlayerDigitalButtons[0].Button1 = pressed;
            else if (player == 1)
                InputCode.PlayerDigitalButtons[1].Button1 = pressed;
        }

        public void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Not applicable on Android.
        }

        public void Stop()
        {
            _started = false;
            foreach (var player in _pointerPlayers.Values.Distinct())
                SetTrigger(player, false);
            _pointerPlayers.Clear();
        }
    }
}
