using System.Net.WebSockets;

namespace ShedLink;

/// <summary>
/// Thin wrapper around a connected dashboard's WebSocket.
/// We track their IP and whether they've proven they know the token.
/// </summary>
public sealed class ShedWebClient
{
    public WebSocket Socket { get; }

    public string ClientIp { get; }

    public bool IsAuthenticated { get; set; }

    public ShedWebClient(WebSocket socket, string clientIp)
    {
        Socket = socket;
        ClientIp = clientIp;
    }
}
