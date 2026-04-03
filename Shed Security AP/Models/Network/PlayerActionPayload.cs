namespace Shed_Security_AP.Models.Network;

public class PlayerActionPayload
{
    public string ActionType { get; set; } = string.Empty;

    public string TargetPlayer { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}
