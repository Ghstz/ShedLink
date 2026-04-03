namespace ShedLink.Models;

/// <summary>
/// Lets the dashboard mess with world state — setting the time of day, forcing a temporal
/// storm, or canceling one in progress. <c>Action</c> is the verb ("SetTime", "ForceStorm",
/// "StopStorm") and <c>Value</c> carries optional data like the target hour for SetTime.
/// </summary>
public class WorldControlRequestPayload
{
    public string Action { get; set; } = string.Empty;
    public double? Value { get; set; }
}
