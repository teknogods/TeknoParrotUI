using System;

namespace TeknoParrotUi.Common.InputListening.Mouse
{
    /// <summary>
    /// Pure gun-position math shared by cross-platform mouse listeners.
    /// Reproduces the fullscreen analog-byte layouts of
    /// InputListenerRawInput.HandleRawInputGun exactly (standard, inverted,
    /// Luigi's Mansion / Gunslinger Stratos 3 variants, 8-bit and 16-bit).
    /// Kept side-effect free so the port can be verified against the original
    /// without hardware (see Tools/InputMethodAudit gun-math-test).
    /// </summary>
    public static class GunAnalogMath
    {
        public readonly struct GunConfig
        {
            public GunConfig(float minX, float maxX, float minY, float maxY,
                bool is16Bit, bool invertedMouseAxis, bool luigiLayout, bool gunslinger)
            {
                MinX = minX; MaxX = maxX; MinY = minY; MaxY = maxY;
                Is16Bit = is16Bit;
                InvertedMouseAxis = invertedMouseAxis;
                LuigiLayout = luigiLayout;
                Gunslinger = gunslinger;
            }

            public float MinX { get; }
            public float MaxX { get; }
            public float MinY { get; }
            public float MaxY { get; }
            public bool Is16Bit { get; }
            public bool InvertedMouseAxis { get; }
            /// <summary>Luigi's Mansion / Gunslinger byte layout (non-complemented, swapped).</summary>
            public bool LuigiLayout { get; }
            /// <summary>Gunslinger Stratos 3 uses shifted player analog slots.</summary>
            public bool Gunslinger { get; }
        }

        /// <summary>Analog byte index pair (Y-slot, X-slot) for a player.</summary>
        public static (byte IndexA, byte IndexB) GetPlayerIndices(int player, bool gunslinger)
        {
            return player switch
            {
                1 => gunslinger ? ((byte)12, (byte)14) : ((byte)4, (byte)6),
                2 => ((byte)8, (byte)10),
                3 => ((byte)12, (byte)14),
                _ => gunslinger ? ((byte)8, (byte)10) : ((byte)0, (byte)2)
            };
        }

        /// <summary>Convert a normalized (0..1) factor pair to game units.</summary>
        public static (ushort X, ushort Y) ToGameUnits(float factorX, float factorY, in GunConfig cfg)
        {
            float minX = cfg.MinX, maxX = cfg.MaxX, minY = cfg.MinY, maxY = cfg.MaxY;
            if (cfg.Is16Bit)
            {
                // Scale 8-bit ranges (0-255) to 16-bit (0-65535), same as the Windows listener
                if (maxX <= 255 && minX >= 0) { minX *= 257f; maxX *= 257f; }
                if (maxY <= 255 && minY >= 0) { minY *= 257f; maxY *= 257f; }
            }
            var x = (ushort)Math.Round(minX + factorX * (maxX - minX));
            var y = (ushort)Math.Round(minY + factorY * (maxY - minY));
            return (x, y);
        }

        /// <summary>
        /// Write a normalized (0..1) gun position into the analog byte array with
        /// the exact layout of the Windows RawInput listener.
        /// </summary>
        public static void Write(byte[] analogBytes, int player, float factorX, float factorY, in GunConfig cfg)
        {
            var (x, y) = ToGameUnits(factorX, factorY, cfg);
            var (indexA, indexB) = GetPlayerIndices(player, cfg.Gunslinger);

            if (cfg.Is16Bit)
            {
                if (cfg.LuigiLayout)
                {
                    WriteU16(analogBytes, indexB, x);
                    WriteU16(analogBytes, indexA, y);
                }
                else if (cfg.InvertedMouseAxis)
                {
                    WriteU16(analogBytes, indexA, x);
                    WriteU16(analogBytes, indexB, y);
                }
                else
                {
                    WriteU16(analogBytes, indexB, (ushort)~x);
                    WriteU16(analogBytes, indexA, (ushort)~y);
                }
            }
            else
            {
                if (cfg.LuigiLayout)
                {
                    analogBytes[indexB] = (byte)x;
                    analogBytes[indexA] = (byte)y;
                }
                else if (cfg.InvertedMouseAxis)
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

        private static void WriteU16(byte[] analogBytes, byte index, ushort value)
        {
            analogBytes[index] = (byte)(value >> 8);
            analogBytes[index + 1] = (byte)(value & 0xFF);
        }
    }
}
