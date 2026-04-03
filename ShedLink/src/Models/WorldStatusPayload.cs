namespace ShedLink.Models;

/// <summary>
/// World state snapshot pushed alongside the regular telemetry tick. The dashboard
/// uses this to show the current in-game hour, season, whether a storm is raging,
/// and how long until the next one hits.
/// </summary>
public class WorldStatusPayload
{
    public double HourOfDay { get; set; }
    public string Season { get; set; } = string.Empty;
    public bool IsStormActive { get; set; }
    public double DaysUntilStorm { get; set; }
}
