using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TeknoParrotUi.Common;
using TeknoParrotUi.Common.Jvs;

namespace TeknoParrotUi.Helpers
{
    public static class FfbHelper
    {
        private static byte _lastValue = 0;
        private static Int32 _lastValueInt32 = 0;

        public static void UseForceFeedback(ParrotData _parrotData, ref bool endCheckBox)
        {
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            var f = new ForceFeedbackJesus.ForceFeedbackJesus();
            var initResult = f.InitializeHaptic(_parrotData.HapticThrustmasterFix, _parrotData.HapticDevice, _parrotData.ConstantBase, _parrotData.SineBase, _parrotData.FrictionBase, _parrotData.SpringBase, 0);
            if (initResult != string.Empty)
            {
                MessageBox.Show(initResult, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            f.TriggerSpringEffect(4);
            if (InputCode.ButtonMode == EmulationProfile.NamcoWmmt5)
            {
                while (!endCheckBox)
                {
                    //var val1 = BitConverter.ToSingle(BitConverter.GetBytes(JvsHelper.StateView.ReadUInt32(8)), 0) *
                    //           10f;
                    var val2 = BitConverter.ToSingle(BitConverter.GetBytes(JvsHelper.StateView.ReadUInt32(12)), 0) *
                               10f;
                    var val3 = BitConverter.ToSingle(BitConverter.GetBytes(JvsHelper.StateView.ReadUInt32(16)), 0) *
                               10f;

                    //if(_parrotData.HapticThrustmasterFix)
                    //    f.TriggerSpringEffectInfinite(6);

                    f.TriggerFrictionEffectInfinite(val3);
                    //f.TriggerSpringEffectInfinite(val1);
                    f.TriggerSpringEffectInfinite(val2);
                    float my4ByteFloat = BitConverter.ToSingle(BitConverter.GetBytes(JvsHelper.StateView.ReadUInt32(20)), 0);
                    if (my4ByteFloat < 0)
                    {
                        // negative value
                        f.TriggerRightRollEffect(-my4ByteFloat * 10f);
                    }
                    else if (my4ByteFloat > 0)
                    {
                        // positive value
                        f.TriggerLeftRollEffect(my4ByteFloat * 10f);
                    }

                    Thread.Sleep(10);
                }
            }

            if (InputCode.ButtonMode == EmulationProfile.WackyRaces || InputCode.ButtonMode == EmulationProfile.ChaseHq2)
            {
                while (!endCheckBox)
                {
                    byte value = JvsHelper.StateView.ReadByte(8);
                    if (_lastValue != value)
                    {
                        ffbEffectSender(f, value);
                        _lastValue = value;
                    }
                    Thread.Sleep(10);
                }
            }

            if (InputCode.ButtonMode == EmulationProfile.SegaInitialD)
            {
                while (!endCheckBox)
                {
                    Int32 value = JvsHelper.StateView.ReadInt32(8);
                    if (_lastValueInt32 != value)
                    {
                        ffbEffectSender(f, value);
                        _lastValueInt32 = value;
                    }
                    Thread.Sleep(10);
                }
            }


            if (InputCode.ButtonMode == EmulationProfile.Outrun2SPX)
            {
                f.TriggerSpringEffectInfinite(8);
                f.TriggerFrictionEffectInfinite(8);
                while (!endCheckBox)
                {
                    int value = JvsHelper.StateView.ReadInt32(8);
                    if (value > 0x100)
                    {
                        ffbEffectSender(f, (byte)(value - 0x100));
                        JvsHelper.StateView.Write(8, value - 0x100);
                    }

                    Thread.Sleep(10);
                }
            }

            if (InputCode.ButtonMode == EmulationProfile.EuropaRSegaRally3 || InputCode.ButtonMode == EmulationProfile.EuropaRFordRacing)
            {
                while (!endCheckBox)
                {
                    byte value = JvsHelper.StateView.ReadByte(8);
                    if (_lastValue != value)
                    {
                        ffbEffectSender(f, value);
                        _lastValue = value;
                    }
                    Thread.Sleep(10);
                }
            }
            if (InputCode.ButtonMode == EmulationProfile.SegaRacingClassic)
            {
                while (!endCheckBox)
                {
                    byte value = JvsHelper.StateView.ReadByte(9);
                    if (_lastValue != value)
                    {
                        ffbEffectSender(f, value);
                        _lastValue = value;
                    }
                    Thread.Sleep(10);
                }
            }

            f.Uninitialize();
        }

        /// <summary>
        /// 8 Stages of power.
        /// </summary>
        private static void ffbEffectSender(ForceFeedbackJesus.ForceFeedbackJesus jesus, Int32 value)
        {
            switch (InputCode.ButtonMode)
            {
                case EmulationProfile.SegaInitialD:
                    HandleIdFfb(jesus, value);
                    break;
                case EmulationProfile.ChaseHq2:
                    HandleChaseHq2Ffb(jesus, (byte)value);
                    break;
                case EmulationProfile.WackyRaces:
                    HandleWackyFfb(jesus, (byte)value);
                    break;
                case EmulationProfile.SegaRacingClassic:
                    HandleSrcFfb(jesus, (byte)value);
                    break;
                case EmulationProfile.EuropaRSegaRally3:
                    HandleSr3Ffb(jesus, (byte)value);
                    break;
                case EmulationProfile.Outrun2SPX:
                    HandleOr2Ffb(jesus, (byte)value);
                    break;
                case EmulationProfile.EuropaRFordRacing:
                    HandleFordRacingFfb(jesus, (byte)value);
                    break;
            }
        }

        private static void HandleChaseHq2Ffb(ForceFeedbackJesus.ForceFeedbackJesus jesus, byte value)
        {
            if (value >= 1 && value <= 15)
            {
                // Positive
                float v = (float)value / 2;
                jesus.TriggerLeftRollEffect(v);
            }
            if (value >= 100 && value <= 115)
            {
                // Negative
                float v = (float)(value - 100) / 2;
                jesus.TriggerRightRollEffect(v);
            }
            if (value == 0)
            {
                jesus.StopRollEffects();
                jesus.TriggerSpringEffect(4);
            }
        }

        private static void HandleWackyFfb(ForceFeedbackJesus.ForceFeedbackJesus jesus, byte value)
        {
            if (value >= 1 && value <= 15)
            {
                // Positive
                float v = (float)value / 2;
                jesus.TriggerLeftRollEffect(v);
            }
            if (value >= 100 && value <= 115)
            {
                // Negative
                float v = (float)(value - 100) / 2;
                jesus.TriggerRightRollEffect(v);
            }
            if (value == 0)
            {
                jesus.StopRollEffects();
            }
        }

        private static void HandleIdFfb(ForceFeedbackJesus.ForceFeedbackJesus jesus, int value)
        {
            switch ((byte)((value >> 16) & 0xFF))
            {
                case 4:
                    {
                        byte direction = (byte)((byte)(value >> 8) & 0xF);
                        byte strength = (byte)((byte)(value) & 0xFF);

                        if (direction == 0)
                        {
                            strength |= 0x80;
                            strength = (byte)-strength;
                            float v = strength;
                            jesus.TriggerRightRollEffect(v / 12.5f);
                        }
                        else
                        {
                            float v = strength;
                            jesus.TriggerLeftRollEffect(v / 12.5f);
                        }
                    }
                    break;
                case 5:
                    {
                        float v = value & 0xFF;
                        jesus.TriggerSineEffect(v / 4.5f);
                    }
                    break;
                case 6:
                    {
                        float v = value & 0xFF;
                        jesus.TriggerFrictionEffect(v / 4.5f);
                    }
                    break;
            }
        }

        private static void HandleFordRacingFfb(ForceFeedbackJesus.ForceFeedbackJesus jesus, byte value)
        {
            if (value == 0)
            {
                // Reset effects
                jesus.StopRollEffects();
            }
            else if (value >= 1 && value <= 0x0F)
            {
                // Right
                float v = value;
                jesus.TriggerRightRollEffect(v / 4);
            }
            else if (value >= 0x10 && value <= 0x1E)
            {
                // Left
                float v = value - 0x0F;
                jesus.TriggerLeftRollEffect(v / 4);
            }
        }
        private static void HandleOr2Ffb(ForceFeedbackJesus.ForceFeedbackJesus jesus, byte value)
        {
            //if (value == 0)
            //{
            //    // Reset effects
            //    jesus.StopRollEffects();
            //}
            if (value >= 1 && value <= 0x0F)
            {
                // Right
                float v = value;
                jesus.TriggerLeftRollEffect(v);
            }
            else if (value >= 0x10 && value <= 0x1E)
            {
                // Left
                float v = value - 0x0F;
                jesus.TriggerRightRollEffect(v);
            }
        }

        private static void HandleSr3Ffb(ForceFeedbackJesus.ForceFeedbackJesus jesus, byte value)
        {
            if (value == 0)
            {
                // Reset effects
                jesus.StopRollEffects();
            }
            else if (value >= 1 && value <= 0x0F)
            {
                // Right
                float v = value;
                jesus.TriggerLeftRollEffect(v / 4);
            }
            else if (value >= 0x10 && value <= 0x1E)
            {
                // Left
                float v = value - 0x0F;
                jesus.TriggerRightRollEffect(v / 4);
            }
        }

        private static void HandleSrcFfb(ForceFeedbackJesus.ForceFeedbackJesus jesus, byte value)
        {
            if (value >= 0x98 && value <= 0x9F)
            {
                // Constant Force Right
                jesus.TriggerRightRollEffect(value - 0x97);
            }

            if (value >= 0xA8 && value <= 0xAF)
            {
                // Constant Force Left
                jesus.TriggerLeftRollEffect(value - 0xA7);
            }

            if (value >= 0xB8 && value <= 0xBF)
            {
                // Sine Wave
                jesus.TriggerSineEffect(value - 0xB7);
            }

            if (value >= 0xC0 && value <= 0xC7)
            {
                // Spring Force
                jesus.TriggerSpringEffect(value - 0xBF);
            }

            if (value >= 0xD8 && value <= 0xDF)
            {
                // Friction
                jesus.TriggerFrictionEffect(value - 0xD7);
            }
        }
    }
}
