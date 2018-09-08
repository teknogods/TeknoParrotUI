using SharpDX.DirectInput;

namespace TeknoParrotUi.Common
{
    public static class Lazydata
    {
        public static string GamePath { get; set; }
        public static ParrotData ParrotData { get; set; }
        public static Joystick Joystick { get; set; }
    }
}
