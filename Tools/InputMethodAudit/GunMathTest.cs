using System;
using System.Collections.Generic;
using TeknoParrotUi.Common.InputListening.Mouse;

namespace InputMethodAudit
{
    /// <summary>
    /// Port-parity test for the Linux gun math (Phase 2 verification without hardware).
    /// The oracle below is transcribed verbatim from the byte-layout logic of
    /// InputListenerRawInput.HandleRawInputGun (Windows); GunAnalogMath must
    /// produce identical bytes for every combination.
    /// Usage: dotnet run --project Tools/InputMethodAudit -- gun-math-test
    /// </summary>
    internal static class GunMathTest
    {
        public static int Run()
        {
            int cases = 0, failures = 0;

            var ranges = new (float minX, float maxX, float minY, float maxY)[]
            {
                (0, 255, 0, 255),      // default 8-bit range
                (20, 235, 40, 215),    // calibrated sub-range
                (0, 65535, 0, 65535)   // native 16-bit range (no rescale)
            };
            var factors = new (float x, float y)[] { (0f, 0f), (0.25f, 0.75f), (0.5f, 0.5f), (1f, 1f) };

            foreach (var range in ranges)
            foreach (var is16Bit in new[] { false, true })
            foreach (var layout in new[] { "standard", "inverted", "luigi", "gunslinger" })
            foreach (var player in new[] { 0, 1, 2, 3 })
            foreach (var f in factors)
            {
                bool inverted = layout == "inverted";
                bool luigi = layout == "luigi";
                bool gunslinger = layout == "gunslinger";

                var expected = new byte[32];
                var actual = new byte[32];

                Oracle(expected, player, f.x, f.y, range.minX, range.maxX, range.minY, range.maxY,
                    is16Bit, inverted, luigi, gunslinger);

                var cfg = new GunAnalogMath.GunConfig(range.minX, range.maxX, range.minY, range.maxY,
                    is16Bit, inverted, luigi || gunslinger, gunslinger);
                GunAnalogMath.Write(actual, player, f.x, f.y, cfg);

                cases++;
                if (!AreEqual(expected, actual))
                {
                    failures++;
                    Console.WriteLine($"FAIL layout={layout} p={player} 16bit={is16Bit} range=({range.minX}-{range.maxX}) f=({f.x},{f.y})");
                    Console.WriteLine($"  expected: {BitConverter.ToString(expected, 0, 16)}");
                    Console.WriteLine($"  actual:   {BitConverter.ToString(actual, 0, 16)}");
                }
            }

            failures += KeyMapSpotChecks();

            Console.WriteLine($"\nGun math parity: {cases} cases, {failures} failures");
            return failures == 0 ? 0 : 1;
        }

        /// <summary>
        /// Oracle transcribed from InputListenerRawInput.HandleRawInputGun
        /// (fullscreen path): game-unit conversion, per-player index selection
        /// and all byte layout branches.
        /// </summary>
        private static void Oracle(byte[] analogBytes, int player, float factorX, float factorY,
            float minX, float maxX, float minY, float maxY,
            bool is16Bit, bool invertedMouseAxis, bool isLuigisMansion, bool isGunslinger)
        {
            // --- game unit conversion (original lines, PrimevalHunt branches omitted) ---
            if (is16Bit)
            {
                if (maxX <= 255 && minX >= 0)
                {
                    minX = minX * 257.0f;
                    maxX = maxX * 257.0f;
                }
                if (maxY <= 255 && minY >= 0)
                {
                    minY = minY * 257.0f;
                    maxY = maxY * 257.0f;
                }
            }
            ushort x = (ushort)Math.Round(minX + factorX * (maxX - minX));
            ushort y = (ushort)Math.Round(minY + factorY * (maxY - minY));

            // --- per-player index selection (original if/else chain) ---
            byte indexA = 0;
            byte indexB = 0;
            if (player == 0)
            {
                indexA = 0;
                indexB = 2;
                if (isGunslinger) { indexA = 8; indexB = 10; }
            }
            else if (player == 1)
            {
                indexA = 4;
                indexB = 6;
                if (isGunslinger) { indexA = 12; indexB = 14; }
            }
            else if (player == 2) { indexA = 8; indexB = 10; }
            else if (player == 3) { indexA = 12; indexB = 14; }

            // --- byte layouts (original branches) ---
            if (is16Bit)
            {
                if (isLuigisMansion || isGunslinger)
                {
                    analogBytes[indexB] = (byte)(x >> 8);
                    analogBytes[indexB + 1] = (byte)(x & 0xFF);
                    analogBytes[indexA] = (byte)(y >> 8);
                    analogBytes[indexA + 1] = (byte)(y & 0xFF);
                }
                else if (invertedMouseAxis)
                {
                    analogBytes[indexA] = (byte)(x >> 8);
                    analogBytes[indexA + 1] = (byte)(x & 0xFF);
                    analogBytes[indexB] = (byte)(y >> 8);
                    analogBytes[indexB + 1] = (byte)(y & 0xFF);
                }
                else
                {
                    ushort invertedX = (ushort)~x;
                    ushort invertedY = (ushort)~y;
                    analogBytes[indexB] = (byte)(invertedX >> 8);
                    analogBytes[indexB + 1] = (byte)(invertedX & 0xFF);
                    analogBytes[indexA] = (byte)(invertedY >> 8);
                    analogBytes[indexA + 1] = (byte)(invertedY & 0xFF);
                }
            }
            else
            {
                if (isLuigisMansion || isGunslinger)
                {
                    analogBytes[indexB] = (byte)x;
                    analogBytes[indexA] = (byte)y;
                }
                else if (invertedMouseAxis)
                {
                    analogBytes[indexA] = (byte)x;
                    analogBytes[indexB] = (byte)y;
                }
                else
                {
                    analogBytes[indexB] = (byte)~x;
                    analogBytes[indexA] = (byte)~y;
                }
            }
        }

        private static int KeyMapSpotChecks()
        {
            var expectations = new List<(ushort code, TeknoParrotUi.Common.Keys key)>
            {
                (1, TeknoParrotUi.Common.Keys.Escape),
                (2, TeknoParrotUi.Common.Keys.D1),
                (11, TeknoParrotUi.Common.Keys.D0),
                (16, TeknoParrotUi.Common.Keys.Q),
                (28, TeknoParrotUi.Common.Keys.Return),
                (30, TeknoParrotUi.Common.Keys.A),
                (44, TeknoParrotUi.Common.Keys.Z),
                (57, TeknoParrotUi.Common.Keys.Space),
                (59, TeknoParrotUi.Common.Keys.F1),
                (88, TeknoParrotUi.Common.Keys.F12),
                (103, TeknoParrotUi.Common.Keys.Up),
                (108, TeknoParrotUi.Common.Keys.Down),
                (105, TeknoParrotUi.Common.Keys.Left),
                (106, TeknoParrotUi.Common.Keys.Right),
                (999, TeknoParrotUi.Common.Keys.None) // unmapped
            };

            int failures = 0;
            foreach (var (code, expected) in expectations)
            {
                var actual = TeknoParrotUi.Common.InputListening.Keyboard.EvdevKeyMap.ToKeys(code);
                if (actual != expected)
                {
                    failures++;
                    Console.WriteLine($"FAIL keymap: evdev {code} -> {actual}, expected {expected}");
                }
            }
            Console.WriteLine($"Key map spot checks: {expectations.Count} cases, {failures} failures");
            return failures;
        }

        private static bool AreEqual(byte[] a, byte[] b)
        {
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }
    }
}
