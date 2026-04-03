namespace Shed_Security_AP.Models.Network;

public class PlayerInspectResponsePayload
{
    public string PlayerName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Playtime { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public int TotalStrikes { get; set; }
}
