using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Common.Pipes
{
    public class SettingsSyncPipe
    {
        public const string PipeName = "TeknoParrotSettingsSync";
        
        private bool _isRunning = false;
        private Thread _pipeThread;
        private GameProfile _gameProfile;
        private Action<string> _logCallback;
        private Action<GameProfile> _saveCallback;

        public SettingsSyncPipe(GameProfile gameProfile, Action<GameProfile> saveCallback, Action<string> logCallback = null)
        {
            _gameProfile = gameProfile;
            _saveCallback = saveCallback;
            _logCallback = logCallback;
        }

        public void Start()
        {
            if (_isRunning || _gameProfile == null)
                return;

            _isRunning = true;
            _pipeThread = new Thread(ListenForSettings);
            _pipeThread.IsBackground = true;
            _pipeThread.Start();
        }

        private void ListenForSettings()
        {
            while (_isRunning)
            {
                try
                {
                    using (var pipeServer = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, 
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                    {
                        Log($"[SettingsSync] Waiting for game to connect...");
                        
                        pipeServer.WaitForConnection();
                        
                        Log($"[SettingsSync] Game connected, reading settings...");

                        using (var reader = new StreamReader(pipeServer, Encoding.UTF8))
                        {
                            string line;
                            var updatedSettings = new Dictionary<string, string>();

                            while ((line = reader.ReadLine()) != null)
                            {
                                if (string.IsNullOrWhiteSpace(line))
                                    continue;

                                var parts = line.Split(new[] { '=' }, 2);
                                if (parts.Length == 2)
                                {
                                    var key = parts[0].Trim();
                                    var value = parts[1].Trim();
                                    updatedSettings[key] = value;
                                    Log($"[SettingsSync] Received: {key} = {value}");
                                }
                            }

                            if (updatedSettings.Count > 0)
                            {
                                ProcessSettingsUpdate(updatedSettings);
                            }
                        }
                    }
                }
                catch (IOException)
                {
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Log($"[SettingsSync] Error: {ex.Message}");
                    }
                }
            }
        }

        private void ProcessSettingsUpdate(Dictionary<string, string> updatedSettings)
        {
            Log($"[SettingsSync] Processing {updatedSettings.Count} setting(s)...");

            foreach (var setting in updatedSettings)
            {
                FieldInformation configValue = null;
                string displayKey = setting.Key;

                // Check if the key contains a category specifier (Category.FieldName)
                if (setting.Key.Contains("."))
                {
                    var parts = setting.Key.Split(new[] { '.' }, 2);
                    var category = parts[0].Trim();
                    var fieldName = parts[1].Trim();

                    configValue = _gameProfile.ConfigValues?.FirstOrDefault(cv =>
                        cv.CategoryName.Equals(category, StringComparison.OrdinalIgnoreCase) &&
                        cv.FieldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

                    displayKey = $"{category}.{fieldName}";
                }
                else
                {
                    // Simple field name - find first match
                    configValue = _gameProfile.ConfigValues?.FirstOrDefault(cv =>
                        cv.FieldName.Equals(setting.Key, StringComparison.OrdinalIgnoreCase));

                    // Check for duplicates and warn
                    var duplicates = _gameProfile.ConfigValues?.Where(cv =>
                        cv.FieldName.Equals(setting.Key, StringComparison.OrdinalIgnoreCase)).ToList();

                    if (duplicates?.Count > 1)
                    {
                        Log($"[SettingsSync] Warning: Multiple fields named '{setting.Key}' found in categories: {string.Join(", ", duplicates.Select(d => d.CategoryName))}");
                        Log($"[SettingsSync] Updating first match in category '{configValue?.CategoryName}'. Use 'Category.FieldName' format to specify.");
                    }
                }

                if (configValue != null)
                {
                    string oldValue = configValue.FieldValue;
                    configValue.FieldValue = setting.Value;
                    Log($"[SettingsSync] Updated '{displayKey}' [{configValue.CategoryName}]: '{oldValue}' -> '{setting.Value}'");
                }
                else
                {
                    Log($"[SettingsSync] Warning: Setting '{displayKey}' not found in game profile");
                }
            }

            try
            {
                _saveCallback?.Invoke(_gameProfile);
                Log($"[SettingsSync] Game profile saved successfully");
            }
            catch (Exception ex)
            {
                Log($"[SettingsSync] Error saving game profile: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            
            try
            {
                using (var npcs = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.None))
                {
                    npcs.Connect(100);
                }
            }
            catch
            {
            }

            Thread.Sleep(100);
        }

        private void Log(string message)
        {
            _logCallback?.Invoke(message);
        }
    }
}
