namespace Shed_Security_AP.Models.Local;

/// <summary>
/// One row in the local audit log. Tracks what the admin did, who they did it to,
/// and when. Serialized to daily JSON files by <see cref="Shed_Security_AP.Services.LocalAuditService"/>.
/// </summary>
public class AuditEntry
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
