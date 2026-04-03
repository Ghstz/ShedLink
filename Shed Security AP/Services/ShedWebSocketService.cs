using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Shed_Security_AP.Models.Network;

namespace Shed_Security_AP.Services;

/// <summary>
/// The dashboard's WebSocket client. Handles connecting to the ShedLink server mod,
/// authenticating with a token, and running a background receive loop that dispatches
/// incoming messages to whoever's listening. All sends are serialized with a lock
/// so concurrent commands from different ViewModels don't corrupt the wire.
/// </summary>
public class ShedWebSocketService : IWebSocketService, IDisposable
{
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _receiveCts;
    private readonly Lock _sendLock = new();
    private TaskCompletionSource<(bool Success, string? ErrorMessage)>? _authTcs;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public event EventHandler<NetworkMessage>? MessageReceived;
    public event EventHandler? Disconnected;

    public async Task<(bool Success, string? ErrorMessage)> ConnectAsync(string ip, string port, string token)
    {
        try
        {
            await DisconnectAsync();

            _socket = new ClientWebSocket();
            _receiveCts = new CancellationTokenSource();
            _authTcs = new TaskCompletionSource<(bool, string?)>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            Uri uri;
            if (ip.Contains("ngrok", StringComparison.OrdinalIgnoreCase))
            {
                var cleanedIp = ip
                    .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                    .TrimEnd('/');
                uri = new Uri($"wss://{cleanedIp}/shedlink");
            }
            else
            {
                uri = new Uri($"ws://{ip}:{port}/shedlink");
            }

            await _socket.ConnectAsync(uri, _receiveCts.Token);

            // First thing after the handshake — prove we're allowed to be here
            var authMessage = new NetworkMessage
            {
                Type = "Auth",
                Payload = JsonSerializer.SerializeToElement(
                    new AuthPayload { Token = token }, JsonOptions)
            };
            await SendMessageAsync(authMessage);

            // Kick off the listener on a background thread so we don't block the caller
            _ = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));

            // Give the server 10 seconds to say yes or no — any longer and something's wrong
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var reg = timeoutCts.Token.Register(
                () => _authTcs.TrySetResult((false, "Server did not respond in time.")));

            return await _authTcs.Task;
        }
        catch
        {
            _authTcs?.TrySetCanceled();
            _authTcs = null;
            _socket?.Dispose();
            _socket = null;
            return (false, "Failed to connect. Check IP and Port.");
        }
    }

    public async Task DisconnectAsync()
    {
        if (_socket is null)
            return;

        _receiveCts?.Cancel();

        if (_socket.State == WebSocketState.Open)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await _socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "Client disconnecting", timeoutCts.Token);
            }
            catch
            {
                // If the socket is already gone or the server hung up, that's fine — we tried
            }
        }

        _socket.Dispose();
        _socket = null;
        _receiveCts?.Dispose();
        _receiveCts = null;
    }

    public async Task SendMessageAsync(NetworkMessage message)
    {
        if (_socket is null || _socket.State != WebSocketState.Open)
            return;

        var json = JsonSerializer.Serialize(message, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        // WebSocket frames can't overlap, so we lock to prevent interleaved sends
        lock (_sendLock)
        {
            _socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None).GetAwaiter().GetResult();
        }

        await Task.CompletedTask;
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];

        try
        {
            while (_socket is not null && _socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await _socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), ct);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        OnDisconnected();
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);

                var message = await JsonSerializer.DeserializeAsync<NetworkMessage>(ms, JsonOptions, ct);
                if (message is not null)
                {
                    if (message.Type == "AuthResponse" && _authTcs is { Task.IsCompleted: false })
                    {
                        try
                        {
                            var response = message.Payload.Deserialize<AuthResponsePayload>(JsonOptions);
                            _authTcs.TrySetResult((
                                response?.Success ?? false,
                                response?.Success == true ? null : response?.Message ?? "Authentication failed."));
                        }
                        catch (JsonException)
                        {
                            _authTcs.TrySetResult((false, "Malformed auth response from server."));
                        }
                    }
                    else
                    {
                        MessageReceived?.Invoke(this, message);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // We asked it to stop — this is expected, not an error
        }
        catch
        {
            // Something went wrong mid-conversation — treat it as a disconnect
            OnDisconnected();
        }
    }

    private void OnDisconnected()
    {
        _authTcs?.TrySetResult((false, "Connection lost before authentication completed."));
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _receiveCts?.Cancel();
        _receiveCts?.Dispose();
        _socket?.Dispose();

        GC.SuppressFinalize(this);
    }
}
