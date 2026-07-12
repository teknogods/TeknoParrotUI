using System.Collections.Generic;

namespace TeknoParrotUi.Common.InputListening.Keyboard
{
    /// <summary>
    /// Maps Linux evdev keyboard codes (input-event-codes.h KEY_*) to the Win32
    /// virtual-key based <see cref="Keys"/> enum used by RawInputButton bindings,
    /// so keyboard bindings behave identically on Windows (RawInput) and Linux
    /// (evdev). Modifiers map to the generic ShiftKey/ControlKey/Menu values —
    /// the same values Windows RawInput reports — so bindings captured on either
    /// platform stay compatible.
    /// </summary>
    public static class EvdevKeyMap
    {
        private static readonly Dictionary<ushort, Keys> Map = new Dictionary<ushort, Keys>
        {
            [1] = Keys.Escape,
            [2] = Keys.D1, [3] = Keys.D2, [4] = Keys.D3, [5] = Keys.D4, [6] = Keys.D5,
            [7] = Keys.D6, [8] = Keys.D7, [9] = Keys.D8, [10] = Keys.D9, [11] = Keys.D0,
            [12] = Keys.OemMinus, [13] = Keys.Oemplus, [14] = Keys.Back, [15] = Keys.Tab,
            [16] = Keys.Q, [17] = Keys.W, [18] = Keys.E, [19] = Keys.R, [20] = Keys.T,
            [21] = Keys.Y, [22] = Keys.U, [23] = Keys.I, [24] = Keys.O, [25] = Keys.P,
            [26] = Keys.OemOpenBrackets, [27] = Keys.OemCloseBrackets, [28] = Keys.Return,
            [29] = Keys.ControlKey,
            [30] = Keys.A, [31] = Keys.S, [32] = Keys.D, [33] = Keys.F, [34] = Keys.G,
            [35] = Keys.H, [36] = Keys.J, [37] = Keys.K, [38] = Keys.L,
            [39] = Keys.OemSemicolon, [40] = Keys.OemQuotes, [41] = Keys.Oemtilde,
            [42] = Keys.ShiftKey, [43] = Keys.OemPipe,
            [44] = Keys.Z, [45] = Keys.X, [46] = Keys.C, [47] = Keys.V, [48] = Keys.B,
            [49] = Keys.N, [50] = Keys.M,
            [51] = Keys.Oemcomma, [52] = Keys.OemPeriod, [53] = Keys.OemQuestion,
            [54] = Keys.ShiftKey,        // right shift
            [55] = Keys.Multiply,        // keypad *
            [56] = Keys.Menu,            // left alt
            [57] = Keys.Space,
            [58] = Keys.CapsLock,
            [59] = Keys.F1, [60] = Keys.F2, [61] = Keys.F3, [62] = Keys.F4, [63] = Keys.F5,
            [64] = Keys.F6, [65] = Keys.F7, [66] = Keys.F8, [67] = Keys.F9, [68] = Keys.F10,
            [69] = Keys.NumLock, [70] = Keys.Scroll,
            [71] = Keys.NumPad7, [72] = Keys.NumPad8, [73] = Keys.NumPad9, [74] = Keys.Subtract,
            [75] = Keys.NumPad4, [76] = Keys.NumPad5, [77] = Keys.NumPad6, [78] = Keys.Add,
            [79] = Keys.NumPad1, [80] = Keys.NumPad2, [81] = Keys.NumPad3,
            [82] = Keys.NumPad0, [83] = Keys.Decimal,
            [87] = Keys.F11, [88] = Keys.F12,
            [96] = Keys.Return,          // keypad enter
            [97] = Keys.ControlKey,      // right ctrl
            [98] = Keys.Divide,          // keypad /
            [99] = Keys.PrintScreen,
            [100] = Keys.Menu,           // right alt
            [102] = Keys.Home,
            [103] = Keys.Up,
            [104] = Keys.PageUp,
            [105] = Keys.Left,
            [106] = Keys.Right,
            [107] = Keys.End,
            [108] = Keys.Down,
            [109] = Keys.PageDown,
            [110] = Keys.Insert,
            [111] = Keys.Delete,
            [119] = Keys.Pause,
            [125] = Keys.LWin,
            [126] = Keys.RWin,
            [127] = Keys.Apps
        };

        /// <summary>Convert an evdev key code to a Keys value; Keys.None when unmapped.</summary>
        public static Keys ToKeys(ushort evdevCode) =>
            Map.TryGetValue(evdevCode, out var key) ? key : Keys.None;

        /// <summary>
        /// All evdev codes producing a Keys value (a Keys value can map to
        /// several codes, e.g. left/right Shift). Used by the X11 fallback to
        /// know which X keycodes (= evdev code + 8) to watch for a binding.
        /// </summary>
        public static IEnumerable<ushort> CodesFor(Keys key)
        {
            foreach (var pair in Map)
            {
                if (pair.Value == key)
                    yield return pair.Key;
            }
        }
    }
}
