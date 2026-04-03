namespace Shed_Security_AP.Models.Network;

public class TeleportRequestPayload
{
    public string PlayerName { get; set; } = string.Empty;
    public string DestinationPlayer { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}
