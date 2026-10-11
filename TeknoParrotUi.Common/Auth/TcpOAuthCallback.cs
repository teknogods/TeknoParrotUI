using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TeknoParrotUi.Common.Auth
{
    public sealed class TcpOAuthCallback : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        public Uri RedirectUri { get; }
        public TcpOAuthCallback()
        {
            _listener.Start();
            RedirectUri = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/callback");
        }
        public async Task<string> WaitAsync(string state, CancellationToken cancellation)
        {
            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellation.Register(() => completion.TrySetCanceled(cancellation));
            _ = AcceptAsync(state, completion, cancellation);
            return await completion.Task.ConfigureAwait(false);
        }
        private async Task AcceptAsync(string state, TaskCompletionSource<string> completion, CancellationToken cancellation)
        {
            try
            {
                while (!completion.Task.IsCompleted)
                {
                    var client = await _listener.AcceptTcpClientAsync(cancellation).ConfigureAwait(false);
                    _ = ServeAsync(client, state, completion, cancellation);
                }
            }
            catch (Exception error) when (error is SocketException or ObjectDisposedException or OperationCanceledException)
            { completion.TrySetCanceled(); }
        }
        private static async Task ServeAsync(TcpClient client, string state, TaskCompletionSource<string> completion, CancellationToken cancellation)
        {
            using (client)
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                try
                {
                    var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                    var request = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
                    var length = request?.Length ?? 0;
                    string header;
                    do
                    {
                        header = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
                        length += header?.Length ?? 0;
                        if (length > 16384) return;
                    } while (!string.IsNullOrEmpty(header));
                    var parts = request?.Split(' ');
                    if (parts == null || parts.Length < 2 || parts[0] != "GET") return;
                    var target = parts[1];
                    var separator = target.IndexOf('?');
                    var path = separator < 0 ? target : target[..separator];
                    if (path != "/callback")
                    {
                        await RespondAsync(stream, "404 Not Found", "", timeout.Token);
                        return;
                    }
                    var query = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var pair in (separator < 0 ? "" : target[(separator + 1)..]).Split('&', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var split = pair.IndexOf('=');
                        string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
                        query[Decode(split < 0 ? pair : pair[..split])] = Decode(split < 0 ? "" : pair[(split + 1)..]);
                    }
                    if (!query.TryGetValue("state", out var returnedState) || state != returnedState)
                    {
                        await RespondAsync(stream, "400 Bad Request", "This login link has expired. Start login again from TeknoParrot.", timeout.Token);
                        return;
                    }
                    query.TryGetValue("code", out var code);
                    completion.TrySetResult(string.IsNullOrEmpty(code) ? null : code);
                    await RespondAsync(stream, "200 OK", string.IsNullOrEmpty(code)
                        ? "Login cancelled. You can close this tab."
                        : "Login received. You can close this tab and return to TeknoParrot.", timeout.Token);
                }
                catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or UriFormatException) { }
            }
        }
        private static async Task RespondAsync(NetworkStream stream, string status, string message, CancellationToken cancellation)
        {
            var body = Encoding.UTF8.GetBytes("<!doctype html><html><meta charset='utf-8'><title>TeknoParrot</title><body><p>" + message + "</p></body></html>");
            var head = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(head, cancellation).ConfigureAwait(false);
            await stream.WriteAsync(body, cancellation).ConfigureAwait(false);
        }
        public void Dispose() => _listener.Stop();
    }
}
