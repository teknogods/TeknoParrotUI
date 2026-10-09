using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    public class OAuthHelper
    {
#if DEBUG && USE_LOCALHOST
        private const string AuthorizeEndpoint = "https://localhost:44339/api/OAuth/authorize";
        private const string TokenEndpoint = "https://localhost:44339/api/OAuth/token";
#else
        private const string AuthorizeEndpoint = "https://teknoparrot.com/api/OAuth/authorize";
        private const string TokenEndpoint = "https://teknoparrot.com/api/OAuth/token";
#endif

        private const string ClientId = "teknoparrot_wpf_client";

        // RFC 8252 loopback redirect: the browser sends the code back to a listener on 127.0.0.1 with a port picked per
        // login. Unlike a custom URI scheme this needs no registry entry, so it also works when the browser runs outside
        // of the app's environment (Wine/Proton hand URLs to the Linux browser, which knows nothing of teknoparrot://).
        // Must stay "/callback": the website's IsAllowedRedirectUri accepts loopback redirects on that exact path only.
        private const string CallbackPath = "/callback";
        private static readonly TimeSpan LoginTimeout = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan CallbackReadTimeout = TimeSpan.FromSeconds(10);

        private readonly HttpClient _httpClient;
        private string _tokenCache;
        private string _refreshTokenCache;
        private DateTime _tokenExpiry = DateTime.MinValue;
        private TcpListener _callbackListener;

        public OAuthHelper()
        {
            _httpClient = new HttpClient();

            // Try and load the existing token if the user logged in before.
            LoadToken();
        }

        public class TokenData
        {
            public string Token { get; set; }
            public string RefreshToken { get; set; }
            public DateTime Expiry { get; set; }
        }

        public Task<bool> AuthenticateAsync()
        {
            return AuthenticateAsync(false);
        }

        /// <summary>
        /// The browser login. <paramref name="freshLogin"/> adds prompt=login: the website asks for the password again even
        /// with a live website session, and the token then carries auth_time (the "fresh login" that the Initial D Online
        /// secret reads need). Without it the flow is unchanged.
        /// Returns false when the user cancels on the website, after <see cref="LoginTimeout"/>, or when a newer login
        /// replaced this one.
        /// </summary>
        public async Task<bool> AuthenticateAsync(bool freshLogin)
        {
            // A newer login replaces one still waiting (e.g. the user closed the browser tab and pressed login again).
            _callbackListener?.Stop();

            var listener = new TcpListener(IPAddress.Loopback, 0);
            _callbackListener = listener;
            try
            {
                listener.Start();
                var redirectUri = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}{CallbackPath}";

                var codeVerifier = GenerateCodeVerifier();
                var codeChallenge = GenerateCodeChallenge(codeVerifier);

                var state = Guid.NewGuid().ToString("N");
                var authorizationUrl = $"{AuthorizeEndpoint}?" +
                    $"response_type=code&" +
                    $"client_id={ClientId}&" +
                    $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                    $"code_challenge={codeChallenge}&" +
                    $"code_challenge_method=S256&" +
                    $"state={state}" +
                    (freshLogin ? "&prompt=login" : "");

                OpenBrowser(authorizationUrl);

                var authorizationCode = await WaitForAuthorizationCodeAsync(listener, state);

                if (string.IsNullOrEmpty(authorizationCode))
                {
                    return false;
                }

                // The browser has the focus now, bring the UI back so the user sees the login finish.
                Application.Current?.Dispatcher.Invoke(() => Application.Current.MainWindow?.Activate());

                return await ExchangeCodeForTokenAsync(authorizationCode, codeVerifier, redirectUri);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Authentication failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                listener.Stop();
                if (_callbackListener == listener)
                {
                    _callbackListener = null;
                }
            }
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // No browser registered (seen on some Wine prefixes). The listener is already running, so the login still
                // completes if the user opens the link by hand.
                Debug.WriteLine($"[Auth] Failed to open the browser: {ex.Message}");
                try
                {
                    Clipboard.SetText(url);
                }
                catch (Exception clipboardEx)
                {
                    Debug.WriteLine($"[Auth] Failed to copy the login link: {clipboardEx.Message}");
                }

                MessageBox.Show("TeknoParrot could not open your web browser. The login link has been copied to your clipboard, paste it into your browser to continue.",
                    "Login", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private string GenerateCodeVerifier()
        {
            byte[] bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        private string GenerateCodeChallenge(string codeVerifier)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
                return Convert.ToBase64String(challengeBytes)
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .TrimEnd('=');
            }
        }

        /// <summary>
        /// Serves the loopback listener until the browser comes back from the website. Returns the authorization code, or
        /// null when the user cancelled, the wait timed out or the listener was stopped.
        /// </summary>
        private static async Task<string> WaitForAuthorizationCodeAsync(TcpListener listener, string expectedState)
        {
            var result = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = AcceptCallbacksAsync(listener, expectedState, result);

            var finished = await Task.WhenAny(result.Task, Task.Delay(LoginTimeout)).ConfigureAwait(false);
            if (finished != result.Task)
            {
                Debug.WriteLine("[Auth] Timed out waiting for the browser login");
                return null;
            }

            return await result.Task.ConfigureAwait(false);
        }

        private static async Task AcceptCallbacksAsync(TcpListener listener, string expectedState, TaskCompletionSource<string> result)
        {
            // Browsers also open idle speculative connections and ask for favicon.ico, so each connection is served on its
            // own until one of them carries the callback.
            while (!result.Task.IsCompleted)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // The listener was stopped: the login finished, timed out or was replaced by a newer one.
                    result.TrySetResult(null);
                    return;
                }

                _ = ServeCallbackAsync(client, expectedState, result);
            }
        }

        private static async Task ServeCallbackAsync(TcpClient client, string expectedState, TaskCompletionSource<string> result)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    var readRequest = ReadRequestTargetAsync(stream);
                    if (await Task.WhenAny(readRequest, Task.Delay(CallbackReadTimeout)).ConfigureAwait(false) != readRequest)
                    {
                        // A speculative connection that never sent a request, disposing the client ends the read.
                        return;
                    }

                    var target = await readRequest.ConfigureAwait(false);
                    if (target == null)
                    {
                        return;
                    }

                    var queryStart = target.IndexOf('?');
                    var path = queryStart < 0 ? target : target.Substring(0, queryStart);
                    if (path != CallbackPath)
                    {
                        await WriteResponseAsync(stream, "404 Not Found", null).ConfigureAwait(false);
                        return;
                    }

                    var query = ParseQuery(queryStart < 0 ? "" : target.Substring(queryStart + 1));
                    if (!query.TryGetValue("state", out var state) || state != expectedState)
                    {
                        await WriteResponseAsync(stream, "400 Bad Request", "This login link has expired. Please start the login again from TeknoParrot.").ConfigureAwait(false);
                        return;
                    }

                    if (query.TryGetValue("code", out var code) && !string.IsNullOrEmpty(code))
                    {
                        // Hand the code over first: the login can finish even if the browser tab is already gone.
                        result.TrySetResult(code);
                        await WriteResponseAsync(stream, "200 OK", "You are now logged in. You can close this tab and return to TeknoParrot.").ConfigureAwait(false);
                    }
                    else
                    {
                        // error=access_denied and friends: the user cancelled on the website.
                        query.TryGetValue("error", out var error);
                        Debug.WriteLine($"[Auth] Login returned no code: {error}");
                        result.TrySetResult(null);
                        await WriteResponseAsync(stream, "200 OK", "The login was cancelled. You can close this tab.").ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Auth] Callback connection failed: {ex.Message}");
                }
            }
        }

        /// <summary>Reads the request head and returns the target of a GET request line ("/callback?code=...").</summary>
        private static async Task<string> ReadRequestTargetAsync(NetworkStream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true))
            {
                var requestLine = await reader.ReadLineAsync().ConfigureAwait(false);

                // Read the headers too: closing a socket with unread data resets the connection, and the browser would
                // show a connection error instead of the page.
                string header;
                do
                {
                    header = await reader.ReadLineAsync().ConfigureAwait(false);
                } while (!string.IsNullOrEmpty(header));

                var parts = requestLine?.Split(' ');
                return parts != null && parts.Length >= 2 && parts[0] == "GET" ? parts[1] : null;
            }
        }

        private static async Task WriteResponseAsync(NetworkStream stream, string status, string message)
        {
            var body = message == null ? "" :
                "<!doctype html><html><head><meta charset=\"utf-8\"><title>TeknoParrot</title></head>" +
                "<body style=\"font-family:sans-serif;text-align:center;margin-top:4em\">" +
                $"<h2>TeknoParrot</h2><p>{message}</p></body></html>";
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var headBytes = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {status}\r\n" +
                "Content-Type: text/html; charset=utf-8\r\n" +
                $"Content-Length: {bodyBytes.Length}\r\n" +
                "Cache-Control: no-store\r\n" +
                "Connection: close\r\n\r\n");

            await stream.WriteAsync(headBytes, 0, headBytes.Length).ConfigureAwait(false);
            await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length).ConfigureAwait(false);
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var values = new Dictionary<string, string>();
            foreach (var pair in query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf('=');
                var key = separator < 0 ? pair : pair.Substring(0, separator);
                var value = separator < 0 ? "" : pair.Substring(separator + 1);
                values[Decode(key)] = Decode(value);
            }

            return values;

            static string Decode(string s) => Uri.UnescapeDataString(s.Replace('+', ' '));
        }

        private async Task<bool> ExchangeCodeForTokenAsync(string code, string codeVerifier, string redirectUri)
        {
            Debug.WriteLine($"Exchanging code: {code}");
            Debug.WriteLine($"Code verifier: {codeVerifier}");

            var formContent = new Dictionary<string, string>
    {
        { "grant_type", "authorization_code" },
        { "code", code },
        { "redirect_uri", redirectUri },
        { "client_id", ClientId },
        { "code_verifier", codeVerifier }
    };

            foreach (var item in formContent)
            {
                Debug.WriteLine($"Form content: {item.Key}={item.Value}");
            }

            var content = new FormUrlEncodedContent(formContent);
            Debug.WriteLine($"Calling token endpoint: {TokenEndpoint}");

            var response = await _httpClient.PostAsync(TokenEndpoint, content);

            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();

                _tokenCache = tokenResponse.AccessToken;
                _refreshTokenCache = tokenResponse.RefreshToken;
                _tokenExpiry = DateTime.Now.AddSeconds(tokenResponse.ExpiresIn);

                // Store it locally so that people don't have to relogin on every start of TPUI
                SaveToken();
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to exchange code for token: {response.StatusCode} - {errorContent}");
            }
        }

        private async Task<bool> RefreshAccessTokenAsync()
        {
            if (string.IsNullOrEmpty(_refreshTokenCache))
            {
                Debug.WriteLine("Cannot refresh token: No refresh token available");
                return false;
            }

            try
            {
                Debug.WriteLine("Refreshing access token using refresh token");

                var formContent = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", _refreshTokenCache },
            { "client_id", ClientId }
        };

                var content = new FormUrlEncodedContent(formContent);
                var response = await _httpClient.PostAsync(TokenEndpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();

                    _tokenCache = tokenResponse.AccessToken;
                    _refreshTokenCache = tokenResponse.RefreshToken;  // Update with new refresh token
                    _tokenExpiry = DateTime.Now.AddSeconds(tokenResponse.ExpiresIn);

                    SaveToken();
                    Debug.WriteLine("Token refreshed successfully, valid until: " + _tokenExpiry);
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Failed to refresh token: {response.StatusCode} - {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing token: {ex.Message}");
                return false;
            }
        }

        public JwtSecurityToken DecodeToken()
        {
            if (string.IsNullOrEmpty(_tokenCache))
            {
                return null;
            }

            var handler = new JwtSecurityTokenHandler();
            return handler.ReadJwtToken(_tokenCache);
        }

        public string GetUserName()
        {
            var token = DecodeToken();
            return token?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == "name")?.Value;
        }

        public string GetUserId()
        {
            var token = DecodeToken();
            return token?.Subject;
        }

        public string GetAccessToken()
        {
            return _tokenCache;
        }

        public Task<bool> EnsureAuthenticatedAsync()
        {
            // Don't force login by default
            return EnsureAuthenticatedAsync(false);
        }
        public async Task<bool> EnsureAuthenticatedAsync(bool shouldLogin)
        {
            Debug.WriteLine($"[Auth] Token check - Current time: {DateTime.Now}, Token expiry: {_tokenExpiry}");
            Debug.WriteLine($"[Auth] Has token: {!string.IsNullOrEmpty(_tokenCache)}, Has refresh token: {!string.IsNullOrEmpty(_refreshTokenCache)}");

            // If token is still valid for more than 5 minutes, use it
            if (!string.IsNullOrEmpty(_tokenCache) && DateTime.Now.AddMinutes(5) < _tokenExpiry)
            {
                Debug.WriteLine("[Auth] Token is still valid, using existing token");
                return true;
            }

            Debug.WriteLine("[Auth] Token is expired or will expire soon");

            // If we have a refresh token, try to use it
            if (!string.IsNullOrEmpty(_refreshTokenCache))
            {
                Debug.WriteLine("[Auth] Attempting to refresh the token");
                bool refreshed = await RefreshAccessTokenAsync();
                if (refreshed)
                {
                    Debug.WriteLine("[Auth] Token refreshed successfully");
                    return true;
                }
                Debug.WriteLine("[Auth] Token refresh failed");
            }
            else
            {
                Debug.WriteLine("[Auth] No refresh token available");
            }

            Debug.WriteLine("[Auth] Falling back to full authentication");
            // If refresh failed or we don't have a refresh token, authenticate from scratch
            if (shouldLogin)
            {
                return await AuthenticateAsync();
            }

            // Not logged in, and we don't want to force a login. (for example, when checking token state on app boot)
            return false;
        }

        public bool Logout()
        {
            try
            {
                Debug.WriteLine("Logging out user");

                _tokenCache = null;
                _refreshTokenCache = null;
                _tokenExpiry = DateTime.MinValue;

                // On a specific logout, we also clear the user data.
                // We don't do this when the token can't be validated as it could be caused by not being connected to the internet 
                // or other edge cases.
                Lazydata.ParrotData.SegaId = "";
                Lazydata.ParrotData.NamcoId = "";
                Lazydata.ParrotData.MarioKartId = "";
                Lazydata.ParrotData.GoldenTeePcbId = "";
                Lazydata.ParrotData.GoldenTeeCardId = "";
                KizunaOnlineHelper.ForgetAccount();
                Lazydata.ParrotData.IsLoggedIn = false;

                string tokenFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TeknoParrot");

                string tokenFilePath = Path.Combine(tokenFolder, "auth_token.dat");

                if (File.Exists(tokenFilePath))
                {
                    File.Delete(tokenFilePath);
                    Debug.WriteLine("Token file deleted");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during logout: {ex.Message}");
                return false;
            }
        }

        public void SaveToken()
        {
            if (!string.IsNullOrEmpty(_tokenCache))
            {
                try
                {
                    var tokenData = new TokenData
                    {
                        Token = _tokenCache,
                        RefreshToken = _refreshTokenCache,
                        Expiry = _tokenExpiry
                    };

                    string tokenJson = JsonSerializer.Serialize(tokenData);
                    string encryptedToken = EncryptString(tokenJson);

                    string tokenFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "TeknoParrot");

                    Directory.CreateDirectory(tokenFolder);

                    string tokenFilePath = Path.Combine(tokenFolder, "auth_token.dat");
                    File.WriteAllText(tokenFilePath, encryptedToken);

                    Debug.WriteLine("Token saved successfully");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to save token: {ex.Message}");
                }
            }
        }

        public void LoadToken()
        {
            try
            {
                string tokenFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TeknoParrot");

                string tokenFilePath = Path.Combine(tokenFolder, "auth_token.dat");

                if (File.Exists(tokenFilePath))
                {
                    string encryptedToken = File.ReadAllText(tokenFilePath);
                    string tokenJson = DecryptString(encryptedToken);
                    var tokenData = JsonSerializer.Deserialize<TokenData>(tokenJson);

                    _tokenCache = tokenData.Token;
                    _refreshTokenCache = tokenData.RefreshToken;
                    _tokenExpiry = tokenData.Expiry;

                    Debug.WriteLine("Token loaded successfully, valid until: " + _tokenExpiry);
                    Debug.WriteLine("Refrsh token: " + _refreshTokenCache);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load token: {ex.Message}");
            }

            // Reset token state if loading failed
            _tokenCache = null;
            _refreshTokenCache = null;
            _tokenExpiry = DateTime.MinValue;
        }

        private string EncryptString(string plainText)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = ProtectedData.Protect(
                plainBytes,
                null,
                DataProtectionScope.CurrentUser);

            return Convert.ToBase64String(encryptedBytes);
        }

        private string DecryptString(string encryptedText)
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            byte[] plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                null,
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(plainBytes);
        }

        private class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; }

            [JsonPropertyName("token_type")]
            public string TokenType { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("refresh_token")]
            public string RefreshToken { get; set; }
        }
    }
}