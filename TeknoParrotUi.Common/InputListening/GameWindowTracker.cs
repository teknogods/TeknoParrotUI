using System;
using System.Collections.Generic;
using System.IO;

namespace TeknoParrotUi.Common.InputListening
{
    /// <summary>
    /// Tracks the game we launched so the RawInput listeners can find its
    /// window. HookedWindows.txt only lists the classic gun-game window titles;
    /// with merged input running RawInput for every game, keyboard/mouse
    /// bindings would otherwise never activate for games whose window title is
    /// not in that list (the focus gate in HandleRawInputButton drops all
    /// presses until the game window is found and focused).
    /// </summary>
    public static class GameWindowTracker
    {
        private static readonly object Sync = new object();
        private static readonly HashSet<string> ProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Process id of the process the session started (loader or emulator), 0 when none.</summary>
        public static volatile int GameProcessId;

        public static void Reset()
        {
            lock (Sync)
            {
                ProcessNames.Clear();
                GameProcessId = 0;
            }
        }

        /// <summary>Register an executable path whose process may own the game window (game exe, second exe, emulator/loader exe).</summary>
        public static void AddExecutable(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            try
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(name))
                    return;
                lock (Sync)
                    ProcessNames.Add(name);
            }
            catch
            {
                // malformed path — ignore
            }
        }

        /// <summary>True when the process is (or was spawned as) part of the launched game.</summary>
        public static bool IsGameProcess(int processId, string processName)
        {
            if (GameProcessId != 0 && processId == GameProcessId)
                return true;
            lock (Sync)
                return ProcessNames.Contains(processName);
        }
    }
}
