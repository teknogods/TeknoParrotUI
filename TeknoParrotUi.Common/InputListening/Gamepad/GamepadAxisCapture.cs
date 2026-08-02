using System;

namespace TeknoParrotUi.Common.InputListening.Gamepad
{
    /// <summary>
    /// Selects the stick axis that moved furthest beyond its dead zone in one
    /// gamepad sample. This avoids a fixed X-before-Y polling bias when a real
    /// stick produces small movement on both axes during control assignment.
    /// </summary>
    public static class GamepadAxisCapture
    {
        public static bool TrySelectDominantThumb(
            in XiGamepad current,
            in XiGamepad previous,
            int inputIndex,
            out XInputButton binding,
            out string displayName)
        {
            var best = default(Candidate);
            Consider(ref best, current.LeftThumbY, previous.LeftThumbY,
                XiGamepad.LeftThumbDeadZone, Axis.LeftY);
            Consider(ref best, current.LeftThumbX, previous.LeftThumbX,
                XiGamepad.LeftThumbDeadZone, Axis.LeftX);
            Consider(ref best, current.RightThumbY, previous.RightThumbY,
                XiGamepad.RightThumbDeadZone, Axis.RightY);
            Consider(ref best, current.RightThumbX, previous.RightThumbX,
                XiGamepad.RightThumbDeadZone, Axis.RightX);

            if (!best.Found)
            {
                binding = null;
                displayName = string.Empty;
                return false;
            }

            binding = new XInputButton
            {
                IsButton = false,
                XInputIndex = inputIndex,
                IsAxisMinus = best.Value < 0,
                IsLeftThumbX = best.Axis == Axis.LeftX,
                IsLeftThumbY = best.Axis == Axis.LeftY,
                IsRightThumbX = best.Axis == Axis.RightX,
                IsRightThumbY = best.Axis == Axis.RightY
            };
            displayName = best.Axis switch
            {
                Axis.LeftX => "LeftThumbX",
                Axis.LeftY => "LeftThumbY",
                Axis.RightX => "RightThumbX",
                Axis.RightY => "RightThumbY",
                _ => string.Empty
            } + (best.Value < 0 ? "-" : "+");
            return true;
        }

        private static void Consider(
            ref Candidate best,
            short value,
            short previous,
            short deadZone,
            Axis axis)
        {
            if (value == previous)
                return;

            var magnitude = Math.Abs((int)value);
            if (magnitude <= deadZone)
                return;

            var range = short.MaxValue - deadZone;
            var score = (long)(magnitude - deadZone) * 1_000_000 / range;
            var change = Math.Abs((int)value - previous);
            if (best.Found &&
                (score < best.Score || score == best.Score && change <= best.Change))
                return;

            best = new Candidate(true, axis, value, score, change);
        }

        private enum Axis
        {
            LeftX,
            LeftY,
            RightX,
            RightY
        }

        private readonly record struct Candidate(
            bool Found,
            Axis Axis,
            short Value,
            long Score,
            int Change);
    }
}
