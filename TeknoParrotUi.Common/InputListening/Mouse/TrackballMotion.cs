using System;
using System.Threading;

namespace TeknoParrotUi.Common.InputListening.Mouse
{
    public static class TrackballMotion
    {
        private static readonly long[] X = new long[3], Y = new long[3];
        public static bool IsMapping(InputMapping mapping) => mapping is
            InputMapping.P1Trackball or InputMapping.P2Trackball or InputMapping.P3Trackball;
        public static void Reset()
        {
            for (var player = 0; player < 3; player++) { Interlocked.Exchange(ref X[player], 0); Interlocked.Exchange(ref Y[player], 0); }
        }
        public static void Add(int player, int x, int y)
        {
            if (player < 0 || player >= 3) return;
            Interlocked.Add(ref X[player], x); Interlocked.Add(ref Y[player], y);
        }
        public static uint Cumulative(int player, bool vertical) =>
            player < 0 || player >= 3 ? 0 : unchecked((uint)Interlocked.Read(ref (vertical ? ref Y[player] : ref X[player])));
        public static int Delta(int player, bool vertical, ref uint previous)
        {
            var current = Cumulative(player, vertical);
            var delta = unchecked((int)(current - previous));
            previous = current;
            return delta;
        }
    }
}
