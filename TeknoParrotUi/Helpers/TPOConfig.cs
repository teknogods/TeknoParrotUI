using System;
using System.Diagnostics;
using System.Linq;

namespace TeknoParrotUi.Helpers
{
    /// <summary>
    /// Holds TeknoParrot Online session parameters passed via CLI arguments or tponline:// deep links.
    /// CLI:       TeknoParrotUi.exe --tponline --game=ID8 --room=MyRoom --action=create|join [--password=secret]
    ///            TeknoParrotUi.exe --tponline --game=ID8 --action=quick-join
    /// Deep link: tponline://join?room=MyRoom&game=ID8
    ///            tponline://quick-game?game=ID8
    /// </summary>
    public static class TPOConfig
    {
        public const string ChatBaseUrl = "https://teknoparrot.com:3333/Home/Chat";

        public static string GameId { get; private set; }
        public static string RoomName { get; private set; }
        public static string Password { get; private set; }
        public static string Action { get; private set; }

        public static bool IsConfigured => !string.IsNullOrEmpty(Action);

        public static void Clear()
        {
            GameId = null;
            RoomName = null;
            Password = null;
            Action = null;
        }

        /// <summary>
        /// Parses TPO CLI arguments. Only applies when --tponline is present WITHOUT --profile=
        /// (--profile= + --tponline is the internal game-launch invocation from TPO2Callback).
        /// </summary>
        public static bool ParseCliArgs(string[] args)
        {
            if (args == null || !args.Contains("--tponline"))
                return false;

            // Internal game-launch invocation, not lobby CLI mode
            if (args.Any(x => x.StartsWith("--profile=")))
                return false;

            string game = GetArgValue(args, "--game=");
            string room = GetArgValue(args, "--room=");
            string action = GetArgValue(args, "--action=");
            string password = GetArgValue(args, "--password=");

            if (string.IsNullOrEmpty(action))
                return false;

            action = action.ToLowerInvariant();
            if (action != "create" && action != "join" && action != "quick-join")
            {
                Debug.WriteLine($"[TPO] Unknown --action={action}, ignoring TPO CLI args");
                return false;
            }

            if (action == "quick-join")
            {
                if (string.IsNullOrEmpty(game))
                {
                    Debug.WriteLine("[TPO] --game= is required for quick-join");
                    return false;
                }
            }
            else if (string.IsNullOrEmpty(room))
            {
                Debug.WriteLine("[TPO] --room= is required for create/join");
                return false;
            }

            GameId = game;
            RoomName = room;
            Password = password;
            Action = action;
            return true;
        }

        /// <summary>
        /// Parses a tponline:// deep link, e.g. tponline://join?room=MyRoom&game=ID8
        /// </summary>
        public static bool ParseDeepLink(string uri)
        {
            try
            {
                if (string.IsNullOrEmpty(uri) || !uri.StartsWith("tponline://", StringComparison.OrdinalIgnoreCase))
                    return false;

                var parsed = new Uri(uri);
                string action = parsed.Host.ToLowerInvariant(); // "join", "create" or "quick-game"

                if (action == "quick-game")
                    action = "quick-join";

                if (action != "join" && action != "create" && action != "quick-join")
                    return false;

                string room = null, game = null, password = null;
                var query = parsed.Query.TrimStart('?');
                foreach (var pair in query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = pair.Split(new[] { '=' }, 2);
                    if (kv.Length != 2) continue;
                    var value = Uri.UnescapeDataString(kv[1]);
                    switch (kv[0].ToLowerInvariant())
                    {
                        case "room": room = value; break;
                        case "game": game = value; break;
                        case "password": password = value; break;
                    }
                }

                if (string.IsNullOrEmpty(room) && action != "quick-join")
                    return false;

                if (action == "quick-join" && string.IsNullOrEmpty(game))
                    return false;

                GameId = game;
                RoomName = room;
                Password = password;
                Action = action;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TPO] Failed to parse deep link: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Builds the chat URL for the embedded browser. The server handles
        /// auto-join (?roomName=) and auto-create (?createRoom=&game=&password=).
        /// </summary>
        public static string BuildChatUrl()
        {
            if (!IsConfigured)
                return ChatBaseUrl;

            if (Action == "quick-join")
                return $"{ChatBaseUrl}?quickGame={Uri.EscapeDataString(GameId)}";

            if (Action == "join")
                return $"{ChatBaseUrl}?roomName={Uri.EscapeDataString(RoomName)}";

            // create
            var url = $"{ChatBaseUrl}?createRoom={Uri.EscapeDataString(RoomName)}";
            if (!string.IsNullOrEmpty(GameId))
                url += $"&game={Uri.EscapeDataString(GameId)}";
            if (!string.IsNullOrEmpty(Password))
                url += $"&password={Uri.EscapeDataString(Password)}";
            return url;
        }

        /// <summary>
        /// Registers the tponline:// protocol handler in HKCU so Discord links open the app.
        /// </summary>
        public static void RegisterProtocol()
        {
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;

                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\tponline"))
                {
                    // Skip if already registered pointing at this exe
                    using (var existingCmd = key.OpenSubKey(@"shell\open\command"))
                    {
                        var current = existingCmd?.GetValue("") as string;
                        if (current != null && current.Contains(exePath))
                            return;
                    }

                    key.SetValue("", "URL:TeknoParrot Online Protocol");
                    key.SetValue("URL Protocol", "");

                    using (var cmdKey = key.CreateSubKey(@"shell\open\command"))
                    {
                        cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TPO] Failed to register tponline:// protocol: {ex.Message}");
            }
        }

        private static string GetArgValue(string[] args, string prefix)
        {
            var arg = args.FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            return arg?.Substring(prefix.Length);
        }
    }
}
