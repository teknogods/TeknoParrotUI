using System;

namespace TeknoParrotUi.Common
{
    public class ParrotData
    {
        public bool UseSto0ZDrivingHack { get; set; }
        public int StoozPercent { get; set; }
        public bool UseMouse { get; set; }
        public bool XInputMode { get; set; }
        public int GunSensitivityPlayer1 { get; set; }
        public int GunSensitivityPlayer2 { get; set; }
        public bool FullAxisGas { get; set; }
        public bool FullAxisBrake { get; set; }
        public bool ReverseAxisGas { get; set; }
        public bool ReverseAxisBrake { get; set; }
        public string HapticDevice { get; set; }
        public bool UseHaptic { get; set; }
        public bool HapticThrustmasterFix { get; set; }

        public Int16 ConstantBase { get; set; }
        public Int16 SineBase { get; set; }
        public Int16 FrictionBase { get; set; }
        public Int16 SpringBase { get; set; }

        public string LastPlayed { get; set; }
    }
}
