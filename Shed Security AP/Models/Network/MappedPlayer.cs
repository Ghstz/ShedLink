namespace Shed_Security_AP.Models.Network;

/// <summary>
/// A player positioned on the radar canvas. Carries both the original world coordinates
/// (for tooltips) and the computed canvas pixel coordinates (for rendering the dot).
/// </summary>
public class MappedPlayer
{
    public string Name { get; set; } = string.Empty;
    public double CanvasX { get; set; }
    public double CanvasY { get; set; }
    public double WorldX { get; set; }
    public double WorldZ { get; set; }
}
