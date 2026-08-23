using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TeknoParrotUi.Helpers
{
    public sealed class SunshineStatus
    {
        public bool Status { get; set; }
        public bool Running { get; set; }
        public bool Managed { get; set; }
        public string Version { get; set; }
        public string Platform { get; set; }
        public string ConnectionMode { get; set; }
        public bool ConnectionOpen { get; set; }
        public int ActiveSessions { get; set; }
        public int PairedClients { get; set; }
        public bool PairingPending { get; set; }
    }

    public sealed class SunshineClientInfo
    {
        public string Uuid { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public bool Connected { get; set; }

        public string DisplayName
        {
            get
            {
                var name = string.IsNullOrWhiteSpace(Name) ? "Moonlight Client" : Name;
                var state = Connected ? "Connected" : "Paired • Offline";

                if (!Enabled)
                    state += " • Disabled";

                return $"{name} — {state}";
            }
        }
    }

    public static class SunshineManager
    {
        private const string ManagedApiBaseUrl = "https://127.0.0.1:47990";

        private static readonly HttpClient HttpClient;

        static SunshineManager()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var previousCertificateCallback =
                ServicePointManager.ServerCertificateValidationCallback;

            ServicePointManager.ServerCertificateValidationCallback =
                (sender, certificate, chain, sslPolicyErrors) =>
                {
                    var request = sender as HttpWebRequest;

                    if (request?.RequestUri != null &&
                        request.RequestUri.Port == 47990 &&
                        (
                            string.Equals(
                                request.RequestUri.Host,
                                "127.0.0.1",
                                StringComparison.OrdinalIgnoreCase
                            ) ||
                            string.Equals(
                                request.RequestUri.Host,
                                "localhost",
                                StringComparison.OrdinalIgnoreCase
                            )
                        ))
                    {
                        return true;
                    }

                    if (previousCertificateCallback != null)
                    {
                        return previousCertificateCallback(
                            sender,
                            certificate,
                            chain,
                            sslPolicyErrors
                        );
                    }

                    return sslPolicyErrors == SslPolicyErrors.None;
                };

            HttpClient = CreateHttpClient();
        }

        public static string SunshineDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sunshine");

        public static string SunshineExecutablePath =>
            Path.Combine(SunshineDirectory, "sunshine.exe");

        private static HttpClient CreateHttpClient()
        {
            return new HttpClient
            {
                BaseAddress = new Uri(ManagedApiBaseUrl),
                Timeout = TimeSpan.FromSeconds(3)
            };
        }

        public static bool IsInstalled()
        {
            return File.Exists(SunshineExecutablePath);
        }

        public static bool IsRunning()
        {
            try
            {
                return Process
                    .GetProcessesByName("sunshine")
                    .Any(p => !p.HasExited);
            }
            catch
            {
                return false;
            }
        }

        private static void ForceStopBundledProcess()
        {
            Process[] processes;

            try
            {
                processes = Process.GetProcessesByName("sunshine");
            }
            catch
            {
                return;
            }

            foreach (var process in processes)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                }
                catch
                {
                    // Best-effort emergency fallback only.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        public static void Start()
        {
            if (!IsInstalled())
            {
                throw new FileNotFoundException(
                    "Sunshine could not be found.",
                    SunshineExecutablePath
                );
            }

            if (IsRunning())
                return;

            var startInfo = new ProcessStartInfo
            {
                FileName = SunshineExecutablePath,
                Arguments = $"--managed --parent-pid {Process.GetCurrentProcess().Id}",
                WorkingDirectory = SunshineDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(startInfo);
        }

        public static async Task<SunshineStatus> GetStatusAsync()
        {
            var json = await GetJsonAsync("/api/managed/status");

            return new SunshineStatus
            {
                Status = json.Value<bool?>("status") ?? false,
                Running = json.Value<bool?>("running") ?? false,
                Managed = json.Value<bool?>("managed") ?? false,
                Version = json.Value<string>("version") ?? string.Empty,
                Platform = json.Value<string>("platform") ?? string.Empty,
                ConnectionMode = json.Value<string>("connection_mode") ?? "closed",
                ConnectionOpen = json.Value<bool?>("connection_open") ?? false,
                ActiveSessions = json.Value<int?>("active_sessions") ?? 0,
                PairedClients = json.Value<int?>("paired_clients") ?? 0,
                PairingPending = json.Value<bool?>("pairing_pending") ?? false
            };
        }

        public static async Task<IReadOnlyList<SunshineClientInfo>> GetClientsAsync()
        {
            var json = await GetJsonAsync("/api/managed/clients");
            var clientsToken = json["clients"];
            var clients = new List<SunshineClientInfo>();

            if (clientsToken is JArray array)
            {
                foreach (var token in array.OfType<JObject>())
                    clients.Add(ParseClient(token));
            }
            else if (clientsToken is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    if (property.Value is JObject clientObject)
                    {
                        var client = ParseClient(clientObject);
                        if (string.IsNullOrWhiteSpace(client.Uuid))
                            client.Uuid = property.Name;
                        clients.Add(client);
                    }
                }
            }

            return clients;
        }

        private static SunshineClientInfo ParseClient(JObject client)
        {
            var uuid = FirstString(client, "uuid", "uniqueid", "unique_id", "id");
            var name = FirstString(client, "name", "display_name", "friendly_name", "hostname");
            var enabled = client.Value<bool?>("enabled") ?? true;
            var connected = client.Value<bool?>("connected") ?? false;

            return new SunshineClientInfo
            {
                Uuid = uuid,
                Name = name,
                Enabled = enabled,
                Connected = connected
            };
        }

        private static string FirstString(JObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                var token = obj[name];
                if (token == null || token.Type == JTokenType.Null)
                    continue;

                var value = token.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        public static async Task SetConnectionModeAsync(string mode)
        {
            if (!string.Equals(mode, "open", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(mode, "closed", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Managed Sunshine connection mode must be either 'open' or 'closed'.",
                    nameof(mode)
                );
            }

            var payload = new JObject
            {
                ["mode"] = mode.ToLowerInvariant()
            };

            await PostJsonAsync("/api/managed/connection-mode", payload);
        }

        public static async Task PairAsync(string pin, string name)
        {
            if (string.IsNullOrWhiteSpace(pin) ||
                pin.Length != 4 ||
                !pin.All(char.IsDigit))
            {
                throw new ArgumentException("Pairing PIN must contain exactly 4 digits.", nameof(pin));
            }

            var payload = new JObject
            {
                ["pin"] = pin,
                ["name"] = name ?? string.Empty
            };

            var response = await PostJsonAsync("/api/managed/pair", payload);
            if (!(response.Value<bool?>("status") ?? false))
                throw new InvalidOperationException("Sunshine rejected the pairing request.");
        }

        public static async Task UnpairAsync(string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
                throw new ArgumentException("A client UUID is required.", nameof(uuid));

            var payload = new JObject
            {
                ["uuid"] = uuid
            };

            var response = await PostJsonAsync("/api/managed/unpair", payload);
            if (!(response.Value<bool?>("status") ?? false))
                throw new InvalidOperationException("Sunshine could not unpair the selected client.");
        }

        public static async Task DisconnectAllAsync()
        {
            await PostAsync("/api/managed/disconnect-all");
        }

        public static async Task StopAsync()
        {
            if (!IsRunning())
                return;

            try
            {
                await PostAsync("/api/managed/shutdown");
            }
            catch
            {
                // If the managed API is unavailable, wait briefly and then use the
                // bundled-process-only fallback below.
            }

            if (await WaitForRunningStateAsync(false, TimeSpan.FromSeconds(5)))
                return;

            ForceStopBundledProcess();
            await WaitForRunningStateAsync(false, TimeSpan.FromSeconds(3));
        }

        public static async Task RestartAsync()
        {
            await StopAsync();
            Start();

            var deadline = DateTime.UtcNow.AddSeconds(8);
            Exception lastError = null;

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var status = await GetStatusAsync();
                    if (status.Running && status.Managed)
                        return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }

                await Task.Delay(200);
            }

            throw new InvalidOperationException(
                "Sunshine restarted, but its managed API did not become available.",
                lastError
            );
        }

        public static async Task<bool> WaitForRunningStateAsync(bool expectedRunning, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline)
            {
                if (IsRunning() == expectedRunning)
                    return true;

                await Task.Delay(100);
            }

            return IsRunning() == expectedRunning;
        }

        private static async Task<JObject> GetJsonAsync(string path)
        {
            using (var response = await HttpClient.GetAsync(path))
            {
                return await ReadJsonResponseAsync(response);
            }
        }

        private static async Task<JObject> PostAsync(string path)
        {
            using (var response = await HttpClient.PostAsync(path, null))
            {
                return await ReadJsonResponseAsync(response);
            }
        }

        private static async Task<JObject> PostJsonAsync(string path, JObject payload)
        {
            using (var content = new StringContent(
                payload.ToString(Formatting.None),
                Encoding.UTF8,
                "application/json"))
            using (var response = await HttpClient.PostAsync(path, content))
            {
                return await ReadJsonResponseAsync(response);
            }
        }

        private static async Task<JObject> ReadJsonResponseAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var message = string.IsNullOrWhiteSpace(body)
                    ? response.ReasonPhrase
                    : body;

                throw new HttpRequestException(
                    $"Sunshine managed API returned {(int)response.StatusCode}: {message}"
                );
            }

            if (string.IsNullOrWhiteSpace(body))
                return new JObject();

            return JObject.Parse(body);
        }
    }
}