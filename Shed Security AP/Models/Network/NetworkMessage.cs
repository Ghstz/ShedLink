using System.Text.Json;

namespace Shed_Security_AP.Models.Network;

/// <summary>
/// The wire format for every message between the dashboard and server.
/// <c>Type</c> identifies the message kind, <c>Payload</c> is a raw JSON element
/// we deserialize lazily based on the type. Mirrors the server-side version exactly.
/// </summary>
public class NetworkMessage
{
    public required string Type { get; set; }

    public JsonElement Payload { get; set; }
}
