using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
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
#if DEBUG
        private const string AuthorizeEndpoint = "https://localhost:44339/api/OAuth/authorize";
        private const string TokenEndpoint = "https://localhost:44339/api/OAuth/token";
#else
        private const string AuthorizeEndpoint = "https://teknoparrot.com/api/OAuth/authorize";
        private const string TokenEndpoint = "https://teknoparrot.com/api/OAuth/token";
#endif

        private const string ClientId = "teknoparrot_wpf_client";
        private const string RedirectUri = "teknoparrot://oauth/callback";

        private readonly HttpClient _httpClient;
        private string _tokenCache;
        private string _refreshTokenCache;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public OAuthHelper()
        {
            _httpClient = new HttpClient();
            RegisterUriSchemeHandler();

            // Try and load the existing token if the user logged in before.
            LoadToken();
        }

        public class TokenData
        {
            public string Token { get; set; }
            public string RefreshToken { get; set; }
            public DateTime Expiry { get; set; }
        }

        private void RegisterUriSchemeHandler()
        {
            try
            {
                // Check if the protocol is already registered so we don't do it twice or more
                var process = new Process();
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = $"/c ftype teknoparrot";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                process.WaitForExit();

                string output = process.StandardOutput.ReadToEnd();
                if (string.IsNullOrEmpty(output) || !output.Contains(RedirectUri.Split(':')[0]))
                {
                    string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;

                    process = new Process();
                    process.StartInfo.FileName = "cmd.exe";
                    process.StartInfo.Arguments = $"/c reg add HKCU\\Software\\Classes\\teknoparrot /ve /t REG_SZ /d \"URL:TeknoParrot Protocol\" /f && " +
                                                 "reg add HKCU\\Software\\Classes\\teknoparrot /v \"URL Protocol\" /t REG_SZ /d \"\" /f && " +
                                                $"reg add HKCU\\Software\\Classes\\teknoparrot\\shell\\open\\command /ve /t REG_SZ /d \"\\\"{exePath}\\\" \\\"%1\\\"\" /f";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to register URI handler: {ex.Message}");
            }
        }

        public async Task<bool> AuthenticateAsync()
        {
            try
            {

                var codeVerifier = GenerateCodeVerifier();
                var codeChallenge = GenerateCodeChallenge(codeVerifier);

                var state = Guid.NewGuid().ToString("N");
                var authorizationUrl = $"{AuthorizeEndpoint}?" +
                    $"response_type=code&" +
                    $"client_id={ClientId}&" +
                    $"redirect_uri={Uri.EscapeDataString(RedirectUri)}&" +
                    $"code_challenge={codeChallenge}&" +
                    $"code_challenge_method=S256&" +
                    $"state={state}";

                Process.Start(new ProcessStartInfo
                {
                    FileName = authorizationUrl,
                    UseShellExecute = true
                });

                var authorizationCode = await WaitForAuthorizationCodeAsync();

                if (string.IsNullOrEmpty(authorizationCode))
                {
                    return false;
                }

                return await ExchangeCodeForTokenAsync(authorizationCode, codeVerifier);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Authentication failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
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

        private TaskCompletionSource<string> _authorizationCodeTcs;

        public void HandleCallback(string uri)
        {
            try
            {
                if (_authorizationCodeTcs != null && !_authorizationCodeTcs.Task.IsCompleted)
                {
                    var authorizationCode = ExtractAuthorizationCode(uri);
                    _authorizationCodeTcs.SetResult(authorizationCode);
                }
            }
            catch (Exception ex)
            {
                if (_authorizationCodeTcs != null && !_authorizationCodeTcs.Task.IsCompleted)
                {
                    _authorizationCodeTcs.SetException(ex);
                }
            }
        }

        private string ExtractAuthorizationCode(string uri)
        {
            var queryParams = new Uri(uri).Query.TrimStart('?')
                .Split('&')
                .Select(param => param.Split('='))
                .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));

            if (queryParams.TryGetValue("code", out var code))
            {
                return code;
            }

            throw new Exception("No authorization code found in the callback URI");
        }

        private Task<string> WaitForAuthorizationCodeAsync()
        {
            _authorizationCodeTcs = new TaskCompletionSource<string>();
            return _authorizationCodeTcs.Task;
        }

        private async Task<bool> ExchangeCodeForTokenAsync(string code, string codeVerifier)
        {
            Debug.WriteLine($"Exchanging code: {code}");
            Debug.WriteLine($"Code verifier: {codeVerifier}");

            var formContent = new Dictionary<string, string>
    {
        { "grant_type", "authorization_code" },
        { "code", code },
        { "redirect_uri", RedirectUri },
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

        public async Task<bool> LogoutAsync()
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