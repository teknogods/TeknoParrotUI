using System;
using System.Linq;

namespace TeknoParrotUi.Common.InputProfiles.Helpers
{
    public static class LightGunAxisHelper
    {
        public static void InitializeAxes(byte[] values, GameProfile profile)
        {
            ushort centerX = CalculatePosition(0.5, profile.xAxisMin, profile.xAxisMax);
            ushort centerY = CalculatePosition(0.5, profile.yAxisMin, profile.yAxisMax);
            var inputApi = profile.ConfigValues?.FirstOrDefault(field => field.FieldName == "Input API");
            bool rawInput = inputApi?.FieldValue == "RawInput" ||
                (inputApi?.FieldValue == "MergedInput" && inputApi.FieldOptions?.Contains("RawInput") == true);
            bool relativeInput = profile.ConfigValues?.Any(field =>
                field.FieldName == "Use Relative Input" && field.FieldValue == "1") == true;
            bool swapped = !profile.InvertedMouseAxis && (rawInput || relativeInput);
            if (!profile.InvertedMouseAxis)
            {
                centerX = (ushort)(ushort.MaxValue - centerX);
                centerY = (ushort)(ushort.MaxValue - centerY);
            }

            for (int player = 0; player < 4; ++player)
            {
                WriteAxis(values, player * 4 + (swapped ? 2 : 0), centerX);
                WriteAxis(values, player * 4 + (swapped ? 0 : 2), centerY);
            }
        }

        public static ushort CalculatePosition(double factor, double min, double max)
        {
            // Keep existing profile calibration ranges, but expand before rounding.
            if (min >= byte.MinValue && min <= byte.MaxValue &&
                max >= byte.MinValue && max <= byte.MaxValue)
            {
                min *= 257.0;
                max *= 257.0;
            }

            factor = Math.Max(0.0, Math.Min(1.0, factor));
            double position = min + factor * (max - min);
            return (ushort)Math.Round(Math.Max(ushort.MinValue, Math.Min(ushort.MaxValue, position)));
        }

        public static void WriteAxis(byte[] values, int index, ushort value)
        {
            // ACIO and the existing 16-bit lightgun layout store the high byte first.
            values[index] = (byte)(value >> 8);
            values[index + 1] = (byte)value;
        }
    }
}
