namespace ShedLink.Models;

/// <summary>
/// The deep-dive info panel for a specific player. Includes their IP (useful for
/// alt-account detection), total playtime, current position, and how many anti-cheat
/// strikes they've accumulated. This only gets sent on demand — we don't broadcast it.
/// </summary>
public class PlayerInspectResponsePayload
{
    public string PlayerName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Playtime { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public int TotalStrikes { get; set; }
}
