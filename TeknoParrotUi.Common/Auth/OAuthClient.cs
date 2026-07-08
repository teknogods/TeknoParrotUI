using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TeknoParrotUi.Common.Auth
{
    /// <summary>
    /// Cross-platform OAuth2 + PKCE client for teknoparrot.com accounts.
    ///
    /// On Windows it uses the classic teknoparrot://oauth/callback custom scheme
    /// (the redirect URI the production server whitelists); the browser launches a
    /// second app instance which relays the callback over a named pipe. On other
    /// platforms it uses a loopback redirect (RFC 8252), which requires the
    /// server-side loopback whitelist fix to be deployed.
    ///
    /// Token cache: %LocalAppData%/TeknoParrot/auth_token.dat, DPAPI-protected on
    /// Windows — the same file and format as the classic UI, so a login in either
    /// frontend is shared. On other platforms a plain JSON file is used until a
    /// keychain integration exists.
    /// </summary>
    public class OAuthClient
    {
        private const string AuthorizeEndpoint = "https://teknoparrot.com/api/OAuth/authorize";
        private const string TokenEndpoint = "https://teknoparrot.com/api/OAuth/token";
        private const string ClientId = "teknoparrot_wpf_client";

        // Classic custom-scheme redirect — the only redirect URI the production
        // server whitelists today. The browser bounces to teknoparrot://oauth/callback,
        // Windows launches a second instance of this exe with the URI as argument,
        // and that instance relays it to the waiting login over a named pipe.
        private const string SchemeRedirectUri = "teknoparrot://oauth/callback";
        private const string CallbackPipeName = "TeknoParrotOAuthCallback";

        private readonly HttpClient _httpClient = new();
        private string _token;
        private string _refreshToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public OAuthClient()
        {
            LoadToken();
        }

        public bool IsLoggedIn => !string.IsNullOrEmpty(_token);

        private class TokenData
        {
            public string Token { get; set; }
            public string RefreshToken { get; set; }
            public DateTime Expiry { get; set; }
        }

        private class TokenResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("access_token")]
            public string AccessToken { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
            public string RefreshToken { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }
        }

        /// <summary>Reads a claim (e.g. "unique_name") from the cached JWT without validation.</summary>
        public string GetClaim(string claim)
        {
            if (string.IsNullOrEmpty(_token))
                return null;
            try
            {
                var payload = _token.Split('.')[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                var json = JsonDocument.Parse(Convert.FromBase64String(payload));
                return json.RootElement.TryGetProperty(claim, out var value) ? value.ToString() : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// The logged-in username. The production server serializes ClaimTypes.Name
        /// with the full schema URI (outbound claim mapping disabled), so several
        /// candidate keys are tried.
        /// </summary>
        public string GetUserName() =>
            GetClaim("unique_name")
            ?? GetClaim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")
            ?? GetClaim("name");

        /// <summary>The logged-in account's email address.</summary>
        public string GetEmail() =>
            GetClaim("email")
            ?? GetClaim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");

        public async Task<bool> LoginAsync(CancellationToken ct = default)
        {
            // Windows: use the classic teknoparrot:// scheme redirect — the only one
            // production currently allows. Loopback (RFC 8252) is used elsewhere and
            // becomes the universal path once the server-side loopback fix is deployed.
            if (OperatingSystem.IsWindows())
                return await LoginViaCustomSchemeAsync(ct);
            return await LoginViaLoopbackAsync(ct);
        }

        /// <summary>
        /// Called by a second app instance launched with a teknoparrot:// URI:
        /// relays the OAuth callback to the instance waiting in LoginAsync.
        /// </summary>
        public static bool TryForwardCallback(string uri)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", CallbackPipeName, PipeDirection.Out);
                client.Connect(3000);
                var payload = Encoding.UTF8.GetBytes(uri);
                client.Write(payload, 0, payload.Length);
                client.Flush();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> LoginViaCustomSchemeAsync(CancellationToken ct)
        {
            RegisterSchemeHandler();

            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = GenerateCodeChallenge(codeVerifier);
            var state = Guid.NewGuid().ToString("N");

            using var pipe = new NamedPipeServerStream(CallbackPipeName, PipeDirection.In, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            var authorizationUrl = $"{AuthorizeEndpoint}?" +
                "response_type=code&" +
                $"client_id={ClientId}&" +
                $"redirect_uri={Uri.EscapeDataString(SchemeRedirectUri)}&" +
                $"code_challenge={codeChallenge}&" +
                "code_challenge_method=S256&" +
                $"state={state}";

            Process.Start(new ProcessStartInfo { FileName = authorizationUrl, UseShellExecute = true });

            string callbackUri;
            try
            {
                await pipe.WaitForConnectionAsync(ct);
                using var reader = new StreamReader(pipe, Encoding.UTF8);
                callbackUri = await reader.ReadToEndAsync();
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            // teknoparrot://oauth/callback?code=...&state=...
            if (!Uri.TryCreate(callbackUri, UriKind.Absolute, out var parsed))
                return false;
            var query = System.Web.HttpUtility.ParseQueryString(parsed.Query);
            var code = query["code"];
            var returnedState = query["state"];
            if (string.IsNullOrEmpty(code) || returnedState != state)
                return false;

            return await ExchangeCodeForTokenAsync(code, codeVerifier, SchemeRedirectUri);
        }

        /// <summary>Registers teknoparrot:// in HKCU so the browser can bounce back to this exe.</summary>
        private static void RegisterSchemeHandler()
        {
            try
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                    return;

                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\teknoparrot");
                using (var existingCmd = key.OpenSubKey(@"shell\open\command"))
                {
                    var current = existingCmd?.GetValue("") as string;
                    if (current != null && current.Contains(exePath))
                        return;
                }

                key.SetValue("", "URL:TeknoParrot Protocol");
                key.SetValue("URL Protocol", "");
                using var cmdKey = key.CreateSubKey(@"shell\open\command");
                cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
            }
            catch
            {
                // registry unavailable — login will fail visibly, nothing to do here
            }
        }

        private async Task<bool> LoginViaLoopbackAsync(CancellationToken ct)
        {
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = GenerateCodeChallenge(codeVerifier);
            var state = Guid.NewGuid().ToString("N");

            // Loopback listener on an ephemeral port
            using var listener = new HttpListener();
            var port = GetFreePort();
            var redirectUri = $"http://127.0.0.1:{port}/callback/";
            listener.Prefixes.Add(redirectUri);
            listener.Start();

            var authorizationUrl = $"{AuthorizeEndpoint}?" +
                "response_type=code&" +
                $"client_id={ClientId}&" +
                $"redirect_uri={Uri.EscapeDataString(redirectUri.TrimEnd('/'))}&" +
                $"code_challenge={codeChallenge}&" +
                "code_challenge_method=S256&" +
                $"state={state}";

            // System browser — works on Windows (shell) and Linux (xdg-open)
            Process.Start(new ProcessStartInfo { FileName = authorizationUrl, UseShellExecute = true });

            string code;
            using (ct.Register(listener.Stop))
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch (Exception) when (ct.IsCancellationRequested)
                {
                    return false;
                }

                var query = context.Request.QueryString;
                code = query["code"];
                var returnedState = query["state"];

                var ok = !string.IsNullOrEmpty(code) && returnedState == state;
                var html = ok
                    ? "<html><body style='font-family:sans-serif'><h2>Login successful</h2>You can close this window and return to TeknoParrot.</body></html>"
                    : "<html><body style='font-family:sans-serif'><h2>Login failed</h2>Please try again from TeknoParrot.</body></html>";
                var buffer = Encoding.UTF8.GetBytes(html);
                context.Response.ContentType = "text/html";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length, ct);
                context.Response.Close();

                if (!ok)
                    return false;
            }

            return await ExchangeCodeForTokenAsync(code, codeVerifier, redirectUri.TrimEnd('/'));
        }

        public void Logout()
        {
            _token = null;
            _refreshToken = null;
            _tokenExpiry = DateTime.MinValue;
            try { File.Delete(TokenFilePath); } catch { }
        }

        /// <summary>Returns a valid access token, refreshing it if needed, or null.</summary>
        public async Task<string> GetValidTokenAsync()
        {
            if (string.IsNullOrEmpty(_token))
                return null;
            if (DateTime.Now < _tokenExpiry.AddMinutes(-5))
                return _token;
            return await RefreshAccessTokenAsync() ? _token : null;
        }

        private static int GetFreePort()
        {
            var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        private static string GenerateCodeVerifier()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private static string GenerateCodeChallenge(string codeVerifier)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
            return Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private async Task<bool> ExchangeCodeForTokenAsync(string code, string codeVerifier, string redirectUri)
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", redirectUri },
                { "client_id", ClientId },
                { "code_verifier", codeVerifier }
            });

            var response = await _httpClient.PostAsync(TokenEndpoint, content);
            if (!response.IsSuccessStatusCode)
                return false;

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(await response.Content.ReadAsStringAsync());
            _token = tokenResponse.AccessToken;
            _refreshToken = tokenResponse.RefreshToken;
            _tokenExpiry = DateTime.Now.AddSeconds(tokenResponse.ExpiresIn);
            SaveToken();
            return true;
        }

        private async Task<bool> RefreshAccessTokenAsync()
        {
            if (string.IsNullOrEmpty(_refreshToken))
                return false;

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", _refreshToken },
                { "client_id", ClientId }
            });

            var response = await _httpClient.PostAsync(TokenEndpoint, content);
            if (!response.IsSuccessStatusCode)
                return false;

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(await response.Content.ReadAsStringAsync());
            _token = tokenResponse.AccessToken;
            _refreshToken = tokenResponse.RefreshToken;
            _tokenExpiry = DateTime.Now.AddSeconds(tokenResponse.ExpiresIn);
            SaveToken();
            return true;
        }

        // ---------- Token persistence (file-compatible with the classic UI on Windows) ----------

        private static string TokenFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeknoParrot");

        private static string TokenFilePath => Path.Combine(TokenFolder,
            OperatingSystem.IsWindows() ? "auth_token.dat" : "auth_token_plain.dat");

        private void SaveToken()
        {
            if (string.IsNullOrEmpty(_token))
                return;
            try
            {
                var json = JsonSerializer.Serialize(new TokenData { Token = _token, RefreshToken = _refreshToken, Expiry = _tokenExpiry });
                Directory.CreateDirectory(TokenFolder);
                File.WriteAllText(TokenFilePath, Protect(json));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save token: {ex.Message}");
            }
        }

        private void LoadToken()
        {
            try
            {
                if (!File.Exists(TokenFilePath))
                    return;
                var data = JsonSerializer.Deserialize<TokenData>(Unprotect(File.ReadAllText(TokenFilePath)));
                _token = data.Token;
                _refreshToken = data.RefreshToken;
                _tokenExpiry = data.Expiry;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load token: {ex.Message}");
                _token = null;
                _refreshToken = null;
                _tokenExpiry = DateTime.MinValue;
            }
        }

        private static string Protect(string plainText)
        {
            if (OperatingSystem.IsWindows())
            {
                var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText), null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            // TODO: libsecret/keychain integration for Linux/macOS
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
        }

        private static string Unprotect(string protectedText)
        {
            var bytes = Convert.FromBase64String(protectedText);
            if (OperatingSystem.IsWindows())
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser));
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
