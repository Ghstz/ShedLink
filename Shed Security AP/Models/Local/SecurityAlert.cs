namespace Shed_Security_AP.Models.Local;

/// <summary>
/// An immutable snapshot of one anti-cheat alert, displayed in the live alert feed.
/// We use a primary constructor here because alerts are read-only once created —
/// there's no reason to mutate them after the server sends them.
/// </summary>
public class SecurityAlert(string timestamp, string playerName, string alertType, string details, int strikeCount)
{
    public string Timestamp { get; } = timestamp;
    public string PlayerName { get; } = playerName;
    public string AlertType { get; } = alertType;
    public string Details { get; } = details;
    public int StrikeCount { get; } = strikeCount;
}
