namespace Shed_Security_AP.Models.Network;

public class WorldStatusPayload
{
    public double HourOfDay { get; set; }
    public string Season { get; set; } = string.Empty;
    public bool IsStormActive { get; set; }
    public double DaysUntilStorm { get; set; }
}
