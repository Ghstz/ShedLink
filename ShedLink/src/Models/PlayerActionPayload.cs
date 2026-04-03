namespace ShedLink.Models;

/// <summary>
/// Carries a moderation action from the dashboard — kick, ban, unban, whitelist, etc.
/// <c>ActionType</c> maps directly to the server command we'll run, and <c>Reason</c>
/// is optional context that gets logged in the audit trail.
/// </summary>
public class PlayerActionPayload
{
    public string ActionType { get; set; } = string.Empty;

    public string TargetPlayer { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}
