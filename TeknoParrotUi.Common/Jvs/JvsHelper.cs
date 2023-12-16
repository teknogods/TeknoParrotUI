using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace TeknoParrotUi.Common.Jvs
{
    public static class JvsHelper
    {
        public static MemoryMappedFile StateSection;
        public static MemoryMappedViewAccessor StateView;

        static JvsHelper()
        {
            StateSection = MemoryMappedFile.CreateOrOpen("TeknoParrot_JvsState", 64);
            StateView = StateSection.CreateViewAccessor();
        }

        /// <summary>
        /// Calculates gas position.
        /// </summary>
        /// <param name="gas">Joystick axis value.</param>
        /// <param name="isFullAxis">Is Full Axis.</param>
        /// <param name="isReverseAxis">If we want to reverse the axis.</param>
        /// <returns>JVS friendly value.</returns>
        public static byte CalculateGasPos(int gas, bool isFullAxis, bool isReverseAxis, byte minValue = 0, byte maxValue = 255)
        {
            var value = 0;
            var divider = maxValue - minValue;

            if (isFullAxis)
                value = gas / (ushort.MaxValue / divider);
            else
                value = gas / (short.MaxValue / divider);

            value += minValue;

            if (isReverseAxis)
                value = (maxValue - value) + minValue;

            if (value < minValue)
                return minValue;

            if (value > maxValue)
                return maxValue;

            return (byte)value;
        }

        public static byte CalculateSto0ZWheelPos(int wheel, int stoozPercent, bool isXinput = false)
        {
            // DEADZONE STUFF
            if (isXinput)
                wheel += short.MaxValue; // because fuck minus
            // OFFSET VALUE FOR CALCULATIONS
            int lx = wheel - short.MaxValue;

            // SETUP
            //double deadzone = 0.25f * 32767; /* OLD sTo0z Fix STATIC WAY */
            double deadzone = ((double)stoozPercent / 100) * short.MaxValue;
            double magnitude = Math.Sqrt(lx * lx);
            double normalizedLX = lx / magnitude;
            double normalizedMagnitude = 0;

            // CALCULATE
            if (magnitude > deadzone)
            {
                if (magnitude > short.MaxValue) magnitude = short.MaxValue;

                magnitude -= deadzone;
                normalizedMagnitude = (normalizedLX * (magnitude / (short.MaxValue - deadzone))) + 1;
                var oldRange = 2;
                var newRange = 255;
                normalizedMagnitude = (normalizedMagnitude * newRange) / oldRange;
            }
            else
            {
                magnitude = 127.5;
                normalizedMagnitude = 127.5;
            }

            var finalMagnitude = Convert.ToByte(normalizedMagnitude);
            return finalMagnitude;
        }

        /// <summary>
        /// Calculates wheel position.
        /// </summary>
        /// <param name="wheel">Joystick axis value.</param>
        /// <param name="isXinput"></param>
        /// <param name="isSonic"></param>
        /// <param name="minValue">Minimum JVS Value.</param>
        /// <param name="maxValue">Maximum JVS Value.</param>
        /// <returns>JVS friendly value.</returns>
        public static byte CalculateWheelPos(int wheel, bool isXinput = false, bool isSonic = false, int minValue = 0, int maxValue = 255)
        {
            var divider = maxValue - minValue;
            if (isSonic)
                divider = 0xD0;
            if (isXinput)
            {
                wheel += short.MaxValue; // because fuck minus
            }
            var value = wheel / (ushort.MaxValue / divider);
            value += minValue;
            if (isSonic)
                value += 0x1D;
            return (byte)value;
        }

        /// <summary>
        /// Encodes a JVS package to be safe on the wire.
        /// </summary>
        /// <param name="packageBytes">Bytes, without sync code.</param>
        /// <returns>Encoded bytes.</returns>
        private static byte[] EncodePackage(List<byte> packageBytes)
        {
            var responseBytes = new List<byte>() { (byte)JVSPacket.SYNC_CODE };
            for (int i = 0; i < packageBytes.Count; i++)
            {
                var b = packageBytes[i];

                if (b == 0xD0)
                {
                    responseBytes.Add(0xD0);
                    responseBytes.Add(0xCF);
                }
                else if (b == 0xE0)
                {
                    responseBytes.Add(0xD0);
                    responseBytes.Add(0xDF);
                }
                else
                {
                    responseBytes.Add(b);
                }
            }

            return responseBytes.ToArray();
        }

        /// <summary>
        /// Crafts a valid JVS package with status and report.
        /// </summary>
        /// <param name="node">Target node.</param>
        /// <param name="bytes">package bytes.</param>
        /// <returns>Complete JVS package.</returns>
        public static byte[] CraftJvsPackageWithStatusAndReport(byte node, byte[] bytes)
        {
            if (bytes == null)
            {
                Debug.WriteLine("Error sent!");
                var errorBytes = new List<byte>
                {
                    (byte)JVSPacket.SYNC_CODE,
                    node,
                    0x09,
                    (byte)JVSStatus.UNKNOWN,
                    (byte)JVSReport.OK,
                    0x00,
                    0x00,
                    0x00,
                    0x00,
                    0x00,
                    0x01,
                    0x0D
                };
                return errorBytes.ToArray();
            }
            var packageBytes = new List<byte>
            {
                node,
                (byte) (bytes.Length + 3), // +3 because of Status bytes and CRC.
                (byte)JVSStatus.OK,
                (byte)JVSReport.OK
            };
            packageBytes.AddRange(bytes);
            packageBytes.Add(CalcChecksumAndAddStatusAndReport(0x00, bytes));
            return EncodePackage(packageBytes);
        }

        /// <summary>
        /// Crafts a valid JVS package.
        /// </summary>
        /// <param name="node">Target node.</param>
        /// <param name="bytes">package bytes.</param>
        /// <returns>Complete JVS package.</returns>
        public static byte[] CraftJvsPackage(byte node, byte[] bytes)
        {
            var packageBytes = new List<byte> { node, (byte)(bytes.Length + 1) };
            packageBytes.AddRange(bytes);
            packageBytes.Add(CalcChecksum(0x00, bytes, bytes.Length));

            return EncodePackage(packageBytes);
        }

        public static byte CalcChecksumAndAddStatusAndReport(int dest, byte[] bytes)
        {
            var packageForCalc = new List<byte>
            {
                (byte)JVSStatus.OK,
                (byte)JVSReport.OK
            };
            packageForCalc.AddRange(bytes);
            return CalcChecksum(dest, packageForCalc.ToArray(), packageForCalc.Count);
        }

        /// <summary>
        /// Calculates JVS checksum.
        /// </summary>
        /// <param name="dest">Destination node.</param>
        /// <param name="bytes">The data.</param>
        /// <param name="length">Length</param>
        /// <returns></returns>
        public static byte CalcChecksum(int dest, byte[] bytes, int length)
        {
            var csum = dest + length + 1;

            for (var i = 0; i < length; i++)
                csum = (csum + bytes[i]) % 256;

            return (byte)csum;
        }

        /// <summary>
        /// Converts byte array to string
        /// </summary>
        /// <param name="ba">Byte array.</param>
        /// <returns>Parsed string.</returns>
        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:X2} ", b);
            return hex.ToString();
        }
    }
}
