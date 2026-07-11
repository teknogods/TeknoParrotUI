using System;
using System.Diagnostics;

namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Lightweight diagnostic log for the Proton bridge components.
    /// GameSession forwards these lines to the game-running console so users
    /// can see whether pipes are created, the game connected and traffic flows.
    /// </summary>
    public static class ProtonLog
    {
        /// <summary>Subscribed by GameSession; also mirrored to Console/Debug.</summary>
        public static event Action<string> LineWritten;

        public static void Write(string message)
        {
            var line = $"[bridge {DateTime.Now:HH:mm:ss.fff}] {message}";
            Debug.WriteLine(line);
            Console.WriteLine(line);
            LineWritten?.Invoke(line);
        }
    }
}
