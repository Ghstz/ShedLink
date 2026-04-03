namespace ShedLink.Models;

/// <summary>
/// The dashboard wants to pull up the full dossier on a specific player.
/// We look up their IP, playtime, position, and strike history server-side
/// and send it back in a <see cref="PlayerInspectResponsePayload"/>.
/// </summary>
public class PlayerInspectRequestPayload
{
    public string PlayerName { get; set; } = string.Empty;
}
