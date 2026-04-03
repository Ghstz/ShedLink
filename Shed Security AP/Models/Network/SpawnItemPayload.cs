namespace Shed_Security_AP.Models.Network;

public class SpawnItemPayload
{
    public string TargetPlayer { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}
