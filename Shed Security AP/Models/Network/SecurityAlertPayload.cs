namespace Shed_Security_AP.Models.Network;

public class SecurityAlertPayload
{
    public string Timestamp { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public int StrikeCount { get; set; }
}
