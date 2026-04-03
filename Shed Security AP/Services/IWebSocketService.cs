using Shed_Security_AP.Models.Network;

namespace Shed_Security_AP.Services;

/// <summary>
/// The WebSocket contract that all ViewModels talk to. Keeps the networking
/// details out of the UI layer — ViewModels just send messages and subscribe
/// to events, they never touch raw sockets.
/// </summary>
public interface IWebSocketService
{
    Task<(bool Success, string? ErrorMessage)> ConnectAsync(string ip, string port, string token);

    Task DisconnectAsync();

    Task SendMessageAsync(NetworkMessage message);

    event EventHandler<NetworkMessage> MessageReceived;

    event EventHandler Disconnected;
}
