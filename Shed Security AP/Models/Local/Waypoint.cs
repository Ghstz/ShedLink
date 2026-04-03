namespace Shed_Security_AP.Models.Local;

/// <summary>
/// A named teleport destination saved locally. Coordinates are relative to world spawn.
/// <see cref="ToString"/> returns the name so it displays nicely in dropdown menus.
/// </summary>
public class Waypoint
{
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    public override string ToString() => Name;
}
