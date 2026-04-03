namespace ShedLink.Models;

/// <summary>
/// Fired whenever the anti-cheat system flags suspicious behavior — speed hacks,
/// impossible item stacks, fly exploits, etc. The dashboard shows these as toast
/// notifications and logs them in the alert history. <c>StrikeCount</c> is the
/// running total so admins can see repeat offenders at a glance.
/// </summary>
public class SecurityAlertPayload
{
    public string Timestamp { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public int StrikeCount { get; set; }
}
