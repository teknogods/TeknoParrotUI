using System;
using System.Text.Json;
using System.Collections.Generic;

namespace TeknoParrotUi
{
    // This class helps process JavaScript messages received via custom JS events
    public class JSMessageHandler
    {
        private readonly TPO2Callback _callback;

        public JSMessageHandler(TPO2Callback callback)
        {
            _callback = callback;
        }

        // Process a request from JavaScript
        public bool ProcessMessage(string request, Action<string> successCallback, Action<int, string> failureCallback)
        {
            try
            {
                var message = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request);

                if (message != null && message.TryGetValue("action", out var actionElement) && actionElement.GetString() is string action)
                {
                    switch (action)
                    {
                        case "showMessage":
                            _callback.showMessage(message["message"].GetString());
                            successCallback("");
                            return true;

                        case "startGame":
                            _callback.startGame(
                                message["uniqueRoomName"].GetString(),
                                message["realRoomName"].GetString(),
                                message["gameId"].GetString(),
                                message["playerId"].GetString(),
                                message["playerName"].GetString(),
                                message["playerCount"].GetString()
                            );
                            successCallback("");
                            return true;
                    }
                }

                failureCallback(0, "Unrecognized action");
            }
            catch (Exception ex)
            {
                failureCallback(0, ex.Message);
            }

            return false;
        }
    }
}