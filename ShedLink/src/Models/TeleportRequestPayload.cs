namespace ShedLink.Models;

/// <summary>
/// Two flavors of teleport live here: player-to-coordinates and player-to-player.
/// If <c>DestinationPlayer</c> is set we ignore the XYZ fields and warp them
/// to that player's current position instead. The coordinates are relative
/// (offset from spawn), and we convert to absolute engine coords server-side.
/// </summary>
public class TeleportRequestPayload
{
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>If this is non-empty, we teleport to this player's location and skip the XYZ fields entirely.</summary>
    public string DestinationPlayer { get; set; } = string.Empty;

    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}
