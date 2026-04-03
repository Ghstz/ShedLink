using System.Text.Json;

namespace ShedLink.Models;

/// <summary>
/// The envelope for every message that goes over the wire between server and dashboard.
/// <c>Type</c> tells you what kind of message it is, and <c>Payload</c> carries the
/// data as a raw <see cref="System.Text.Json.JsonElement"/> so we can deserialize it
/// lazily into whatever concrete type the handler expects.
/// </summary>
public class NetworkMessage
{
    public required string Type { get; set; }

    public JsonElement Payload { get; set; }
}
