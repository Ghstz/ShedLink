namespace ShedLink.Models;

/// <summary>
/// Dashboard wants to whitelist/ban/unban a player.
/// <c>ActionType</c> maps to: AddWhitelist, RemoveWhitelist, AddBan, RemoveBan.
/// </summary>
public class AccessControlActionPayload
{
    public string ActionType { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
}
